"""Build and verify chapters 31-35/L04. All fixtures are local and bounded."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--no-build", action="store_true", help="Reuse a previously built Release solution")
    args = parser.parse_args()
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    env = os.environ.copy()
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", DOTNET_NOLOGO="1",
               DOTNET_CLI_HOME=str(artifacts / "dotnet-home"), NUGET_PACKAGES=str(artifacts / "packages"))
    base = ROOT / "examples/Integration"
    count = 0

    def run(command, cwd=ROOT, timeout=90):
        return subprocess.run(list(map(str, command)), cwd=cwd, env=env, capture_output=True,
                              text=True, encoding="utf-8", errors="replace", timeout=timeout)

    def require(condition, message):
        nonlocal count
        count += 1
        if not condition:
            raise AssertionError(message)

    if not args.no_build:
        for command in (["dotnet", "restore", "Integration.slnx", "--locked-mode", "--nologo"],
                        ["dotnet", "build", "Integration.slnx", "-c", "Release", "--no-restore", "--nologo"]):
            result = run(command, cwd=base, timeout=180)
            if result.returncode:
                raise RuntimeError(result.stdout + result.stderr)
    for number, output in {"31": "success=1;errors=2\n0\ndisposed=True\n",
                           "32": "lost=1\natomic=4000\nlocked=4000\n", "33": "0,1,4,9\n0,1,4,9\n"}.items():
        result = run(["dotnet", base / "AsyncLab/bin/Release/net10.0/AsyncLab.dll", number])
        require(result.returncode == 0 and result.stdout.replace("\r\n", "\n") == output and not result.stderr,
                f"AsyncLab {number}: {result}")
    result = run(["dotnet", base / "PipelineChecks/bin/Release/net10.0/PipelineChecks.dll"])
    require(result.returncode == 0 and result.stdout.startswith("PASS PipelineChecks:") and not result.stderr,
            result.stdout + result.stderr)
    print(result.stdout.strip())
    cli = base / "PipelineWorker/bin/Release/net10.0/PipelineWorker.dll"
    data = ROOT / "labs/04-background-pipeline/data"
    expected = json.loads((data / "expected-first.json").read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory(prefix="pipeline-check-", dir=artifacts) as temporary:
        folder = Path(temporary)

        def invoke(arguments):
            return run(["dotnet", cli, *arguments])

        def report(arguments, code, wanted):
            result = invoke(arguments)
            require(result.returncode == code, f"Exit {code}: {result}")
            require(json.loads(result.stdout) == wanted, f"Unexpected report: {result}")

        canonical = None
        for workers, capacity, window in [(1, 1, 1), (2, 1, 3), (4, 8, 16)]:
            state = folder / f"state-{workers}.json"
            command = ["--file", data / "mixed.ndjson", "--state", state, "--workers", workers,
                       "--capacity", capacity, "--window", window]
            report(command, 4, expected)
            saved = state.read_bytes()
            require(canonical is None or saved == canonical, "Concurrency changed checkpoint")
            canonical = saved
            report(command, 4, {**expected, "added": 0, "duplicates": 3})
            require(state.read_bytes() == saved, "Repeated import rewrote checkpoint")
        state = folder / "state-1.json"
        saved = state.read_bytes()
        bad = folder / "bad.ndjson"
        for name, content in [("oversized-line", b"x" * 4097), ("oversized-input", b"{}\n" * 5_600_000)]:
            bad.write_bytes(content)
            result = invoke(["--file", bad, "--state", state])
            require(result.returncode == 3 and not result.stdout, f"{name}: {result}")
            require(state.read_bytes() == saved, "Fatal error changed state")
        bad.write_bytes(b"bad\n" * 25)
        report(["--file", bad, "--state", state], 4,
               {"processed": 25, "added": 0, "duplicates": 0, "failed": 25, "total": 2, "retried": 0,
                "errors": [{"line": i, "code": "invalid_json"} for i in range(1, 21)], "errorsTruncated": True})
        for payload in (b"", b"\xef\xbb\xbf"):
            bad.write_bytes(payload)
            # BOM by itself denotes a single blank physical line; the empty file has none.
            if payload:
                report(["--file", bad, "--state", state], 4,
                       {"processed": 1, "added": 0, "duplicates": 0, "failed": 1, "total": 2, "retried": 0,
                        "errors": [{"line": 1, "code": "invalid_json"}], "errorsTruncated": False})
            else:
                report(["--file", bad, "--state", state], 0,
                       {"processed": 0, "added": 0, "duplicates": 0, "failed": 0, "total": 2, "retried": 0,
                        "errors": [], "errorsTruncated": False})
        for arguments in ([], ["--file", bad, "--state", state, "--workers", 0],
                          ["--file", bad, "--state", state, "--capacity", 0],
                          ["--file", bad, "--state", state, "--window", 0],
                          ["--file", bad, "--state", state, "--workers", "abc"],
                          ["--file", state, "--state", state], ["--unknown"]):
            result = invoke(arguments)
            require(result.returncode == 2 and not result.stdout, f"Usage: {result}")
        result = invoke(["--file", folder / "missing", "--state", state])
        require(result.returncode == 3 and not result.stdout, "Missing input")
        state.write_text("corrupt", encoding="utf-8")
        result = invoke(["--file", bad, "--state", state])
        require(result.returncode == 3 and not result.stdout and state.read_text() == "corrupt", "Corrupt state retained")
        # Larger bounded workload, including duplicates: all counts and exact state contents.
        rows = [{"id": str(uuid.UUID(int=i)), "date": "2026-01-10", "category": "food", "amount": "1.00"}
                for i in range(1, 2001)]
        bad.write_text("\n".join(json.dumps(row) for row in rows + rows), encoding="utf-8")
        loaded = folder / "load.json"
        report(["--file", bad, "--state", loaded], 0,
               {"processed": 4000, "added": 2000, "duplicates": 2000, "failed": 0, "total": 2000,
                "retried": 0, "errors": [], "errorsTruncated": False})
        require({row["id"] for row in json.loads(loaded.read_text())["records"]} == {row["id"] for row in rows},
                "No missing/extra IDs under load")
        result = invoke(["--help"])
        require(result.returncode == 0 and "--workers" in result.stdout, "CLI help")
    print(f"PASS L04: {count} scenario assertions")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, RuntimeError, ValueError, OSError, subprocess.SubprocessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
