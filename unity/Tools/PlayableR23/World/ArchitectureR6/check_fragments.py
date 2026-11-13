"""Check actual farm render partitions and a retained interior opening; no engine."""
from pathlib import Path
import base64,json,numpy as np
from check_footprint import raw_triangle,surface
from prepare import Footprints,polygon_area,OUT
HERE=Path(__file__).resolve().parent
staged=json.loads((HERE/'staged/classic-bigbox-vr-architecture.json').read_text())
parts={(p['chunk'],f['triangle']):f for p in staged['patches']for f in p.get('fragments',[])}
assert set(parts)=={(270,17924),(270,17926)},'Only the two actual compared gray farm faces can split'
reports=[]
for (chunk,triangle),row in parts.items():
 p,n,c=raw_triangle('classic-bigbox-vr',chunk,triangle)
 weights=np.array(row['barycentric']).reshape(-1,3,3);flags=np.frombuffer(base64.b64decode(row['triangleKinds']),'u1')
 assert len(weights)==len(flags)<=256 and np.isfinite(weights).all()and weights.min()>=0 and weights.max()<=1 and np.allclose(weights.sum(2),1,atol=1e-9)
 xyz=weights@p;normal=np.cross(p[1]-p[0],p[2]-p[0]);axis=np.argmax(abs(normal));axes=[i for i in range(3)if i!=axis]
 area=polygon_area(p[:,axes]);areas=np.array([polygon_area(t[:,axes])for t in xyz]);assert abs(areas.sum()-area)<area*1e-7,'Partition conserves original draw surface area'
 assert set(flags)=={1,255},'Actual source gaps remain original rendering, never brick fill'
 covered=areas[flags==1].sum()/area;assert abs(covered-row['sourceCoverage'])<1e-7 and covered>.95
 assert np.all(np.sum(np.cross(xyz[:,1]-xyz[:,0],xyz[:,2]-xyz[:,0])*normal,axis=1)>0),'Original winding retained'
 reports.append(dict(chunk=chunk,triangle=triangle,fragments=len(flags),covered=covered,originalRenderingRetained=1-covered))
# A small genuine hole cannot pass the original full-face rule. Partial
# source rendering must keep that same hole as original/unclassified triangles.
strips=[]
for a,b,d,e in [(0,0,2,.99),(0,1.01,2,2),(0,.99,.99,1.01),(1.01,.99,2,1.01)]:strips.extend([[[a,b,0],[d,b,0],[d,e,0]],[[a,b,0],[d,e,0],[a,e,0]]])
index=Footprints([surface(strips)]);p=np.array([[0,0,0],[2,0,0],[2,2,0]],float);n=np.tile([0,0,1],(3,1));c=np.tile([.5,.4,.3],(3,1));assert index.classify('vc',p,n,c)is None
split=index.fragments('vc',p,n,c);assert split is not None;w=np.array(split['barycentric']).reshape(-1,3,3);flags=np.frombuffer(base64.b64decode(split['triangleKinds']),'u1');q=w@p;hole=sum(polygon_area(t[:,:2])for t,flag in zip(q,flags)if flag==255)
assert abs(hole-.0002)<1e-9,'Half of actual .02x.02 opening remains unpainted in this diagonal half-square'
assert index.fragments('other-material',p,n,c)is None and index.fragments('vc',p,n*-1,c)is None
print('PASS actual two farm partitions: conserved area/winding, valid barycentric bounds, source coverage and unpainted interior opening.',reports)
(HERE/'fragment-checks.json').write_text(json.dumps(dict(farm=reports,smallInteriorHoleArea=hole,collisionChanged=False,engineRun=False),indent=2)+'\n')
