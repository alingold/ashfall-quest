"""Run maintained CPU/source checks. Never launches Unity or opens network sockets."""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UNITY = ROOT / 'unity'
TOOLS = UNITY / 'Tools'
PLAYABLE = TOOLS / 'PlayableR23'


def run(command, label=None, output=None):
    result = subprocess.run([str(x) for x in command], cwd=ROOT, capture_output=True, text=True)
    if output is not None:
        (output / (label + '.log')).write_text(result.stdout + result.stderr)
    print(result.stdout + result.stderr, end='')
    if result.returncode:
        raise SystemExit(result.returncode)


def modules(output):
    output.mkdir(parents=True)
    sources = [ROOT / line.strip() for line in
               (PLAYABLE / 'Networking/Current/sources.txt').read_text().splitlines() if line.strip()]
    sources = sorted(set(sources + list((UNITY / 'Assets/Ashfall/Modules/Pickups').glob('*.cs'))))
    compiler = Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
    flags = [compiler, '/nologo', '/langversion:5', '/warn:4', '/warnaserror+']
    library = output / 'Live.dll'
    run(flags + ['/target:library', '/out:' + str(library)] + sources, 'compile-live', output)
    cases = [('HealthStates/HealthTests.cs', []),
             ('HealthStates/R2Tests.cs', ['CandidateHealthCombatBridge.cs']),
             ('Pickups/Tests.cs', []), ('Pickups/BatchTests.cs', []),
             ('Pickups/AdversarialR1Adapted.cs', [])]
    tests = [(TOOLS / 'Modules' / name, extras) for name, extras in cases]
    tests.append((PLAYABLE / 'TeamLaunch/Cases.cs', []))
    tests.append((PLAYABLE / 'Match/MatchChecks.cs', []))
    rows = []
    for test, extras in tests:
        label = test.parent.name + '-' + test.stem
        executable = output / (label + '.exe')
        run(flags + ['/target:exe', '/out:' + str(executable), '/reference:' + str(library), test]
            + [test.parent / extra for extra in extras], label + '-compile', output)
        run([executable], label, output)
        rows.append(dict(test=str(test.relative_to(ROOT)), exit=0))
    (output / 'results.json').write_text(json.dumps(rows, indent=2) + '\n')


def project(output, responses):
    listed = set()
    for unit in ['Assembly-CSharp', 'Assembly-CSharp-Editor']:
        for suffix in ['.rsp', '.rsp2']:
            for line in (responses / (unit + suffix)).read_text(encoding='utf-8-sig').splitlines():
                if re.match(r'^"Assets/.*\.cs"$', line):
                    listed.add((UNITY / line.strip('"')).resolve())
    command = [sys.executable, PLAYABLE / 'check_editor_project.py',
               '--response-root', responses, '--output-root', output]
    actual = {path.resolve() for path in (UNITY / 'Assets').rglob('*.cs')}
    for path in sorted(actual - listed):
        command += ['--new-editor-source' if 'Editor' in path.relative_to(UNITY).parts
                    else '--new-runtime-source', path.relative_to(UNITY).as_posix()]
    run(command)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('suite', choices=['lan', 'modules', 'project', 'api', 'all'])
    parser.add_argument('--output-root', required=True, type=Path)
    parser.add_argument('--response-root', type=Path,
                        default=UNITY / 'Library/Bee/artifacts/1300b0aE.dag')
    args = parser.parse_args()
    output = args.output_root.resolve()
    if output.is_relative_to(UNITY) or output.exists():
        parser.error('Choose a fresh output directory outside unity/')
    suites = ['lan', 'modules', 'project', 'api'] if args.suite == 'all' else [args.suite]
    for suite in suites:
        destination = output / suite if args.suite == 'all' else output
        if suite == 'lan':
            run([sys.executable, PLAYABLE / 'Networking/Current/check_lan_current.py',
                 '--output-root', destination])
        elif suite == 'modules':
            modules(destination)
        elif suite == 'project':
            project(destination, args.response_root.resolve())
        else:
            run([sys.executable, PLAYABLE / 'check_integration.py', '--output-root', destination])


if __name__ == '__main__':
    main()
