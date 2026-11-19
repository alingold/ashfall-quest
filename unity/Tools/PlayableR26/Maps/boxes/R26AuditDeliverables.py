import json,hashlib,math
from pathlib import Path
from collections import Counter
from PIL import Image
root=Path('/home/al/ashfall/unity');out=Path('/home/al/ashfall/campaign/boxes');sa=root/'Assets/StreamingAssets/PlayableR23'
rows=json.loads((out/'verdicts.json').read_bytes());cat=json.loads((sa/'TeamLaunch/launch-maps.json').read_bytes())['rows'];policy=json.loads((root/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapPodPolicy.json').read_bytes())['maps'];policy={r['id']:r for r in policy}
assert len(rows)==108 and {r['mapId'] for r in rows}=={r['id'] for r in cat}
proof=[]
for r in rows:
 path=out/'review'/(r['mapId'].replace('archive:','')+'.png');im=Image.open(path);assert im.size==(1800,730);im.verify()
 assert r['verdict'] in ('BOXED','OPEN','UNSURE') and 0<=r['confidence']<=1
 assert policy[r['mapId']]['verdict']==r['verdict'] and policy[r['mapId']]['pods']==r['pods']
 assert all(math.isfinite(v) for p in r['proposedSpawns'] for v in p)
 assert r['verdict']!='BOXED' or not r['pods'] and r['proposedSpawns']
 folder=sa/r['folder'];m=json.loads((folder/'map.json').read_bytes());assert m['sourceSha256']==r['sourceSha256'];assert hashlib.sha256((folder/'geometry.bytes').read_bytes()).hexdigest()==r['geometrySha256']
 proof.append(dict(mapId=r['mapId'],review=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),verdict=r['verdict'],spawnCandidates=len(r['proposedSpawns'])))
for i in range(1,10):
 im=Image.open(out/'review'/('overview-%02d.png'%i));assert im.size==(1800,1200);im.verify()
result=dict(maps=len(rows),counts=dict(Counter(r['verdict'] for r in rows)),excluded=[r['mapId'] for r in rows if r.get('excluded')],unsure=[r['mapId'] for r in rows if r['verdict']=='UNSURE'],mapImages=len(proof),contactSheets=9,boxedSpawnCandidates=sum(len(r['proposedSpawns']) for r in rows if r['verdict']=='BOXED'),proof=proof)
(out/'R26BoxesArtifactAudit.json').write_text(json.dumps(result,indent=1)+'\n');print('PASS: 108 exact catalog ids/source+geometry hashes; 108 readable 1800x730 reviews; 9 readable 1800x1200 sheets; BOXED pods and finite candidate invariants')
