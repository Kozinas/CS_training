"""Build the C# examples and check observable contracts without external Python packages."""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger-dll", type=Path, help="Check a learner's already built L01 DLL")
    parser.add_argument("--core-only", action="store_true", help="Only chapters 01-24, without external NuGet packages")
    args = parser.parse_args()
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    env = os.environ.copy()
    env.update({
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_NOLOGO": "1",
        "DOTNET_CLI_HOME": str(artifacts / "dotnet-home"),
        "NUGET_PACKAGES": str(artifacts / "packages"),
    })
    checks = 0

    def run(command, cwd=ROOT, timeout=60):
        return subprocess.run(command, cwd=cwd, env=env, capture_output=True,
                              text=True, encoding="utf-8", errors="replace", timeout=timeout)

    def require(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            raise AssertionError(message)

    def normalize(text):
        return text.replace("\r\n", "\n")

    if args.ledger_dll:
        ledger = args.ledger_dll.resolve()
        if not ledger.is_file():
            raise ValueError(f"DLL not found: {ledger}")
    else:
        build = run(["dotnet", "build", "CSharpCourse.slnx", "-c", "Release", "--nologo"],
                    cwd=ROOT / "examples", timeout=120)
        if build.returncode:
            raise RuntimeError(build.stdout + build.stderr)
        print("PASS build: CSharpCourse.slnx (Release, warnings treated as errors)")
        base = ROOT / "examples"
        foundation = base / "Foundations/bin/Release/net10.0/Foundations.dll"
        ledger = base / "ExpenseLedger/bin/Release/net10.0/ExpenseLedger.dll"
        expected = {
            "01": "Hello, C#!\n12\n",
            "02": "total=100\n",
            "03": "2\n2.5\nFalse\nTrue\n53.73\n",
            "04": "15\n",
            "05": "426.70\n11\nFalse\n",
            "06": "2\nTrue\n12.50\n0\n",
            "07": "9\n9\n20.00\n",
            "08": "food\n12.50\nTrue\n",
            "09": "2\n",
        }
        for chapter, output in expected.items():
            result = run(["dotnet", str(foundation), chapter])
            require(result.returncode == 0 and normalize(result.stdout) == output and not result.stderr,
                    f"Foundations {chapter}: {result}")
        contracts = run(["dotnet", str(base / "ContractChecks/bin/Release/net10.0/ContractChecks.dll")])
        require(contracts.returncode == 0 and not contracts.stderr, contracts.stdout + contracts.stderr)
        print(contracts.stdout.strip())

        advanced = base / "TypesAndQueries/bin/Release/net10.0/TypesAndQueries.dll"
        advanced_expected = {
            "11": "Detailed\ntext\n",
            "12": "True\nTrue\n3.50\n",
            "13": "3\n5\n0\nduplicate_id\n",
            "14": "0\n1\n2\n",
            "15": "3\n2\ndisposed\n",
            "16": "2,3\n2,3\n",
            "17": "large\nTrue\n1500.00\n",
            "18": "duplicate_id\nfood=20.00 (2)\ntravel=30.00 (1)\nnotifications=4\n",
        }
        for chapter, output in advanced_expected.items():
            result = run(["dotnet", str(advanced), chapter])
            require(result.returncode == 0 and normalize(result.stdout) == output and not result.stderr,
                    f"TypesAndQueries {chapter}: {result}")
        core = run(["dotnet", str(base / "CoreChecks/bin/Release/net10.0/CoreChecks.dll")])
        require(core.returncode == 0 and not core.stderr, core.stdout + core.stderr)
        print(core.stdout.strip())

        runtime = base / "RuntimeLab/bin/Release/net10.0/RuntimeLab.dll"
        runtime_expected = {
            "19": "RuntimeLab\n42\nTrue\n7\n7\n1\n",
            "20": "1\n0\n7\n",
            "21": "Name\n0\nGreaterThan\nTrue\n4\n",
            "23": "True\n42\nFalse\n9\n7\n",
            "24": "ledger\nList\n0\n3\n5\n9\n7\n6\n",
        }
        for chapter, output in runtime_expected.items():
            result = run(["dotnet", str(runtime), chapter])
            require(result.returncode == 0 and normalize(result.stdout) == output and not result.stderr,
                    f"RuntimeLab {chapter}: {result}")
        for project in ("RuntimeChecks", "CompilerLab"):
            result = run(["dotnet", str(base / f"{project}/bin/Release/net10.0/{project}.dll")])
            require(result.returncode == 0 and result.stdout.startswith(f"PASS {project}:")
                    and not result.stderr, result.stdout + result.stderr)
            print(result.stdout.strip())
        result = run(["dotnet", "run", "--file", "FileBased/hello.cs"], cwd=base, timeout=120)
        require(result.returncode == 0 and normalize(result.stdout) == "file-based C# 14\n"
                and not result.stderr, f"File-based app: {result}")
        if not args.core_only:
            integration = run([sys.executable, "-B", str(ROOT / "tools/verify_integration.py")],
                              timeout=240)
            require(integration.returncode == 0 and not integration.stderr,
                    integration.stdout + integration.stderr)
            print(integration.stdout.strip())
            pipeline = run([sys.executable, "-B", str(ROOT / "tools/verify_pipeline.py"), "--no-build"],
                           timeout=240)
            require(pipeline.returncode == 0 and not pipeline.stderr, pipeline.stdout + pipeline.stderr)
            print(pipeline.stdout.strip())
            engineering = run([sys.executable, "-B", str(ROOT / "tools/verify_engineering.py")], timeout=360)
            require(engineering.returncode == 0 and not engineering.stderr,
                    engineering.stdout + engineering.stderr)
            print(engineering.stdout.strip())
            data = run([sys.executable, "-B", str(ROOT / "tools/verify_data.py")], timeout=360)
            require(data.returncode == 0 and not data.stderr, data.stdout + data.stderr)
            print(data.stdout.strip())
            web = run([sys.executable, "-B", str(ROOT / "tools/verify_web.py")], timeout=360)
            require(web.returncode == 0 and not web.stderr, web.stdout + web.stderr)
            print(web.stdout.strip())
            specializations = run([sys.executable, "-B", str(ROOT / "tools/verify_specializations.py")], timeout=360)
            require(specializations.returncode == 0 and not specializations.stderr,
                    specializations.stdout + specializations.stderr)
            print(specializations.stdout.strip())
            delivery = run([sys.executable, "-B", str(ROOT / "tools/verify_delivery.py")], timeout=720)
            require(delivery.returncode == 0 and not delivery.stderr, delivery.stdout + delivery.stderr)
            print(delivery.stdout.strip())

    with tempfile.TemporaryDirectory(prefix="ledger-check-", dir=artifacts) as temporary:
        folder = Path(temporary)

        def invoke(arguments):
            return run(["dotnet", str(ledger), *map(str, arguments)])

        def success(name, content, expected):
            file = folder / (name + ".txt")
            file.write_bytes(content if isinstance(content, bytes) else content.encode("utf-8"))
            result = invoke([file])
            require(result.returncode == 0 and normalize(result.stdout) == expected and not result.stderr,
                    f"{name}: expected success {expected!r}; got {result}")

        def failure(name, content, code, prefix):
            file = folder / (name + ".txt")
            file.write_bytes(content if isinstance(content, bytes) else content.encode("utf-8"))
            result = invoke([file])
            require(result.returncode == code and not result.stdout and result.stderr.startswith(prefix),
                    f"{name}: expected code={code}, stderr prefix={prefix!r}, empty stdout; got {result}")

        report = "count=3\ntotal=50.00\nfood=20.00\ntravel=30.00\n"
        valid = (ROOT / "labs/01-expense-ledger/data/valid.txt").read_text(encoding="utf-8")
        success("normal", valid, report)
        success("empty", "", "count=0\ntotal=0.00\n")
        success("blank", "\n \t\n", "count=0\ntotal=0.00\n")
        success("bom", b"\xef\xbb\xbf" + valid.encode("utf-8"), report)
        success("crlf", valid.replace("\n", "\r\n").encode("utf-8"), report)
        success("no-final-newline", valid.rstrip("\n"), report)
        success("field-spaces", " 2026-01-10 ; FOOD ; 12.50 \n",
                "count=1\ntotal=12.50\nfood=12.50\n")
        success("limits", "2026-01-01;max;999999.99\n2026-01-01;min;0.01\n",
                "count=2\ntotal=1000000.00\nmax=999999.99\nmin=0.01\n")
        success("duplicate", "2024-02-29;a;1\n2024-02-29;A;1\n", "count=2\ntotal=2.00\na=2.00\n")
        success("fractions", "2026-01-01;f;1.5\n2026-01-01;f;001.50\n", "count=2\ntotal=3.00\nf=3.00\n")
        success("long-category", f"2026-01-01;{'a' * 32};1\n",
                f"count=1\ntotal=1.00\n{'a' * 32}=1.00\n")
        for i, amount in enumerate(["0", "-1", "+1", "1000000", "1.234", "1,50", ".5",
                                     "1.", "1e2", "NaN", "1,000", "1..5", "", "1 0",
                                     "９", "9" * 80]):
            failure(f"amount-{i}", f"2026-01-01;food;{amount}\n", 4, "line 1:")
        for i, date in enumerate(["2026-02-29", "2026-02-30", "2026-13-01", "26-01-01", "2026-1-01"]):
            failure(f"date-{i}", f"{date};food;1\n", 4, "line 1:")
        for i, category in enumerate(["", "a" * 33, "еда", "1food", "foo bar", "a/b"]):
            failure(f"category-{i}", f"2026-01-01;{category};1\n", 4, "line 1:")
        failure("extra-field", "2026-01-01;food;1;extra\n", 4, "line 1:")
        failure("missing-field", "2026-01-01;food\n", 4, "line 1:")
        failure("line-number", "\n2026-01-01;food;1\ninvalid\n", 4, "line 3:")
        failure("no-partial-report", "2026-01-01;food;1\n2026-01-02;food;bad\n", 4, "line 2:")
        failure("invalid-utf8", b"2026-01-01;food;1\n\xff", 4, "encoding:")
        failure("utf16", valid.encode("utf-16"), 4, "encoding:")
        row = "2026-01-01;a;0.01\n"
        success("100000-records", row * 100_000, "count=100000\ntotal=1000.00\na=1000.00\n")
        failure("100001-records", row * 100_001, 4, "line 100001:")
        for arguments, code, prefix in [([], 2, "usage:"), (["a", "b"], 2, "usage:"),
                                       ([folder / "missing.txt"], 3, "io:"), ([folder], 3, "io:")]:
            result = invoke(arguments)
            require(result.returncode == code and not result.stdout and result.stderr.startswith(prefix),
                    f"arguments={arguments}: {result}")

    print(f"PASS: {checks} build-independent scenario assertions")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (AssertionError, ValueError, RuntimeError, OSError, subprocess.TimeoutExpired) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        sys.exit(1)
