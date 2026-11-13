"""Transfer released R6 material classifications onto original Unity map vertices.

No geometry or collision file is rewritten. Ambiguous coincident surfaces are
left unchanged and counted, rather than painting a neighbouring source face.
"""
from pathlib import Path
import argparse,ast,base64,collections,hashlib,json,struct,sys,shutil,itertools,time,uuid
import numpy as np
from godot_resource import Resource
ROOT=Path(__file__).resolve().parents[5]
SOURCE=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
WORLD=ROOT/'campaign-staging/20261104-three-chats/world'
sys.path.insert(0,str(WORLD/'r12-fixture-shard-1'))
from pack_reader import Pack
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'
PINS={'assets/data/architecture_r24.json':'1cb3f22b3267a0d825eadbd3cc2b132f17740fdd0061a74ed5ff44e6e133644d',
      'scripts/architecture_r24.gd':'3622e23809ea9563ea71c83089fee40245e943c0403019bb6ee91af0c6ceaded',
      'assets/materials/architecture_family_r6.gdshader':'fe897496270fbfeccc88ef918eaacaa1a4568573c380b57a4dc0eab11c5162bb'}
EXISTING={'alfa-aglae-souza','classic-bigbox-vr'}

def export_folders():
 # Execute ONLY WORLD's pure published exports() mapping, never its main or
 # module setup that creates shared staging directories/mutable source data.
 path=Path('D:/ashfall-r6-staging/world/propagate_architecture.py');tree=ast.parse(path.read_text());function=next(n for n in tree.body if isinstance(n,ast.FunctionDef)and n.name=='exports');ns={'WORLD':WORLD};exec(compile(ast.Module(body=[function],type_ignores=[]),str(path),'exec'),ns)
 result={}
 for folder,work in ns['exports']():
  metadata=json.loads((folder/'result.json').read_text());slug=metadata['map'].removeprefix('archive:');assert slug not in result;result[slug]=(folder,work)
 assert len(result)==9
 return result

def source_export(slug,plan):
 folders=export_folders()[slug];error=[]
 for folder in folders:
  path=folder/'result.json'
  if not path.exists():continue
  data=json.loads(path.read_text())
  if data.get('map')!='archive:'+slug:continue
  meshes={m['path']:m for m in data['meshes']if m.get('channels')};okay=True
  for row in plan['meshes']:
   mesh=meshes.get(row['path'])
   if mesh is None or mesh['transform']!=row['transform']:okay=False;break
   for field in('vertices','normals','colors','indices'):
    channel=folder/mesh['channels'].get(field,'missing')
    if not channel.is_file()or sha(channel)!=row['hashes'][field]:okay=False;error.append((str(folder),row['path'],field));break
   if not okay:break
  if okay:return folder,meshes
 raise AssertionError(('Exact immutable R6 channel binding unavailable',slug,error))
def sha(p):
 with Path(p).open('rb')as f:return hashlib.file_digest(f,'sha256').hexdigest()
def normals(surface):
 n=surface['vertex_count'];v=surface['vertex_data'];q=np.frombuffer(v[n*12:],'<u2').reshape(n,-1)[:,:2].astype(np.float32)/65535*2-1
 v=np.column_stack((q,1-np.abs(q).sum(axis=1)));t=np.maximum(-v[:,2],0)
 v[:,0]+=np.where(v[:,0]>=0,-t,t);v[:,1]+=np.where(v[:,1]>=0,-t,t)
 v/=np.maximum(np.linalg.norm(v,axis=1)[:,None],1e-20);return v
