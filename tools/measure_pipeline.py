"""Small end-to-end load experiment; timings are observations, never pass/fail thresholds."""
from __future__ import annotations

import json
import os
from pathlib import Path
import statistics
import subprocess
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]


def main():
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    cli = ROOT / "examples/Integration/PipelineWorker/bin/Release/net10.0/PipelineWorker.dll"
    results = []
    with tempfile.TemporaryDirectory(prefix="pipeline-measure-", dir=artifacts) as temporary:
        folder = Path(temporary)
        source = folder / "input.ndjson"
        with source.open("w", encoding="utf-8", newline="\n") as output:
            for i in range(1, 10001):
                output.write(json.dumps({"id": str(uuid.UUID(int=i)), "date": "2026-01-10",
                                         "category": "food", "amount": "1.00"}) + "\n")
        for workers in (1, 4):
            elapsed = []
            # One unreported warmup process, then three independent runs with fresh state.
            for iteration in range(4):
                state = folder / f"state-{workers}-{iteration}.json"
                started = time.perf_counter()
                result = subprocess.run(["dotnet", str(cli), "--file", str(source), "--state", str(state),
                                         "--workers", str(workers), "--capacity", "8", "--window", "16"],
                                        cwd=ROOT, env={**os.environ, "DOTNET_NOLOGO": "1"},
                                        capture_output=True, text=True, encoding="utf-8", timeout=90)
                duration = time.perf_counter() - started
                if result.returncode or json.loads(result.stdout) != {
                    "processed": 10000, "added": 10000, "duplicates": 0, "failed": 0,
                    "total": 10000, "retried": 0, "errors": [], "errorsTruncated": False
                }:
                    raise RuntimeError(result.stdout + result.stderr)
                if iteration:
                    elapsed.append(duration)
            median = statistics.median(elapsed)
            results.append({"workers": workers, "capacity": 8, "window": 16, "records": 10000,
                            "seconds": elapsed, "median_seconds": median, "records_per_second": 10000 / median})
    output = artifacts / "pipeline-load.json"
    output.write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print(output.read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
