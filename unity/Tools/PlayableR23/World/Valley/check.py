"""Focused actual-data and transform checks; no engine/runtime/native claims."""
from pathlib import Path
import collections,json,math,struct
import numpy as np
from prepare import Converter,DEFAULT,DEST,HERE,REFLECT,sha,transform,unpack
checks=0
def check(ok,label):
    global checks
    assert ok,label
    checks+=1
c=Converter(DEFAULT,False)
try:c.require_batches()
except ValueError as error:check('1124'in str(error),'Full conversion refuses genuinely lost1124 poses before publication')
else:raise AssertionError('Lost poses accepted')
check(not (DEST/'map.json').exists() or not json.loads((DEST/'map.json').read_text())['diagnosticOnly'],'Diagnostic never published as playable Valley')
out=HERE/'diagnostic-direct';d=json.loads((out/'map.json').read_text());p=json.loads((out/'provenance.json').read_text());check(d['diagnosticOnly'] and d['mapId']=='diagnostic:valley-direct' and not p['completePlayableMap'],'Clearly noncatalog diagnostic')
check(p['qualificationSha256']==sha(DEFAULT.parent/'qualification.json') and p['sourceWorldSha256']==sha(DEFAULT/'world.json') and p['sourcePayloadSha256']==sha(DEFAULT/'payload.bin'),'Original qualified evidence intact')
check(p['qualification']['scriptErrors']["Attempt to call function 'get_class' in base 'null instance' on a null instance."]==1633,'Typed-null qualification preserved, not claimed clean')
check(sha(out/'geometry.bytes')==p['geometrySha256'],'Geometry identity')
chunks=[]
with (out/'geometry.bytes').open('rb') as f:
    magic,count=struct.unpack('<ii',f.read(8));check(magic==0x52323357 and count==d['chunks']<=2048,'v2 magic/bounded chunk count')
    for index in range(count):
        mid,flags,visible,nv,ni=struct.unpack('<iiiii',f.read(20));v=np.frombuffer(f.read(nv*72),'<f4').reshape(-1,18);ii=np.frombuffer(f.read(ni*4),'<i4');check(0<=mid<len(d['materials']) and flags in [0,1,3] and visible in [0,1] and nv<=60000 and ni%3==0,'Native schema header')
        check(np.isfinite(v).all() and ii.min()>=0 and ii.max()<nv,'Finite18float vertices/valid triangle indices');chunks.append((mid,flags,visible,v,ii))
    check(not f.read(),'Full binary consumed')
expected_render=0;visible_count=0
for node in c.data['meshInstances']:
    if not node['visible']:continue
    visible_count+=1
    for surface in c.meshes[node['mesh']]['surfaces']:
        a=surface['arrays'];expected_render+=(a[12]['count'] if a[12] and a[12]['count'] else a[0]['count'])//3
