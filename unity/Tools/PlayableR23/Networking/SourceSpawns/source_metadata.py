"""Lazy read of hash-bound R10 spawn metadata, never an engine/resource load."""
from pathlib import Path
import hashlib,importlib.util,json,struct,sys,collections
R=Path(__file__).resolve().parents[5]
sys.path.insert(0,str(R/'unity/Tools/PlayableR23/World/ArchitectureR6'))
from godot_resource import Resource,decode_rscc
sys.path.insert(0,str(R/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'))
from pack_reader import Pack
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
LIB=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
def sha(path):
 with Path(path).open('rb')as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(value):return struct.unpack('<f',struct.pack('<f',value))[0]
class MetadataResource(Resource):
 def __init__(self,raw):
  assert len(raw)<=256*1024*1024
  if raw[:4]==b'RSCC':self.b,self.provenance=decode_rscc(raw);self.at=0
  else:assert raw[:4]==b'RSRC';self.b=raw;self.at=4;self.provenance={}
  assert self.num()==0;self.num();major=self.num();minor=self.num();version=self.num();assert major==4 and version==6
  self.kind=self.string();self.num('Q');flags=self.num();self.num('Q');self.real='d'if flags&4 else'f'
  if flags&8:self.string()
  assert not any(self.num()for _ in range(11));self.strings=[self.string()for _ in range(self.num())]
  self.external=[(self.string(),self.string(),self.num('Q')if flags&2 else None)for _ in range(self.num())]
  self.table=[(self.string(),self.num('Q'))for _ in range(self.num())];assert self.table and all(self.at<=off<len(self.b)for _,off in self.table)
  self.cache={};self.types=[]
  for _,off in self.table:self.at=off;self.types.append(self.string())
 def read(self,index):
  assert isinstance(index,int)and 0<=index<len(self.table)
  if index not in self.cache:
   self.at=self.table[index][1];typ=self.string();props={}
   for _ in range(self.num()):
    key=self.key();assert key not in props;props[key]=self.value()
   assert self.at<=(self.table[index+1][1]if index+1<len(self.table)else len(self.b));self.cache[index]=dict(type=typ,props=props)
  return self.cache[index]
 def ref(self,value):
  assert isinstance(value,tuple)and value[:2]==('reference',2),value
  return self.read(value[2])
 def mesh_box(self,value):
  mesh=self.ref(value);assert mesh['type']=='ArrayMesh',mesh['type'];surfaces=mesh['props'].get('_surfaces',[]);assert surfaces
  boxes=[s['aabb']for s in surfaces];low=[min(b[i]for b in boxes)for i in range(3)];high=[max(f32(b[i]+b[i+3])for b in boxes)for i in range(3)]
  return low+[f32(high[i]-low[i])for i in range(3)]
 def scene_nodes(self,geometry):
  scene=self.ref(geometry);assert scene['type']=='PackedScene';b=scene['props']['_bundled'];ints=struct.unpack('<'+'i'*(len(b['nodes'])//4),b['nodes']);at=0;nodes=[]
  for _ in range(b['node_count']):
   parent,owner,typ,name,instance,count=ints[at:at+6];at+=6;props={}
   for _ in range(count):key,value=ints[at:at+2];at+=2;props[b['names'][key]]=b['variants'][value]
   groups=ints[at];at+=1;group_names=[b['names'][g]for g in ints[at:at+groups]];at+=groups
   nodes.append(dict(parent=parent,type=b['names'][typ]if typ>=0 else None,name=b['names'][name&0x3fffffff],props=props,groups=group_names))
  assert at==len(ints);return nodes
def merged_box(box,other):
 low=[min(box[i],other[i])for i in range(3)];high=[max(f32(box[i]+box[i+3]),f32(other[i]+other[i+3]))for i in range(3)]
 return low+[f32(high[i]-low[i])for i in range(3)]
def extract(row,pack):
 slug=row['id'][8:];original=LIB/slug/'converted/manifest.json';assert sha(original)==row['manifestSha256'];manifest=json.loads(original.read_text())
 assert manifest['map_id']==row['id']and manifest['source_sha256']==row['sourceSha256']and manifest['cache_fingerprint']==row['cacheFingerprint']
 prepared=pack.read('local_archive_r10/'+slug+'.res');assert hashlib.sha256(prepared).hexdigest()==row['adapterSha256']
 resource=MetadataResource(prepared);root=resource.read(len(resource.table)-1);assert root['props']['source_fingerprint']==row['cacheFingerprint'];details=root['props']['details']
 panels=[]
 for panel in details['panels']:
  box=panel['box'];assert len(box)==6
  for attachment in panel.get('attachments',[]):box=merged_box(box,resource.mesh_box(attachment['mesh']))
  grow=f32(.015);panels.append([f32(box[i]-grow)for i in range(3)]+[f32(box[i+3]+f32(grow*2))for i in range(3)])
 nodes=resource.scene_nodes(root['props']['geometry']);hazards=[]
 identity=[1.,0,0,0,1.,0,0,0,1.,0,0,0]
 for i,node in enumerate(nodes):
  if node['type']!='CollisionShape3D':continue
  parent=nodes[node['parent']];props=node['props'];shape_ref=props.get('shape')
  if parent['type']!='Area3D'or not(parent['props'].get('collision_layer',1)&32)or props.get('disabled',False)or shape_ref is None:continue
  cursor=i
  while cursor>=0 and cursor<len(nodes):
   p=nodes[cursor]['props'];assert p.get('transform',identity)==identity and p.get('position',[0,0,0])==[0,0,0] and p.get('rotation',[0,0,0])==[0,0,0] and p.get('scale',[1,1,1])==[1,1,1],(slug,nodes[cursor]['name'],'nonidentity hazard frame')
   next_index=nodes[cursor]['parent'];assert next_index!=cursor;cursor=next_index
  shape=resource.ref(shape_ref);assert shape['type']=='ConvexPolygonShape3D';raw=shape['props']['points'];width=struct.calcsize(resource.real);assert len(raw)%(width*3)==0
  points=list(struct.unpack('<'+resource.real*(len(raw)//width),raw));ys=points[1::3]
  # archive_map1003._safe_paths_r18 disables exactly these source ground ribbons.
  if row['id']=='archive:aftermath-light-vs-dark-gods-badbar'and min(ys)>=80 and max(ys)<=96:continue
  hazards.append(dict(sourceNode=parent['name']+'/'+node['name'],points=points))
 sem=json.loads((FROZEN/'assets/data/map_semantics_r14.json').read_text())['maps'].get(slug,{})
 if sem:assert sem['cache_fingerprint']==row['cacheFingerprint']and sem['source_sha256']==row['sourceSha256']
 def flat(values):return [float(x)for v in values for x in v]
 result=dict(version=1,mapId=row['id'],sourceSha256=row['sourceSha256'],manifestSha256=row['manifestSha256'],cacheFingerprint=row['cacheFingerprint'],adapterSha256=row['adapterSha256'],coordinates='original unreflected Godot geometry-local metres',center=manifest['center'],boundsMin=manifest['bounds_min'],boundsMax=manifest['bounds_max'],solidMin=manifest['solid_min'],solidMax=manifest['solid_max'],recorded=flat(manifest.get('recorded_spawns',[])),nativeMatch=flat(r['center']for r in sem.get('deathmatch',[])),nativeWarmup=flat(r['center']for r in sem.get('warmup',[])),spawnFloors=flat(details.get('spawn_floors_r9',[])),closedPanels=flat(panels),hazards=hazards)
 proof=dict(mapId=row['id'],manifest=str(original),manifestSha256=sha(original),adapterSha256=hashlib.sha256(prepared).hexdigest(),compressedBytes=len(prepared),decodedBytes=len(resource.b),resourceCount=len(resource.table),metadataResourcesRead=len(resource.cache),recorded=len(result['recorded'])//3,nativeMatch=len(result['nativeMatch'])//3,nativeWarmup=len(result['nativeWarmup'])//3,authoredFloors=len(result['spawnFloors'])//6,closedPanels=len(panels),hazardHulls=len(hazards),opaqueMeshBuffersRead=True,renderVertexAttributesInterpreted=False,renderGeometryRegenerated=False,engineExecuted=False)
 return result,proof
if __name__=='__main__':
 row=next(r for r in json.loads((R/'unity/Assets/StreamingAssets/PlayableR23/TeamLaunch/launch-maps.json').read_text())['rows']if r['id']=='archive:alfa-aglae-souza');pack=Pack(R/'builds/ASHFALL-Map-Adapters-R10.pck')
 try:data,proof=extract(row,pack);print(json.dumps(proof,indent=2))
 finally:pack.f.close()
