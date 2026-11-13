"""Neutral-only C#5 gates. Never starts Editor/UPM/devices. Creates a new run folder."""
import pathlib, hashlib, json, subprocess, sys
ROOT = pathlib.Path(__file__).resolve().parents[4]
PACKET = ROOT / 'campaign-staging/20261104-three-chats/unity/AudioRuntimeR1'
run = PACKET / sys.argv[1]
run.mkdir(exist_ok=False)
inputs = PACKET / 'inputs'
dependencies = [p for p in inputs.rglob('*.cs') if 'Engine' not in p.parts]
owned = list((ROOT/'unity/Assets/Ashfall/Integration/AudioRuntime').rglob('*.cs'))
tests = ROOT/'unity/Tools/Integration/AudioRuntime/RuntimeTests.cs'
sources = dependencies + owned + [tests]
sealed = []
records = []
for i, p in enumerate(sources):
    dest = run/'sealed'/str(i)/p.name
    dest.parent.mkdir(parents=True)
    data = p.read_bytes()
    dest.write_bytes(data)
    records.append(dict(path=str(p), sealed=str(dest), sha256=hashlib.sha256(data).hexdigest()))
    sealed.append(dest)
(run/'inputs.json').write_text(json.dumps(records,indent=2))
csc = pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
exe = run/'RuntimeTests.exe'
args = ['/nologo','/warnaserror+','/langversion:5','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sealed]
(run/'compile.rsp').write_text('\n'.join(args))
command = [str(csc),'@'+str(run/'compile.rsp')]
compiled = subprocess.run(command,capture_output=True,text=True)
(run/'compile.log').write_text(compiled.stdout+compiled.stderr)
result = dict(compile_exit=compiled.returncode, command=command, scope='neutral C#5; guarded engine code excluded by preprocessor; no native approval')
if compiled.returncode == 0:
    test = subprocess.run([str(exe), str(run/'fixtures')], capture_output=True,text=True)
    (run/'tests.log').write_text(test.stdout+test.stderr)
    result.update(test_exit=test.returncode)
(run/'result.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
print((run/('compile.log' if compiled.returncode else 'tests.log')).read_text())
sys.exit(compiled.returncode or result.get('test_exit',1))
