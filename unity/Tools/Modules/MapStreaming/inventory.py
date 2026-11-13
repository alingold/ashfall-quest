"""Read-only full GLB inventory and independent NumPy geometry oracle. No payload exports."""
import json,struct,hashlib,copy
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/MapStreaming/phase03'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/MapStreaming/phase03'
REV='bdba6e30012e1733efd5021fd8ded593f82f26e6'
def digest(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def write(p,d):p.write_text(json.dumps(d,separators=(',',':'),allow_nan=False),encoding='utf-8')
def generate(path,mapid,expected,policy):
 before=digest(path)
 assert before==expected
 with path.open('rb') as f:
  magic,version,length,n,kind=struct.unpack('<IIIII',f.read(20));assert magic==0x46546c67 and version==2 and length==path.stat().st_size and kind==0x4e4f534a
  raw=f.read(n);doc=json.loads(raw);bl,kind=struct.unpack('<II',f.read(8));base=f.tell();assert kind==0x004e4942 and base+bl==length
 binary=np.memmap(path,dtype='u1',mode='r',offset=base,shape=(bl,))
 def acc(i):
  a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']];assert 'sparse' not in a
  dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);c={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
  x=np.ndarray((a['count'],c),dt,buffer=binary,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',c*dt.itemsize),dt.itemsize)).astype('float64')
  if a.get('normalized'):x=np.maximum(-1,x/np.iinfo(dt).max)
  assert np.isfinite(x).all()
  return x
 def local(node):
  if 'matrix'in node:return np.array(node['matrix'],float).reshape((4,4),order='F')
  x,y,z,w=node.get('rotation',[0,0,0,1]);m=np.eye(4);m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(node.get('scale',[1,1,1]));m[:3,3]=node.get('translation',[0,0,0]);return m
 instances=[];oracle=[];seen=set()
 def walk(i,parent,anc):
  assert i not in anc and i not in seen;seen.add(i);node=doc['nodes'][i];m=parent@local(node)
  if 'mesh'in node:
   for j,p in enumerate(doc['meshes'][node['mesh']]['primitives']):
    sid=f'node/{i}/mesh/{node["mesh"]}/primitive/{j}';assert p.get('mode',4)==4
    attrs=p['attributes'];pos=acc(attrs['POSITION']);norm=acc(attrs['NORMAL']) if 'NORMAL'in attrs else None;uv=acc(attrs['TEXCOORD_0']) if 'TEXCOORD_0'in attrs else None
    idx=acc(p['indices']).ravel().astype('int64') if 'indices'in p else np.arange(len(pos));assert len(idx)%3==0 and idx.min()>=0 and idx.max()<len(pos)
    if np.linalg.det(m[:3,:3])>0:idx=idx.reshape(-1,3)[:,[0,2,1]].ravel()
    xyz=(pos[idx]@m[:3,:3].T)+m[:3,3];xyz[:,2]*=-1
    normals=None;zero=0
    if norm is not None:
     normals=norm[idx]@np.linalg.inv(m[:3,:3]);lens=np.linalg.norm(normals,axis=1);zero=int((lens==0).sum());normals/=np.where(lens==0,1,lens)[:,None];normals[:,2]*=-1
    # Ordered weighted moments catch winding and correspondence, independent of batch partition.
    weights=(np.arange(len(idx))%3+1)[:,None]
    sums=lambda a:None if a is None else (a*weights).sum(axis=0).tolist()
    oracle.append(dict(id=sid,triangles=len(idx)//3,positions=sums(xyz),normals=sums(normals),uv=sums(None if uv is None else uv[idx]),zeroNormals=zero))
    instances.append(dict(id=sid,node=i,mesh=node['mesh'],primitive=j,matrix=m.reshape(-1,order='F').tolist(),attributes=attrs,indices=p.get('indices',-1),material=p.get('material',-1),collisionRole='UNKNOWN'))
  for c in node.get('children',[]):walk(c,m,anc|{i})
 for i in doc['scenes'][doc.get('scene',0)]['nodes']:walk(i,np.eye(4),set())
 assert len(seen)==len(doc['nodes'])
 refs={k:doc.get(k,[]) for k in ['nodes','scenes','meshes','accessors','bufferViews','buffers','materials','textures','images','samplers']}
 ranges=[]
 for i,a in enumerate(doc['accessors']):
  v=doc['bufferViews'][a['bufferView']];ranges.append(dict(id=i,offset=base+v.get('byteOffset',0)+a.get('byteOffset',0),viewOffset=base+v.get('byteOffset',0),viewLength=v['byteLength'],componentType=a['componentType'],type=a['type'],count=a['count'],normalized=a.get('normalized',False),byteStride=v.get('byteStride',0)))
 d=dict(format='ASHFALL-MAPREF/1',schema=1,mapId=mapid,source=dict(path=str(path.resolve()),sha256=before,revision=REV,approvalDisposition='pilot-quarantine',length=length,jsonSha256=hashlib.sha256(raw).hexdigest(),binOffset=base,binLength=bl),policy=policy,instances=instances,accessorRanges=ranges,references=refs)
 assert digest(path)==before
 slug=mapid.split(':')[-1];write(OUT/(slug+'.mapref.json'),d);write(OUT/(slug+'.oracle.json'),oracle)
 return dict(mapId=mapid,source=d['source'],nodes=len(seen),instances=len(instances),accessors=len(ranges),triangles=sum(x['triangles'] for x in oracle),zeroNormalCorners=sum(x['zeroNormals'] for x in oracle),maprefBytes=(OUT/(slug+'.mapref.json')).stat().st_size)
def main():
 semantics=ROOT/'quest-r22/source/assets/data/map_semantics_r14.json';pol=json.loads(semantics.read_text())['maps'];receipts=[]
 for row in json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/World/conversion.json').read_text(encoding='utf-8-sig')):
  p=Path(row['sourcePath']);slug=p.parents[1].name;v=pol[slug];indoor='paintball'in slug
  receipts.append(generate(p,'archive:'+slug,row['sourceSha256'],dict(indoor=indoor,capsules=False if indoor else 'UNRESOLVED',spawnValidation='UNKNOWN',nativeDeathmatchMarkers=v.get('deathmatch',[]),nativeWarmupMarkers=v.get('warmup',[]),deathmatchMissing=not bool(v.get('deathmatch')),semanticSourceSha256=digest(semantics),collisionRole='UNKNOWN',playableApproval=False)))
 write(DOC/'inventory-results.json',receipts);print(json.dumps(receipts,indent=2))
if __name__=='__main__':main()
