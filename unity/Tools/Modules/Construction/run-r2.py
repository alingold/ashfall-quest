"""Direct C#5 compiler runs. Each invocation requires a new immutable run label."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[4]
STAGE = ROOT / 'campaign-staging/20261104-three-chats/unity/Construction'
EVIDENCE = ROOT / 'docs/agent-runs/20261104-three-chats/unity/Construction/evidence/r2'
label = sys.argv[1]
if not label.isalnum():
    raise SystemExit('Use a new alphanumeric run label')
out = STAGE / 'r2' / label
out.mkdir(exist_ok=False)
compiler = Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
core = ROOT / 'unity/Assets/Ashfall/Core/Contracts.cs'
module = Path('unity/Assets/Ashfall/Modules/Construction')
tools = ROOT / 'unity/Tools/Modules/Construction'
snapshot = STAGE / 'revisions/r1-before-r2'
original = ROOT / 'campaign-staging/20261104-three-chats/unity/reviews/Construction-r1/Adversarial.cs'
results = []

def run(name, base, test):
    sources = [core] + sorted((base / module).glob('*.cs')) + [test]
    exe = out / (name + '.exe')
    rsp = out / (name + '.rsp')
    rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/out:"' + str(exe) + '"\n' + '\n'.join('"' + str(p) + '"' for p in sources), encoding='utf-8')
    compile_result = subprocess.run([str(compiler), '@' + str(rsp)], capture_output=True, text=True)
    (out / (name + '-compile.txt')).write_text(compile_result.stdout + compile_result.stderr)
    receipt = {'name': name, 'compileExit': compile_result.returncode,
               'inputs': [{'path': str(p), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources]}
    if compile_result.returncode == 0:
        test_result = subprocess.run([str(exe)], capture_output=True, text=True)
        (out / (name + '-results.txt')).write_text(test_result.stdout + test_result.stderr)
        receipt['testExit'] = test_result.returncode
        receipt['summary'] = test_result.stdout.splitlines()[-1] if test_result.stdout else test_result.stderr
        receipt['exeSha256'] = hashlib.sha256(exe.read_bytes()).hexdigest()
    results.append(receipt)
    print(name, receipt.get('summary', compile_result.stdout), flush=True)

run('BeforeBuilder', snapshot, snapshot / 'unity/Tools/Modules/Construction/ConstructionTests.cs')
run('BeforeIndependent', snapshot, original)
run('AfterBuilder', ROOT, tools / 'ConstructionTests.cs')
run('AfterOriginalIndependent', ROOT, original)
run('AfterRegressions', ROOT, tools / 'R1Regressions.cs')
run('AfterR2', ROOT, tools / 'R2Tests.cs')
(out / 'receipts.json').write_text(json.dumps(results, indent=2))
print('Receipts:', out / 'receipts.json')
if any(r['compileExit'] or r.get('testExit', 1) for r in results if r['name'] in ('BeforeBuilder','AfterBuilder','AfterRegressions','AfterR2')):
    raise SystemExit(1)
