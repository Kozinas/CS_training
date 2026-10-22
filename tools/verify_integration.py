"""Restore locked packages, build chapters 25-30 and verify L03 without external HTTP services."""
from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = Path(__file__).resolve().parents[1]


def main():
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    env = os.environ.copy()
    # Do not let the caller's optional import settings change the test contract.
    env = {key: value for key, value in env.items() if not key.upper().startswith("CS_IMPORT_")}
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
               DOTNET_NOLOGO="1", DOTNET_CLI_HOME=str(artifacts / "dotnet-home"),
               NUGET_PACKAGES=str(artifacts / "packages"))
    base = ROOT / "examples/Integration"
    checks = 0

    def run(command, cwd=ROOT, extra=None, timeout=90):
        return subprocess.run(command, cwd=cwd, env={**env, **(extra or {})},
                              capture_output=True, text=True, encoding="utf-8",
                              errors="replace", timeout=timeout)

    def require(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            raise AssertionError(message)

    for command in (["dotnet", "restore", "Integration.slnx", "--locked-mode", "--nologo"],
                    ["dotnet", "build", "Integration.slnx", "-c", "Release", "--no-restore", "--nologo"]):
        result = run(command, cwd=base, timeout=180)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
    print("PASS Integration build: locked restore; Release; warnings treated as errors")
    demo = base / "BclLab/bin/Release/net10.0/BclLab.dll"
    expected = {
        "25": "first\nsecond\nlater\n2,3\n2\n1267650600228229401496703205376\n",
        "26": "True\n2,1,1\nTrue\n2026-01-15T17:00:00.0000000+00:00\nTrue\nTrue\n",
        "27": "food,office\ntea & milk\nledger\n",
    }
    for number, output in expected.items():
        result = run(["dotnet", str(demo), number])
        require(result.returncode == 0 and result.stdout.replace("\r\n", "\n") == output
                and not result.stderr, f"BclLab {number}: {result}")
    result = run(["dotnet", str(base / "IntegrationChecks/bin/Release/net10.0/IntegrationChecks.dll")])
    require(result.returncode == 0 and result.stdout.startswith("PASS IntegrationChecks:") and not result.stderr,
            result.stdout + result.stderr)
    print(result.stdout.strip())

    cli = base / "ImportCli/bin/Release/net10.0/ImportCli.dll"
    data = ROOT / "labs/03-import-tool/data"
    with tempfile.TemporaryDirectory(prefix="import-check-", dir=artifacts) as temporary:
        folder = Path(temporary)
        state = folder / "state.json"

        def invoke(arguments, extra=None):
            return run(["dotnet", str(cli), *map(str, arguments)], extra=extra)

        def report(arguments, code, expected, extra=None):
            result = invoke(arguments, extra)
            require(result.returncode == code, f"Expected exit {code}: {result}")
            require(json.loads(result.stdout) == expected, f"Unexpected report: {result.stdout}")
            return result

        first = json.loads((data / "expected-first.json").read_text(encoding="utf-8"))
        command = ["import", "--file", data / "mixed.json", "--state", state]
        report(command, 4, first)
        checkpoint = state.read_bytes()
        require(len(json.loads(checkpoint)["records"]) == 2, "Persist only valid records")
        repeated = {**first, "added": 0, "duplicates": 3}
        report(command, 4, repeated)
        require(state.read_bytes() == checkpoint, "Idempotent state")
        exported = folder / "export.json"
        result = invoke(["export", "--state", state, "--output", exported])
        require(result.returncode == 0 and result.stdout.strip() == "exported", str(result))
        require(exported.read_bytes() == checkpoint, "Canonical export")
        report(["import", "--file", exported, "--state", state], 0,
               dict(added=0, duplicates=2, failed=0, total=2, errors=[]))
        result = invoke([*command, "--pretty"])
        require(result.returncode == 4 and "Failed" in result.stdout and "invalid_date" in result.stdout,
                "Spectre report")

        def failure(arguments, code, extra=None):
            result = invoke(arguments, extra)
            require(result.returncode == code and not result.stdout and result.stderr,
                    f"Expected failure {code}: {result}")
            require(state.read_bytes() == checkpoint, "Failure changed old state")

        failure(["import", "--state", state], 2)
        failure(["import", "--file", data / "valid.json", "--url", "https://example.invalid", "--state", state], 2)
        failure(["import", "--file", state, "--state", state], 2)
        failure(["import", "--file", folder / "missing.json", "--state", state], 3)
        failure(["import", "--file", data / "valid.json", "--state", state, "--unknown"], 2)
        failure(["import", "--url", "http://example.invalid/input", "--state", state], 2)
        failure(command, 2, {"CS_IMPORT_Import__MaxBytes": "0"})
        failure(command, 2, {"CS_IMPORT_Import__MaxBytes": "abc"})
        failure(command, 3, {"CS_IMPORT_Import__MaxRecords": "1"})
        failure(command, 3, {"CS_IMPORT_Import__MaxBytes": "128"})
        for index, value in enumerate([
            b"{", b"null", b'{"version":2,"records":[]}', b'{"version":1,"records":null}',
            b'{"version":1,"version":1,"records":[]}', b"\xff",
            b'{"version":1,"records":[],"extra":1}',
        ]):
            bad = folder / f"bad-{index}.json"
            bad.write_bytes(value)
            failure(["import", "--file", bad, "--state", state], 3)
        bom = folder / "bom.json"
        bom.write_bytes(b"\xef\xbb\xbf" + (data / "valid.json").read_bytes())
        report(["import", "--file", bom, "--state", state], 0,
               dict(added=0, duplicates=2, failed=0, total=2, errors=[]))
        empty = folder / "empty.json"
        empty.write_text('{"version":1,"records":[]}', encoding="utf-8")
        report(["import", "--file", empty, "--state", state], 0,
               dict(added=0, duplicates=0, failed=0, total=2, errors=[]))
        fresh = folder / "fresh.json"
        report(["import", "--file", empty, "--state", fresh], 0,
               dict(added=0, duplicates=0, failed=0, total=0, errors=[]))
        require(not fresh.exists(), "Empty import should not create data checkpoint")
        corrupt = folder / "corrupt.json"
        corrupt.write_text("{broken", encoding="utf-8")
        failure(["import", "--file", data / "valid.json", "--state", corrupt], 3)
        require(corrupt.read_text() == "{broken", "Corrupt checkpoint was overwritten")
        result = invoke(["--help"])
        require(result.returncode == 0 and "import" in result.stdout and "export" in result.stdout, "CLI help")

        payload = (data / "valid.json").read_bytes()

        class Handler(BaseHTTPRequestHandler):
            requests = 0

            def do_GET(self):
                type(self).requests += 1
                self.send_response(503 if type(self).requests == 1 else 200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

            def log_message(self, *args):
                pass

        server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            url = f"http://127.0.0.1:{server.server_port}/data"
            remote_state = folder / "remote.json"
            report(["import", "--url", url, "--state", remote_state], 0,
                   dict(added=2, duplicates=0, failed=0, total=2, errors=[]))
            require(Handler.requests == 2, "CLI GET retry")
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)
    print(f"PASS L03: {checks} scenario assertions; CLI; configuration; checkpoint; loopback HTTP")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (AssertionError, ValueError, RuntimeError, OSError, subprocess.TimeoutExpired) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
