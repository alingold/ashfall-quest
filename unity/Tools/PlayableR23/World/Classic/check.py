"""Verify staged payload directly against immutable source accessors and collision."""
from pathlib import Path
import hashlib,json,mmap,re,struct
import numpy as np
from spawn import TriangleScene,capsule_clear,room
HERE=Path(__file__).resolve().parent;OUT=HERE/'StagedPayload'
report=json.loads((OUT/'provenance.json').read_text());manifest=json.loads((OUT/'map.json').read_text());plan=json.loads((HERE/'chunk-sources.json').read_text());proof=json.loads((HERE/'spawn-proof.json').read_text());checks=0
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for file,digest in report['sourceInputs'].items():assert sha(Path(file))==digest;checks+=1
for name,row in report['textureInputs'].items():assert sha(OUT/name)==row['sha256']==sha(Path(row['source']));checks+=1
assert sha(OUT/'geometry.bytes')==report['geometrySha256']
source=Path(next(p for p in report['sourceInputs']if p.replace('\\','/').endswith('source/map.glb')));sf=source.open('rb');raw=mmap.mmap(sf.fileno(),0,access=mmap.ACCESS_READ);n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);offset=28+n
def acc(index):
    a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
    x=np.ndarray((a['count'],width),dt,buffer=raw,offset=offset+v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',width*dt.itemsize),dt.itemsize))
    return np.maximum(-1,x.astype(np.float32)/np.iinfo(dt).max)if a.get('normalized')else x
assert all(not any(k in node for k in ['matrix','translation','rotation','scale','children'])for node in doc['nodes']),'Classic check assumes its genuine flat identity-node source'
f=(OUT/'geometry.bytes').open('rb');binary=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ);magic,chunks=struct.unpack_from('<ii',binary);assert magic==0x52323357 and chunks==manifest['chunks']==len(plan)==280
cursor=8;triangles=0;vertices=0;seen=set();parts=[];lo=np.full(3,np.inf);hi=np.full(3,-np.inf)
for ci,chunk in enumerate(plan):
    material,collision,visible,nv,ni=struct.unpack_from('<iiiii',binary,cursor);cursor+=20;assert material==chunk['material']and collision==visible==1 and nv<=60000 and ni%3==0
    v=np.ndarray((nv,12),'<f4',buffer=binary,offset=cursor);cursor+=nv*48;idx=np.ndarray((ni,),'<i4',buffer=binary,offset=cursor);cursor+=ni*4
    assert np.isfinite(v).all()and idx.min()>=0 and idx.max()<nv;checks+=1
    vo=io=0
    for part in chunk['parts']:
        node=doc['nodes'][part['nodeIndex']];primitive=doc['meshes'][node['mesh']]['primitives'][part['primitiveIndex']];attrs=primitive['attributes'];count=part['vertices'];indices=part['indices'];actual=v[vo:vo+count]
        identity=(part['nodeIndex'],part['primitiveIndex']);assert identity not in seen;seen.add(identity)
        source_indices=acc(primitive['indices']).ravel();assert np.array_equal(idx[io:io+indices]-vo,source_indices);checks+=1
        position=acc(attrs['POSITION']).astype(np.float32).copy();position[:,2]*=-1;assert np.array_equal(position,actual[:,:3]);checks+=1
        uv=acc(attrs['TEXCOORD_0']).astype(np.float32).copy()if'TEXCOORD_0'in attrs else np.zeros((count,2),np.float32);uv[:,1]=1-uv[:,1];assert np.array_equal(uv,actual[:,6:8]);checks+=1
        color=acc(attrs['COLOR_0']).astype(np.float32)if'COLOR_0'in attrs else np.ones((count,4),np.float32)
        if color.shape[1]==3:color=np.column_stack((color,np.ones(count,np.float32)))
        assert np.array_equal(color,actual[:,8:12]);checks+=1
        normal=acc(attrs['NORMAL']).astype(np.float32).copy();normal[:,2]*=-1;lengths=np.linalg.norm(normal,axis=1);good=lengths>1e-10;normal[good]/=lengths[good,None]
        assert np.allclose(normal[good],actual[good,3:6],atol=1e-6);checks+=1
        key=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_(.+)',node['name'])[4];assert manifest['materials'][material]['name']==key;checks+=1
        vo+=count;io+=indices
    assert vo==nv and io==ni;triangles+=ni//3;vertices+=nv;lo=np.minimum(lo,v[:,:3].min(axis=0));hi=np.maximum(hi,v[:,:3].max(axis=0));parts.append(v[idx,:3].reshape(-1,3,3).copy())
assert cursor==len(binary)and len(seen)==len(doc['meshes'])==1946 and triangles==1319789 and vertices==1685957
assert np.allclose(lo,manifest['boundsMin'],atol=1e-4)and np.allclose(hi,manifest['boundsMax'],atol=1e-4)
scene=TriangleScene(parts)
for name,label in [('gameSpawn','game'),('warmupSpawn','warmup')]:
    p=np.array(manifest[name],np.float32).astype(float);hit=scene.floor(p[0],p[2],p[1]+.6,1)
    assert hit is not None and abs(hit[0][1]+.12-p[1])<.0001 and hit[1][1]>.7;checks+=1
    verify=room(scene,hit[0]);assert verify is not None;checks+=1
    assert np.allclose(p,proof[label]['spawn'],atol=1e-4)
    # Also require a conservatively tall capsule at the published root's offset.
    assert capsule_clear(scene,p+[0,-.02,0],height=1.95)[0];checks+=1
assert manifest['spawn']==manifest['gameSpawn'] and 'recordedSpawn'not in manifest
assert abs(manifest['cameraFarDistance']-report['sourceCameraFarDistance'])<.001
assert len(manifest['materials'])==51 and len(report['textureInputs'])==96 and not report['missingTextures']
assert sum(bool(r['alpha'])for r in manifest['materials'])==3
assert sum(p.stat().st_size for p in OUT.iterdir()if p.is_file())<=300*1024*1024
# Exact collision fixtures exercise the source capsule distance rather than a box proxy.
floor=np.array([[[-5,0,-5],[5,0,-5],[0,0,5]]],np.float32);origin=np.array([0,0,0.])
assert capsule_clear(TriangleScene([floor]),origin)[0];checks+=1
for axis,coordinate in [(0,.2),(1,1.9)]:
    wall=np.array([[[coordinate,-1,-1],[coordinate,3,-1],[coordinate,1,1]]],np.float32)
    if axis==1:wall=np.array([[[-1,coordinate,-1],[1,coordinate,-1],[0,coordinate,1]]],np.float32)
    assert not capsule_clear(TriangleScene([floor,wall]),origin)[0];checks+=1
result=dict(passed=True,checks=checks,sourceNodesVerified=len(seen),verticesVerified=vertices,sourceAndPayloadTriangles=triangles,textureBytesUnchanged=96,chunks=chunks,payloadBytes=sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()),gameSpawn=manifest['gameSpawn'],warmupSpawn=manifest['warmupSpawn'],engineRun=False,deviceRun=False)
(HERE/'check-results.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
