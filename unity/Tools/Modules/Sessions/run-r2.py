"""Direct C#5 build; every generated binary/log remains in Sessions r2 paths."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
STAGE = ROOT / 'campaign-staging/20261104-three-chats/unity/Sessions'
OUT = STAGE / 'r2'
EVIDENCE = ROOT / 'docs/agent-runs/20261104-three-chats/unity/Sessions/evidence/r2'
CSC = Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
CORE = ROOT / 'unity/Assets/Ashfall/Core/Contracts.cs'
PROD = ROOT / 'unity/Assets/Ashfall/Modules/Sessions'
TOOLS = Path(__file__).resolve().parent
REVIEW = ROOT / 'campaign-staging/20261104-three-chats/unity/reviews/Sessions-r1/AdversarialTests.cs'
receipts = []

def run(name, args, expected=0):
    result = subprocess.run([str(a) for a in args], capture_output=True, text=True)
    (EVIDENCE / (name + '.txt')).write_text(result.stdout + result.stderr, encoding='utf-8')
    receipts.append(dict(name=name, argv=[str(a) for a in args], exit_code=result.returncode, expected=expected))
    print(name, result.returncode, result.stdout.strip(), result.stderr.strip())
    if result.returncode != expected:
        raise RuntimeError('Unexpected result: ' + name)

def compile(name, source_dir, test=None):
    target = OUT / (name + ('.exe' if test else '.dll'))
    args = [CSC, '/nologo', '/langversion:5', '/warnaserror+', '/target:' + ('exe' if test else 'library'), '/out:' + str(target), CORE, source_dir / 'Ports.cs', source_dir / 'Session.cs']
    if test:
        args.append(test)
    run('compile-' + name, args)
    return target

OUT.mkdir(parents=True, exist_ok=True)
EVIDENCE.mkdir(parents=True, exist_ok=True)
if (EVIDENCE / 'candidate-manifest.json').exists():
    raise RuntimeError('Frozen r2 evidence exists; use a new revision instead of overwriting.')
try:
    before = STAGE / 'revisions/r1-before-r2/production'
    run('before-original', [compile('BeforeOriginal', before, TOOLS / 'SessionTests.cs')])
    run('before-reviewer', [compile('BeforeReviewer', before, REVIEW)], expected=1)
    compile('Sessions', PROD)
    run('after-original', [compile('SessionTests', PROD, TOOLS / 'SessionTests.cs')])
    run('after-reviewer', [compile('ReviewerTests', PROD, REVIEW)])
    run('after-r2', [compile('R2Tests', PROD, TOOLS / 'R2Tests.cs')])
finally:
    (EVIDENCE / 'commands.json').write_text(json.dumps(receipts, indent=2), encoding='utf-8')
