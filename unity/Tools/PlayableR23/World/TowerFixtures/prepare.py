from pathlib import Path
import collections,hashlib,json,mmap,re,struct,time
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];SRC=ROOT/'quest-r24/source'
FROZEN=ROOT/'campaign-staging/20261104-three-chats/world/morning-r5/r5/delivery/SOURCE-CONTENT.json'
FROZEN_SHA='d4fb1053acfbaa7f3eb285879472bfdd136e09bf2f7e1f2dc6478f23d8f12bc7';CONTENT='da1db74984ea1b82a8b60b7cb8baae9d31dc5483f387fdcf105f3dd0a308895c'
ARCHIVE=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
SLUGS=['alfa-aglae-souza','classic-bigbox-vr'];LIMIT=8*1024*1024

def sha(path):
 h=hashlib.sha256()
 with path.open('rb')as f:
  for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
 return h.hexdigest()

def verified_sources():
 assert sha(FROZEN)==FROZEN_SHA,'frozen R5 SOURCE-CONTENT digest differs'
 frozen=json.loads(FROZEN.read_text());assert frozen['content_id']==CONTENT
 expected={r['path']:r['sha256']for r in frozen['files']};inputs={}
 for name in ['assets/data/map_fixtures_r18.json','scripts/map_fixtures_r18.gd','scripts/archive_semantics_r8.gd','scripts/archive_catalog1003.gd','scripts/archive_panel_r8.gd','scripts/archive_map1003.gd','assets/audio/sfx/r18/metal_grate.wav']:
  actual=sha(SRC/name);assert actual==expected[name],name+' no longer matches frozen R5';inputs[name]=actual
 return inputs

def components(tri):
 # archive_semantics_r8._components: vertex weld at1mm, Godot round ties away from0.
 flat=np.ascontiguousarray(tri.reshape(-1,3),dtype=np.float32);scaled=flat*np.float32(1000)
 keys=np.where(scaled>=0,np.floor(scaled+.5),np.ceil(scaled-.5)).astype(np.int64)
 unique,first,inverse=np.unique(keys,axis=0,return_index=True,return_inverse=True);parents=np.arange(len(unique),dtype=np.int32)
 def root(i):
  while parents[i]!=i:parents[i]=parents[parents[i]];i=int(parents[i])
  return i
 for corners in inverse.reshape(-1,3):
  a=root(int(corners[0]));parents[root(int(corners[1]))]=a;parents[root(int(corners[2]))]=root(a)
 roots=np.array([root(i)for i in range(len(unique))],np.int32);tri_roots=roots[inverse.reshape(-1,3)[:,0]];groups=collections.OrderedDict()
 for ti,r in enumerate(tri_roots):groups.setdefault(int(r),[]).append(ti)
 # AABB uses first representative for each welded point exactly as source, not inflated bounds.
 for r,ids in groups.items():
  points=flat[first[roots==r]];lo=points.min(axis=0);hi=points.max(axis=0)
  yield np.array(ids,np.int32),(lo+hi)*np.float32(.5),hi-lo,lo,hi

def intersects(lo,hi,otherlo,otherhi):return (lo<=otherhi).all()and(hi>=otherlo).all()

