"""Check the L08 reference rule and prove that two deliberate defects are detected."""
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def main():
    base = ROOT / 'examples/Capstone'
    env = {**os.environ, 'DOTNET_CLI_HOME': str(ROOT / '.artifacts/dotnet-home'),
           'NUGET_PACKAGES': str(ROOT / '.artifacts/packages'), 'DOTNET_CLI_TELEMETRY_OPTOUT': '1'}
    def run(command, expected=0):
        result = subprocess.run(command, cwd=base, env=env, capture_output=True,
                                encoding='utf-8', errors='replace', timeout=120)
        if result.returncode != expected:
            raise RuntimeError(result.stdout + result.stderr)
        return result
    run(['dotnet', 'restore', 'Capstone.slnx', '--locked-mode'])
    run(['dotnet', 'build', 'Capstone.slnx', '-c', 'Release', '--no-restore', '--nologo'])
    command = ['dotnet', str(base / 'AcceptanceChecks/bin/Release/net10.0/AcceptanceChecks.dll'),
               str(ROOT / 'labs/08-capstone/data/budget-cases.json')]
    correct = run(command)
    if 'PASS Capstone: 47 checks;' not in correct.stdout or correct.stderr:
        raise RuntimeError(correct.stdout + correct.stderr)
    for mutation, marker in [('strict-boundary', 'fixture: exact'),
                             ('double-enumeration', 'Sequence enumerated twice.')]:
        defective = run([*command, mutation], expected=1)
        if marker not in defective.stderr or defective.stdout:
            raise RuntimeError(f'Wrong failure for {mutation}: {defective}')
    output = ROOT / '.artifacts/capstone'
    output.mkdir(parents=True, exist_ok=True)
    (output / 'summary.json').write_text(json.dumps({'reference_assertions': 47,
        'detected_mutations': ['strict-boundary', 'double-enumeration'],
        'database_budget_implementation': False, 'student_project_assessed': False}, indent=2) + '\n', encoding='utf-8')
    print(correct.stdout.strip())
    print('PASS Capstone regression: strict boundary and repeated enumeration both rejected')


if __name__ == '__main__':
    main()
