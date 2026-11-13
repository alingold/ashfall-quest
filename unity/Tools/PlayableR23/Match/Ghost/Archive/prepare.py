"""Read-only R6/archive adapter metadata for the genuine recovery spawn rules."""
from pathlib import Path
import hashlib,itertools,json,struct,sys,time
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5];S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
U=ROOT/'unity/Assets/StreamingAssets/PlayableR23';L=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
sys.path.insert(0,str(ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6'));from godot_resource import Resource
sys.path.insert(0,str(ROOT/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'));from pack_reader import Pack
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
manifest=S.parent/'SOURCE-MANIFEST.json';assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256']for r in json.loads(manifest.read_text())['files']};used={}
def source(path):
 p=S/path;assert sha(p)==pins[path],path;used[path]=pins[path];return p
for path in ['scripts/net_bridge1000.gd','scripts/archive_map1003.gd','scripts/community_map1002.gd','scripts/map_semantics_r14.gd']:source(path)
sem=json.loads(source('assets/data/map_semantics_r14.json').read_text())['maps'];adapters=json.loads(source('assets/data/archive_adapters_r10.json').read_text())['files']
launch=json.loads((U/'TeamLaunch/launch-maps.json').read_text());assert launch['sourceManifestSha256']==sha(manifest)
def point(v):return dict(x=float(v[0]),y=float(v[1]),z=float(v[2]))
def box(aabb):return dict(position=point(aabb[:3]),size=point(aabb[3:]))
def merged_boxes(resource,details):
 result=[]
 for p in details.get('panels',[]):
  a=np.array(p['box'],np.float32);lo=a[:3];hi=lo+a[3:]
  for attach in p.get('attachments',[]):
   ref=attach['mesh'];assert ref[1]==2;mesh=resource.resources[ref[2]]
   for surface in mesh['props']['_surfaces']:
    b=np.array(surface['aabb'],np.float32);lo=np.minimum(lo,b[:3]);hi=np.maximum(hi,b[:3]+b[3:])
  # Original _panel_spawn_cells_r9 merges then grows(.015), float32 AABB.
  lo=lo-np.float32(.015);size=hi-lo+np.float32(.015);result.append(box([*lo,*size]))
 return result
def scene_nodes(resource):
 b=next(r['props']['_bundled']for r in resource.resources if r['type']=='PackedScene');a=struct.unpack('<'+'i'*(len(b['nodes'])//4),b['nodes']);at=0;out=[]
 for index in range(b['node_count']):
  parent,owner,typ,name,instance,count=a[at:at+6];at+=6;props={}
  for j in range(count):key,val=a[at:at+2];at+=2;props[b['names'][key]]=b['variants'][val]
  count=a[at];at+=1;groups=[b['names'][g]for g in a[at:at+count]];at+=count
  m=np.eye(4);t=props.get('transform',[1,0,0,0,1,0,0,0,1,0,0,0]);m[:3,:3]=np.array(t[:9]).reshape(3,3).T;m[:3,3]=t[9:]
  if 0<=parent<len(out):m=out[parent]['matrix']@m
  out.append(dict(index=index,parent=parent,props=props,groups=groups,name=b['names'][name&0x3fffffff],matrix=m))
 assert at==len(a);return out
def planes(points):
 points=np.unique(points,axis=0);assert 4<=len(points)<=256,('actual convex vertices',len(points))
 # Source convex point hull, no AABB replacement. Test all support planes; the
 # small authored kill-shapes generally contain eight unique corners.
 result={}
 for a,b,c in itertools.combinations(range(len(points)),3):
  n=np.cross(points[b]-points[a],points[c]-points[a]);length=np.linalg.norm(n)
  if length<1e-8:continue
  n/=length;d=float(np.dot(n,points[a]));signed=points@n-d
  if np.max(signed)<=1e-5:pass
  elif np.min(signed)>=-1e-5:n=-n;d=-d
  else:continue
  key=tuple(np.rint(np.r_[n*1e6,d*1e4]).astype(np.int64));result.setdefault(key,dict(x=float(n[0]),y=float(n[1]),z=float(n[2]),d=d))
 assert len(result)>=4,'source hazard hull must have volume'
 values=list(result.values());assert all(np.max(points@np.array([p['x'],p['y'],p['z']])-p['d'])<.0001 for p in values)
 return values
def hazards(resource,slug):
 nodes=scene_nodes(resource);areas={n['index']:n for n in nodes if int(n['props'].get('collision_layer',0))&32};out=[];disabled=0
 for node in nodes:
  if node['parent']not in areas or 'shape'not in node['props']or node['props'].get('disabled',0):continue
  area=areas[node['parent']];ref=node['props']['shape'];assert ref[1]==2;shape=resource.resources[ref[2]];kind=shape['type'];props=shape['props']
  if kind=='ConvexPolygonShape3D':local=np.frombuffer(props['points'],'<f4').reshape(-1,3).astype(np.float64)
  elif kind=='BoxShape3D':local=np.array(list(itertools.product(*[[-v*.5,v*.5]for v in props['size']])),np.float64)
  else:raise AssertionError((slug,'unsupported exact source hazard',kind))
  lo=local.min(0);hi=local.max(0)
  if slug=='aftermath-light-vs-dark-gods-badbar'and'archive_killzone'in area['groups']and lo[1]>=80 and hi[1]<=96:disabled+=1;continue
  world=local@node['matrix'][:3,:3].T+node['matrix'][:3,3]
  out.append(dict(name=node['name'],kind=kind,minimum=point(world.min(0)),maximum=point(world.max(0)),planes=planes(world)))
 return out,disabled
pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');rows=[];started=time.monotonic();maxBytes=0
try:
 for row in launch['rows']:
  if not row['hasArchiveMetadata']:continue
  slug=row['id'][8:];path=L/slug/'converted/manifest.json';m=json.loads(path.read_text());s=sem[slug]
  assert sha(path)==row['manifestSha256'] and m['source_sha256']==row['sourceSha256']==s['source_sha256']
  assert m['cache_fingerprint']==row['cacheFingerprint']==s['cache_fingerprint']
  raw=pack.read('local_archive_r10/'+slug+'.res');assert hashlib.sha256(raw).hexdigest()==adapters[slug]==row['adapterSha256']
  resource=Resource(raw);details=resource.resources[-1]['props']['details'];assert resource.resources[-1]['props']['source_fingerprint']==m['cache_fingerprint']
  hazard,disabled=hazards(resource,slug);maxBytes=max(maxBytes,len(resource.b))
  rows.append(dict(id=row['id'],sourceSha256=row['sourceSha256'],manifestSha256=row['manifestSha256'],cacheFingerprint=row['cacheFingerprint'],adapterSha256=row['adapterSha256'],boundsMin=point(m['bounds_min']),boundsMax=point(m['bounds_max']),solidMin=point(m['solid_min']),solidMax=point(m['solid_max']),center=point(m['center']),recorded=[point(p)for p in m['recorded_spawns']],deathmatch=[point(p['center'])for p in s.get('deathmatch',[])],warmup=[point(p['center'])for p in s.get('warmup',[])],spawnFloors=[box(p)for p in details.get('spawn_floors_r9',[])],panels=merged_boxes(resource,details),hazards=hazard,disabledSafePathHazards=disabled))
  print(len(rows),slug,'recorded/native',len(rows[-1]['recorded']),len(rows[-1]['deathmatch']),'panels/hazards',len(rows[-1]['panels']),len(hazard),flush=True)
  del resource,raw
finally:pack.f.close()
assert len(rows)==105
out=HERE/'Payload';out.mkdir(exist_ok=True);payload=out/'maps.json';payload.write_text(json.dumps(dict(version=1,sourceManifestSha256=sha(manifest),contentId=launch['contentId'],sourceInputs=used,rows=rows),separators=(',',':'))+'\n')
assert payload.stat().st_size<12*1024*1024
(HERE/'prepare-results.json').write_text(json.dumps(dict(rows=len(rows),bytes=payload.stat().st_size,seconds=time.monotonic()-started,largestDecodedResource=maxBytes,sourceInputs=used),indent=2)+'\n')
print('Prepared',len(rows),'genuine archive recovery rows;',payload.stat().st_size,'bytes;',round(time.monotonic()-started,2),'seconds. No Assets writes.')