def load_points(slug,plan,build_lookup=True):
 pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');raw=pack.read('local_archive_r10/'+slug+'.res');pack.f.close()
 expected=json.loads((SOURCE/'assets/data/archive_adapters_r10.json').read_text())['files'][slug]
 assert hashlib.sha256(raw).hexdigest()==expected,'Prepared source map changed'
 resource=Resource(raw);nodes={n['name']:n for n in resource.nodes()};points={};selected=collections.Counter();source_materials={}
 # These are actual Main exports after source R14/R17 repair stages. The old
 # packed adapter alone is insufficient: repairs compact several mesh arrays.
 folder,meshes=source_export(slug,plan)
 for row in plan['meshes']:
  node=nodes[row['path']];mesh=meshes[row['path']];channels=mesh['channels'];n=row['vertices']
  for field in ('vertices','normals','colors','indices'):assert sha(folder/channels[field])==row['hashes'][field],(row['path'],field)
  assert mesh['transform']==row['transform']
  pos=np.fromfile(folder/channels['vertices'],'<f4').reshape(n,3);normal=np.fromfile(folder/channels['normals'],'<f4').reshape(n,3)
  flag=np.frombuffer(base64.b64decode(row['flags']),np.uint8);assert len(flag)==n
  color=np.rint(np.fromfile(folder/channels['colors'],'<f4').reshape(n,4)[:,:3]*255).astype(np.int32)
  # Prepared Godot positions have millimetre encoding differences from the
  # original GLB. Bucket aliases cover boundaries; actual distance, original
  # vertex colour and the face normal still decide each correspondence.
  lo=np.rint((pos-.0025)*100).astype(np.int32);hi=np.rint((pos+.0025)*100).astype(np.int32)
  for i in range(n)if build_lookup else []:
   value=(pos[i].copy(),normal[i].copy(),int(flag[i]));rgb=tuple(color[i].tolist())
   for bucket in itertools.product(*(range(int(lo[i,j]),int(hi[i,j])+1)for j in range(3))):
    points.setdefault((row['material'],bucket+rgb),[]).append(value)
  selected.update(int(v)for v in flag if v!=255)
  ref=node['props']['material_override'];source_materials[row['material']]=resource.external[ref[2]][1]
 return points,selected,source_materials
def material_properties(paths):
 pack=Pack(ROOT/'builds/ASHFALL-Local-Maps.pck');result=[]
 for key,path in paths.items():
  res=Resource(pack.read(path.removeprefix('res://')))
  props=next(r['props']for r in res.resources if r['type']=='StandardMaterial3D')
  result.append(dict(key=key,tint=props.get('albedo_color',[1,1,1,1]),roughness=props.get('roughness',1),vertexSrgb=bool(props.get('vertex_color_is_srgb',False))))
 pack.f.close();return result
def source_faces(slug,plan):
 folder,meshes=source_export(slug,plan);lookup={};count=0;surfaces=[]
 for row in plan['meshes']:
  ch=meshes[row['path']]['channels'];n=row['vertices'];flags=np.frombuffer(base64.b64decode(row['flags']),np.uint8)
  for key in ('vertices','normals','colors','indices'):assert sha(folder/ch[key])==row['hashes'][key]
  positions=np.fromfile(folder/ch['vertices'],'<f4').reshape(n,3).copy();positions[:,2]*=-1
  normals=np.fromfile(folder/ch['normals'],'<f4').reshape(n,3).copy();normals[:,2]*=-1
  colors=np.fromfile(folder/ch['colors'],'<f4').reshape(n,4)[:,:3];ix=np.fromfile(folder/ch['indices'],'<i4').reshape(-1,3)
  # Godot GL uses the LAST source corner. Reverse winding only after reading
  # that value, then retain the value on the entire matched Unity draw face.
  kinds=flags[ix[:,2]];selected=np.flatnonzero(kinds!=255)
  order=ix[:,[0,2,1]];surfaces.append(dict(node=row['path'],material=row['material'],positions=positions[order],normals=normals[order],colors=colors[order],kinds=kinds))
  for ti in selected:
   at=ix[ti,[0,2,1]];p=positions[at];center=p.mean(axis=0);lo=np.floor((center-.005)/.05).astype(np.int32);hi=np.floor((center+.005)/.05).astype(np.int32)
   value=(p,normals[at],colors[at],int(kinds[ti]),row['path'],int(ti))
   for bucket in itertools.product(*(range(int(lo[j]),int(hi[j])+1)for j in range(3))):lookup.setdefault((row['material'],bucket),[]).append(value)
  count+=len(selected)
 return lookup,count,surfaces

def polygon_area(poly):
 if len(poly)<3:return 0.
 p=np.asarray(poly,dtype=np.float64);return abs(float(np.sum(p[:,0]*np.roll(p[:,1],-1)-p[:,1]*np.roll(p[:,0],-1))*.5))

