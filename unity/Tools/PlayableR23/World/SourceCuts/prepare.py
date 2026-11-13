"""Prepare immutable v003 barycentric cuts; no live payload or engine writes."""
from pathlib import Path
import json,hashlib,struct,base64,collections,numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];INPUT=Path(r'D:/ashfall-r6-staging/world/classic-source-cuts-v003');BASE=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr';OUT=HERE/'Payload';OUT.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(INPUT/'HANDOFF.json')=='b7d9871c07df8f9e9083f2a06594a42e0229cda25af3b1f9e119fc2236f0e20f'
assert sha(INPUT/'SOURCE-CUT-PARTITIONS.json')=='243f1dd751d97ab46dade4efcfacbac4b10c8f0ff9e23b990875e09f27523114'
data=json.loads((INPUT/'SOURCE-CUT-PARTITIONS.json').read_text());summary=data['summary'];manifest=json.loads((BASE/'map.json').read_text());tower=BASE/manifest['towerFixturesFile'];art=ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt/classic-bigbox-vr.json';arch=BASE/'architecture.json'
assert sha(BASE/'map.json')==summary['baseMapSha256'];assert sha(tower)==summary['baseSidecarSha256'];assert sha(BASE/'geometry.bytes')==summary['baseGeometrySha256']
assert len(data['appendDefinitions'])==381 and [x['ownerIndex']for x in data['appendDefinitions']]==list(range(337,718));assert summary['partitionDeferrals']==47
old=json.loads(tower.read_text());patches={x['chunk']:x for x in old['chunks']};assert len(old['definitions'])==337
archdata=json.loads(arch.read_text());kinds={};masks={}
for p in archdata['patches']:
 kinds.update({(p['chunk'],tri):kind for tri,kind in zip(p['triangles'],base64.b64decode(p['triangleKinds']))})
 for f in p.get('fragments',[]):masks[p['chunk'],f['triangle']]=list(zip(np.array(f['barycentric']).reshape(-1,3,3),base64.b64decode(f['triangleKinds'])))
def det(t):
 a=np.asarray(t,dtype='float64');return float((a[1,1]-a[0,1])*(a[2,2]-a[0,2])-(a[1,2]-a[0,2])*(a[2,1]-a[0,1]))
def cross(a,b):return a[0]*b[1]-a[1]*b[0]
def intersect(a,b):
 # Convex intersection in exact source barycentric domain, no mask expansion.
 poly=[np.array(x[1:],dtype='float64')for x in a]
 for i in range(3):
  start,end=np.asarray(b[i][1:]),np.asarray(b[(i+1)%3][1:]);edge=end-start;previous=poly[-1]if poly else None;result=[]
  for current in poly:
   before=cross(edge,previous-start);after=cross(edge,current-start)
   if (before>=0)!=(after>=0):result.append(previous+(current-previous)*(before/(before-after)))
   if after>=0:result.append(current)
   previous=current
  poly=result
  if not poly:break
 out=[]
 for i in range(1,len(poly)-1):
  tri=np.array([[1-x.sum(),x[0],x[1]]for x in [poly[0],poly[i],poly[i+1]]]);area=det(tri)
  if area>0:out.append(tri.tolist())
 return out
faces=collections.defaultdict(list);old_stolen=0;mask_intersections=0;emitted=0
for row in data['trianglePartitions']:
 c,t=row['chunk'],row['triangle'];p=patches.get(c)
 if p:
  assert any(a<=t<a+n for a,n in zip(p['staticTriangleRanges'][::2],p['staticTriangleRanges'][1::2]))
  for part in p['parts']:
   assert not any(a<=t<a+n for a,n in zip(part['triangleRanges'][::2],part['triangleRanges'][1::2]))
 original_static=row['staticBarycentricTriangles'];static=[]
 if (c,t)in masks:
  mask_intersections+=1
  for tri in original_static:
   for mask,kind in masks[c,t]:static.extend((fragment,kind)for fragment in intersect(tri,mask))
 else:static=[(x,kinds.get((c,t),255))for x in original_static]
 owners=row['ownedBarycentricTriangles'];orig_area=sum(det(x)for x in original_static);new_area=sum(det(x)for x,k in static)
 assert abs(orig_area-new_area)<2e-8,(c,t,'static mask conservation',orig_area,new_area)
 alltris=[x for x,k in static]+[x for owner in owners for x in owner['triangles']]
 assert abs(sum(det(x)for x in alltris)-1)<2e-8,(c,t,'total area')
 for tri in alltris:
  a=np.asarray(tri);assert np.isfinite(a).all() and a.min()>-1e-10 and a.max()<1+1e-10 and np.max(np.abs(a.sum(1)-1))<1e-9 and det(a)>0
 faces[c].append((t,static,owners));emitted+=len(alltris)
assert len(faces)==27 and mask_intersections==2
path=OUT/'classic-source-cuts-v003.bytes'
with path.open('wb')as f:
 def i(*v):f.write(struct.pack('<'+'i'*len(v),*v))
 def string(v):b=v.encode();i(len(b));f.write(b)
 def doubles(v):f.write(struct.pack('<'+'d'*len(v),*v))
 def vec(v):f.write(struct.pack('<3f',*v))
 i(0x34544353,1)
 for value in ['archive:classic-bigbox-vr',sha(INPUT/'SOURCE-CUT-PARTITIONS.json'),sha(BASE/'map.json'),sha(tower),sha(BASE/'geometry.bytes'),sha(arch),sha(art),summary['preparedR10Sha256']]:string(value)
 i(manifest['chunks'],337,381,27,10907)
 for row in data['appendDefinitions']:
  i(row['sourcePanelId']);string(row['id']);string(row['sound']);i(int(row['glass']));f.write(struct.pack('<f',row['health']))
  for key in ['center','size','probe','normal']:vec(row[key])
 for guard in data['chunkGuards']:
  i(guard['chunk'],guard['material'],guard['collisionFlags'],guard['visible'],guard['vertices'],guard['indices']);string(guard['materialKey']);string(guard['vertexSha256']);string(guard['indexSha256']);i(len(faces[guard['chunk']]))
  for triangle,static,owners in faces[guard['chunk']]:
   i(triangle,len(static),len(owners))
   for tri,kind in static:i(kind);doubles(np.asarray(tri).reshape(-1))
   for owner in owners:
    i(owner['ownerIndex'],owner['sourceOwner'],len(owner['triangles']))
    for tri in owner['triangles']:doubles(np.asarray(tri).reshape(-1))
new=dict(manifest);new['sourceCutsFile']=path.name;new['sourceCutsSha256']=sha(path);new['sourceCutsBaseMapSha256']=sha(BASE/'map.json');(OUT/'map.json').write_text(json.dumps(new,indent=2)+'\n')
receipt={'sourceCutsSha256':sha(INPUT/'SOURCE-CUT-PARTITIONS.json'),'baseMapSha256':sha(BASE/'map.json'),'baseTowerSha256':sha(tower),'geometrySha256':sha(BASE/'geometry.bytes'),'architectureSha256':sha(arch),'sourceArtSha256':sha(art),'binarySha256':sha(path),'binaryBytes':path.stat().st_size,'mapCandidateSha256':sha(OUT/'map.json'),'protectedOwners':337,'newOwners':381,'totalOwners':718,'rawFaces':10907,'emittedTriangles':emitted,'staticMaskIntersections':mask_intersections,'remainingStatic':492,'unchangedDeferrals':47,'engineExecuted':False}
(HERE/'prepared.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt,indent=2))
