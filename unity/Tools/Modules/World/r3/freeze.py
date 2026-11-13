"""Freeze this bounded candidate; manifests are evidence, not approval receipts."""
from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[5]
LOG=ROOT/'docs/agent-runs/20261104-three-chats/unity/World/r3'
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/World/r3'
def row(p):
    b=p.read_bytes();return dict(path=p.relative_to(ROOT).as_posix(),bytes=len(b),sha256=hashlib.sha256(b).hexdigest())
inputs=[ROOT/p for p in ['unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/COLLISION-LAYERS-v1.md','unity/Assets/Ashfall/Core/Contracts.cs','docs/agent-runs/20261104-three-chats/unity/ownership.json','docs/agent-runs/20261104-three-chats/unity/phase03-ownership.json','docs/agent-runs/20261104-three-chats/unity/reviews/World-r2/REPORT.md','docs/agent-runs/20261104-three-chats/unity/reviews/World-r2/source-verification.json','quest-r22/source/scripts/map_semantics_r14.gd','quest-r22/source/scripts/archive_map1003.gd','game/scripts/archive_map1003.gd','quest-r22/source/assets/data/map_semantics_r14.json','unity/Tools/Modules/World/WorldTests.cs','unity/Tools/Modules/World/SpawnLiftRegression.cs','docs/agent-runs/20261104-three-chats/unity/reviews/World-r2/Holdouts.cs','docs/agent-runs/20261104-three-chats/unity/reviews/World-r1/r2/Adversarial.cs','campaign-staging/20261104-three-chats/unity/World/alfa-aglae-souza.json','campaign-staging/20261104-three-chats/unity/World/battlefield-paintball-gods-pitdevil.json']]
(LOG/'inputs.json').write_text(json.dumps(dict(questRevision='bdba6e30012e1733efd5021fd8ded593f82f26e6',files=[row(p) for p in inputs]),indent=2))
changed=list((ROOT/'unity/Assets/Ashfall/Modules/World').rglob('*.cs'))+list((ROOT/'unity/Tools/Modules/World/r3').glob('*'))+list(OUT.rglob('*'))+list(LOG.glob('*'))
changed=sorted(set(p for p in changed if p.is_file() and p.name!='changed-file-sha256.json'))
manifest=LOG/'changed-file-sha256.json';manifest.write_text(json.dumps(dict(status='candidate; independent review required',files=[row(p) for p in changed]),indent=2))
print('manifest SHA256 '+hashlib.sha256(manifest.read_bytes()).hexdigest())
print('entries '+str(len(changed)))