def clip_half(poly,a,b,inside=True,inset=0.):
 if len(poly)<3:return []
 edge=b-a;length=float(np.linalg.norm(edge));out=[];previous=np.asarray(poly[-1],np.float64)
 def distance(p):return (edge[0]*(p[1]-a[1])-edge[1]*(p[0]-a[0]))/max(length,1e-30)-inset
 pd=distance(previous);ps=pd>=0 if inside else pd<=0
 for current in poly:
  current=np.asarray(current,np.float64);cd=distance(current);cs=cd>=0 if inside else cd<=0
  if ps!=cs:
   denominator=pd-cd
   if abs(denominator)>1e-30:out.append(previous+(current-previous)*(pd/denominator))
  if cs:out.append(current)
  previous=current;pd=cd;ps=cs
 return out

def ccw(poly):
 p=np.asarray(poly,np.float64)
 if len(p)<3:return p
 a=p[1]-p[0];b=p[2]-p[0];cross=a[0]*b[1]-a[1]*b[0];return p if cross>=0 else p[::-1]

def subtract_convex(subject,cover):
 # The outside pieces are mutually disjoint. Only the final intersection is
 # removed, so repeated/overlapping covers cannot inflate union coverage.
 remaining=subject;pieces=[];cover=ccw(cover)
 for i in range(len(cover)):
  a,b=cover[i],cover[(i+1)%len(cover)];outside=clip_half(remaining,a,b,False)
  if polygon_area(outside)>1e-11:pieces.append(outside)
  remaining=clip_half(remaining,a,b,True)
  if polygon_area(remaining)<=1e-11:break
 return pieces

def uncovered(target,covers):
 pieces=[ccw(target)]
 for cover in covers:
  next_pieces=[]
  for piece in pieces:next_pieces.extend(subtract_convex(piece,cover))
  if len(next_pieces)>256:return None # bounded exact polygon work, never a guessed fill
  pieces=next_pieces
  if not pieces:break
 return sum(polygon_area(p)for p in pieces)

def overlap_area(target,cover):
 poly=ccw(target);cover=ccw(cover)
 for i in range(len(cover)):poly=clip_half(poly,cover[i],cover[(i+1)%len(cover)],True)
 return polygon_area(poly)

