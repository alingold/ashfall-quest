from pathlib import Path
import hashlib, json, subprocess
R=Path(r'C:\Users\aling\Documents\ashfall')
O=R/'campaign-staging/20261104-three-chats/unity/DesktopPawnR1'
CSC=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
run=O/'compile03'
assert not run.exists(), 'Preserve earlier evidence: choose a new compile directory'
run.mkdir()
inputs=[]
paths=[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Assets/Ashfall/Integration/DesktopPlaytest/DesktopPlaytestContext.cs']
paths+=sorted((R/'unity/Assets/Ashfall/Modules/Player').glob('*.cs'))
paths+=sorted((R/'unity/Assets/Ashfall/Integration/DesktopPlaytest/Pawn').glob('*.cs'))
paths+=sorted((R/'unity/Tools/Integration/DesktopPlaytest/DesktopPawnR1').glob('*.cs'))
sources=[]
for i,p in enumerate(paths):
    q=run/('%03d-'%i+p.name);q.write_bytes(p.read_bytes());sources.append(q)
    inputs.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=sha(q)))
(run/'inputs.json').write_text(json.dumps(inputs,indent=2))
# Actual installed reference list from the root-qualified native compile, frozen by hash here.
base=R/'campaign-staging/20261104-three-chats/unity/reviews/AvatarLifecycleReviewR2/fresh-native.rsp'
lines=base.read_text(encoding='utf-8-sig').splitlines()
refs=[x.strip('"').split(':',1)[1] for x in lines if x.strip('"').startswith('/reference:')]
# Keep only actual BCL/Unity API dependencies required by this pawn, not unrelated editor tools.
names={'mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','System.Runtime.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.MathematicsModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.InputModule.dll','Unity.InputSystem.dll'}
refs=[Path(x) for x in refs if Path(x).name in names]
refdir=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\NetStandard\ref\2.1.0')
for name in ['mscorlib.dll','System.dll','System.Core.dll','System.Runtime.dll']:
    if not any(p.name==name for p in refs) and (refdir/name).exists():refs.append(refdir/name)
(run/'references.json').write_text(json.dumps([dict(path=str(p),sha256=sha(p)) for p in refs],indent=2))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER','/out:'+str(run/'DesktopPawnNative.dll')]
args+=['/reference:'+str(p) for p in refs]
args+=[str(p) for p in sources]
rsp=run/'native.rsp';rsp.write_text('\n'.join('"'+x+'"' for x in args),encoding='utf-8')
cmd=[str(CSC),'/noconfig','@'+str(rsp)]
x=subprocess.run(cmd,cwd=R,capture_output=True,text=True,errors='replace')
(run/'compile.log').write_text(x.stdout+x.stderr)
(run/'results.json').write_text(json.dumps(dict(command=cmd,exitCode=x.returncode,output=x.stdout+x.stderr,nativeExecution='NOT RUN - root engine lane required'),indent=2))
print(x.stdout+x.stderr);print('exit',x.returncode)
# Read and record every requested plan/pin fully without reproducing huge historical manifests.
docs=[R/'docs/TWO-CHAT-20-AGENT-OVERRIDE.md',R/'docs/HILL-CLIMB-ORCHESTRATION.md',R/'docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md',R/'docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md',R/'docs/agent-runs/20261104-0020/preflight.json']
docs+=sorted((R/'unity/Documentation').glob('*.md'))
U=R/'docs/agent-runs/20261104-three-chats/unity'
docs+=[U/'GAMEPLAY-CHECKPOINT-v1.md']+[U/('accepted-neutral-v%d.json'%i) for i in range(1,7)]
docs+=[U/'phase16/source-index.json',U/'phase16/dispatch-plan.json',U/'phase18/ROOT-REPORT.md',U/'phase18/source-index.json',U/'phase18/root-manifest.json',U/'phase18/dispatch-plan.json',base]
read=[]
for p in docs:
    content=p.read_text(encoding='utf-8-sig');read.append(dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)))
(run/'read-inputs.json').write_text(json.dumps(read,indent=2))
for phase in ['phase16','phase18']:
    rows=json.loads((U/phase/'source-index.json').read_text())
    keys={str(Path(i['path'])) for i in inputs}
    relevant=[r for r in rows if str(Path(r.get('path',r.get('original','')))) in keys]
    (run/(phase+'-comparison.json')).write_text(json.dumps([dict(path=r.get('path',r.get('original')),pinned=r['sha256'],current=sha(R/r.get('path',r.get('original'))),match=r['sha256'].lower()==sha(R/r.get('path',r.get('original')))) for r in relevant],indent=2))
