"""Supporting halfspaces of the original small convex source point clouds.

No shape expansion or topology adoption. Original points are retained verbatim;
each output plane is placed at the exact maximum projection of those points.
Native Godot/PhysX point-query boundary behavior remains unqualified.
"""
import itertools,math
import numpy as np
def planes(points):
 p=np.unique(np.asarray(points,dtype=np.float64).reshape(-1,3),axis=0)
 assert 4<=len(p)<=64 and np.isfinite(p).all()and np.linalg.matrix_rank(p-p[0])==3
 ids=np.array(list(itertools.combinations(range(len(p)),3)),dtype=np.int32)
 a=p[ids[:,0]];n=np.cross(p[ids[:,1]]-a,p[ids[:,2]]-a);length=np.linalg.norm(n,axis=1);keep=length>1e-12;n=n[keep]/length[keep,None];a=a[keep]
 # A supporting face has every other vertex on one side. The tolerance here
 # only discovers candidate normals; offset=max projection cannot cut the hull.
 distance=np.einsum('ij,kj->ik',n,p)-np.einsum('ij,ij->i',n,a)[:,None]
 positive=(distance<=1e-8).all(axis=1);negative=(distance>=-1e-8).all(axis=1)
 result=set()
 for normal in np.concatenate((n[positive],-n[negative])):
  x,y,z=map(float,normal);support=max(x*float(v[0])+y*float(v[1])+z*float(v[2])for v in p)
  result.add((x,y,z,-support))
 result=sorted(result);assert len(result)>=4
 # Do not round normal/offset data to deduplicate nearly coplanar faces.
 assert all(x*v[0]+y*v[1]+z*v[2]+d<=0 for x,y,z,d in result for v in p)
 return [float(x)for plane in result for x in plane]
def contains(flat,point):
 return all(flat[i]*point[0]+flat[i+1]*point[1]+flat[i+2]*point[2]+flat[i+3]<=0 for i in range(0,len(flat),4))
def controls():
 checks=0
 box=list(itertools.product((-2.,2.),(-3.,3.),(-4.,4.)));result=planes(box);assert len(result)==24;checks+=1
 for p in itertools.product((-2.1,-1.,0.,2.1),(-3.1,0.,3.1),(-4.1,0.,4.1)):
  assert contains(result,p)==(abs(p[0])<=2 and abs(p[1])<=3 and abs(p[2])<=4);checks+=1
 tetra=[[0,0,0],[1,0,0],[0,1,0],[0,0,1]];result=planes(tetra);assert len(result)==16;checks+=1
 for p in itertools.product((-.1,.1,.4,.9),repeat=3):
  assert contains(result,p)==(min(p)>=0 and sum(p)<=1);checks+=1
 rng=np.random.default_rng(987143)
 for count in (4,8,15,40):
  source=rng.normal(size=(count,3)).astype(np.float32).astype(float);result=planes(source)
  for _ in range(50):
   weights=rng.random(count);weights/=weights.sum();p=(source*weights[:,None]).sum(axis=0);assert contains(result,p);checks+=1
  for axis in range(3):
   for sign in (-1,1):
    p=source.mean(axis=0);p[axis]=source[:,axis].max()+100 if sign>0 else source[:,axis].min()-100;assert not contains(result,p);checks+=1
 return checks
if __name__=='__main__':print('PASS',controls(),'convex source metadata numerical controls; no native physics claim')
