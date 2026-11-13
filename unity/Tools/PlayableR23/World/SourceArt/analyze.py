"""Read-only source stage replay; no engine, no original map/payload mutations."""
from pathlib import Path
import collections,hashlib,importlib.util,json,math,mmap,re,struct,sys
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');SRC=FROZEN/'source'
assert hashlib.sha256((FROZEN/'SOURCE-MANIFEST.json').read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
PIN={r['path']:r['sha256']for r in json.loads((FROZEN/'SOURCE-MANIFEST.json').read_text())['files']}
LIBRARY=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
PLAN=Path('D:/ashfall-r6-staging/world/native/parity-source-art-plans-v001/result.json')
PLANS=json.loads(PLAN.read_text())['maps']
FAMILY_PLAN=Path('D:/ashfall-r6-staging/world/native/parity-source-art-family7-v001/result.json')
assert hashlib.sha256(FAMILY_PLAN.read_bytes()).hexdigest()=='ce1bab0e154817cb9e3beed2f9f249dad9e99ad09578bd85d3ba45fb3b955967'
PLANS.update(json.loads(FAMILY_PLAN.read_text())['maps'])
sys.path.insert(0,str(ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6'));from godot_resource import Resource
sys.path.insert(0,str(ROOT/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'));from pack_reader import Pack
spec=importlib.util.spec_from_file_location('source_components',ROOT/'unity/Tools/PlayableR23/WorldLoot/prepare.py');helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
def sha(p):return helper.sha(p)
def source(path):
 p=SRC/path;assert sha(p)==PIN[path],path;return p
def batches(slug,key='vc'):
 folder=LIBRARY/slug;receipt=json.loads((folder/'source/geometry.json').read_text());gpath=folder/'source/geometry.glb';assert sha(gpath)==receipt['sha256']
 sem=json.loads(source('assets/data/map_semantics_r14.json').read_text())['maps'][slug];assert receipt['source']==sem['source_sha256']
 result=collections.OrderedDict()
 with gpath.open('rb')as f,mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ)as raw:
  length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);base=28+length
  def acc(index):
   a=g['accessors'][index];b=g['bufferViews'][a['bufferView']];dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];v=np.ndarray((a['count'],width),dt,raw,base+b.get('byteOffset',0)+a.get('byteOffset',0),(b.get('byteStride',width*dt.itemsize),dt.itemsize)).copy()
   if a.get('normalized'):v=np.maximum(-1,v.astype(np.float32)/np.iinfo(dt).max)
   return v
  for node in g['nodes']:
   m=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_'+key,node.get('name',''))
   if not m:continue
   assert not any(x in node for x in ['children','matrix','translation','rotation','scale'])
   kind,cx,cz=m.groups();name=f'Draw_{kind}_{key}_{int(cx)//4}_{int(cz)//4}'
   for prim in g['meshes'][node['mesh']]['primitives']:
    p=acc(prim['attributes']['POSITION']).astype('<f4');ix=acc(prim['indices']).astype(int).ravel()if'indices'in prim else np.arange(len(p));ix=ix.reshape(-1,3)[:,[0,2,1]]
    c=acc(prim['attributes']['COLOR_0'])if'COLOR_0'in prim['attributes']else np.ones((len(p),4));c=c[ix];
    if c.shape[-1]==3:c=np.concatenate((c,np.ones((*c.shape[:-1],1))),axis=-1)
    result.setdefault(name,[]).append((p[ix],c))
 return collections.OrderedDict((k,dict(faces=np.concatenate([x[0]for x in v]),colors=np.concatenate([x[1]for x in v]).astype('<f4')))for k,v in result.items())
def project(v,x,z):
 p=np.column_stack((v@x,v[:,1],v@z)).astype(np.float32);low=p.min(0);size=p.max(0)-low;return (p-low)/np.maximum(size,np.float32(.0001)),size
def container(faces):
 v=faces.reshape(-1,3);best=None;area=math.inf
 for tri in faces:
  for j in range(3):
   e=tri[j]-tri[(j+1)%3];length=np.linalg.norm(e)
   if abs(e[1])>.01 or length<1:continue
   x=e/length;p,size=project(v,x,np.cross(np.array([0,1,0],np.float32),x))
   if size[0]*size[2]<area:area=size[0]*size[2];best=(p,size)
 if best is None:return None
 p,size=best
 if size[0]<size[2]:p=p[:,[2,1,0]];size=size[[2,1,0]]
 return p if np.linalg.norm(size-np.array([12.2007,2.9986,2.6332]))<=.075 else None
def axes(faces):
 v=faces.reshape(-1,3);mean=np.zeros(3,np.float32)
 for p in v:mean+=p
 mean/=len(v);d=v-mean;xx=sum(float(x*x)for x in d[:,0]);zz=sum(float(z*z)for z in d[:,2]);xz=sum(float(x*z)for x,z in d[:,[0,2]]);angle=.5*math.atan2(2*xz,xx-zz);x=np.array([math.cos(angle),0,math.sin(angle)],np.float32)
 return project(v,x,np.cross(np.array([0,1,0],np.float32),x))
def mesh_faces(faces):
 # Mesh.get_faces -> TriangleMesh::create snaps with a float32 step,
 # Math.snapped(double,double), then returns float32 Vector3 components.
 step=float(np.float32(.0001));return (np.floor(faces.astype(np.float64)/step+.5)*step).astype('<f4')
def face_digest(faces):return hashlib.sha256(mesh_faces(faces).tobytes()).hexdigest()
def prepared(slug):
 pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');raw=pack.read('local_archive_r10/'+slug+'.res');pack.f.close();expected=json.loads(source('assets/data/archive_adapters_r10.json').read_text())['files'][slug];assert hashlib.sha256(raw).hexdigest()==expected
 resource=Resource(raw);result={};nodes=resource.nodes()
 # Godot renames repeated SurfaceTool outputs to @MeshInstance3D@N. Their
 # names are not classification authority: the actual shared material is.
 tire_refs={n['props']['material_override']for n in nodes if n['name']=='RecoveredDetailR10_tires'}
 def art_kind(node):
  ref=node['props'].get('material_override')
  if ref in tire_refs:return 'tire_r10'
  if not ref or ref[1]!=2:return None
  material=resource.resources[ref[2]]
  if material['type']!='ShaderMaterial':return None
  shader=material['props'].get('shader')
  path=resource.external[shader[2]][1]if shader and shader[1]==3 else None
  return {'res://assets/materials/archive_container_r10.gdshader':'container_r10','res://assets/materials/archive_van_r10.gdshader':'van_r10'}.get(path)
 for node in nodes:
  kind=art_kind(node)
  if'mesh'not in node['props']or not(node['name'].startswith('Draw_')or kind):continue
  transform=node['props'].get('transform',[1,0,0,0,1,0,0,0,1,0,0,0]);assert transform==[1,0,0,0,1,0,0,0,1,0,0,0],(slug,node['name'],'nonidentity prepared transform')
  ref=node['props']['mesh'];mesh=resource.resources[ref[2]]
  if'_surfaces'not in mesh['props']:continue
  surfaces=mesh['props']['_surfaces'];assert len(surfaces)==1;s=surfaces[0];n=s['vertex_count'];fmt=s['format'];v=np.frombuffer(s['vertex_data'],'<f4',n*3).reshape(n,3).copy();assert len(s['vertex_data'])==n*20
  normals=np.frombuffer(s['vertex_data'][n*12:],'<u2').reshape(n,4)[:,:2].astype(np.float32)/65535*2-1;normals=np.column_stack((normals,1-np.abs(normals).sum(-1)));t=np.maximum(-normals[:,2],0);normals[:,:2]+=np.where(normals[:,:2]>=0,-t[:,None],t[:,None]);normals/=np.linalg.norm(normals,axis=1)[:,None]
  ix=np.frombuffer(s['index_data'],'<u2'if n<65536 else'<i4').astype(np.int32)if'index_data'in s else np.arange(n,dtype=np.int32)
  attr=s.get('attribute_data',b'');stride=len(attr)//n;colors=np.ndarray((n,4),'u1',attr,strides=(stride,1)).astype(np.float32)/255 if fmt&8 else np.ones((n,4),np.float32);offset=4 if fmt&8 else 0
  uv=np.ndarray((n,2),'<f4',attr,offset=offset,strides=(stride,4)).copy()if fmt&16 else np.zeros((n,2),np.float32);offset+=8 if fmt&16 else 0
  uv2=np.ndarray((n,2),'<f4',attr,offset=offset,strides=(stride,4)).copy()if fmt&32 else np.zeros((n,2),np.float32)
  result[node['name']]=dict(vertices=v,normals=normals,colors=colors,uv=np.c_[uv,uv2[:,0]],indices=ix,faces=v[ix].reshape(-1,3,3),artKind=kind,materialReference=node['props'].get('material_override'))
 return result
_reference=None
def reference():
 global _reference
 if _reference is not None:return _reference
 for row in batches('alfa-aglae-souza').values():
  for p in helper.red_components(row['faces']):
   if p['triangles']==98 and np.linalg.norm(p['center']-np.array([-41.716,21.82875,11.37975]))<.02:_reference=axes(row['faces'][p['ids']])[0];return _reference
 raise ValueError('Actual ALFA reference van absent')
def van(faces):
 p,size=axes(faces)
 if size[1]<.8 or size[1]>4 or size[0]<2 or size[0]>12:return None
 ref=reference()
 for sx in [False,True]:
  for sz in [False,True]:
   q=p.copy()
   if sx:q[:,0]=1-q[:,0]
   if sz:q[:,2]=1-q[:,2]
   if np.all(np.min(((q[:,None,:]-ref[None,:,:])**2).sum(-1),axis=1)<=.0001):return q
 return None
def r10(slug):
 raw=batches(slug);out={};details=[];counts=collections.Counter()
 for name,row in raw.items():
  f=row['faces'];groups=helper.red_components(f);erase=set();van_boxes=[];selected=[]
  for part in groups:
   if part['triangles']not in[12,98]or part['size'][1]<.8 or part['size'][1]>4.1:continue
   ids=part['ids'];uv=container(f[ids])if part['triangles']==12 else van(f[ids]);kind='container_r10'if part['triangles']==12 else'van_r10'
   if uv is not None:
    erase.update(ids);details.append(dict(node=name,kind=kind,ids=ids,uv=uv));counts[kind]+=1
    if kind=='van_r10':van_boxes.append((part['low'],part['high']))
  for part in groups:
   if part['triangles']!=25 or part['size'][1]>.95:continue
   if any(np.all(part['center']>=low-.65)and np.all(part['center']<=high+.65)and part['high'][1]<low[1]+.55 for low,high in van_boxes):erase.update(part['ids']);details.append(dict(node=name,kind='tire_r10',ids=part['ids'],uv=np.zeros((len(part['ids'])*3,3),np.float32)));counts['tire_r10']+=1
  keep=np.array([i for i in range(len(f))if i not in erase]);out[name]=dict(faces=f[keep],colors=row['colors'][keep],original=keep)
 return raw,out,details,counts
def main():
 audit=json.loads(source('assets/data/map_art_audit_r14.json').read_text());r14={r['id']:r.get('repair_selectors',[])for r in audit['maps']}
 for slug in ['classic-bigbox-vr','alfa-aglae-souza']:
  stage=prepared(slug);print(slug,'prepared',len(stage))
  for plan in r14['archive:'+slug]:
   name=plan['node'];row=stage[name];assert len(row['vertices'])==plan['vertex_count']and len(row['indices'])==plan['index_count'];erase={int(t)for p in plan['parts']for t in p['triangles']};keep=np.array([i for i in range(len(row['faces']))if i not in erase]);row['faces']=row['faces'][keep]
  for name,plan in PLANS[slug]['r17']['plans'].items():
   row=stage[name];digest=face_digest(row['faces']);assert digest==plan['sha256'],name;print('r17',name,len(row['faces']),True)
if __name__=='__main__':main()
