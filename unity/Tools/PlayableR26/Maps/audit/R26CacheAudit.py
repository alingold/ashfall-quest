import json,math,collections
from pathlib import Path
out=Path(__file__).resolve().parent
source=Path('/home/al/ashfall/unity/Assets/StreamingAssets/PlayableR23/Loot/caches.json')
summary=[]
def cell(p,s):return tuple(math.floor(v/k) for v,k in zip(p,s))
for r in json.loads(source.read_bytes())['maps']:
 points=[(x['position'][0],x['position'][1],-x['position'][2]) for x in r.get('sites',[])]
 neighborhoods=collections.Counter(cell(p,(96,36,96)) for p in points)
 chosen=set();kept=0
 for p in points:
  c=cell(p,(32,12,32));crowded=neighborhoods[cell(p,(96,36,96))]>=24
  if crowded and c in chosen:continue
  if crowded:chosen.add(c)
  kept+=1
 summary.append(dict(map=r['mapId'],markers=len(points),maxNeighborhood=max(neighborhoods.values(),default=0),occupied=len(neighborhoods),crowded=sum(v>=24 for v in neighborhoods.values()),retained=kept))
summary.sort(key=lambda r:-r['markers'])
(out/'R26CacheDensity.json').write_text(json.dumps(summary,indent=1))
print('maps',len(summary),'thinned',sum(r['retained']<r['markers'] for r in summary));print(summary[:8])
