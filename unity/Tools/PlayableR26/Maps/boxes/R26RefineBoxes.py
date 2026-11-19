import sys,json
sys.path.insert(0,'/home/al/ashfall/tools/boxes')
import R26BoxesReview as b
import numpy as np
rows=json.loads((b.OUT/'analysis.json').read_bytes())
for r in rows:
 m,ch=b.maps.load(str(b.SA/r['folder']));T=b.world(ch)
 # Hints themselves can be above the feet (humanoid heads / direction rings).
 # Locate feet first. Probe ceilings from feet, not from the top of the glyph.
 hints=np.concatenate([np.array(r['markers']).reshape(-1,3),np.array(r['figureHints']).reshape(-1,3)])
 floors=[];ceilings=[]
 for p in hints:
  h=b.ray(T,p+[0,.6,0])
  if h and p[1]-8<=h[0]<=p[1]+.5:
   f=np.array([p[0],h[0]+.12,p[2]]);roof=b.ray(T,f+[0,1.9,0],True);floors.append(f);ceilings.append(roof)
 force=sum(h is not None and h[1]==b.maps.FORCEFIELD for h in ceilings)/max(1,len(ceilings));cover=sum(h is not None for h in ceilings)/max(1,len(ceilings))
 # Broad forcefield shell contains vertical faces too; a small local roof is not
 # sufficient. Manual visual verdicts are applied separately after contact review.
 ff=[c['pos'][c['idx']] for c in ch if c['mat']==b.maps.FORCEFIELD and c['flags']]
 vertical=False
 if ff:
  ft=np.concatenate(ff);n=np.cross(ft[:,1]-ft[:,0],ft[:,2]-ft[:,0]);area=np.linalg.norm(n,axis=1);vertical=bool(np.sum(area[np.abs(n[:,1])<area*.3])>100)
 if force>=.5 and vertical:r['verdict']='BOXED';r['confidence']=.98
 elif force>=.5 and not vertical:r['verdict']='BOXED';r['confidence']=.85
 elif cover>=.9 and len(floors)>3:r['verdict']='BOXED';r['confidence']=.87
 elif r['verdict']=='BOXED' and r['forcefieldCoverage']<.5:r['verdict']='UNSURE';r['confidence']=.65
 r['markerRoofCoverage']=cover;r['markerForcefieldCoverage']=force;r['forcefieldWalls']=vertical
 if r['verdict']=='BOXED':
  interior=[f.tolist() for f,h in zip(floors,ceilings) if h and (force<.5 or h[1]==b.maps.FORCEFIELD) and h[0]-f[1]>=1.9]
  r['proposedSpawns']=interior or r['proposedSpawns'];r['pods']=False;r['spawnMode']='authored-markers' if interior else 'interior-floor'
  roofs=[h[0] for h in ceilings if h and (force<.5 or h[1]==b.maps.FORCEFIELD)]
  if roofs:r['ceiling']=float(np.median(roofs))
  if r['proposedSpawns']:r['base']=float(np.median(np.array(r['proposedSpawns'])[:,1]))
 r['reason']+=f' Ceiling above {cover:.0%} of {len(floors)} verified figure/marker floors; forcefield {force:.0%}; forcefield walls {vertical}.'
 # All three overlay colors remain visible even when the points coincide.
 r['currentSpawnProblem']='elevated-warmup-in-sandbox' if r['mapId']=='archive:blue-gods-quietowl' else r['currentSpawnProblem']
 print(r['mapId'],r['verdict'],round(force,2),round(cover,2),len(r['proposedSpawns']),flush=True)
(b.OUT/'analysis.json').write_text(json.dumps(rows,indent=1)+'\n')
