import sys,json,numpy as np,hashlib
sys.path.insert(0,'/home/al/ashfall/tools/boxes');import R26BoxesReview as b
rows=json.loads((b.OUT/'analysis.json').read_bytes())
OPEN='atrocious-gods-trashpandakiller bi-polar-gods-trashpandakiller light-vs-dark-gods-badbar sweet-dreams-gods-badbar quantum-city-gods-pitdevil the-holler-gods-alamo-hank trashed-gods-pitdevil'.split()
BOXED='bamboo-gods-pitdevil banana-land-gods-pitdevil beast-mode-gods-pitdevil candle-magic-gods-goodwitch thor-city-gods-thor zelda-kokiri-forest-solos-ilikepies1211 chess-ifun 5-levels-zyro heavens-arena-big-battles-swa-wildbarnacle sharp-objects-kiwishade survive-gods-goodwitch'.split()
UNSURE='caskades-gods-alamo-hank classic-bw-187norvis skb-paintball-gods-skblong'.split()
OVERRIDES={r['id']:r for r in json.loads((__import__('pathlib').Path(__file__).with_name('R26BoxesOverrides.json')).read_bytes())['maps']}
old=json.loads((b.ROOT/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapPodPolicy.json').read_bytes());oldpods={r['id']:r['pods'] for r in old['maps']}
for r in rows:
 slug=r['mapId'].replace('archive:','');r['visualReviewed']=True
 if slug in OPEN:r['verdict']='OPEN';r['confidence']=.94;r['reason']+=' Visual correction: open arena and local building roofs; no continuous arena ceiling.'
 if slug in BOXED:r['verdict']='BOXED';r['confidence']=.94;r['reason']+=' Visual correction: surrounding walls and overhead shell enclose the playable arena.'
 if slug in UNSURE:r['verdict']='UNSURE';r['confidence']=.5;r['reason']+=' Visual review cannot establish a continuous arena ceiling from the exported payload.'
 if slug=='viral-gods-ghostlymostly88':r['confidence']=1.;r['reason']+=' Author confirmed BOXED in headset (LEAD request).'
 if slug in ('blue-gods-quietowl','magnum-chaosvr'):r['confidence']=1.;r['reason']+=' Author-confirmed enclosed arena.'
 if r['mapId'] in OVERRIDES:
  reviewed=OVERRIDES[r['mapId']];r['verdict']=reviewed['verdict'];r['confidence']=reviewed['confidence'];r['reason']+=' '+reviewed['reason']
 if slug=='cold-hearted-gods-pitdevil':r['excluded']=True;r['selectionStatus']='EXCLUDED';r['reason']+=' LEAD excluded this glitched double map from map selection.'
 r['pods']=False if r['verdict']=='BOXED' else oldpods.get(r['mapId'],r['mapId'] not in b.maps.GROUND)
 if r['verdict']!='BOXED':r['spawnMode']='top-down';continue
 m,ch=b.maps.load(str(b.SA/r['folder']));T=b.world(ch);ff=[c for c in ch if c['mat']==b.maps.FORCEFIELD and c['flags']];FT=b.world(ff) if ff else None
 hints=np.concatenate([np.array(r['markers']).reshape(-1,3),np.array(r['figureHints']).reshape(-1,3)]);sites=[];kind=[];roofs=[]
 # Prefer points genuinely under an arena shell; markers outside it are not grants.
 for p in hints:
  hit=b.ray(T,p+[0,.6,0]);
  if not hit or not p[1]-8<=hit[0]<=p[1]+.5:continue
  f=np.array([p[0],hit[0]+.12,p[2]]);roof=(b.ray(FT,f+[0,1.9,0],True) if FT is not None else None) or b.ray(T,f+[0,1.9,0],True)
  if roof:sites.append(f.tolist());kind.append('authored-marker' if len(r['markers']) else 'archived-figure');roofs.append(roof[0])
 # A marker may be outside a smaller box. For that case probe floors from
 # inside the shell, below its roof, and retain only points with standing room.
 if not sites:
  bounds=np.concatenate([c['pos'] for c in ff]) if ff else np.array([r['boundsMin'],r['boundsMax']]);lo=bounds.min(0);hi=bounds.max(0)
  seed=float(np.median(hints[:,1])) if len(hints) else r['base']
  for z in np.linspace(lo[2]+2,hi[2]-2,19):
   for x in np.linspace(lo[0]+2,hi[0]-2,19):
    top=(b.ray(FT,[x,lo[1]+2,z],True) if FT is not None else None) or b.ray(T,[x,lo[1]+2,z],True)
    if top is None:continue
    start=min(seed+.6,top[0]-2.2);floor=b.ray(T,[x,start,z])
    if floor is None or floor[0]<lo[1]-10 or top[0]-floor[0]<2.2:continue
    sites.append([x,floor[0]+.12,z]);kind.append('interior-grid');roofs.append(top[0])
 # Preserve up to 96 distinct feet sites, with no source-top fallback.
 unique=[];kinds=[]
 for p,k in zip(sites,kind):
  if all(np.linalg.norm(np.array(p)-np.array(q))>=.8 for q in unique):unique.append([round(float(v),4) for v in p]);kinds.append(k)
 r['proposedSpawns']=unique[:128];r['spawnMode']='authored-markers' if any(k!='interior-grid' for k in kinds) else 'interior-floor';r['spawnOrigins']=kinds[:128]
 if roofs:r['ceiling']=float(np.median(roofs))
 if unique:r['base']=float(np.median(np.array(unique)[:,1]))
 else:r['verdict']='UNSURE';r['confidence']=.4;r['reason']+=' No interior floor candidate found: manual correction required.'
 if r['mapId']=='archive:blue-gods-quietowl':r['currentSpawnProblem']='elevated-warmup-in-sandbox'
 elif r['currentSpawnProblem']=='ok':r['currentSpawnProblem']='roof-fallback-risk' if not r['markers'] else 'ok'
 print(slug,r['verdict'],len(r['proposedSpawns']),r['spawnMode'],flush=True)
(b.OUT/'analysis.json').write_text(json.dumps(rows,indent=1)+'\n')
# Human-review handoff keeps all evidence and coordinates in a single exact-map row.
(b.OUT/'verdicts.json').write_text(json.dumps(rows,indent=1)+'\n')
policy=[]
for r in rows:
 policy.append(dict(id=r['mapId'],pods=r['pods'],verdict=r['verdict'],sourceSha256=r['sourceSha256'],source='BOXES visual review',evidence=r['reason'],spawns=[dict(x=p[0],y=p[1],z=-p[2]) for p in r['proposedSpawns']]))
path=b.ROOT/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapPodPolicy.json'
path.write_text('{\n "version":1,\n "coordinates":"source-local Godot, Y up; runtime reflects Z once",\n "maps":[\n'+',\n'.join('  '+json.dumps(r,separators=(',',':')) for r in policy)+'\n ]\n}\n')
print('COUNTS',__import__('collections').Counter(r['verdict'] for r in rows),flush=True)
