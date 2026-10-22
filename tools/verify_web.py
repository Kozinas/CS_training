"""Check L06 through WebApplicationFactory and a real Kestrel process on loopback."""
from __future__ import annotations

from concurrent.futures import ThreadPoolExecutor
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import re
import statistics
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main():
    base = ROOT / "examples/Web"
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    output = artifacts / "web"
    output.mkdir(exist_ok=True)
    env = {**os.environ, "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"),
           "NUGET_PACKAGES": str(artifacts / "packages"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"}
    checks = 0
    count_lock = threading.Lock()

    def require(condition, message):
        nonlocal checks
        with count_lock:
            checks += 1
        if not condition:
            raise RuntimeError(message)

    def run(command, timeout=180):
        result = subprocess.run(list(map(str, command)), cwd=base, env=env, capture_output=True,
                                text=True, encoding="utf-8", errors="replace", timeout=timeout)
        require(result.returncode == 0, result.stdout + result.stderr)
        return result

    run(["dotnet", "restore", "Web.slnx", "--locked-mode"])
    run(["dotnet", "build", "Web.slnx", "-c", "Release", "--no-restore"])
    run(["dotnet", "tool", "restore"])
    run(["dotnet", "ef", "migrations", "has-pending-model-changes", "--project", "LedgerWeb", "--configuration", "Release", "--no-build"])
    dll = base / "LedgerWeb/bin/Release/net10.0/LedgerWeb.dll"

    with tempfile.TemporaryDirectory(prefix="web-check-", dir=artifacts) as temporary:
        folder = Path(temporary).resolve()
        assert folder.is_relative_to(artifacts.resolve())
        results = folder / "results"
        run(["dotnet", "test", "--project", "Web.Tests", "-c", "Release", "--no-build", "--report-trx", "--results-directory", results])
        reports = list(results.glob("*.trx"))
        require(len(reports) == 1, "One fresh TRX")
        counters = ET.parse(reports[0]).find("t:ResultSummary/t:Counters", NS)
        require(counters is not None and counters.get("total") == counters.get("passed") == "30", "30 successful integration cases, no skipped tests")
        print("PASS Web.Tests: 30 cases; real Identity/SQLite; HTTP, Razor, gRPC, SignalR, outbox, telemetry")

        class Upstream(BaseHTTPRequestHandler):
            def log_message(self, *args):
                pass

            def do_GET(self):
                time.sleep(0.05)
                payload = b'{"error":"unavailable"}'
                self.send_response(503)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

        upstream = ThreadingHTTPServer(("127.0.0.1", 0), Upstream)
        thread = threading.Thread(target=upstream.serve_forever, daemon=True)
        thread.start()
        env.update({"DOTNET_ENVIRONMENT": "Development", "Ledger__Path": str(folder / "web.db"),
                    "Keys__Directory": str(folder / "keys"), "Web__AllowHttp": "true", "Worker__Enabled": "false",
                    "Rates__BaseUrl": f"http://127.0.0.1:{upstream.server_port}/", "Telemetry__Console": "true",
                    "Logging__LogLevel__Microsoft.EntityFrameworkCore": "Warning"})
        run(["dotnet", dll, "--migrate"])
        run(["dotnet", dll, "--migrate"])
        process = None
        log = None
        logs = []

        def start(worker):
            nonlocal process, log
            env["Worker__Enabled"] = str(worker).lower()
            path = folder / f"server-{len(logs)}.log"
            logs.append(path)
            log = path.open("wb")
            process = subprocess.Popen(["dotnet", str(dll), "--urls", "http://127.0.0.1:0"],
                                       cwd=base / "LedgerWeb", env=env, stdout=log, stderr=subprocess.STDOUT)
            until = time.monotonic() + 30
            while time.monotonic() < until:
                text = path.read_text(encoding="utf-8", errors="replace")
                match = re.search(r"Now listening on: (http://127\.0\.0\.1:\d+)", text)
                if match:
                    return match[1]
                if process.poll() is not None:
                    raise RuntimeError(text)
                time.sleep(0.05)
            raise RuntimeError("Kestrel startup deadline")

        def stop():
            nonlocal process, log
            if process is not None:
                if process.poll() is None:
                    process.terminate()  # Deliberate process termination to verify durable restart.
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)
                process = None
            if log is not None:
                log.close()
                log = None

        try:
            url = start(False)

            def request(path, body=None, token=None, method=None, expected=200, count=True):
                headers = {"Content-Type": "application/json"}
                if token:
                    headers["Authorization"] = "Bearer " + token
                data = json.dumps(body).encode() if body is not None else None
                req = urllib.request.Request(url + path, data=data, headers=headers, method=method)
                try:
                    response = urllib.request.urlopen(req, timeout=10)
                except urllib.error.HTTPError as error:
                    response = error
                with response:
                    payload = response.read()
                    message = f"{path}: expected {expected}, got {response.status}: {payload[:200]!r}"
                    if count:
                        require(response.status == expected, message)
                    elif response.status != expected:
                        raise RuntimeError(message)
                return json.loads(payload) if payload else None

            password = "Course-Only-Passw0rd!"
            credentials = {"email": "process@example.test", "password": password}
            request("/auth/register", credentials, expected=201)
            access = request("/auth/token", credentials)["accessToken"]
            request("/api/v1/report", expected=401)
            document = request("/openapi/v1.json")
            require("/api/v1/imports" in document["paths"], "OpenAPI includes import")
            expense = str(uuid.uuid4())
            batch = {"batchId": str(uuid.uuid4()), "rows": [{"id": expense, "date": "2026-01-10", "category": "food", "amount": 12.50}]}
            require(request("/api/v1/imports", batch, access) == {"added": 1, "duplicates": 0, "replayed": False}, "First process import")
            require(request("/api/v1/projection", token=access) == {"count": 0, "amountMinor": 0}, "Worker is disabled")
            require(request("/api/v1/report", token=access) == [{"category": "food", "count": 1, "amountMinor": 1250}], "Strong report")
            require(request("/api/v1/rate", token=access, expected=503)["title"] == "upstream_unavailable", "Real HTTP dependency failure")
            request(f"/api/v1/expenses/{expense}", {"expectedVersion": 0, "amount": 20}, access, method="PUT", expected=204)
            stop()
            url = start(True)
            # Same opaque token and persistent key ring survive process replacement.
            require(request("/api/v1/imports", batch, access) == {"added": 1, "duplicates": 0, "replayed": True}, "Receipt survives restart and later update")
            until = time.monotonic() + 10
            while True:
                projection = request("/api/v1/projection", token=access, count=False)
                if projection == {"count": 1, "amountMinor": 2000}:
                    break
                if time.monotonic() >= until:
                    raise RuntimeError(f"Worker did not catch up: {projection}")
                time.sleep(0.1)
            require(projection == {"count": 1, "amountMinor": 2000}, "Durable consumer catches up after restart")
            require(request(f"/api/v1/expenses/{expense}", token=access)["version"] == 1, "Version survives restart")

            def timed_read(_):
                begin = time.perf_counter()
                value = request("/api/v1/report", token=access)
                if value != [{"category": "food", "count": 1, "amountMinor": 2000}]:
                    raise RuntimeError(f"Unexpected load response: {value}")
                return (time.perf_counter() - begin) * 1000

            with ThreadPoolExecutor(max_workers=4) as pool:
                timings = list(pool.map(timed_read, range(40)))
            ordered = sorted(timings)
            load = {"requests": 40, "concurrency": 4, "errors": 0, "p50_ms": round(statistics.median(timings), 3),
                    "p95_ms": round(ordered[37], 3), "max_ms": round(max(timings), 3)}
            require(len(timings) == 40, "Small load experiment completed")
            stop()
            log_text = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in logs)
            require("ledger.import" in log_text and "Activity.TraceId" in log_text and "System.Net.Http" in log_text, "Console trace includes app and HTTP spans")
            require(password not in log_text and access not in log_text, "No password or bearer token in server logs")
            (output / "load.json").write_text(json.dumps(load, indent=2) + "\n", encoding="utf-8")
            # Keep only diagnostic span lines, not raw application logs or transient credentials.
            trace_lines = [line for line in log_text.splitlines() if line.startswith(("Activity.", "Instrumentation scope", "    http.request.method", "    http.response.status_code"))]
            (output / "trace-sample.txt").write_text("\n".join(trace_lines[:100]) + "\n", encoding="utf-8")
        finally:
            stop()
            upstream.shutdown()
            upstream.server_close()
            thread.join(timeout=5)

    summary = {"tests": 30, "scenario_assertions": checks, "http_server": "Kestrel", "database": "SQLite",
               "grpc_signalr": "TestServer", "external_broker_redis_oidc": False, "load": load}
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"PASS L06: {checks} assertions; Kestrel restart, durable outbox, real upstream HTTP, load and exported traces")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (AssertionError, RuntimeError, OSError, ValueError, subprocess.TimeoutExpired) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        sys.exit(1)