class Footprints:
 CELL=16.;PLANE=.005;CORE_INSET=.0075;NORMAL=.9995;GEOMETRIC_NORMAL=.9999
 def __init__(self,surfaces):
  self.surfaces=surfaces;self.grid=collections.defaultdict(list);self.components=[];self.rejected_large=0
  selected_colors={(s['material'],*np.rint(c*255).astype(int).tolist())for s in surfaces for c in s['colors'][s['kinds']!=255,0]};self.selected_colors=selected_colors
  for si,s in enumerate(surfaces):
   p=s['positions'];rgb=np.rint(s['colors'][:,0]*255).astype(np.int32);geom=np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0]);length=np.linalg.norm(geom,axis=1);geom/=np.maximum(length[:,None],1e-20);mean=s['normals'].mean(1);geom*=np.where(np.sum(geom*mean,axis=1)<0,-1,1)[:,None];s['geometric']=geom
   eligible=(np.max(abs(s['colors']-s['colors'][:,0:1]),axis=(1,2))<.005)&(length>1e-9);parents=np.arange(len(p),dtype=np.int32);vertices={}
   def root(i):
    while parents[i]!=i:parents[i]=parents[parents[i]];i=int(parents[i])
    return i
   for ti in np.flatnonzero(eligible):
    tag=(int(s['kinds'][ti]),*rgb[ti].tolist(),*np.rint(geom[ti]*100).astype(int).tolist())
    # Source connected-component contract: shared welded positions at1mm.
    q=p[ti].astype(float)*1000;q=np.where(q>=0,np.floor(q+.5),np.ceil(q-.5)).astype(np.int64)
    for point in q:
     key=tag+tuple(point);old=vertices.get(key)
     if old is None:vertices[key]=int(ti)
     else:parents[root(int(ti))]=root(old)
   comp=np.arange(len(p),dtype=np.int32)
   for ti in np.flatnonzero(eligible):comp[ti]=root(int(ti))
   self.components.append(comp)
   s['low']=p.min(1);s['high']=p.max(1);low=np.floor((s['low']-self.PLANE)/self.CELL).astype(int);high=np.floor((s['high']+self.PLANE)/self.CELL).astype(int);dominant=np.argmax(abs(geom),axis=1)
   for ti in np.flatnonzero(eligible):
    if np.prod(high[ti]-low[ti]+1)>512:self.rejected_large+=1;continue
    tag=(s['material'],*rgb[ti].tolist())
    if tag not in selected_colors:continue
    axis=int(dominant[ti]);sign=1 if geom[ti,axis]>=0 else-1
    for cell in itertools.product(*(range(low[ti,j],high[ti,j]+1)for j in range(3))):self.grid[tag+(axis,sign)+cell].append((si,int(ti)))
 def collect(self,material,p,normal,color):
  p=np.asarray(p,float);normal=np.asarray(normal,float);color=np.asarray(color,float)
  if np.max(abs(color-color[0]))>=.005:return None
  tag=(material,*np.rint(color[0]*255).astype(int).tolist())
  if tag not in self.selected_colors:return None
  mean=normal.mean(0);ml=np.linalg.norm(mean)
  if ml<.99:return None
  mean/=ml
  if np.min(normal@mean)<=self.NORMAL:return None
  gn=np.cross(p[1]-p[0],p[2]-p[0]);gl=np.linalg.norm(gn)
  if gl<1e-8:return None
  gn/=gl
  if abs(float(gn@mean))<self.GEOMETRIC_NORMAL:return None
  low=np.floor((p.min(0)-self.PLANE)/self.CELL).astype(int);high=np.floor((p.max(0)+self.PLANE)/self.CELL).astype(int)
  if np.prod(high-low+1)>512:return None
  candidate=set();directions=[j for j in range(3)if abs(mean[j])>=np.max(abs(mean))-.025]
  for cell in itertools.product(*(range(low[j],high[j]+1)for j in range(3))):
   for j in directions:candidate.update(self.grid.get(tag+(j,1 if mean[j]>=0 else-1)+cell,()))
  if len(candidate)>2048:return None
  groups=collections.defaultdict(list);all_polys=[];axes=[i for i in range(3)if i!=int(np.argmax(abs(gn)))];origin=p[0,axes];target=ccw(p[:,axes]-origin);area=polygon_area(target)
  nodes=collections.defaultdict(list)
  for si,ti in candidate:nodes[si].append(ti)
  plow=p.min(0)-self.PLANE;phigh=p.max(0)+self.PLANE
  for si,ids in nodes.items():
   s=self.surfaces[si];ids=np.array(ids,np.int32);q=s['positions'][ids].astype(float)
   okay=(s['high'][ids]>=plow).all(1)&(s['low'][ids]<=phigh).all(1)&(abs(s['geometric'][ids]@gn)>=self.GEOMETRIC_NORMAL)&(abs((q-p[0])@gn).max(1)<=self.PLANE)&((s['normals'][ids]@mean).min(1)>self.NORMAL)&(abs(s['colors'][ids]-color[0]).max(axis=(1,2))<.005)
   for ti,vertices in zip(ids[okay],q[okay]):
    poly=ccw(vertices[:,axes]-origin);overlap=overlap_area(target,poly)
    if overlap<=max(1e-10,area*1e-10):continue
    kind=int(s['kinds'][ti]);key=(si,int(self.components[si][ti]),kind);groups[key].append(poly);all_polys.append((kind,poly))
    if len(all_polys)>128:return None
  if not groups:return None
  return groups,all_polys,target,area
 def classify(self,material,p,normal,color):
  collected=self.collect(material,p,normal,color)
  if collected is None:return None
  groups,all_polys,target,area=collected
  core=target
  for i in range(3):core=clip_half(core,target[i],target[(i+1)%3],True,self.CORE_INSET)
  core_area=polygon_area(core);winners=[]
  for key,covers in groups.items():
   kind=key[2]
   if kind==255:continue
   missing=uncovered(target,covers)
   if missing is None or missing>max(1e-9,area*.0001):continue
   core_missing=uncovered(core,covers)if core_area>1e-8 else missing
   if core_missing is None or core_missing>max(1e-9,core_area*1e-7):continue
   other=[poly for otherkind,poly in all_polys if otherkind!=kind]
   other_missing=uncovered(target,other)if other else area
   if other_missing is None or area-other_missing>max(1e-8,area*.00001):continue
   winners.append((kind,1-missing/area))
  kinds={k for k,fraction in winners}
  return (next(iter(kinds)),max(f for k,f in winners))if len(kinds)==1 else None
 def fragments(self,material,p,normal,color):
  # Different prepared triangulations may leave thin interior seams. Transfer
  # ONLY actual source-covered polygon fragments, retaining every uncovered
  # fragment in its original material instead of relaxing the no-hole rule.
  collected=self.collect(material,p,normal,color)
  if collected is None:return None
  groups,all_polys,target,area=collected
  selected={kind for kind,poly in all_polys if kind!=255}
  if len(selected)!=1:return None
  kind=next(iter(selected));covers=[poly for k,poly in all_polys if k==kind]
  other=[poly for k,poly in all_polys if k!=kind]
  if other:
   other_missing=uncovered(target,other)
   if other_missing is None or area-other_missing>max(1e-8,area*.00001):return None
  missing=uncovered(target,covers)
  if missing is None or missing>area*.05:return None
  remaining=[target];render=[]
  for cover in covers:
   next_remaining=[];cover=ccw(cover)
   for piece in remaining:
    clipped=piece
    for i in range(len(cover)):clipped=clip_half(clipped,cover[i],cover[(i+1)%len(cover)],True)
    if polygon_area(clipped)>1e-10:render.append((kind,clipped))
    next_remaining.extend(subtract_convex(piece,cover))
   remaining=next_remaining
   if len(remaining)+len(render)>128:return None
  render.extend((255,piece)for piece in remaining if polygon_area(piece)>1e-10)
  # Keep the raw triangle's own winding/order for interpolation. The clipping
  # target is CCW but source/raw normals can point along either projected axis.
  p=np.asarray(p,float);gn=np.cross(p[1]-p[0],p[2]-p[0]);axes=[i for i in range(3)if i!=int(np.argmax(abs(gn)))];raw=p[:,axes]-p[0,axes]
  inverse=np.linalg.inv(np.column_stack((raw[1],raw[2])));bary=[];kinds=[]
  for flag,poly in render:
   for i in range(1,len(poly)-1):
    q=np.asarray([poly[0],poly[i],poly[i+1]],float)
    if polygon_area(q)<=1e-10:continue
    a,b=q[1]-q[0],q[2]-q[0]
    if (raw[1,0]*raw[2,1]-raw[1,1]*raw[2,0])*(a[0]*b[1]-a[1]*b[0])<0:q=q[[0,2,1]]
    uv=q@inverse.T;weights=np.c_[1-uv.sum(1),uv]
    if weights.min()<-1e-6 or weights.max()>1+1e-6:return None
    weights=np.clip(weights,0,1);weights/=weights.sum(1)[:,None];bary.extend(float(x)for x in weights.ravel());kinds.append(flag)
  if not kinds or len(kinds)>256:return None
  return dict(barycentric=bary,triangleKinds=base64.b64encode(bytes(kinds)).decode(),sourceCoverage=1-missing/area)
