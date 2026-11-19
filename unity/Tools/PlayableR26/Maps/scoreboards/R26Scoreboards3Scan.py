"""Bi-Polar stock ring search in arbitrary local planes, without size/location gates.
No source mutation. Baseline candidates retained verbatim for review/provenance.
"""
from pathlib import Path
import json,sys,hashlib,collections
import numpy as np
sys.path.insert(0,str(Path(__file__).parent))
from R26ScoreboardsDetect import ROOT,RING,TRIM,ring_components,cloud_error
from R26ScoreboardsProbe import components,folders
from r23geom import MapData
OUT=Path('/home/al/ashfall/campaign/scoreboards3')

def axes(T):
 P=T.reshape(-1,3);center=P.mean(0);_,s,v=np.linalg.svd(P-center,full_matrices=False)
 if s[1]<1e-7 or s[2]>s[1]*.0002:return None
 n=v[-1];edges=np.concatenate([T[:,1]-T[:,0],T[:,2]-T[:,1],T[:,0]-T[:,2]])
 lengths=np.linalg.norm(edges,axis=1);u=edges[np.argmin(np.where(lengths>1e-7,lengths,np.inf))];u=u/np.linalg.norm(u);up=np.cross(n,u)
 return center,u,up,n

def local(T,a):
 c,u,v,n=a;return np.stack(((T-c)@u,(T-c)@v,(T-c)@n),axis=-1)

def reference():
 m=MapData(str(ROOT/'Assets/StreamingAssets/PlayableR23/Maps/bi-polar-gods-trashpandakiller'))
 T=np.concatenate([c.triangles for c in m.chunks if m.materials[c.material]==RING])
 for ids,_ in ring_components(T):
  P=np.unique(np.round(T[ids].reshape(-1,3),4),axis=0)
  if len(ids)==16 and np.linalg.norm(P.mean(0)-[234.169,193.6025,7.6181])<1:
   uv=P[:,:2];return (uv-uv.min(0))/np.ptp(uv,axis=0)
 raise RuntimeError('Bi-Polar template absent')

def split_rings(L):
    if len(L)==16:
        yield np.arange(16),False
        return
    # Search both in-plane axis assignments, independent of world up and scale.
    L=L/max(np.ptp(L.reshape(-1,3),axis=0).max(),1e-12)
    for swap in [False,True]:
        x=L[:,:,int(swap)];y=L[:,:,int(not swap)]
        def levels(a):
            out=[]
            for v in np.sort(a.reshape(-1)):
                if not out or v-out[-1][-1]>1e-5:out.append([v])
                else:out[-1].append(v)
            return np.array([np.mean(g) for g in out])
        xs,ys=levels(x),levels(y)
        for i in range(len(xs)-3):
            horizontal=(x.min(1)>=xs[i]-2e-5)&(x.max(1)<=xs[i+3]+2e-5)
            if horizontal.sum()<16:continue
            for j in range(len(ys)-4):
                keep=horizontal&(y.min(1)>=ys[j]-2e-5)&(y.max(1)<=ys[j+4]+2e-5)
                if keep.sum()==16:yield np.flatnonzero(keep),True