def source_triangles(slug,row):
 source=ARCHIVE/slug/'source/map.glb';assert sha(source)==row['source_sha256'],'raw source GLB mismatch'
 stream=source.open('rb');raw=mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ);n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);base=28+n
 assert struct.unpack_from('<I',raw,0)[0]==0x46546c67
 def acc(i):
  a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']];assert 'sparse'not in a
  dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);w={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
  value=np.ndarray((a['count'],w),dt,buffer=raw,offset=base+v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dt.itemsize*w),dt.itemsize))
  if a.get('normalized'):return np.maximum(-1,value.astype(np.float32)/np.iinfo(dt).max)
  return value
 def local(node):
  if 'matrix'in node:return np.array(node['matrix']).reshape(4,4,order='F')
  x,y,z,w=node.get('rotation',[0,0,0,1]);m=np.eye(4);m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(node.get('scale',[1,1,1]));m[:3,3]=node.get('translation',[0,0,0]);return m
 specs=[];assemblies=row['tower_gate_grates'];wanted=set()
 for oi,assembly in enumerate(assemblies):
  for pi,part in enumerate(assembly['parts']):
   for mi,member in enumerate(part.get('members',[part])):
    spec=dict(owner=oi,part=pi,member=mi,key=member['key'],role=part['role'],count=member['triangles'],center=np.array(member['center']),size=np.array(member['size']),sourceMin=member['min'],sourceMax=member['max']);specs.append(spec);wanted.add(member['key'])
 low=np.min(np.array([a['min']for a in assemblies]),axis=0)-.5;high=np.max(np.array([a['max']for a in assemblies]),axis=0)+.5
 groups=collections.OrderedDict();selected=set()
 def walk(index,parent):
  node=doc['nodes'][index];matrix=parent@local(node);match=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_(.+)',node.get('name',''))
  if 'mesh'in node and match and match[4]in wanted:
   kind,x,z,key=match.groups();batch=(kind,key,int(x)//4,int(z)//4)
   for primitive in doc['meshes'][node['mesh']]['primitives']:
    assert primitive.get('mode',4)==4;pos=acc(primitive['attributes']['POSITION']);idx=acc(primitive['indices']).ravel().astype(np.int32)if'indices'in primitive else np.arange(len(pos),dtype=np.int32)
    xyz=(pos.astype(np.float64)@matrix[:3,:3].T+matrix[:3,3]).astype(np.float32);tri=xyz[idx].reshape(-1,3,3).copy();groups.setdefault(batch,[]).append(tri)
    if intersects(xyz.min(axis=0),xyz.max(axis=0),low,high):selected.add(batch)
  for child in node.get('children',[]):walk(child,matrix)
 for index in doc['scenes'][doc.get('scene',0)]['nodes']:walk(index,np.eye(4))
 matched=set();debug=[];direct_components=set()
 for key,parts in groups.items():
  if key not in selected:continue
  tri=np.concatenate(parts)
  for ids,center,size,lo,hi in components(tri):
   if not intersects(lo,hi,low,high):continue
   debug.append((key,len(ids),center.tolist(),size.tolist(),tri[ids].copy()))
   for si,spec in enumerate(specs):
    if si in matched or spec['key']!=key[1]or len(ids)!=spec['count']:continue
    if np.linalg.norm(center-spec['center'])<=.04 and np.linalg.norm(size-spec['size'])<=.05:
     spec['triangles']=tri[ids].copy();spec['batch']=list(key);spec['actualMin']=lo.tolist();spec['actualMax']=hi.tolist();spec['actualCenter']=center.tolist();spec['actualSize']=size.tolist();matched.add(si);direct_components.add(len(debug)-1);break
 # Cached Godot fixtures can split near-coincident raw-GLB welded components.
 # Recover ONLY original complete triangles, with exact catalog count/AABB and a
 # unique subset. Every adapted raw component must be consumed in full by the
 # catalog members; never cut, add, move or approximate a triangle.
 recovered_components=collections.defaultdict(list)
 for si,spec in enumerate(specs):
  if si in matched:continue
  choices=[];low=np.array(spec['sourceMin']);high=np.array(spec['sourceMax'])
  for di,(key,total,component_center,component_size,tri)in enumerate(debug):
   if di in direct_components or key[1]!=spec['key']:continue
   mask=(tri.min(axis=1)>=low-.0045).all(axis=1)&(tri.max(axis=1)<=high+.0045).all(axis=1);ids=np.flatnonzero(mask)
   if len(ids)!=spec['count']:continue
   part=tri[ids];lo=part.min(axis=(0,1));hi=part.max(axis=(0,1));center=(lo+hi)*np.float32(.5);size=hi-lo
   if np.linalg.norm(center-spec['center'])<=.04 and np.linalg.norm(size-spec['size'])<=.05:choices.append((di,key,ids,part,lo,hi,center,size))
  if not choices:
   # Current raw batching may join two cached components. Coalesce only when
   # BOTH source rows have identical break owner/material/role and the exact
   # whole raw component has their summed count and union AABB. No arbitrary
   # 105/37 allocation is invented inside overlapping source bounding boxes.
   merged=[]
   for sj,other in enumerate(specs):
    if sj<=si or sj in matched or(other['owner'],other['key'],other['role'])!=(spec['owner'],spec['key'],spec['role']):continue
    unionlo=np.minimum(low,other['sourceMin']);unionhi=np.maximum(high,other['sourceMax']);expected_count=spec['count']+other['count']
    for di,(key,total,cc,ss,tri)in enumerate(debug):
     if di in direct_components or total!=expected_count or key[1]!=spec['key']:continue
     if np.linalg.norm(np.array(cc)-(unionlo+unionhi)*.5)<=.04 and np.linalg.norm(np.array(ss)-(unionhi-unionlo))<=.05:merged.append((sj,di,key,tri))
   assert len(merged)==1,f'{slug} member{si}: no unique exact source member/coalesced component recovery'
   sj,di,key,tri=merged[0];other=specs[sj];lo=tri.min(axis=(0,1));hi=tri.max(axis=(0,1));spec.update(triangles=tri.copy(),batch=list(key),actualMin=lo.tolist(),actualMax=hi.tolist(),actualCenter=((lo+hi)*.5).tolist(),actualSize=(hi-lo).tolist(),coalescedSpecs=[si,sj]);other.update(triangles=np.empty((0,3,3),np.float32),coalescedInto=si);matched.update([si,sj]);direct_components.add(di)
   recovered_components[di].append((si,np.arange(len(tri),dtype=np.int32)));continue
  assert len(choices)==1,f'{slug} member{si}: nonunique exact member recovery'
  di,key,ids,tri,lo,hi,center,size=choices[0];spec.update(triangles=tri.copy(),batch=list(key),actualMin=lo.tolist(),actualMax=hi.tolist(),actualCenter=center.tolist(),actualSize=size.tolist(),rawComponentSplit=True);matched.add(si);recovered_components[di].append((si,ids))
 recoveries=[]
 for di,members in recovered_components.items():
  key,count,center,size,tri=debug[di];all_ids=np.concatenate([ids for si,ids in members]);assert len(all_ids)==len(set(all_ids.tolist()))==count,f'{slug}: adapted component not fully/disjointly covered'
  covered=[sj for si,ids in members for sj in specs[si].get('coalescedSpecs',[si])];recoveries.append(dict(batch=list(key),rawComponentTriangles=count,sourceMemberSpecs=covered,sourceMemberTriangles=[specs[sj]['count']for sj in covered],exactWholeComponentConserved=True,coalescedSameOwnerMaterialRole=any('coalescedSpecs'in specs[si]for si,ids in members)))
 assert len(matched)==len(specs),f'{slug}: source member specs matched{len(matched)}/{len(specs)} missing'+str([(i,s['owner'],s['role'],s['key'],s['count'])for i,s in enumerate(specs)if i not in matched])
 return specs,source,recoveries

def triangle_keys(tri):
 # Position-exact unordered keys survive Unity handedness/winding and batching only.
 # Every selected original vertex is retained; matching never snaps/clips/reconstructs.
 p=np.ascontiguousarray(tri,dtype='<f4');p[p==0]=0 # equivalent signed zeros are same position
 return np.sort(p.view('V12').reshape(-1,3),axis=1).copy().view('V36').ravel()

def area(tri):
 p=tri.astype(np.float64);return float(np.linalg.norm(np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0]),axis=1).sum()*.5)

