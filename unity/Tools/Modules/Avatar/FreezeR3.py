from pathlib import Path
import json,hashlib,shutil,datetime
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/Avatar/r3'
EV=ROOT/'docs/agent-runs/20261104-three-chats/unity/Avatar/phase03'
manifest=EV/'candidate-manifest.json'
if manifest.exists():raise SystemExit('Candidate frozen; do not overwrite')
def entry(p):
    b=p.read_bytes();return dict(path=p.relative_to(ROOT).as_posix(),bytes=len(b),sha256=hashlib.sha256(b).hexdigest().upper())
inputs=['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/COLLISION-LAYERS-v1.md','unity/Assets/Ashfall/Core/Contracts.cs','docs/agent-runs/20261104-three-chats/unity/ownership.json','docs/agent-runs/20261104-three-chats/unity/phase03-ownership.json','docs/agent-runs/20261104-three-chats/unity/reviews/Avatar-r2/REPORT.md','docs/agent-runs/20261104-three-chats/unity/Avatar/evidence/r2/candidate-manifest.json']
for tree in ['game','quest-r22/source']:
    inputs += [f'{tree}/scripts/{s}' for s in ['grip_hand_086.gd','player_body.gd','gemini_player_models_099.gd']]
    inputs += [f'{tree}/assets/hands/xrtools086/Hand_Glove_low_{side}.gltf' for side in 'LR']
records=[]
for rel in inputs:
    p=ROOT/rel;record=entry(p);dest=OUT/'inputs'/'consumed'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    if entry(p)['sha256']!=record['sha256'] or entry(dest)['sha256']!=record['sha256']:raise SystemExit('Input drift: '+rel)
    records.append(record)
for record in json.loads((EV/'source-inputs.json').read_text()):
    if hashlib.sha256(Path(record['path']).read_bytes()).hexdigest().upper()!=record['sha256']:raise SystemExit('Source drift '+record['path'])
for p in [Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'),Path('C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')]:
    b=p.read_bytes();records.append(dict(path=str(p),bytes=len(b),sha256=hashlib.sha256(b).hexdigest().upper(),role='tool binary'))
(EV/'consumed-inputs.json').write_text(json.dumps(dict(schema=1,baseline='bdba6e30012e1733efd5021fd8ded593f82f26e6',inputs=records),indent=2))
r2=json.loads((ROOT/inputs[12]).read_text());checks=[]
for e in r2['files']:
    p=ROOT/e['path']
    if '/Avatar/r2/' in e['path'] or '/Avatar/evidence/r2/' in e['path']:
        actual=entry(p);checks.append(dict(path=e['path'],matches=actual['sha256']==e['sha256'] and actual['bytes']==e['bytes']))
    elif e['path'].startswith('unity/Assets/Ashfall/Modules/Avatar/'):
        p=OUT/'inputs/r2/Avatar'/e['path'].split('Modules/Avatar/')[1];actual=entry(p);checks.append(dict(path=str(p.relative_to(ROOT)),matches=actual['sha256']==e['sha256']))
if not all(e['matches'] for e in checks):raise SystemExit('r2 preservation mismatch')
(EV/'r2-preservation.json').write_text(json.dumps(checks,indent=2))
production=list((ROOT/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'))
tools=[ROOT/'unity/Tools/Modules/Avatar'/n for n in ['RunR3.py','FreezeR3.py','AvatarR3Tests.cs','AvatarEngineR3Tests.cs','AvatarTests.cs','AvatarR2Tests.cs','AvatarEngineR2Tests.cs']]
for p in production+tools:
    dest=OUT/'source'/p.relative_to(ROOT);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
files=sorted(set(production+tools+list(OUT.rglob('*'))+list(EV.rglob('*'))))
files=[p for p in files if p.is_file() and p!=manifest]
data=dict(schema=1,version='Avatar-r3/skinned-glove-1',status='candidate-awaiting-independent-review',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),neutral_checks=107,engine='NOT_COMPILED_OR_RUN',files=[entry(p) for p in files])
manifest.write_text(json.dumps(data,indent=2))
for e in data['files']:
    if entry(ROOT/e['path'])!=e:raise SystemExit('Candidate drift '+e['path'])
print('Frozen',len(data['files']),'files; manifest SHA256',entry(manifest)['sha256'])
print('Preserved r2 entries',len(checks))
