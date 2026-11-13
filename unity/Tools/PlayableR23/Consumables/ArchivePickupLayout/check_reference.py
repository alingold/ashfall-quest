from pathlib import Path
import json,hashlib
HERE=Path(__file__).resolve().parent
pins=json.loads((HERE/'native-reference-pins.json').read_text());root=Path(pins['root'])
for name,sha in pins['files'].items():assert hashlib.sha256((root/name).read_bytes()).hexdigest()==sha,name
record=json.loads((root/'result.json').read_text());receipt=json.loads((root/'receipt.json').read_text())
assert record['sourceManifestSha256']=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
assert record['passed'] and receipt['resultPassed'] and not receipt['strictPassed']
alpha=record['records'][1];loot=alpha['lootPointsGlobal'];assert len(loot)==242
max_food=0;max_xz=0;dy=[];tested=0
for snapshot in alpha['snapshots'][1:]:
 assert len(snapshot['foods'])==12 and len(snapshot['boxes'])==78
 assert sum(x['visible'] for x in snapshot['boxes'])==72
 for k,item in enumerate(snapshot['foods']):
  point=loot[(k*19+11)%len(loot)];delta=max(abs(item['globalPosition'][i]-point[i]) for i in range(3));max_food=max(max_food,delta);assert delta==0;tested+=1
 boxes=[x for x in snapshot['boxes'] if x['role']=='archive72'];assert len(boxes)==72
 for k,item in enumerate(boxes):
  point=loot[(k*13+5)%len(loot)];p=item['globalPosition'];xz=max(abs(p[i]-point[i]) for i in (0,2));max_xz=max(max_xz,xz);assert xz<.00002
  # Exact source box minY=-.075, ray floor at source lootY-.6,
  # and inventory HOVER_HEIGHT=.4452: predicted root shift=-.0798.
  delta=p[1]-point[1];dy.append(delta);assert abs(delta-(-.0798))<.0001;tested+=1
 assert all(not x['visible'] for x in snapshot['boxes'] if x['role']=='base')
out={'passed':True,'observedSourceCases':tested,'foodDeltaMax':max_food,'ammoXZDeltaMax':max_xz,'ammoYDeltaMin':min(dy),'ammoYDeltaMax':max(dy),'policy':'direct12food/bag positions; defer72ammo; hide6base','qualification':'Genuine source XR-off reference only; strictPassed false (missing optional food amount metadata and known shutdown errors). No Unity physics acceptance.'}
(HERE/'reference-results.json').write_text(json.dumps(out,indent=2)+'\n');print(json.dumps(out,indent=2))
