"""Actual farm witness + opening/overlap/plane/colour controls. CPU only."""
from pathlib import Path
import json,struct,numpy as np
from prepare import Footprints,uncovered,polygon_area,ROOT,OUT
HERE=Path(__file__).resolve().parent;W=Path('D:/ashfall-r6-staging/parity-v002');cases=0
def check(yes,why):
 global cases
 cases+=1;assert yes,why
def raw_triangle(slug,chunk,triangle):
 with(OUT/slug/'geometry.bytes').open('rb')as f:
  _,chunks=struct.unpack('<ii',f.read(8))
  for c in range(chunks):
   mid,co,vi,nv,ni=struct.unpack('<iiiii',f.read(20))
   if c!=chunk:f.seek(nv*48+ni*4,1);continue
   v=np.frombuffer(f.read(nv*48),'<f4').reshape(nv,12);ix=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3)[triangle];return v[ix,:3],v[ix,3:6],v[ix,8:11]
 raise AssertionError('Actual witness absent')
def surface(points,kinds=None,normal=None,color=None):
 points=np.asarray(points,np.float32);n=len(points)
 if normal is None:normal=np.array([0,0,1],np.float32)
 if color is None:color=np.array([.5,.4,.3],np.float32)
 return dict(node='actual-source-component',material='vc',positions=points,normals=np.tile(normal,(n,3,1)),colors=np.tile(color,(n,3,1)),kinds=np.full(n,1,np.uint8)if kinds is None else np.asarray(kinds,np.uint8))
# Actual source data: all five same-plane/color classified triangles from WORLD.
witness=json.loads((W/'FARM-COPLANAR-FOOTPRINT.json').read_text());p,n,c=raw_triangle('classic-bigbox-vr',270,17925);q=np.array([r['sourcePositions']for r in witness['coplanarColourMatchedSourceTriangles']],np.float32);q[:,:,2]*=-1
s=surface(q,normal=n[0],color=c[0]);index=Footprints([s]);result=index.classify('vc',p,n,c);check(result is not None and result[0]==1 and result[1]>.9999,'actual re-triangulated farm wall becomes kind1 from source union')
# Union conserves coverage even if every covering polygon is repeated.
target=np.array([[0,0],[2,0],[0,2]],float);half=np.array([[0,0],[2,0],[0,1]],float);check(abs(uncovered(target,[half,half])-uncovered(target,[half]))<1e-10,'duplicate polygons never inflate covered union area')
square=np.array([[[0,0,0],[2,0,0],[2,2,0]],[[0,0,0],[2,2,0],[0,2,0]]],float);tri=square[0];nn=np.tile([0,0,1],(3,1));cc=np.tile([.5,.4,.3],(3,1));full=Footprints([surface(square)])
check(full.classify('vc',tri,nn,cc)[0]==1,'actual coplanar same-component rectangle covers its complete triangle')
check(full.classify('different-material',tri,nn,cc) is None,'material-wide substitution rejected');check(full.classify('vc',tri,nn,cc+.02) is None,'different original RGB rejected');check(full.classify('vc',tri,nn*-1,cc)is None,'opposite source face normal rejected')
check(full.classify('vc',tri+np.array([0,0,.006]),nn,cc)is None,'parallel wall6mm away rejected');check(full.classify('vc',tri+np.array([2.1,0,0]),nn,cc)is None,'nearby disjoint wall rectangle is not painted')
conflict=Footprints([surface(np.r_[square,square],kinds=[1,1,4,4])]);check(conflict.classify('vc',tri,nn,cc)is None,'overlapping source kinds cannot choose arbitrary class');unknown=Footprints([surface(np.r_[square,square],kinds=[1,1,255,255])]);check(unknown.classify('vc',tri,nn,cc)is None,'unclassified coincident source surface is an ambiguity')
# Real-shaped opening: triangulated strips surround a central1m window. The
# bounding rectangle covers the target, but the actual triangle union does not.
strips=[]
for a,b,d,e in [(0,0,2,.5),(0,1.5,2,2),(0,.5,.5,1.5),(1.5,.5,2,1.5)]:strips.extend([[[a,b,0],[d,b,0],[d,e,0]],[[a,b,0],[d,e,0],[a,e,0]]])
hole=Footprints([surface(strips)]);check(hole.classify('vc',tri,nn,cc)is None,'genuine window hole remains uncovered despite full outer bounding rectangle')
# Tiny interior hole also rejects: near-total area alone is insufficient.
strips=[]
for a,b,d,e in [(0,0,2,.99),(0,1.01,2,2),(0,.99,.99,1.01),(1.01,.99,2,1.01)]:strips.extend([[[a,b,0],[d,b,0],[d,e,0]],[[a,b,0],[d,e,0],[a,e,0]]])
hole=Footprints([surface(strips)]);check(hole.classify('vc',tri,nn,cc)is None,'small interior opening cannot hide in allowed edge-area tolerance')
print('PASS',cases,'actual farm union and hole/rectangle/material/RGB/oriented-normal/plane/overlap controls; coverage',result[1])
(HERE/'footprint-checks.json').write_text(json.dumps(dict(cases=cases,actualFarmKind=result[0],actualFarmCoverage=result[1],engineRun=False),indent=2)+'\n')
