"""Neutral tests only. Every invocation requires a fresh owned output directory."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[5]
BASE = ROOT / 'campaign-staging/20261104-three-chats/unity/Pickups/r3/runs'
if len(sys.argv) != 2 or not sys.argv[1].isalnum():
    raise SystemExit('usage: bundled-python run.py UNIQUE_ALPHANUMERIC_RUN')
out = BASE / sys.argv[1]
out.mkdir(parents=True, exist_ok=False)
csc = Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources = [ROOT / p for p in (
    'unity/Assets/Ashfall/Core/Contracts.cs',
    'unity/Assets/Ashfall/Modules/Pickups/Ports.cs',
    'unity/Assets/Ashfall/Modules/Pickups/PickupRules.cs')]
tests = [ROOT / 'unity/Tools/Modules/Pickups' / p for p in (
    'Tests.cs', 'AdversarialR1Adapted.cs', 'BatchTests.cs', 'r3/TestsR3.cs')]
inputs = []
for path in sources + tests:
    data = path.read_bytes()
    snapshot = out / 'inputs' / path.relative_to(ROOT)
    snapshot.parent.mkdir(parents=True, exist_ok=True)
    snapshot.write_bytes(data)
    inputs.append(dict(path=str(path.relative_to(ROOT)), bytes=len(data),
                       sha256=hashlib.sha256(data).hexdigest()))
(out / 'inputs.json').write_text(json.dumps(inputs, indent=2))
frozen = [out / 'inputs' / p.relative_to(ROOT) for p in sources]
results = []
commands = []
def run(args, label):
    commands.append(subprocess.list2cmdline([str(a) for a in args]))
    process = subprocess.run([str(a) for a in args], capture_output=True, text=True)
    (out / (label + '.log')).write_text(process.stdout + process.stderr)
    print(label, process.returncode, process.stdout, process.stderr)
    results.append(dict(command=label, exit=process.returncode))
    return process.returncode
run([csc, '/nologo', '/langversion:5', '/warnaserror+', '/target:library',
     '/out:' + str(out / 'Pickups.dll')] + frozen, 'library')
for test in tests:
    exe = out / (test.stem + '.exe')
    if run([csc, '/nologo', '/langversion:5', '/warnaserror+', '/out:' + str(exe)] +
           frozen + [out / 'inputs' / test.relative_to(ROOT)], 'compile-' + test.stem) == 0:
        run([exe], 'test-' + test.stem)
(out / 'commands.txt').write_text('\n'.join(commands) + '\n')
(out / 'results.json').write_text(json.dumps(results, indent=2))
drift = [p['path'] for p in inputs if hashlib.sha256((ROOT / p['path']).read_bytes()).hexdigest() != p['sha256']]
(out / 'drift.json').write_text(json.dumps(drift))
raise SystemExit(1 if drift or any(r['exit'] for r in results) else 0)
