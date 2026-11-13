"""Immutable R3 CPU execution and real installed-reference native compile only."""
from pathlib import Path
import subprocess,json,hashlib,shutil,sys
R=Path(__file__).resolve().parents[4]
S=R/'campaign-staging/20261104-three-chats/unity/WorldRuntimeR3'
D=R/'docs/agent-runs/20261104-three-chats/unity/WorldRuntimeR3'
name=sys.argv[1] if len(sys.argv)>1 else 'run01'
assert name in ('run01','run02','run03')
D=D/name;D.mkdir(exist_ok=False)
RUN=S/name;RUN.mkdir(exist_ok=False)
OLD=R/'campaign-staging/20261104-three-chats/unity/reviews/WorldRuntimeReviewR2'
CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
inputs=[]
def copy(p,t):
 t.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,t);inputs.append(dict(source=str(p),snapshot=str(t),sha256=sha(t)));return t
for p in (OLD/'frozen/dependencies').rglob('*.cs'):copy(p,RUN/'dependencies'/p.relative_to(OLD/'frozen/dependencies'))
for base in ['unity/Assets/Ashfall/Integration/WorldRuntime','unity/Tools/Integration/WorldRuntime']:
 for p in (R/base).rglob('*.cs'):copy(p,RUN/'snapshot'/p.relative_to(R))
for name in ['Adversarial.cs','Sibling.cs','MutationCpuV2.cs']:
 paths=list(OLD.rglob(name));assert paths,name
 copy(paths[0],RUN/name)
for p in (OLD/'frozen/combat').glob('*.cs'):copy(p,RUN/'combat'/p.name)
for name in ['NativeWorldR2Diagnostic.cs','CallbackDiagnostic.cs']:copy(OLD/name,RUN/name)
commands=[]
def run(label,args):
 q=subprocess.run([str(a) for a in args],capture_output=True,text=True)
 commands.append(dict(label=label,args=[str(a) for a in args],exit=q.returncode,stdout=q.stdout,stderr=q.stderr))
 (D/'commands.json').write_text(json.dumps(commands,indent=2));print(label,q.returncode,q.stdout,q.stderr)
 assert q.returncode==0,label
prod=list((RUN/'snapshot/unity/Assets').rglob('*.cs'));tests=RUN/'snapshot/unity/Tools/Integration/WorldRuntime';deps=list((RUN/'dependencies').rglob('*.cs'))
for label,extra in [('retained28',[tests/'RuntimeBindingTests.cs']),('adversarial22',[RUN/'Adversarial.cs']),('independent6',[RUN/'MutationCpuV2.cs']),('combat4',list((RUN/'combat').glob('*.cs'))+[RUN/'Sibling.cs'])]:
 exe=RUN/(label+'.exe')
 run(label+'-compile',[CSC,'/nologo','/langversion:5','/warn:4','/warnaserror+','/r:System.Runtime.Serialization.dll','/out:'+str(exe)]+deps+prod+[tests/'WorldRuntimeFixture.cs']+extra)
 run(label,[exe])
# Preserve the reference-only switches; replace every source by exact captured sources.
old=(OLD/'native-corrected.rsp').read_text().splitlines()
opts=[line for line in old if line.startswith('/') and not line.startswith('/out:')]
native=prod+deps+list(tests.glob('Native*.cs'))+[tests/'WorldRuntimeFixture.cs',RUN/'NativeWorldR2Diagnostic.cs',RUN/'CallbackDiagnostic.cs']
rsp=RUN/'native.rsp';rsp.write_text('\n'.join(opts+['/out:"'+str(RUN/'NativeR3.dll')+'"']+['"'+str(p)+'"' for p in native])+'\n')
run('native-reference-compile',[CSC,'/noconfig','@'+str(rsp)])
refs=[]
for line in opts:
 if line.startswith('/r:'):
  p=Path(line[3:].strip('"'));refs.append(dict(path=str(p),sha256=sha(p)))
(D/'reference-inputs.json').write_text(json.dumps(refs,indent=2))
(D/'inputs.json').write_text(json.dumps(inputs,indent=2))
(D/'input-verification.json').write_text(json.dumps([dict(path=x['source'],snapshot=x['snapshot'],snapshotMatches=sha(Path(x['snapshot']))==x['sha256'],sourceMatches=sha(Path(x['source']))==x['sha256']) for x in inputs],indent=2))