def transfer(slug,publish=False):
 for path,digest in PINS.items():assert sha(SOURCE/path)==digest,'Released R6 source changed: '+path
 plan=json.loads((SOURCE/'assets/data/architecture_r24.json').read_text())['maps']['archive:'+slug]
 folder=OUT/slug;manifest=json.loads((folder/'map.json').read_text());assert manifest['version']==1
 geometry=folder/'geometry.bytes';before=sha(geometry);points,source_counts,source_mats=load_points(slug,plan,False);del points
 faces,source_face_count,surfaces=source_faces(slug,plan);started=time.monotonic();print(slug,'building bounded coplanar index',flush=True);footprints=Footprints(surfaces);print(slug,'index ready',round(time.monotonic()-started,2),'seconds',flush=True)
 material_keys=[m['name']for m in manifest['materials']];changes=[];counts=collections.Counter();foot_counts=collections.Counter();ambiguous=0;normal_rejected=0;foot_examples=[];direct_examples={}
 with geometry.open('rb')as f:
  magic,chunks=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and chunks==manifest['chunks']
  for chunk in range(chunks):
   mid,collision,visible,nv,ni=struct.unpack('<iiiii',f.read(20));v=np.frombuffer(f.read(nv*48),'<f4').reshape(nv,12);ix=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3)
   if not visible or material_keys[mid]not in source_mats:continue
   centers=v[ix,:3].mean(axis=1);bins=np.floor(centers/.05).astype(np.int32);indices=[];kinds=[];fragments=[]
   for ti,bucket in enumerate(map(tuple,bins.tolist())):
    candidates=faces.get((material_keys[mid],bucket),())
    at=ix[ti];p=v[at,:3];normal=v[at,3:6];color=v[at,8:11];near=[]
    for q,qn,qc,kind,node,source_triangle in candidates:
     for shift in range(3):
      a=np.roll(q,shift,axis=0);distance=float(np.linalg.norm(p-a,axis=1).max())
      if distance<=.005 and np.max(abs(color-np.roll(qc,shift,axis=0)))<.005 and np.min(np.sum(normal*np.roll(qn,shift,axis=0),axis=1))>.9995:near.append((distance,kind,node,source_triangle));break
    if near:
     distance=min(x[0]for x in near);flags={item[1]for item in near if item[0]<=distance+.00002}
     if len(flags)!=1:ambiguous+=1;continue
     kind=next(iter(flags))
     if kind not in direct_examples:
      witness=min(near,key=lambda item:item[0]);direct_examples[kind]=dict(chunk=chunk,triangle=ti,kind=kind,material=material_keys[mid],sourceNode=witness[2],sourceTriangle=witness[3],maxCornerDistance=witness[0])
    else:
     normal_rejected+=1;fallback=footprints.classify(material_keys[mid],p,normal,color)
     if fallback is None:
      # These two personally compared, exact source-ray-bound farm faces have
      # different prepared tessellation. No wider fallback activation here.
      if slug=='classic-bigbox-vr'and chunk==270 and ti in[17924,17926]:
       split=footprints.fragments(material_keys[mid],p,normal,color)
       if split is not None:fragments.append(dict(triangle=int(ti),**split))
      continue
     kind,coverage=fallback;foot_counts[kind]+=1
     if len(foot_examples)<8 or(chunk==270 and ti in[17925,17926]):foot_examples.append(dict(chunk=chunk,triangle=ti,kind=kind,unionCoverage=coverage))
    if kind!=255:indices.append(ti);kinds.append(kind);counts[kind]+=1
   if indices or fragments:changes.append(dict(chunk=chunk,vertices=nv,triangles=indices,triangleKinds=base64.b64encode(bytes(kinds)).decode(),fragments=fragments))
 assert sha(geometry)==before
 textures=['brick.png','concrete.png','corrugated.png','planks.png','statue-stone.png']
 data=dict(version=1,mapId=manifest['mapId'],geometrySha256=before,chunks=chunks,materials=material_properties(source_mats),patches=changes,
           family=plan.get('family_details_r6',{}),textures=textures+['freight.png'],sourcePins=PINS)
 export,_=source_export(slug,plan)
 report=dict(map=slug,exportFolder=str(export),preparedR10Sha256=json.loads((SOURCE/'assets/data/archive_adapters_r10.json').read_text())['files'][slug],sourcePlanSha256=PINS['assets/data/architecture_r24.json'],geometrySha256=before,selectedSourceVertices=dict(source_counts),sourceClassifiedFaces=source_face_count,transferredUnityFaces=dict(counts),directSourceWitnesses=list(direct_examples.values()),coplanarFootprintFaces=dict(foot_counts),footprintExamples=foot_examples,patches=len(changes),ambiguousFacesUnchanged=ambiguous,normalOrDistanceRejected=normal_rejected,geometryUnchanged=True,shaderNativeRun=False,rule='Keep exact oriented <=5mm whole-face matches. Fallback: one source welded component/kind, original material+uniform RGB+oriented normals, <=5mm plane, actual triangle union>=99.99%, covered7.5mm inset core, no competing kind/255 overlap. Original last-corner selector.')
 here=Path(__file__).resolve().parent; (here/(slug+'-transfer.json')).write_text(json.dumps(report,indent=2)+'\n')
 staged=here/'staged';staged.mkdir(exist_ok=True);(staged/(slug+'-architecture.json')).write_text(json.dumps(data,separators=(',',':'))+'\n')
 if publish:
  publish_staged(slug,data,manifest)
 print(json.dumps({k:v for k,v in report.items()if k not in['footprintExamples','selectedSourceVertices','directSourceWitnesses']},indent=2));return report