check(visible_count==239 and expected_render==581636==sum(len(i)//3 for m,fl,vis,v,i in chunks if vis),'Every239 source-visible direct instance; exact581636 render triangles')
check(sum(len(i)//3 for m,fl,vis,v,i in chunks if fl)==454466,'Actual collision triangle census')
check(p['collisionTriangleKinds']=={'concave':451394,'convex':300,'cylinder64':2772},'Source concave exact451394/hull300/cylinder2772 triangle counts')
convex=[(v,i) for m,fl,vis,v,i in chunks if fl==3];check(len(convex)==20,'Each9convex/11cylinder remains separate collider owner')
check(sum(len(i)==252*3 for v,i in convex)==11,'Eleven64sided prisms,252 triangles each')
check(len(d['boxes'])==1138 and p['shapeOccurrences']==1272,'Every original box and enabled StaticBody shape occurrence')
box_by_name={box['name']:box for box in d['boxes']}
import itertools
for record in p['sourceShapeRecords']:
    if record['kind']!='BoxShape3D':continue
    name=record['body']+'/shape'+str(record['shapeIndex']);box=box_by_name[name];b,t=transform(record['sourceTransform']);size=np.asarray(unpack(record['sourceProperties']['size']));corners=np.asarray(list(itertools.product([-1,1],repeat=3)))*.5
    original=(corners*size@b.T+t)@REFLECT;x,y,z,w=box['rotation'];r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    recreated=corners*np.asarray(box['size'])@r.T+box['center'];dist=np.linalg.norm(original[:,None,:]-recreated[None,:,:],axis=2)
    check(dist.min(axis=1).max()<2e-5,'Actual source OBB corners preserved by center/size/quaternion '+name)
for vertices,indices in convex:
    faces=indices.reshape(-1,3);edges=collections.Counter(tuple(sorted((int(tri[a]),int(tri[b])))) for tri in faces for a,b in [(0,1),(1,2),(2,0)])
    check(set(edges.values())=={2} and len(vertices)-len(edges)+len(faces)==2,'Each cooked convex owner is a closed manifold, not a combined union')
check(len(d['chunkSourceLayers'])==count==len(d['chunkBackfaceCollision']),'Aligned original layers/backfaces')
check(set(d['chunkSourceLayers'])|{box['sourceLayer'] for box in d['boxes']}=={0,1,4,8,9},'Source collision roles1/4/8/9 preserved across chunks/boxes')
check(d['spawn']==[0,1,-8] and d['markers']['extraction_position']==[0,.20000000298023224,-15],'Actual source spawn/markers reflected once')
# Actual first massif: last6 floats must be original local arrays, not baked world.
first=c.data['meshInstances'][0];surface=c.meshes[first['mesh']]['surfaces'][0];a=surface['arrays'];src=np.column_stack((c.array(a[0]),c.array(a[1])));actual=np.concatenate([v for mid,fl,vis,v,i in chunks if vis and d['materials'][mid]['name']==first['activeSurfaceMaterials'][0]['resourceRef']])
check(np.array_equal(np.unique(src,axis=0),np.unique(actual[:,12:18],axis=0)),'Original raw Godot local massif positions/normals byte-exact')
check(np.allclose(actual[:,:3],(actual[:,12:15]+np.asarray(first['globalTransform'][9:]))@REFLECT,atol=2e-5),'Actual massif world translation/reflection once')
# Independent nonuniform/sheared normal test: transformed normal remains
# perpendicular to both transformed source tangent directions.
basis=np.array([[2.,.2,0],[0,3.,.1],[0,0,4.]])
matrix=basis.ravel(order='F').tolist()+[5,6,7];normal=np.array([[1,1,0.]])/math.sqrt(2);v=c.vertices(np.zeros((1,3)),normal,np.zeros((1,2)),np.ones((1,4)),matrix)
check(abs(v[0,3:6]@(np.array([1,-1,0])@basis.T@REFLECT))<1e-6 and abs(v[0,3:6]@(np.array([0,0,1])@basis.T@REFLECT))<1e-6,'Inverse-transpose world normal preserves tangent orthogonality under shear/nonuniform scale')
check(np.array_equal(v[0,12:15],[0,0,0]) and np.allclose(v[0,15:18],normal[0]),'Source-local payload untouched under affine conversion')
for name,row in p['textureImages'].items():check(sha(out/name)==sha(row['source'])==row['sha256'],'Exact original source PNG view bytes '+name)
check(len(p['textureImages'])==36,'36 exact source images across all actual source materials, including awaiting batch materials')
for material in d['materials']:
    check(len(material['tint'])==4 and len(material['emissionColor'])==4 and len(material['uvScale'])==len(material['uvOffset'])==3 and material['emissionOperator'] in [0,1],'Material typed field contract')
    if material['profile']=='alpine':check(material['cull']==2 and material['roughness']==1 and material['snowHeight']>0,'Actual alpine profile/snow-height parameter')
cylinders=[s for s in p['sourceShapeRecords'] if s['kind']=='CylinderShape3D'];check(len(cylinders)==11 and max(x['worldSagittaBound'] for x in cylinders)<.002,'Bounded faithful cylinder tessellation; under2mm radial sagitta, no capsule')
report=dict(passed=checks,diagnosticOnly=True,fullPublicationBlocked='1124 missing original MultiMesh poses',renderTriangles=581636,collisionTriangles=454466,boxes=1138,convexOwners=20,chunks=count,pngImages=36,engineRun=False,sourceModified=False)
(HERE/'checks.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
