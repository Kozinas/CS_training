"""Verify chapters 36-39: MTP tests, coverage, format, controlled regression and local NuGet consumption."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--containers", action="store_true", help="Also run real PostgreSQL; requires a running Linux Docker engine")
    args = parser.parse_args()
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    base = ROOT / "examples/Engineering"
    env = {**os.environ, "DOTNET_NOLOGO": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
           "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"), "NUGET_PACKAGES": str(artifacts / "packages")}
    checks = 0

    def run(command, cwd=base, extra=None, timeout=180):
        return subprocess.run(list(map(str, command)), cwd=cwd, env={**env, **(extra or {})},
                              capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)

    def require(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            raise RuntimeError(message)

    def success(command, **kwargs):
        result = run(command, **kwargs)
        require(result.returncode == 0, result.stdout + result.stderr)
        return result

    success(["dotnet", "restore", "Engineering.slnx", "--locked-mode"])
    success(["dotnet", "build", "Engineering.slnx", "-c", "Release", "--no-restore"])
    # The optional project is always compiled, never silently treated as an executed database test.
    success(["dotnet", "restore", "Container.Tests/Container.Tests.csproj", "--locked-mode"])
    success(["dotnet", "build", "Container.Tests/Container.Tests.csproj", "-c", "Release", "--no-restore"])
    success(["dotnet", "format", "whitespace", "Engineering.slnx", "--verify-no-changes", "--no-restore",
             "--include", "ReviewRules", "ReviewRules.Tests", "ReviewDemo", "Integration.Tests"])
    with tempfile.TemporaryDirectory(prefix="engineering-check-", dir=artifacts) as temporary:
        folder = Path(temporary).resolve()
        assert folder.is_relative_to(artifacts.resolve())
        summary = {}
        for project, expected in (("ReviewRules.Tests", 27), ("Integration.Tests", 6)):
            results = folder / project
            command = ["dotnet", "test", "--project", project, "-c", "Release", "--no-build",
                       "--report-trx", "--results-directory", results]
            if project == "ReviewRules.Tests":
                command += ["--coverage", "--coverage-output-format", "cobertura", "--coverage-output", "coverage.cobertura.xml"]
            success(command)
            trx = list(results.glob("*.trx"))
            require(len(trx) == 1, "Exactly one fresh TRX per project")
            counts = ET.parse(trx[0]).find("t:ResultSummary/t:Counters", NS)
            require(counts is not None and int(counts.get("total", "0")) == expected
                    and int(counts.get("passed", "0")) == expected, f"Expected {expected} passed cases for {project}")
            summary[project] = expected
            print(f"PASS {project}: {expected} xUnit cases; no skipped cases")
        coverage = ET.parse(folder / "ReviewRules.Tests/coverage.cobertura.xml")
        package = next((p for p in coverage.findall(".//package") if p.get("name") == "ReviewRules"), None)
        require(package is not None, "Coverage must contain the real ReviewRules assembly")
        summary["coverage"] = {key: package.get(key) for key in ("line-rate", "branch-rate")}
        summary["coverage"]["note"] = "Observed coverage, not a quality gate or proof of correctness"
        output = artifacts / "engineering"
        output.mkdir(exist_ok=True)
        (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
        for arguments, wanted in [(["1000", "1799", "food"], "AutoApproved"),
                                   (["1000", "1800", "food"], "Expired"),
                                   (["501", "0", "travel"], "ManualReview"),
                                   (["1", "-1", "food"], "FutureSubmission")]:
            result = success(["dotnet", base / "ReviewDemo/bin/Release/net10.0/ReviewDemo.dll", *arguments])
            require(result.stdout.strip() == wanted and not result.stderr, f"CLI output: {result}")
        for arguments in ([], ["0", "0", "food"], ["1,25", "0", "food"], ["1", "999999", "food"]):
            result = run(["dotnet", base / "ReviewDemo/bin/Release/net10.0/ReviewDemo.dll", *arguments])
            require(result.returncode == 2 and not result.stdout and result.stderr.startswith("usage:"), "CLI usage contract")

        feed, consumer = folder / "feed", folder / "consumer"
        consumer.mkdir()
        success(["dotnet", "pack", "ReviewRules/ReviewRules.csproj", "-c", "Release", "--no-build", "-o", feed])
        package_file = feed / "CSharpCourse.ReviewRules.1.1.0.nupkg"
        require(package_file.is_file(), "Package exists")
        with zipfile.ZipFile(package_file) as archive:
            require("lib/net10.0/ReviewRules.dll" in archive.namelist() and "PACKAGE.md" in archive.namelist(), "Package content")
        for original, name in (("Consumer.csproj.template", "Consumer.csproj"), ("Program.cs.template", "Program.cs")):
            shutil.copyfile(base / "Packaging" / original, consumer / name)
        shutil.copyfile(base / "global.json", consumer / "global.json")
        config = ET.Element("configuration")
        sources = ET.SubElement(config, "packageSources")
        ET.SubElement(sources, "clear")
        ET.SubElement(sources, "add", key="local-course", value=str(feed))
        ET.ElementTree(config).write(consumer / "NuGet.Config", encoding="utf-8", xml_declaration=True)
        # Fresh cache proves the consumer uses the just-built package, not an old same-version cache entry.
        isolated = {"NUGET_PACKAGES": str(folder / "consumer-packages")}
        success(["dotnet", "restore", "Consumer.csproj"], cwd=consumer, extra=isolated)
        success(["dotnet", "restore", "Consumer.csproj", "--locked-mode"], cwd=consumer, extra=isolated)
        success(["dotnet", "build", "Consumer.csproj", "-c", "Release", "--no-restore"], cwd=consumer, extra=isolated)
        consumed = success(["dotnet", consumer / "bin/Release/net10.0/Consumer.dll"], cwd=consumer, extra=isolated)
        require(consumed.stdout.replace("\r\n", "\n") == "AutoApproved\nExpired\nManualReview\n", "Independent package consumer")
        require("ProjectReference" not in (consumer / "Consumer.csproj").read_text(), "Consumer must not reference source project")
        project = consumer / "Consumer.csproj"
        project.write_text(project.read_text().replace("[1.1.0]", "[1.1.1]"), encoding="utf-8")
        mismatch = run(["dotnet", "restore", "Consumer.csproj", "--locked-mode"], cwd=consumer, extra=isolated)
        require(mismatch.returncode != 0 and "NU1004" in mismatch.stdout + mismatch.stderr,
                "Locked restore must reject a changed package requirement")
        print("PASS pack/restore: fresh cache, local-only feed, locked restore and independent consumer")
    probe = success([sys.executable, "-B", ROOT / "tools/reproduce_boundary.py"], cwd=ROOT, timeout=240)
    print(probe.stdout.strip())
    git_practice = success([sys.executable, "-B", ROOT / "tools/practice_git.py"], cwd=ROOT)
    print(git_practice.stdout.strip())
    if args.containers:
        docker = run(["docker", "info", "--format", "{{.OSType}}"], timeout=30)
        require(docker.returncode == 0 and docker.stdout.strip() == "linux",
                "Container test requires a running Linux Docker engine; start Docker Desktop then retry --containers")
        result = success(["dotnet", "test", "--project", "Container.Tests", "-c", "Release", "--no-build"], timeout=180)
        print(result.stdout.strip())
    else:
        print("INFO PostgreSQL container: compiled; not executed (opt in with --containers)")
    print(f"PASS Engineering: {checks} scenario assertions")


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, subprocess.SubprocessError, ET.ParseError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
