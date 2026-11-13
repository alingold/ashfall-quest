"""Freeze this bounded candidate, never modify historical manifests."""
from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/Construction/r3'
evidence=root/'docs/agent-runs/20261104-three-chats/unity/Construction/evidence/r3'
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()
def entry(p):return {'path':str(p.relative_to(root)),'sha256':sha(p),'bytes':p.stat().st_size}
snap=stage/'frozen-source';snap.mkdir(exist_ok=False)
sources=list((root/'unity/Assets/Ashfall/Modules/Construction').glob('*.cs'))+list((root/'unity/Assets/Ashfall/Integration/ConstructionGeometry').glob('*.cs'))+list((root/'unity/Tools/Integration/ConstructionGeometry').glob('*'))
for p in sources:
    target=snap/p.relative_to(root);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
inputs=[]
for name,original in [('Contracts.cs',root/'unity/Assets/Ashfall/Core/Contracts.cs'),('COLLISION-LAYERS-v1.md',root/'unity/Documentation/COLLISION-LAYERS-v1.md')]+[(s,root/'quest-r22/source/scripts'/s) for s in ['field_builder.gd','build_grid.gd','net_bridge1000.gd']]:
    snapshot=stage/'inputs/source'/name
    inputs.append({'original':str(original),'snapshot':str(snapshot.relative_to(root)),'sha256':sha(snapshot),'currentSha256':sha(original),'matches':sha(original)==sha(snapshot)})
sample=json.loads((stage/'inputs/paintball-small-surface.json').read_text())
parent=Path(sample['parentPath']);glb=Path(sample['sourcePath'])
inputs.append({'original':str(parent),'sha256':sample['parentSha256'],'currentSha256':sha(parent),'matches':sample['parentSha256']==sha(parent)})
inputs.append({'original':str(glb),'sha256':sample['sourceSha256'],'currentSha256':sha(glb),'matches':sample['sourceSha256']==sha(glb)})
for p in ['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','docs/agent-runs/20261104-three-chats/unity/ownership.json','docs/agent-runs/20261104-three-chats/unity/phase03-ownership.json','docs/agent-runs/20261104-three-chats/unity/reviews/Construction-r2/REPORT.md']:
    inputs.append(entry(root/p))
(evidence/'inputs-manifest.json').write_text(json.dumps({'questRevision':'bdba6e30012e1733efd5021fd8ded593f82f26e6','inputs':inputs},indent=2))
if any(not i.get('matches',True) for i in inputs):raise SystemExit('INPUT DRIFT: see inputs-manifest.json')
# Ensure final compiler receipts name the current code bytes before freezing.
for folder in ['final','review-regressions']:
    for record in json.loads((stage/folder/'receipts.json').read_text()):
        for i in record['inputs']:
            if sha(Path(i['path']))!=i['sha256']:raise SystemExit('compiled input drift: '+i['path'])
all_files=sorted(set([p for p in stage.rglob('*') if p.is_file()]+sources+[p for p in evidence.rglob('*') if p.is_file()]))
candidate=evidence/'candidate-manifest.json'
candidate.write_text(json.dumps({'status':'candidate-needs-independent-review','api':'Construction/2 + owned ConstructionGeometry/1','files':[entry(p) for p in all_files]},indent=2))
# Existing production changes plus newly created tools/r3 artifacts. Manifest excludes itself.
changed=[root/'unity/Assets/Ashfall/Modules/Construction/Geometry.cs']+list((root/'unity/Assets/Ashfall/Integration/ConstructionGeometry').glob('*.cs'))+list((root/'unity/Tools/Integration/ConstructionGeometry').glob('*'))+[p for p in stage.rglob('*') if p.is_file()]+[p for p in evidence.rglob('*') if p.is_file()]
manifest=evidence/'changed-file-manifest.json';manifest.write_text(json.dumps({'selfExcluded':True,'candidateManifestSha256':sha(candidate),'files':[entry(p) for p in sorted(set(changed))]},indent=2))
print('Candidate manifest SHA256',sha(candidate));print('Changed-file manifest SHA256',sha(manifest));print('Files',len(set(changed)))
