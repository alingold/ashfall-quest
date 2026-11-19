import sys,json,numpy as np
sys.path.insert(0,'/home/al/ashfall/tools/boxes');import R26BoxesReview as b
rows=json.loads((b.OUT/'analysis.json').read_bytes())
for r in rows:
 m,ch=b.maps.load(str(b.SA/r['folder']));fc=[c for c in ch if c['mat']==b.maps.FORCEFIELD and c['flags']]
 if not fc:continue
 T=b.world(fc);hints=np.array(r['proposedSpawns']).reshape(-1,3)
 if not len(hints):hints=np.array(r['figureHints']).reshape(-1,3)
 roofs=[b.ray(T,p+[0,1.9,0],True) for p in hints];hits=[h[0] for h in roofs if h];cover=len(hits)/max(1,len(roofs));r['shellRoofCoverage']=cover
 if cover>=.8:
  r['verdict']='BOXED';r['confidence']=.98;r['pods']=False;r['spawnMode']='authored-markers' if r['figureHints'] or r['markers'] else 'interior-floor';r['ceiling']=float(np.median(hits))
  r['reason']+=f' Forcefield-only ray reaches the outer shell above {cover:.0%} of floor sites, ignoring intervening building roofs.'
 print(r['mapId'],round(cover,2),r['verdict'],flush=True)
(b.OUT/'analysis.json').write_text(json.dumps(rows,indent=1)+'\n')
