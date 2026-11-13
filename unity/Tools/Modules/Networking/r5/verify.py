from pathlib import Path
import json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkingPeerR4/r5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def record(p):return dict(path=str(p),sha256=sha(p))
required=['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/CONTRACTS-v3.md','unity/Documentation/COLLISION-LAYERS-v1.md','unity/Assets/Ashfall/Core/Contracts.cs','docs/agent-runs/20261104-three-chats/unity/phase05-ownership.json','docs/agent-runs/20261104-three-chats/unity/reviews/Sessions-r3/REPORT.md','docs/agent-runs/20261104-three-chats/unity/reviews/Networking-r3/REPORT.md','docs/agent-runs/20261104-three-chats/unity/SessionReplication/r4/REPORT.md']
checks=[]
def check(p,expected,scope):checks.append(dict(path=str(p),expected=expected,actual=sha(p),match=sha(p).upper()==expected.upper(),scope=scope))
for v in [1,2]:
 p=R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%v);required.append(str(p.relative_to(R)));data=json.loads(p.read_text(encoding='utf-8-sig'))
 if v==1:
  for x in data['sourceChecks']:check(Path(x['path']),x['expected'],'accepted-v1')
 else:
  for group in data['accepted']:
   for x in group['checks']:check(R/x['path'],x['expected'],'accepted-v2')
manifest=R/'docs/agent-runs/20261104-three-chats/unity/Networking/r3/candidate-manifest.json'
data=json.loads(manifest.read_text(encoding='utf-8-sig'));frozen=R/'campaign-staging/20261104-three-chats/unity/Networking/r3/candidate'
for x in data['artifacts']:
 p=R/x['path']
 # Historical artifacts always checked where unchanged; production/tool paths map to sealed source copies.
 if x['path'].startswith('unity/Assets/Ashfall/Modules/Networking/'):
  p=frozen/'production'/Path(x['path']).relative_to('unity/Assets/Ashfall/Modules/Networking')
 elif x['path'].startswith('unity/Tools/Modules/Networking/'):
  p=frozen/'tools'/Path(x['path']).relative_to('unity/Tools/Modules/Networking')
 check(p,x['sha256'],'frozen-r3')
world=R/'campaign-staging/20261104-three-chats/world/world-accepted-v2/handoff.json'
required+=['docs/agent-runs/20261104-three-chats/world/accepted-handoff-v2.json',str(world.relative_to(R))]
w=json.loads(world.read_text(encoding='utf-8-sig'));(D/'world-scope.json').write_text(json.dumps({'revision':'ce3b3b0771d779d93625bbe501c35c8953ab617924b4ae57b41cc1b299b5c0a3','rawSHA256':sha(world),'approvedChanges':w['approvedChanges'],'scope':'read-only identity/patch provenance; no world asset consumed'},indent=2))
for tree,start,end in [('quest-r22',194,235),('game',186,224)]:
 p=R/tree/('source/scripts/net_session1000.gd' if tree=='quest-r22' else 'scripts/net_session1000.gd'); required.append(str(p.relative_to(R)))
 lines=p.read_text(encoding='utf-8').splitlines();(D/(tree+'-reconciliation.txt')).write_text('\n'.join('%d: %s'%(i+1,lines[i]) for i in range(start,min(end,len(lines)))))
package=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Resources\PackageManager\BuiltInPackages\com.unity.transport')
for rel in ['package.json','Runtime/NetworkDriver.cs','Runtime/NetworkParams.cs','Runtime/NetworkSettings.cs','Runtime/NetworkEndpoint.cs']:
 p=package/rel
 if p.exists():required.append(str(p))
(D/'read-inputs.json').write_text(json.dumps([record(R/p) for p in required],indent=2))
(D/'frozen-and-accepted-verification.json').write_text(json.dumps(checks,indent=2))
print('Verified',len(checks),'mismatches',sum(not x['match'] for x in checks));print('World raw',sha(world))

