"""Bounded Framework CPU gates. Never launches an engine or uses Unity stubs."""
import hashlib,json,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/WorldRuntimeR1'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/WorldRuntimeR1'
CSC=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
sources=sorted((OUT/'inputs').rglob('*.cs'))+sorted((ROOT/'unity/Assets/Ashfall/Integration/WorldRuntime').rglob('*.cs'))
sources += [ROOT/'unity/Tools/Integration/WorldRuntime/WorldRuntimeFixture.cs',ROOT/'unity/Tools/Integration/WorldRuntime/RuntimeBindingTests.cs',ROOT/'unity/Tools/Integration/WorldRuntime/NativeWorldEditorFixture.cs']
commands=[]
def run(args,name):
    command=' '.join('"'+str(a)+'"' for a in args)
    commands.append(command)
    p=subprocess.run(command,capture_output=True,text=True)
    (DOC/(name+'.log')).write_text(p.stdout+p.stderr)
    if p.returncode: raise RuntimeError(name+' exit '+str(p.returncode))
exe=OUT/'RuntimeBindingTests.exe'
run([CSC,'/nologo','/langversion:5','/warnaserror+','/r:System.Runtime.Serialization.dll','/out:'+str(exe)]+sources,'compile-off')
run([exe],'cpu-tests')
(DOC/'commands.txt').write_text('\n'.join(commands)+'\n')
print((DOC/'cpu-tests.log').read_text())
# Verify the actual consumed World sources against the coordinator acceptance records.
accepted=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v5.json').read_text(encoding='utf-8-sig'))
checks=[]
for f in (OUT/'inputs').rglob('*.cs'):
    actual=hashlib.sha256(f.read_bytes()).hexdigest()
    relative=f.relative_to(OUT/'inputs')
    module='MapStreaming' if relative.parts[0]=='MapStreaming' else 'World'
    module_path=f.name if module=='MapStreaming' else str(relative)
    live=ROOT/('unity/Assets/Ashfall/Core/Contracts.cs' if f.name=='Contracts.cs' else 'unity/Assets/Ashfall/Modules/'+module+'/'+module_path)
    expected=[c['expected'] for a in accepted['accepted'] for c in a['checks'] if c['path'].lower()==str(live).lower()]
    checks.append(dict(path=str(live),captured=actual,current=hashlib.sha256(live.read_bytes()).hexdigest(),accepted=expected))
    if expected: assert actual.lower()==expected[0].lower()
(DOC/'dependency-verification.json').write_text(json.dumps(checks,indent=2))
assert all(c['current']==c['captured'] for c in checks)
print('Dependencies unchanged; native execution OPEN')
