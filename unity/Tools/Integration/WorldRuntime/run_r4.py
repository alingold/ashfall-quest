from pathlib import Path
import subprocess,json,hashlib,shutil,sys
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/WorldLifetimeBindingsR4';D=R/'docs/agent-runs/20261104-three-chats/unity/WorldLifetimeBindingsR4'
name=sys.argv[1];run=S/name;run.mkdir(exist_ok=False);out=D/name;out.mkdir(exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
inputs=[]
for base in ['unity/Assets/Ashfall/Integration/WorldRuntime','unity/Tools/Integration/WorldRuntime']:
 for p in (R/base).rglob('*'):
  if p.suffix not in ('.cs','.py'):continue
  t=run/p.relative_to(R);t.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,t);inputs.append(dict(source=str(p),snapshot=str(t),sha256=sha(t)))
deps=list((S/'dependencies').rglob('*.cs'))
for p in deps:inputs.append(dict(source=str(p),snapshot=str(p),sha256=sha(p)))
prod=list((run/'unity/Assets').rglob('*.cs'));tests=run/'unity/Tools/Integration/WorldRuntime'
opts=[l for l in (R/'campaign-staging/20261104-three-chats/unity/WorldRuntimeR3/run03/native.rsp').read_text().splitlines() if l.startswith('/') and not l.startswith('/out:')]
commands=[]
def call(label,args):
 q=subprocess.run([str(a) for a in args],capture_output=True,text=True);commands.append(dict(label=label,args=[str(a) for a in args],exit=q.returncode,stdout=q.stdout,stderr=q.stderr));(out/'commands.json').write_text(json.dumps(commands,indent=2));print(label,q.returncode,q.stdout,q.stderr);return q.returncode
CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
native=deps+prod+list(tests.glob('Native*.cs'))+[tests/'WorldRuntimeFixture.cs']
rsp=run/'native.rsp';rsp.write_text('\n'.join(opts+['/out:"'+str(run/'NativeR4.dll')+'"']+['"'+str(p)+'"' for p in native])+'\n')
call('native-reference-compile',[CSC,'/noconfig','@'+str(rsp)])
exe=run/'Retained28.exe';cpu=[p for p in deps+prod if 'Engine' not in p.parts]
if call('cpu-compile',[CSC,'/nologo','/langversion:5','/warn:4','/warnaserror+','/r:System.Runtime.Serialization.dll','/out:'+str(exe)]+cpu+[tests/'WorldRuntimeFixture.cs',tests/'RuntimeBindingTests.cs'])==0:call('retained28',[exe])
refs=[]
for l in opts:
 if l.startswith('/r:'):
  p=Path(l[3:].strip('"'));refs.append(dict(path=str(p),sha256=sha(p)))
(out/'references.json').write_text(json.dumps(refs,indent=2));(out/'inputs.json').write_text(json.dumps(inputs,indent=2));print('captured',len(inputs),'refs',len(refs))

assert all(x['exit']==0 for x in commands), 'Compiler/test failure retained in commands.json'
