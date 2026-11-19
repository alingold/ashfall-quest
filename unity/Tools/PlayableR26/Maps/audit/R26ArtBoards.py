"""Fit thin solid plates to actual authored sign and banner faces on the nine patched art maps."""
import json,struct,sys
from pathlib import Path
import numpy as np
root=Path('/home/al/ashfall/unity');sa=root/'Assets/StreamingAssets/PlayableR23';out=Path(__file__).resolve().parent
result={}
for p in sorted((sa/'Legacy/SourceArt').glob('*.json')):
 d=json.loads(p.read_bytes())
 if not isinstance(d,dict) or 'patches' not in d:continue
 folder=sa/'Legacy/Maps'/p.stem
 if not (folder/'geometry.bytes').exists():folder=sa/'Maps'/p.stem
 m=json.loads((folder/'map.json').read_bytes());raw=(folder/'geometry.bytes').read_bytes();_,n=struct.unpack_from('<ii',raw);offset=8;parts={}
 for chunk in d['patches']:
  for part in chunk['parts']:
   if part['kind'] in ('sign','banner'):parts.setdefault(chunk['chunk'],[]).append(part)
 faces=[]
 for i in range(n):
  mi,col,vis,nv,ni=struct.unpack_from('<5i',raw,offset);offset+=20;v=np.frombuffer(raw,'<f4',nv*12,offset).reshape(-1,12)[:,:3];offset+=nv*48;ix=np.frombuffer(raw,'<i4',ni,offset).reshape(-1,3);offset+=4*ni
  for part in parts.get(i,[]):
   for tri in v[ix[part['triangles']]].astype(float):
    normal=np.cross(tri[1]-tri[0],tri[2]-tri[0]);mag=np.linalg.norm(normal)
    if mag<.0001:continue
    normal/=mag
    faces.append((tri,normal,part['kind'],col))
 # Join connected coplanar triangles across original source batches.
 groups=[]
 for i,(t,n,k,c) in enumerate(faces):
  same=[]
  for g in groups:
   if any(k==faces[j][2] and abs(n@faces[j][1])>.999 and abs((t[0]-faces[j][0][0])@n)<.025 and np.min(np.linalg.norm(t[:,None]-faces[j][0][None,:],axis=2))<.025 for j in g):same.append(g)
  merged=[i]
  for g in same:groups.remove(g);merged+=g
  groups.append(merged)
 rows=[]
 for g in groups:
  t,n,k,_=faces[g[0]];tris=np.array([faces[j][0] for j in g]);P=tris.reshape(-1,3)
  # Select an actual panel edge that minimizes its bounding rectangle in the panel plane.
  best=None
  for edge in np.concatenate([tris[:,1]-tris[:,0],tris[:,2]-tris[:,1],tris[:,0]-tris[:,2]]):
   edge=edge-n*(edge@n);length=np.linalg.norm(edge)
   if length<.001:continue
   u=edge/length;v=np.cross(u,n);xy=np.c_[P@u,P@v];lo=xy.min(0);hi=xy.max(0);area=np.prod(hi-lo)
   if best is None or area<best[0]:best=area,u,v,lo,hi
  area,u,v,lo,hi=best;w,h=hi-lo;center=u*((lo+hi)[0]/2)+v*((lo+hi)[1]/2)+n*np.mean(P@n)
  coverage=sum(np.linalg.norm(np.cross(b-a,c-a))/2 for a,b,c in tris)/area
  if not .97<=coverage<=1.04:print('fragment',p.stem,k,len(g),coverage,flush=True);continue
  rows.append(dict(id='R26-art-'+str(len(rows)),center=center.tolist(),right=u.tolist(),normal=n.tolist(),width=float(w),height=float(h),frontOffset=.035,confidence=1.,approved=True,kind=k,sourceFaces=len(g),existingCollider=all(faces[j][3] for j in g)))
 result[m['mapId']]=dict(source=m['sourceSha256'],panels=rows)
 print(p.stem,len(faces),len(rows),'noncolliding',sum(not r['existingCollider'] for r in rows),flush=True)
(out/'R26ArtBoards.json').write_text(json.dumps(result,indent=2))