def metadata(path):
 meta=Path(str(path)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-central-architecture:'+str(path.relative_to(ROOT))).hex+'\n'+('folderAsset: yes\n'if path.is_dir()else''))

def publish_staged(slug,data=None,manifest=None):
 folder=OUT/slug
 if data is None:data=json.loads((Path(__file__).resolve().parent/'staged'/(slug+'-architecture.json')).read_text())
 if manifest is None:manifest=json.loads((folder/'map.json').read_text())
 assert data['mapId']==manifest['mapId']=='archive:'+slug and data['chunks']==manifest['chunks']and data['geometrySha256']==sha(folder/'geometry.bytes')
 assert sum(len(p['triangles'])for p in data['patches'])>1000,'No useful source-bound classification transfer'
 if slug not in EXISTING:
  assert slug in export_folders(),'Only the seven released central families can receive a new link'
  tex=folder/'architecture';tex.mkdir(exist_ok=True);metadata(tex)
  pins=json.loads((SOURCE.parent/'SOURCE-MANIFEST.json').read_text());expected={r['path']:r['sha256']for r in pins['files']}
  files=[('assets/art_r24/'+name,name)for name in ['brick.png','concrete.png','corrugated.png','planks.png','statue-stone.png']]+[('assets/art_r19/freight.png','freight.png')]
  for relative,name in files:
   source=SOURCE/relative;assert sha(source)==expected[relative];target=tex/name
   if target.exists():assert sha(target)==expected[relative],'Existing local architecture texture differs; never edit a hardlinked shared texture'
   else:
    temporary=target.with_name(target.name+'.architecture-new');temporary.write_bytes(source.read_bytes());temporary.replace(target)
   metadata(target)
  updated=dict(manifest);updated['architectureFile']='architecture.json';assert {k:v for k,v in updated.items()if k!='architectureFile'}=={k:v for k,v in manifest.items()if k!='architectureFile'}
 else:assert manifest.get('architectureFile')=='architecture.json','Root owns enabling existing map manifest'
 target=folder/'architecture.json';temporary=folder/'architecture.json.coplanar-new';temporary.write_text(json.dumps(data,separators=(',',':'))+'\n');temporary.replace(target);metadata(target)
 if slug not in EXISTING:
  # Enable only after every local texture and the complete selector exist.
  target=folder/'map.json';temporary=folder/'map.json.architecture-new';temporary.write_text(json.dumps(updated,indent=2)+'\n');temporary.replace(target)

if __name__=='__main__':
 p=argparse.ArgumentParser();group=p.add_mutually_exclusive_group(required=True);group.add_argument('--map',choices=sorted(export_folders()));group.add_argument('--remaining-central',action='store_true');p.add_argument('--publish',action='store_true');a=p.parse_args()
 slugs=[a.map]if a.map else sorted(set(export_folders())-EXISTING);protected={str(OUT/s/name):sha(OUT/s/name)for s in EXISTING for name in['architecture.json','map.json']};reports=[transfer(s,a.publish)for s in slugs]
 if a.remaining_central:
  assert all(sha(Path(path))==digest for path,digest in protected.items()),'Existing ALFA/Classic changed during new-map work'
  (Path(__file__).resolve().parent/'central7-results.json').write_text(json.dumps(dict(maps=reports,protectedAlfaClassic=protected,engineRun=False),indent=2)+'\n')
