"""Create and resolve a real Git conflict in an isolated disposable repository; never push."""
from __future__ import annotations

import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--keep", action="store_true", help="Keep the isolated repository for inspection")
    args = parser.parse_args()
    artifacts = ROOT / ".artifacts"
    artifacts.mkdir(exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="git-practice-", dir=artifacts)).resolve()
    if not work.is_relative_to(artifacts.resolve()):
        raise RuntimeError("Practice repository must remain inside .artifacts")
    hooks = work / "empty-hooks"
    hooks.mkdir()

    def git(*arguments, expected=0):
        result = subprocess.run(["git", "-c", "user.name=Course Example", "-c", "user.email=course@example.invalid",
                                 "-c", "commit.gpgsign=false", "-c", "core.autocrlf=false",
                                 "-c", "core.hooksPath=" + str(hooks), *arguments], cwd=work,
                                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30)
        if result.returncode != expected:
            raise RuntimeError(result.stdout + result.stderr)
        return result.stdout

    try:
        git("init", "--initial-branch=main", "--quiet")
        file = work / "limits.txt"
        file.write_text("auto=1000\nwindow=30\n", encoding="utf-8", newline="\n")
        git("add", "--", "limits.txt")
        git("commit", "-qm", "Document initial limits")
        git("switch", "-c", "feature/limit")
        file.write_text("auto=500\nwindow=30\n", encoding="utf-8", newline="\n")
        git("add", "--", "limits.txt")
        git("commit", "-qm", "Lower automatic approval limit")
        git("switch", "main")
        file.write_text("auto=2000\nwindow=30\n", encoding="utf-8", newline="\n")
        git("add", "--", "limits.txt")
        git("commit", "-qm", "Raise automatic approval limit")
        git("merge", "--no-edit", "feature/limit", expected=1)
        if "<<<<<<<" not in file.read_text() or not git("ls-files", "-u"):
            raise RuntimeError("Expected a real unresolved conflict")
        # The exercise contract explicitly resolves the business decision to 1500.
        file.write_text("auto=1500\nwindow=30\n", encoding="utf-8", newline="\n")
        git("add", "--", "limits.txt")
        git("diff", "--cached", "--check")
        git("commit", "-qm", "Resolve agreed approval limit to 1500")
        if git("ls-files", "-u") or len(git("rev-list", "--parents", "-n", "1", "HEAD").split()) != 3:
            raise RuntimeError("Expected a resolved merge commit with two parents")
        if git("remote"):
            raise RuntimeError("Practice repository must not have a remote")
        print("PASS Git practice: two branches, reproduced conflict, explicit resolution and two-parent merge; no remote")
        if args.keep:
            print(f"Practice repository: {work}")
    finally:
        if not args.keep:
            if not work.is_relative_to(artifacts.resolve()):
                raise RuntimeError("Refusing cleanup outside .artifacts")
            # Files are created by this exercise; read-only Git objects may need a writable mode on Windows.
            def writable_remove(function, path, exc_info):
                Path(path).chmod(0o700)
                function(path)
            shutil.rmtree(work, onerror=writable_remove)


if __name__ == "__main__":
    main()