def runs(ids):
 ids=sorted(set(int(i)for i in ids));result=[]
 for i in ids:
  if result and result[-2]+result[-1]==i:result[-1]+=1
  else:result.extend([i,1])
 return result

def current_delta(slug,row,specs,inputs,recoveries):
 folder=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/slug;manifest=json.loads((folder/'map.json').read_text());geometry=folder/'geometry.bytes';geometry_sha=sha(geometry);assert manifest['version']==1 and manifest['sourceSha256']==row['source_sha256']
 material_keys=[m['name']for m in manifest['materials']];owners=manifest.get('chunkPanelIds',[-1]*manifest['chunks']);assert len(owners)==manifest['chunks']
 source_lookup={};expected=collections.Counter();expected_areas=collections.defaultdict(float)
 for si,spec in enumerate(specs):
  tri=spec['triangles'].copy();tri[:,:,2]*=-1;spec['unityTriangles']=tri
  for key in triangle_keys(tri):
   k=(spec['key'],bytes(key));prior=source_lookup.get(k)
   assert prior is None or prior==si,'duplicate source identity across different members'
   source_lookup[k]=si;expected[k]+=1
  expected_areas[spec['key']]+=area(tri)
 stream=geometry.open('rb');raw=mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ);magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323357 and count==manifest['chunks'];cursor=8
 partitions=[];found_render=collections.Counter();found_collision=collections.Counter();matched_specs=collections.Counter();selected_owner_chunks=[];render_area=collections.defaultdict(float);collision_area=collections.defaultdict(float);selected_triangles=0
 for ci in range(count):
  header_offset=cursor;mid,coll,visible,nv,ni=struct.unpack_from('<iiiii',raw,cursor);cursor+=20;vertices=np.ndarray((nv,12),'<f4',buffer=raw,offset=cursor);vertex_offset=cursor;cursor+=nv*48;indices=np.ndarray((ni//3,3),'<i4',buffer=raw,offset=cursor);index_offset=cursor;cursor+=ni*4
  assert 0<=mid<len(material_keys)and ni%3==0 and indices.min()>=0 and indices.max()<nv
  key=material_keys[mid]
  if key not in expected_areas:continue
  tri=vertices[indices,:3];chunklo=tri.min(axis=(0,1));chunkhi=tri.max(axis=(0,1));lo=np.min(np.array([a['min']for a in row['tower_gate_grates']]),axis=0);hi=np.max(np.array([a['max']for a in row['tower_gate_grates']]),axis=0);lo,hi=lo*np.array([1,1,-1]),hi*np.array([1,1,-1]);low=np.minimum(lo,hi)-.05;high=np.maximum(lo,hi)+.05
  if not intersects(chunklo,chunkhi,low,high):continue
  chosen={};component_ids=collections.defaultdict(set)
  for ti,rawkey in enumerate(triangle_keys(tri)):
   ident=(key,bytes(rawkey));si=source_lookup.get(ident)
   if si is None:continue
   chosen[ti]=specs[si]['owner'];component_ids[specs[si]['owner']].update(specs[si].get('coalescedSpecs',[si]));matched_specs[si]+=1
   if visible:found_render[ident]+=1
   if coll:found_collision[ident]+=1
  if not chosen:continue
  if owners[ci]>=0:selected_owner_chunks.append(dict(chunk=ci,owner=owners[ci],selectedTriangles=len(chosen)))
  assert owners[ci]<0,f'{slug}: selected existing panel-owned chunk '+str(selected_owner_chunks[-1])
  selected=sorted(chosen);keep=[i for i in range(len(indices))if i not in chosen];parts=[]
  for oi in sorted(set(chosen.values())):
   triangle_ids=[ti for ti in selected if chosen[ti]==oi];parts.append(dict(owner=oi,triangleRanges=runs(triangle_ids),sourceMemberSpecs=sorted(component_ids[oi]),triangles=len(triangle_ids)))
  assert len(keep)+sum(p['triangles']for p in parts)==len(indices)
  for group in [keep]+[[ti for ti in selected if chosen[ti]==p['owner']]for p in parts]:
   assert len(group)==len(set(group))
  selected_triangles+=len(selected);a=area(tri[selected]);
  if visible:render_area[key]+=a
  if coll:collision_area[key]+=a
  partitions.append(dict(chunk=ci,material=mid,materialKey=key,collisionFlags=coll,visible=visible,vertices=nv,indices=ni,indexSha256=hashlib.sha256(raw[index_offset:index_offset+ni*4]).hexdigest(),vertexSha256=hashlib.sha256(raw[vertex_offset:vertex_offset+nv*48]).hexdigest(),staticTriangleRanges=runs(keep),parts=parts))
 assert cursor==len(raw)
 assert found_render==expected,f'{slug}: render source triangle multiset mismatch missing{sum((expected-found_render).values())} extra{sum((found_render-expected).values())}'
 assert found_collision==expected,f'{slug}: collision source triangle multiset mismatch missing{sum((expected-found_collision).values())} extra{sum((found_collision-expected).values())}'
 for k,expected_area in expected_areas.items():assert abs(render_area[k]-expected_area)<1e-8 and abs(collision_area[k]-expected_area)<1e-8,(slug,k,expected_area,render_area[k],collision_area[k])
 definitions=[]
 for oi,assembly in enumerate(row['tower_gate_grates']):
  members=[(si,s)for si,s in enumerate(specs)if s['owner']==oi];tri=np.concatenate([s['unityTriangles']for si,s in members]);centroid=tri[np.argmax(np.linalg.norm(np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]),axis=1))].mean(axis=0);cross=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);best=cross[np.argmax(np.linalg.norm(cross,axis=1))];normal=-best/max(np.linalg.norm(best),1e-20);lo=tri.min(axis=(0,1));hi=tri.max(axis=(0,1))
  definitions.append(dict(id=slug+'/'+assembly['fixture_kind']+'/'+assembly['opening_id'],glass=False,center=((lo+hi)*.5).tolist(),size=(hi-lo).tolist(),probe=centroid.tolist(),normal=normal.tolist(),health=1,fixtureKind=assembly['fixture_kind'],openingId=assembly['opening_id'],cardinal=assembly.get('cardinal',''),sound='res://assets/audio/sfx/r18/metal_grate.wav',sourceSignature=assembly.get('signature_sha256_16',''),triangles=len(tri),sourceMemberSpecs=[si for si,s in members]))
 ownership_signature=hashlib.sha256(np.asarray(owners,dtype='<i4').tobytes()).hexdigest()
 delta=dict(version=1,geometryVersion=1,mapId=manifest['mapId'],sourceContentId=CONTENT,sourceManifestSha256=FROZEN_SHA,sourceGlbSha256=row['source_sha256'],sourceCacheFingerprint=row['cache_fingerprint'],baseGeometrySha256=geometry_sha,baseChunks=count,basePanels=len(manifest.get('destructibles',[])),baseMaterialKeys=material_keys,baseOwnershipSha256=ownership_signature,sourceInputs=[dict(path=k,sha256=v)for k,v in inputs.items()],rawComponentAdaptations=recoveries,definitions=definitions,chunks=partitions)
 report=dict(mapId=manifest['mapId'],sourceSpecs=len(specs),matchedSourceSpecs=len(specs)-sum(len(x['sourceMemberSpecs'])for x in recoveries if x['coalescedSameOwnerMaterialRole']),coveredSourceSpecs=len(specs),coalescedSourceSpecs=sum(len(x['sourceMemberSpecs'])for x in recoveries if x['coalescedSameOwnerMaterialRole']),definitions=len(definitions),gateOwners=sum(a['fixtureKind']=='tower_gate_grate'for a in definitions),hatchOwners=sum(a['fixtureKind']=='tower_mid_hatch'for a in definitions),sourceTriangles=sum(expected.values()),renderTriangles=sum(found_render.values()),collisionTriangles=sum(found_collision.values()),affectedChunks=len(partitions),nativeTriangleSlotsPartitioned=selected_triangles,existingOwnedChunksSelected=selected_owner_chunks,materialSurfaceAreas=dict(expected_areas),baseGeometrySha256=geometry_sha,baseChunks=count,basePanels=delta['basePanels'],staticAndOwnedIndexConservation=True,positionExactSourceMatch=True,renderAndCollisionNoMissesOrDuplicates=True,rawComponentAdaptations=recoveries)
 return delta,report

def main():
 started=time.monotonic();inputs=verified_sources();catalog=json.loads((SRC/'assets/data/map_fixtures_r18.json').read_text());deliveries=[];reports=[]
 for slug in SLUGS:
  row=catalog['maps'][slug];specs,source,recoveries=source_triangles(slug,row);print(slug,'source member specs recovered',len(specs),'triangles',sum(s['count']for s in specs),flush=True);delta,report=current_delta(slug,row,specs,inputs,recoveries);data=(json.dumps(delta,separators=(',',':'))+'\n').encode();deliveries.append((slug,data));reports.append(report);print(slug,'matched current chunks',report['affectedChunks'],'delta bytes',len(data),flush=True)
 assert sum(len(data)for slug,data in deliveries)<=LIMIT
 # Publish both only after every source/native/material/ownership conservation check passes.
 for slug,data in deliveries:(ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/slug/'tower-fixtures-r5.json').write_bytes(data)
 result=dict(sourceContentId=CONTENT,frozenManifestSha256=FROZEN_SHA,maps=reports,deltaBytes=sum(len(data)for slug,data in deliveries),elapsedSeconds=round(time.monotonic()-started,3),engineRun=False,deviceRun=False,geometryModified=False,materialsModified=False)
 (HERE/'result.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
