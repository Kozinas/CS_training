"""Verify UI states, offline durability, portable game rules and numerical results.

Godot compilation is mandatory. Engine execution is optional via --godot PATH.
Mobile SDKs and Unity are intentionally not installed by this script.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
import urllib.request
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", type=Path, help="Path to an installed Godot 4.6.2 .NET console executable")
    args = parser.parse_args()
    base = ROOT / "examples/Specializations"
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    output = artifacts / "specializations"
    output.mkdir(exist_ok=True)
    env = {**os.environ, "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"),
           "NUGET_PACKAGES": str(artifacts / "packages"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
           "TESTINGPLATFORM_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"}
    checks = 0

    def require(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            raise RuntimeError(message)

    def run(command, cwd=base, expected=0, timeout=180):
        result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, encoding="utf-8",
                                errors="replace", capture_output=True, timeout=timeout)
        require(result.returncode == expected, result.stdout + result.stderr)
        return result

    run(["dotnet", "restore", "Specializations.slnx", "--locked-mode"])
    run(["dotnet", "build", "Specializations.slnx", "-c", "Release", "--no-restore", "--nologo"])
    run(["dotnet", "restore", "GodotClient/GodotClient.csproj", "--locked-mode"])
    run(["dotnet", "build", "GodotClient/GodotClient.csproj", "-c", "Debug", "--no-restore", "--nologo"])
    if os.name == "nt":
        run(["dotnet", "restore", "DesktopClient/DesktopClient.csproj", "--locked-mode"])
        run(["dotnet", "build", "DesktopClient/DesktopClient.csproj", "-c", "Release", "--no-restore", "--nologo"])
        desktop = run(["dotnet", "DesktopClient/bin/Release/net10.0-windows/DesktopClient.dll", "--verify"], timeout=30)
        require("PASS WPF:" in desktop.stdout and not desktop.stderr, desktop.stdout + desktop.stderr)

    with tempfile.TemporaryDirectory(prefix="specializations-check-", dir=artifacts) as temporary:
        folder = Path(temporary).resolve()
        assert folder.parent == artifacts.resolve()  # Cleanup is limited to this owned directory.
        run(["dotnet", "Specializations.Tests/bin/Release/net10.0/Specializations.Tests.dll",
             "--report-trx", "--results-directory", folder / "tests"])
        reports = list((folder / "tests").glob("*.trx"))
        require(len(reports) == 1, "Expected one fresh TRX report.")
        counters = ET.parse(reports[0]).find("t:ResultSummary/t:Counters", NS)
        require(counters is not None and counters.get("total") == "43"
                and counters.get("passed") == "43" and counters.get("failed") == "0", "Unexpected test counts.")
        cli = base / "Scenarios/bin/Release/net10.0/Scenarios.dll"

        def scenario(*arguments, expected=0):
            return run(["dotnet", cli, *arguments], expected=expected)

        result = json.loads(scenario("offline", folder / "queue.db").stdout)
        require(result == {"Retained": 1, "Sent": 1, "Pending": 0, "Attempts": 2, "UniqueBatches": 1}, result)
        save = folder / "save.json"
        require(json.loads(scenario("game", save).stdout) == {"Coins": 10, "Added": True}, "First pickup failed.")
        require(json.loads(scenario("game", save).stdout) == {"Coins": 10, "Added": False}, "Restart duplicated a pickup.")
        save.write_text('{"Version":99,"Coins":0,"Pickups":[]}', encoding="utf-8")
        before = save.read_bytes()
        result = scenario("game", save, expected=3)
        require(not result.stdout and save.read_bytes() == before, "Invalid save was overwritten.")

        data = folder / "amounts.json"
        data.write_text("[100,200,300]", encoding="utf-8")
        result = json.loads(scenario("analyze", data).stdout)
        require(result == {"Count": 3, "TotalMinor": 600, "MeanMinor": 200, "SampleDeviationMinor": 100}, result)
        for bad in ["[]", "[1]", "[100,0]", "[100,1.5]", "[100,NaN]", "null", "{}", "[100,200,broken]"]:
            data.write_text(bad, encoding="utf-8")
            result = scenario("analyze", data, expected=3)
            require(not result.stdout and result.stderr.startswith("input/io:"), f"Partial/invalid output: {result}")
        data.write_bytes(b" " * (2 * 1024 * 1024 + 1))
        require(not scenario("analyze", data, expected=3).stdout, "Oversized input produced a report.")
        measurement = json.loads(scenario("measure").stdout)
        require(measurement["scalar"]["Result"] == measurement["vector"]["Result"], measurement)
        (output / "measurement.json").write_text(json.dumps(measurement, indent=2), encoding="utf-8")

        # Real transport check against the preceding lab. No external service or credential is used.
        web = ROOT / "examples/Web/LedgerWeb/bin/Release/net10.0/LedgerWeb.dll"
        if not web.exists():
            run(["dotnet", "restore", "Web.slnx", "--locked-mode"], cwd=ROOT / "examples/Web")
            run(["dotnet", "build", "Web.slnx", "-c", "Release", "--no-restore"], cwd=ROOT / "examples/Web")
        env.update({"DOTNET_ENVIRONMENT": "Development", "Ledger__Path": str(folder / "api.db"),
                    "Keys__Directory": str(folder / "keys"), "Web__AllowHttp": "true", "Worker__Enabled": "false"})
        run(["dotnet", web, "--migrate"])

        def server(dll, workdir, logname):
            log_path = folder / logname
            stream = log_path.open("wb")
            process = subprocess.Popen(["dotnet", str(dll), "--urls", "http://127.0.0.1:0"],
                                       cwd=workdir, env=env, stdout=stream, stderr=subprocess.STDOUT)
            try:
                deadline = time.monotonic() + 30
                while time.monotonic() < deadline:
                    log = log_path.read_text(encoding="utf-8", errors="replace")
                    match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", log)
                    if match:
                        return process, stream, match[1]
                    if process.poll() is not None:
                        raise RuntimeError(log)
                    time.sleep(0.05)
                raise RuntimeError("Server startup deadline.")
            except BaseException:
                stop(process, stream)
                raise

        def stop(process, stream):
            if process.poll() is None:
                process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)
            stream.close()

        process, stream, url = server(web, web.parent, "api.log")
        try:
            def request(path, body, token=None):
                headers = {"Content-Type": "application/json"}
                if token:
                    headers["Authorization"] = "Bearer " + token
                req = urllib.request.Request(url + path, json.dumps(body).encode("utf-8"), headers=headers)
                with urllib.request.urlopen(req, timeout=10) as response:
                    body = response.read()
                    return json.loads(body) if body else None

            user = {"email": "client@example.test", "password": "Course-Client-2026!"}
            request("/auth/register", user)
            token = request("/auth/token", user)["accessToken"]
            request("/api/v1/imports", {"batchId": str(uuid.uuid4()), "rows": [
                {"id": str(uuid.uuid4()), "date": "2026-01-10", "category": "food", "amount": 12.5}]}, token)
            env.update({"LEDGER_API": url + "/", "LEDGER_TOKEN": token})
            result = json.loads(scenario("report").stdout)
            require(result == [{"Category": "food", "Count": 1, "AmountMinor": 1250}], result)
            env.pop("LEDGER_TOKEN")
        finally:
            stop(process, stream)

        blazor = base / "BlazorClient/bin/Release/net10.0/BlazorClient.dll"
        process, stream, url = server(blazor, base / "BlazorClient", "blazor.log")
        try:
            with urllib.request.urlopen(url, timeout=10) as response:
                html = response.read().decode("utf-8")
            require("blazor.web.js" in html and "Отчёт расходов" in html and "server" in html,
                    "Blazor host did not return its page and interactive server marker.")
            with urllib.request.urlopen(url + "/_framework/blazor.web.js", timeout=10) as response:
                require(response.status == 200 and len(response.read()) > 1000, "Blazor framework asset missing.")
        finally:
            stop(process, stream)

    if args.godot:
        version = run([args.godot.resolve(), "--version"])
        require("4.6.2.stable.mono" in version.stdout, "Expected Godot 4.6.2 .NET.")
        result = run([args.godot.resolve(), "--headless", "--path", base / "GodotClient", "--", "--verify"], timeout=30)
        require("PASS Godot:" in result.stdout and "ERROR" not in result.stderr, result.stdout + result.stderr)
    summary = {"xunit_passed": 43, "scenario_assertions": checks, "wpf_executed": os.name == "nt",
               "godot_compiled": True, "godot_executed": bool(args.godot), "mobile_executed": False,
               "unity_executed": False, "browser_e2e_executed": False}
    (output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"PASS Specializations: 43 xUnit/bUnit cases; {checks} scenario assertions; "
          f"WPF={os.name == 'nt'}, Godot engine={bool(args.godot)}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, OSError, ValueError, subprocess.TimeoutExpired) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        sys.exit(1)
