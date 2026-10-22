"""Prove the deadline regression test rejects a deliberate one-character defect in an isolated copy."""
from __future__ import annotations

import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main():
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    source = ROOT / "examples/Engineering"
    env = {**os.environ, "DOTNET_NOLOGO": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
           "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"), "NUGET_PACKAGES": str(artifacts / "packages")}
    with tempfile.TemporaryDirectory(prefix="boundary-repro-", dir=artifacts) as temporary:
        work = Path(temporary).resolve()
        assert work.is_relative_to(artifacts.resolve())
        for project in ("ReviewRules", "ReviewRules.Tests"):
            shutil.copytree(source / project, work / project, ignore=shutil.ignore_patterns("bin", "obj"))
        for file in ("global.json", "Directory.Packages.props", "NuGet.Config", ".editorconfig"):
            shutil.copyfile(source / file, work / file)
        shutil.copyfile(ROOT / "examples/Directory.Build.props", work / "Course.props")
        (work / "Directory.Build.props").write_text(
            (source / "Directory.Build.props").read_text(encoding="utf-8").replace(
                '../Directory.Build.props', 'Course.props'), encoding="utf-8")
        target = work / "ReviewRules/ApprovalPolicy.cs"
        correct = target.read_text(encoding="utf-8")
        marker = "now - submittedAt >= MaximumAge"
        if correct.count(marker) != 1:
            raise RuntimeError("Regression marker changed; update the controlled defect explicitly")
        target.write_text(correct.replace(marker, "now - submittedAt > MaximumAge"), encoding="utf-8")
        for command in (["dotnet", "restore", "ReviewRules.Tests/ReviewRules.Tests.csproj", "--locked-mode"],
                        ["dotnet", "build", "ReviewRules.Tests/ReviewRules.Tests.csproj", "-c", "Release", "--no-restore"]):
            result = subprocess.run(command, cwd=work, env=env, capture_output=True, text=True,
                                    encoding="utf-8", errors="replace", timeout=180)
            if result.returncode:
                raise RuntimeError("Defect must compile before its behavior is checked:\n" + result.stdout + result.stderr)
        result = subprocess.run([
            "dotnet", "test", "--project", "ReviewRules.Tests", "-c", "Release", "--no-build",
            "--filter-method", "Course.ReviewTests.PolicyTests.Exact_deadline_is_expired",
            "--report-trx", "--results-directory", str(work / "results")],
            cwd=work, env=env, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
        files = list((work / "results").glob("*.trx"))
        if len(files) != 1:
            raise RuntimeError("Missing regression result:\n" + result.stdout + result.stderr)
        root = ET.parse(files[0]).getroot()
        counters = root.find("t:ResultSummary/t:Counters", NS)
        failed = root.findall("t:Results/t:UnitTestResult[@outcome='Failed']", NS)
        if (result.returncode == 0 or counters is None or counters.get("total") != "1"
                or len(failed) != 1 or "Exact_deadline_is_expired" not in failed[0].get("testName", "")):
            raise RuntimeError("Expected exactly the deadline assertion to fail:\n" + result.stdout + result.stderr)
        message = failed[0].find("t:Output/t:ErrorInfo/t:Message", NS)
        if message is None or "Expired" not in (message.text or "") or "AutoApproved" not in (message.text or ""):
            raise RuntimeError("Failure was unrelated to the intended boundary defect")
        report = artifacts / "engineering/boundary-repro.txt"
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text("Mutation: >= becomes >\nExpected failing test: Exact_deadline_is_expired\n"
                          + (message.text or "") + "\n", encoding="utf-8")
        print("PASS regression probe: defective copy builds; exact deadline test fails with Expired/AutoApproved")


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)
