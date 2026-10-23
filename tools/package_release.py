"""Create or verify a local L07 API + Blazor release; never deploy to an external service."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def verify_release(folder):
    folder = Path(folder).resolve(strict=True)
    manifest = json.loads((folder / "release.json").read_text(encoding="utf-8"))
    if manifest.get("schema") != 1 or manifest.get("mode") != "framework-dependent":
        raise ValueError("Unsupported release manifest.")
    files = manifest.get("files")
    if not isinstance(files, dict) or not files:
        raise ValueError("Empty file manifest.")
    for name, expected in files.items():
        relative = PurePosixPath(name)
        if relative.is_absolute() or ".." in relative.parts or "\\" in name or ":" in name or name == "release.json":
            raise ValueError("Manifest path escapes the release.")
        path = (folder / name).resolve(strict=True)
        if not path.is_relative_to(folder) or not path.is_file():
            raise ValueError("Invalid release member.")
        if digest(path) != expected:
            raise ValueError(f"Checksum mismatch: {name}")
    actual = {p.relative_to(folder).as_posix() for p in folder.rglob("*") if p.is_file()}
    if actual != set(files) | {"release.json"}:
        raise ValueError("Release contains missing or unexpected files.")
    if not {"api/LedgerWeb.dll", "ui/BlazorClient.dll"} <= set(files):
        raise ValueError("Release must contain both API and UI.")
    return manifest


def create_release(folder, version):
    folder = Path(folder).resolve()
    artifacts = (ROOT / ".artifacts").resolve()
    if not folder.is_relative_to(artifacts) or folder == artifacts:
        raise ValueError("Create releases only inside the repository's .artifacts directory.")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise ValueError("Expected a three-part numeric version.")
    if folder.exists():
        raise ValueError("Destination already exists; choose a new release directory.")
    env = {**os.environ, "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"),
           "NUGET_PACKAGES": str(artifacts / "packages"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"}
    folder.mkdir(parents=True)
    for project, name in [(ROOT / "examples/Web/LedgerWeb/LedgerWeb.csproj", "api"),
                          (ROOT / "examples/Specializations/BlazorClient/BlazorClient.csproj", "ui")]:
        commands = [
            ["dotnet", "restore", str(project), "--locked-mode"],
            ["dotnet", "publish", str(project), "-c", "Release", "--no-restore", "--self-contained", "false",
             "-p:UseAppHost=false", "-p:Version=" + version, "-o", str(folder / name), "--nologo"],
        ]
        for command in commands:
            result = subprocess.run(command, cwd=project.parent, env=env, capture_output=True,
                                    encoding="utf-8", errors="replace", timeout=240)
            if result.returncode:
                raise RuntimeError(result.stdout + result.stderr)
    files = {p.relative_to(folder).as_posix(): digest(p) for p in sorted(folder.rglob("*")) if p.is_file()}
    manifest = {"schema": 1, "version": version, "sdk": "10.0.402", "framework": "net10.0",
                "mode": "framework-dependent", "database_schema": "L06-InitialWeb", "files": files}
    (folder / "release.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    verify_release(folder)
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create")
    create.add_argument("version")
    create.add_argument("folder", type=Path)
    verify = commands.add_parser("verify")
    verify.add_argument("folder", type=Path)
    args = parser.parse_args()
    manifest = create_release(args.folder, args.version) if args.command == "create" else verify_release(args.folder)
    print(f"PASS release: {manifest['version']}; {len(manifest['files'])} files; checksums verified")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        sys.exit(1)
