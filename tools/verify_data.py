"""Verify real SQLite storage, EF migrations, Dapper, concurrency and the L05 process contract."""
from __future__ import annotations

from contextlib import closing
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main():
    base = ROOT / "examples/Data"
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    env = {**os.environ, "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1",
           "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"), "NUGET_PACKAGES": str(artifacts / "packages")}
    checks = 0

    def require(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            raise RuntimeError(message)

    def run(command, cwd=base, timeout=180):
        result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, capture_output=True,
                                text=True, encoding="utf-8", errors="replace", timeout=timeout)
        require(result.returncode == 0, result.stdout + result.stderr)
        return result

    run(["dotnet", "restore", "Data.slnx", "--locked-mode"])
    run(["dotnet", "build", "Data.slnx", "-c", "Release", "--no-restore"])
    run(["dotnet", "tool", "restore"])
    ef = ["--project", "Persistence", "--startup-project", "DataCli", "--configuration", "Release", "--no-build"]
    run(["dotnet", "ef", "migrations", "has-pending-model-changes", *ef])
    dll = base / "DataCli/bin/Release/net10.0/DataCli.dll"
    fixture = ROOT / "labs/05-durable-storage/data"

    with tempfile.TemporaryDirectory(prefix="data-check-", dir=artifacts) as temporary:
        folder = Path(temporary).resolve()
        assert folder.is_relative_to(artifacts.resolve())
        results = folder / "results"
        run(["dotnet", "test", "--project", "Persistence.Tests", "-c", "Release", "--no-build",
             "--report-trx", "--results-directory", results])
        reports = list(results.glob("*.trx"))
        require(len(reports) == 1, "Exactly one fresh TRX")
        counts = ET.parse(reports[0]).find("t:ResultSummary/t:Counters", NS)
        require(counts is not None and counts.get("total") == counts.get("passed") == "26", "26 passed cases, no skipped tests")
        print("PASS Persistence.Tests: 26 cases on real SQLite files; no skipped tests")
        script = folder / "migrations.sql"
        run(["dotnet", "ef", "migrations", "script", "0", *ef, "--output", script])
        require(script.read_text(encoding="utf-8-sig").rstrip("\n") ==
                (base / "sql/migrations.sql").read_text(encoding="utf-8-sig").rstrip("\n"),
                "Checked-in SQL must match the migrations")
        with closing(sqlite3.connect(folder / "script.db")) as connection:
            connection.executescript(script.read_text(encoding="utf-8-sig"))
            require(connection.execute("SELECT count(*) FROM __EFMigrationsHistory").fetchone()[0] == 2, "SQL script applies both migrations")

        def cli(*arguments, code=0):
            result = subprocess.run(["dotnet", str(dll), *map(str, arguments)], cwd=ROOT, env=env,
                                    capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30)
            require(result.returncode == code, f"CLI {arguments}: {result}")
            if code:
                require(not result.stdout and bool(result.stderr), "Failure must have stderr and no success JSON")
                return result.stderr.strip()
            require(not result.stderr, result.stderr)
            return json.loads(result.stdout)

        db = folder / "ledger.db"
        batch = "10000000-0000-0000-0000-000000000001"
        other = "10000000-0000-0000-0000-000000000002"
        conflict = "10000000-0000-0000-0000-000000000003"
        expense = "00000000-0000-0000-0000-000000000001"
        require(cli("init", db) == {"initialized": True}, "Initialize")
        cli("init", db)
        first = {"added": 3, "duplicates": 1, "replayed": False}
        require(cli("import", db, batch, fixture / "batch.json") == first, "First import")
        require(cli("import", db, batch, fixture / "batch.json") == {**first, "replayed": True}, "Replay original receipt")
        require(cli("import", db, other, fixture / "batch.json") == {"added": 0, "duplicates": 4, "replayed": False}, "New batch, same expenses")
        require(cli("import", db, batch, fixture / "conflict.json", code=4) == "batch_id_conflict", "Batch key conflict")
        require(cli("import", db, conflict, fixture / "conflict.json", code=4) == "expense_id_conflict", "Expense conflict")
        require(cli("report", db) == [{"category": "food", "count": 2, "amountMinor": 2000},
                                       {"category": "travel", "count": 1, "amountMinor": 3000}], "Rollback preserved report")
        page = cli("page", db, "0001-01-01", 0, 2)
        require(len(page["rows"]) == 2 and page["next"]["sequence"] == 2, "First keyset page")
        last = cli("page", db, "2026-01-10", 2, 2)
        require(len(last["rows"]) == 1 and last["next"] is None, "Last keyset page")
        require(cli("update", db, expense, 0, "13.50") == {"updated": True, "version": 1}, "Versioned update")
        require(cli("update", db, expense, 0, "99", code=4) == "stale_version_or_missing", "Stale update")
        require(cli("import", db, batch, fixture / "batch.json") == {**first, "replayed": True}, "Receipt survives later edits")
        saved_report = cli("report", db)
        backup, restored = folder / "backup.db", folder / "restored.db"
        cli("backup", db, backup)
        backup_bytes = backup.read_bytes()
        cli("backup", db, backup, code=3)
        require(backup.read_bytes() == backup_bytes, "Existing backup unchanged")
        cli("update", db, expense, 1, "99")
        shutil.copyfile(backup, restored)
        require(cli("report", restored) == saved_report, "Restore in a different database")
        require(cli("integrity", restored) == {"integrity": "ok"}, "Integrity and foreign keys")
        plan = cli("plan", db)
        require(any("IX_Expenses_Day_Sequence" in p and "SEARCH" in p for p in plan), "Range index in actual plan")

        invalid = folder / "invalid.json"
        source = json.loads((fixture / "batch.json").read_text(encoding="utf-8"))
        for bad in ({"version": 2, "records": []}, {"version": 1, "records": [None]},
                    {"version": 1, "records": source["records"] * 251}):
            invalid.write_text(json.dumps(bad), encoding="utf-8")
            cli("import", restored, conflict, invalid, code=3)
        invalid.write_bytes(b" " * 1048577)
        cli("import", restored, conflict, invalid, code=3)
        cli("page", restored, "2026-01-10", 0, 101, code=2)
        cli("update", restored, expense, 1, "1.234", code=2)
        require(cli("report", restored) == saved_report, "Invalid requests cannot change data")

        raced = folder / "raced.db"
        cli("init", raced)
        command = ["dotnet", str(dll), "import", str(raced), batch, str(fixture / "batch.json")]
        processes = [subprocess.Popen(command, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                      text=True, encoding="utf-8") for _ in range(2)]
        outcomes = []
        try:
            for process in processes:
                stdout, stderr = process.communicate(timeout=30)
                require(process.returncode == 0 and not stderr, stdout + stderr)
                outcomes.append(json.loads(stdout))
        finally:
            for process in processes:
                if process.poll() is None:
                    process.kill()
                    process.communicate()
        require(sorted(o["replayed"] for o in outcomes) == [False, True], "Two processes, one receipt")
        with closing(sqlite3.connect(raced)) as connection:
            require(connection.execute("SELECT count(*) FROM Expenses").fetchone()[0] == 3, "No process race duplicates")
            require(connection.execute("SELECT count(*) FROM ImportReceipts").fetchone()[0] == 1, "Exactly one durable receipt")
        output = artifacts / "data"
        output.mkdir(exist_ok=True)
        (output / "query-plan.txt").write_text("\n".join(plan) + "\n", encoding="utf-8")
        (output / "summary.json").write_text(json.dumps({"tests": 26, "migrations": 2, "process_scenarios": checks,
            "provider": "Microsoft.Data.Sqlite 10.0.13", "server_database_tested": False}, indent=2) + "\n", encoding="utf-8")
    print(f"PASS L05: {checks} scenario assertions; locked restore; migration SQL; CLI; process race; backup/restore")


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, ValueError, subprocess.SubprocessError, ET.ParseError, sqlite3.Error) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
