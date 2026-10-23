"""Verify chapters 55-58 and L07 on Windows: native code, migration, publish and recovery."""
from __future__ import annotations

from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid

from package_release import create_release, verify_release

ROOT = Path(__file__).resolve().parents[1]


def main():
    if os.name != "nt":
        raise RuntimeError("This release exercise requires Windows x64, .NET Framework 4.8+ and the C++ AOT toolchain.")
    base = ROOT / "examples/Delivery"
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    output = artifacts / "delivery"
    output.mkdir(exist_ok=True)
    env = {**os.environ, "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"),
           "NUGET_PACKAGES": str(artifacts / "packages"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"}
    checks = 0

    def require(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            raise RuntimeError(str(message))

    def run(command, expected=0, timeout=240, extra=None):
        result = subprocess.run(list(map(str, command)), cwd=base, env={**env, **(extra or {})},
                                capture_output=True, encoding="utf-8", errors="replace", timeout=timeout)
        require(result.returncode == expected, result.stdout + result.stderr)
        return result

    run(["dotnet", "restore", "Delivery.slnx", "--locked-mode"])
    run(["dotnet", "build", "Delivery.slnx", "-c", "Release", "--no-restore", "--nologo"])
    compatibility = []
    for command, count in [([base / "CompatibilityProbe/bin/Release/net48/CompatibilityProbe.exe"], 36),
                           (["dotnet", base / "CompatibilityProbe/bin/Release/net10.0/CompatibilityProbe.dll"], 19)]:
        result = run(command)
        require(f"PASS Compatibility: {count} checks" in result.stdout and not result.stderr, result.stdout + result.stderr)
        compatibility.append(count)
    plugin = json.loads(run(["dotnet", "PluginHost/bin/Release/net10.0/PluginHost.dll",
                             base / "SamplePlugin/bin/Release/net10.0/SamplePlugin.dll"]).stdout)
    require(plugin == {"Name": "whole-hundreds", "Value": 1200, "Unloaded": True, "Retained": True, "Released": True}, plugin)
    lifecycle = json.loads(run(["dotnet", "LifecycleProbe/bin/Release/net10.0/LifecycleProbe.dll"]).stdout)
    require(lifecycle == {"Accepted": 3, "OverloadRejected": True, "ClosedRejected": True,
                          "Processed": [1, 2, 3], "Drained": True}, lifecycle)

    publication = {}
    incidents = []
    with tempfile.TemporaryDirectory(prefix="delivery-check-", dir=artifacts) as temporary:
        folder = Path(temporary).resolve()
        assert folder.parent == artifacts.resolve()  # All automatic cleanup stays in this owned directory.
        fixture = folder / "данные with spaces.bin"
        fixture.write_bytes(bytes(range(256)))
        interop = base / "InteropProbe/bin/Release/net10.0/InteropProbe.dll"
        result = json.loads(run(["dotnet", interop, "size", fixture]).stdout)
        require(result == {"Bytes": 256, "Closed": True, "RejectsClosed": True}, result)
        result = run(["dotnet", interop, "size", folder / "absent.bin"], expected=3)
        require(not result.stdout and result.stderr.strip() == "native-error: 2", result)

        modes = {
            "fdd": ["--self-contained", "false", "-p:PublishAot=false", "-p:PublishTrimmed=false"],
            "single-trimmed": ["--self-contained", "true", "-p:PublishAot=false", "-p:PublishTrimmed=true", "-p:PublishSingleFile=true"],
            "aot": ["--self-contained", "true", "-p:PublishAot=true"],
        }
        for mode, flags in modes.items():
            target = folder / "publish" / mode
            result = run(["dotnet", "publish", "InteropProbe/InteropProbe.csproj", "-c", "Release", "-r", "win-x64",
                          "--no-restore", "--nologo", *flags, "-o", target], timeout=300)
            require("warning IL" not in result.stdout and "warning IL" not in result.stderr,
                    "AOT/trimming warnings must be resolved, not ignored.")
            executable = target / "InteropProbe.exe"
            require(executable.is_file(), f"Missing {mode} executable.")
            require(json.loads(run([executable, "size", fixture]).stdout) == {"Bytes": 256, "Closed": True, "RejectsClosed": True}, mode)
            result = run([executable, "size", folder / "missing.bin"], expected=3)
            require(result.stderr.strip() == "native-error: 2" and not result.stdout, mode)
            publication[mode] = {"files": sum(p.is_file() for p in target.rglob("*")),
                                 "bytes": sum(p.stat().st_size for p in target.rglob("*") if p.is_file()),
                                 "executable_bytes": executable.stat().st_size}
        require(not (folder / "publish/aot/InteropProbe.dll").exists(), "AOT artifact unexpectedly needs an IL application DLL.")
        empty_runtime = folder / "empty-runtime"
        empty_runtime.mkdir()
        isolated = {"DOTNET_ROOT": str(empty_runtime), "DOTNET_ROOT_X64": str(empty_runtime)}
        for mode in ["single-trimmed", "aot"]:
            require(json.loads(run([folder / f"publish/{mode}/InteropProbe.exe", "size", fixture], extra=isolated).stdout)["Bytes"] == 256,
                    f"{mode} failed with an empty configured runtime location.")

        # The API/UI release uses the earlier labs, not a replacement toy service.
        first = folder / "releases/1.0.0"
        second = folder / "releases/1.1.0"
        first_manifest = create_release(first, "1.0.0")
        second_manifest = create_release(second, "1.1.0")
        require(first_manifest["files"]["api/LedgerWeb.dll"] != second_manifest["files"]["api/LedgerWeb.dll"], "Release versions did not reach the assembly.")
        require(verify_release(first)["version"] == "1.0.0" and verify_release(second)["version"] == "1.1.0", "Invalid releases.")
        tamper = first / "ui/appsettings.unexpected.json"
        tamper.write_text("{}", encoding="utf-8")
        try:
            verify_release(first)
        except ValueError:
            require(True, "Unexpected file rejected.")
        else:
            raise RuntimeError("Unexpected release file was accepted.")
        finally:
            tamper.unlink()
        member = first / "api/LedgerWeb.dll"
        original = member.read_bytes()
        try:
            member.write_bytes(original + b"tampered")
            try:
                verify_release(first)
            except ValueError:
                require(True, "Checksum mismatch rejected.")
            else:
                raise RuntimeError("Changed binary was accepted.")
        finally:
            member.write_bytes(original)
        manifest_path = first / "release.json"
        original_manifest = manifest_path.read_bytes()
        try:
            bad = {**first_manifest, "files": {"../escape": "0" * 64}}
            manifest_path.write_text(json.dumps(bad), encoding="utf-8")
            try:
                verify_release(first)
            except ValueError:
                require(True, "Traversal rejected.")
            else:
                raise RuntimeError("Traversal manifest was accepted.")
        finally:
            manifest_path.write_bytes(original_manifest)
        require(verify_release(first) == first_manifest, "Release was not restored after integrity probes.")

        upstream_mode = {"value": "ok"}
        slow_started = threading.Event()
        release_slow = threading.Event()

        class Upstream(BaseHTTPRequestHandler):
            def do_GET(self):
                mode = upstream_mode["value"]
                if mode == "slow":
                    slow_started.set()
                    release_slow.wait(10)
                code = 503 if mode == "fail" else 200
                data = b'{"value":1.25}' if mode != "bad" else b'{"value":-1}'
                try:
                    self.send_response(code)
                    self.send_header("Content-Type", "application/json")
                    self.send_header("Content-Length", str(len(data)))
                    self.end_headers()
                    self.wfile.write(data)
                except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
                    pass
            def log_message(self, *_):
                pass

        upstream = ThreadingHTTPServer(("127.0.0.1", 0), Upstream)
        thread = threading.Thread(target=upstream.serve_forever, daemon=True)
        thread.start()
        mutable = folder / "state"
        mutable.mkdir()
        database = mutable / "ledger.db"
        env.update({"DOTNET_ENVIRONMENT": "Development", "Ledger__Path": str(database),
                    "Keys__Directory": str(mutable / "keys"), "Web__AllowHttp": "true", "Worker__Enabled": "false",
                    "Rates__BaseUrl": f"http://127.0.0.1:{upstream.server_port}/",
                    "Logging__LogLevel__Microsoft.EntityFrameworkCore": "Warning"})
        processes = []

        def start(release, component="api"):
            name = "LedgerWeb" if component == "api" else "BlazorClient"
            path = release / component
            log_path = folder / f"process-{len(processes)}.log"
            stream = log_path.open("wb")
            process = subprocess.Popen(["dotnet", str(path / (name + ".dll")), "--urls", "http://127.0.0.1:0"],
                                       cwd=path, env=env, stdout=stream, stderr=subprocess.STDOUT)
            processes.append((process, stream))
            deadline = time.monotonic() + 30
            while time.monotonic() < deadline:
                content = log_path.read_text(encoding="utf-8", errors="replace")
                match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", content)
                if match:
                    return process, stream, match[1]
                if process.poll() is not None:
                    raise RuntimeError(content)
                time.sleep(0.05)
            raise RuntimeError("Published process did not become available.")

        def stop(process, stream):
            if process.poll() is None:
                process.terminate()  # Deliberate process termination, not a graceful-stop claim.
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)
            stream.close()

        def request(base_url, path, body=None, token=None, expected=200):
            headers = {"Content-Type": "application/json"}
            if token:
                headers["Authorization"] = "Bearer " + token
            request = urllib.request.Request(base_url + path,
                None if body is None else json.dumps(body).encode("utf-8"), headers=headers)
            try:
                response = urllib.request.urlopen(request, timeout=10)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                require(response.status == expected, f"{path}: expected {expected}, got {response.status}")
                content = response.read()
                kind = response.headers.get("Content-Type", "")
                return json.loads(content) if content and "json" in kind else content.decode("utf-8")

        try:
            p, stream, url = start(first)
            request(url, "/health/live")
            request(url, "/health/ready", expected=503)
            incidents.append({"symptom": "ready=503, live=200", "cause": "schema not applied", "action": "explicit migration"})
            stop(p, stream)
            run(["dotnet", first / "api/LedgerWeb.dll", "--migrate"])
            run(["dotnet", first / "api/LedgerWeb.dll", "--migrate"])
            p, stream, url = start(first)
            request(url, "/health/ready")
            credentials = {"email": "release@example.test", "password": "Course-Release-Only-2026!"}
            request(url, "/auth/register", credentials, expected=201)
            token = request(url, "/auth/token", credentials)["accessToken"]
            batch = {"batchId": str(uuid.uuid4()), "rows": [
                {"id": str(uuid.uuid4()), "date": "2026-01-10", "category": "food", "amount": 12.5}]}
            require(request(url, "/api/v1/imports", batch, token)["added"] == 1, "Import failed.")
            expected_report = [{"category": "food", "count": 1, "amountMinor": 1250}]
            require(request(url, "/api/v1/report", token=token) == expected_report, "Wrong initial report.")
            request(url, "/api/v1/rate", token=token)
            upstream_mode["value"] = "fail"
            request(url, "/api/v1/rate", token=token, expected=503)
            require(request(url, "/api/v1/report", token=token) == expected_report, "Unrelated storage endpoint failed.")
            upstream_mode["value"] = "slow"
            request(url, "/api/v1/rate", token=token, expected=504)
            require(slow_started.is_set(), "The deadline probe did not reach the dependency.")
            release_slow.set()
            upstream_mode["value"] = "ok"
            request(url, "/api/v1/rate", token=token)
            incidents.append({"symptom": "rate=503/504, report=200", "cause": "dependency failure/deadline", "action": "restore dependency; keep storage available"})
            stop(p, stream)

            backup = mutable / "backup.db"
            with closing(sqlite3.connect(database)) as source, closing(sqlite3.connect(backup)) as destination:
                source.backup(destination)
                require(destination.execute("PRAGMA integrity_check").fetchone()[0] == "ok", "Backup integrity failed.")

            # Both releases deliberately use the same schema; this is not rollback across a destructive migration.
            for release in (second, first):
                verify_release(release)
                p, stream, url = start(release)
                request(url, "/health/ready")
                require(request(url, "/api/v1/report", token=token) == expected_report, "Upgrade/rollback lost state.")
                require(request(url, "/api/v1/imports", batch, token)["replayed"] is True, "Receipt did not survive replacement.")
                stop(p, stream)

            restored = mutable / "restored.db"
            with closing(sqlite3.connect(backup)) as source, closing(sqlite3.connect(restored)) as destination:
                source.backup(destination)
            env["Ledger__Path"] = str(restored)
            p, stream, url = start(second)
            request(url, "/health/ready")
            require(request(url, "/api/v1/report", token=token) == expected_report, "Restored report differs.")
            require(request(url, "/api/v1/imports", batch, token)["replayed"] is True, "Restored receipt differs.")
            require(request(url, "/api/v1/report", token=token) == expected_report, "Restore/replay doubled the expense.")
            env["Ledger__Api"] = url + "/"
            ui_process, ui_stream, ui_url = start(second, "ui")
            html = request(ui_url, "/")
            require("Отчёт расходов" in html and "blazor.web.js" in html, "Published UI is incomplete.")
            require(len(request(ui_url, "/_framework/blazor.web.js")) > 1000, "Published UI asset is missing.")
            stop(ui_process, ui_stream)
            stop(p, stream)
            incidents.append({"symptom": "release replacement/restore", "cause": "controlled drill", "action": "same-schema rollback and SQLite backup restore validated"})
            require(verify_release(first) == first_manifest and verify_release(second) == second_manifest,
                    "Running the application wrote mutable data into an immutable release.")
        finally:
            release_slow.set()
            for process, stream in processes:
                if not stream.closed:
                    stop(process, stream)
            upstream.shutdown()
            upstream.server_close()
            thread.join(timeout=5)
    summary = {"scenario_assertions": checks, "compatibility_framework": compatibility[0],
               "compatibility_modern": compatibility[1], "native_aot": True, "trimmed_single_file": True,
               "collectible_plugin": True, "graceful_drain": True, "local_release_recovery": True,
               "container_executed": False, "external_deployment": False}
    for name, value in [("summary.json", summary), ("publication.json", publication), ("incidents.json", incidents)]:
        (output / name).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    print(f"PASS Delivery: {checks} scenario assertions; Framework/modern={compatibility[0]}/{compatibility[1]}; "
          "Win32, plugin unload, AOT, trimming, L07 publish/rollback/restore and drain")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError, sqlite3.Error, subprocess.TimeoutExpired) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        sys.exit(1)