def extract(m,ref):
 # Material names in export are shared texture hashes, not prefab identifiers.
 materials={RING,'p95447a31'};parts=[]
 for mat in materials:
  chunks=[c for c in m.chunks if c.visible and m.materials[c.material]==mat]
  if not chunks:continue
  T=np.concatenate([c.triangles for c in chunks]);C=np.concatenate([np.full(len(c.triangles),c.index) for c in chunks])
  edge=np.linalg.norm(T[:,1]-T[:,0],axis=1);unit=np.percentile(edge[edge>1e-8],25)
  for ids in components((T-T.mean((0,1)))/unit):
   if len(ids)<16:continue
   basis=axes(T[ids])
   if basis is None:continue
   L=local(T[ids],basis);groups=list(split_rings(L))
   for sub,split in groups:
    if len(sub)!=16:continue
    raw=T[ids[sub]];P=np.unique(np.round(raw.reshape(-1,3),5),axis=0)
    # Tolerant welding for rotated float32 source vertices.
    Q=local(P,basis)[:,:2];size=np.ptp(Q,axis=0)
    if min(size)<=1e-6:continue
    norm=(Q-Q.min(0))/size
    if len(P)!=16:continue
    opts=[]
    for swap in [False,True]:
     rr=ref[:,::-1] if swap else ref
     for sx in [1,-1]:
      for sy in [1,-1]:opts.append((cloud_error(norm,rr*[sx,sy]+[sx<0,sy<0]),swap))
    error,swap=min(opts)
    if error>.0015:continue
    c,u,v,n=basis
    if swap:u,v=v,u
    # Choose vertical/readable sign; arbitrary tilt remains intact.
    if v[1]<0:v=-v
    u=np.cross(v,n)
    xy=np.c_[P@u,P@v];lo=xy.min(0);size=np.ptp(xy,axis=0);center=u*(lo[0]+size[0]/2)+v*(lo[1]+size[1]/2)+n*np.mean(P@n)
    parts.append(dict(P=P,u=u,v=v,n=n,center=center,size=size,error=error,material=mat,chunks=sorted(set(C[ids[sub]].tolist()))))
 results=[];used=set();trim=[c for c in m.chunks if c.visible and m.materials[c.material]==TRIM]
 for i,a in enumerate(parts):
  if i in used:continue
  opts=[];size=a['size'];tol=max(size)*.00015+1e-5
  for j,b in enumerate(parts):
   if j<=i or j in used or a['material']!=b['material'] or abs(a['n']@b['n'])<.99999:continue
   delta=b['center']-a['center'];d=abs(delta@a['n'])
   if d<=tol or d>min(size)*.25:continue
   if np.linalg.norm(delta-a['n']*(delta@a['n']))>tol:continue
   if np.max(abs(size-b['size']))>tol:continue
   opts.append((d,j))
  if not opts:continue
  depth,j=min(opts);b=parts[j];center=(a['center']+b['center'])/2;n=a['n'];u=a['u'];v=a['v'];gray=[];ci=[]
  for c in trim:
   T=c.triangles;xyz=local(T,(center,u,v,n));keep=np.all(abs(xyz)<=np.r_[size/2+tol,depth/2+tol],axis=(1,2))
   if keep.any():gray.append(T[keep]);ci.append(c.index)
  if not gray:continue
  gray=np.concatenate(gray);P=np.vstack([a['P'],b['P']]);err=float(np.linalg.norm(gray.reshape(-1,1,3)-P[None,:,:],axis=2).min(1).max())
  nearest=np.linalg.norm(gray.reshape(-1,1,3)-P[None,:,:],axis=2).argmin(1).reshape(-1,3)
  unique={tuple(sorted(t)) for t in nearest}
  if not (len(gray)==32 or len(unique)==32) or err>tol*2:continue
  used|={i,j}
  xy=np.c_[a['P']@u,a['P']@v];xs=np.sort(xy[:,0]);ys=np.sort(xy[:,1])
  def levels(values):
   groups=[]
   for x in values:
    if not groups or x-np.mean(groups[-1])>tol:groups.append([x])
    else:groups[-1].append(x)
   return np.array([np.mean(g) for g in groups])
  xs,ys=levels(xs),levels(ys)
  if len(xs)!=4 or len(ys)!=5:continue
  center+=u*((xs[1]+xs[2])/2-center@u)+v*((ys[1]+ys[3])/2-center@v)
  warm=np.array(m.manifest.get('warmupSpawn',m.manifest['spawn']));game=np.array(m.manifest.get('gameSpawn',m.manifest['spawn']))
  if (warm-center)@n<0:n=-n
  right=np.cross(n,v);w=xs[2]-xs[1];h=ys[3]-ys[1];inset=min(w,h)*.0005
  results.append(dict(center=center.tolist(),normal=n.tolist(),right=right.tolist(),up=v.tolist(),width=float(w-2*inset),height=float(h-2*inset),depth=depth,frontOffset=depth/2+min(w,h)*.0025,confidence=.98,approved=False,reason='complete Bi-Polar-family frame; pending visual review',lobbyDistance=float(np.linalg.norm(warm-center)),gameSpawnDistance=float(np.linalg.norm(game-center)),shapeError=a['error'],trimError=err,materials=[a['material'],TRIM],chunks=sorted(set(a['chunks']+b['chunks']+ci))))
 return results

def main():
 before=json.loads((OUT/'R26BeforeAudit.json').read_text());old={r['id']:r for r in before['maps']};ref=reference();rows=[];meta=[]
 for p in folders():
  p=p.parent if p.name=='map.json' else p;m=MapData(str(p));r=old[m.map_id];new=extract(m,ref);r['newGeometryMatches']=len(new)
  for q in new:
   if any(np.linalg.norm(np.array(c.get('frameCenter',c['center']))-q['center'])<min(q['width'],q['height'])*.005 for c in r['candidates']):continue
   q['id']='R26-stock-'+str(len(r['candidates']));r['candidates'].append(q)
  for q in r['candidates']:q.setdefault('up',[0.,1.,0.])
  rows.append(r)
  named=[]
  for name in ['source-chunks.json','source-semantics.json']:
   f=p/name
   if not f.exists():continue
   d=json.loads(f.read_text());keys=collections.Counter()
   def walk(v):
    if isinstance(v,dict):
     for k,x in v.items():
      if any(s in k.lower() for s in ['prefab','asset','guid']):keys[k]+=1
      walk(x)
    elif isinstance(v,list):
     for x in v:walk(x)
   walk(d);named.append(dict(file=name,sha256=hashlib.sha256(f.read_bytes()).hexdigest(),assetKeys=dict(keys)))
  meta.append(dict(id=m.map_id,folder=str(p),sources=named))
  print(m.map_id,len(r['candidates']),len(new),flush=True)
 (OUT/'R26Candidates.json').write_text(json.dumps(dict(reference='Bi-Polar actual mesh',normalizedRing=ref.tolist(),maps=rows),indent=2)+'\n')
 (OUT/'R26SourceInventory.json').write_text(json.dumps(meta,indent=2)+'\n')
 print('DONE',len(rows),'payloads',sum(len(r['candidates']) for r in rows),'candidates',flush=True)
if __name__=='__main__':main()
