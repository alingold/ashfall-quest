"""Complete the source _panel_spawn_cells / warmup state after R18 and R21."""
from pathlib import Path
import collections,hashlib,itertools,json,re,struct,sys
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5];S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
sys.path.insert(0,str(ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6'));from godot_resource import Resource
sys.path.insert(0,str(ROOT/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'));from pack_reader import Pack
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();pins={r['path']:r['sha256']for r in json.loads((S.parent/'SOURCE-MANIFEST.json').read_text())['files']}
used={}
def source(p):assert sha(S/p)==pins[p],p;used[p]=pins[p];return S/p
fixtures=json.loads(source('assets/data/map_fixtures_r18.json').read_text())['maps'];backings=json.loads(source('assets/data/hatch_backing_r21.json').read_text())['maps'];source('scripts/map_fixtures_r18.gd');source('scripts/hatch_backing_r21.gd')
adapters=json.loads(source('assets/data/archive_adapters_r10.json').read_text())['files'];pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck')
data=json.loads((HERE/'Payload/maps.json').read_text());baseline={r['id']:r for r in data['rows']};appended=warmupAdded=attachments=0
def values(p):return np.array([p['x'],p['y'],p['z']],np.float32)
def point(v):return dict(x=float(v[0]),y=float(v[1]),z=float(v[2]))
def grow_box(lo,hi):lo=lo-np.float32(.015);return dict(position=point(lo),size=point(hi-lo+np.float32(.015)))
def components(faces,rawFaces):
 flat=faces.reshape(-1,3);scaled=flat*np.float32(1000);keys=np.where(scaled>=0,np.floor(scaled+.5),np.ceil(scaled-.5)).astype(np.int64)
 _,first,inverse=np.unique(keys,axis=0,return_index=True,return_inverse=True);ids=inverse.reshape(-1,3);parent=np.arange(len(first),dtype=np.int32)
 def root(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=int(parent[i])
  return i
 for corners in ids:
  for k in (1,2):parent[root(int(corners[k]))]=root(int(corners[0]))
 roots=np.array([root(i)for i in range(len(first))])
 groups=collections.OrderedDict()
 for t in range(len(ids)):groups.setdefault(int(roots[ids[t,0]]),[]).append(t)
 for component,selected in groups.items():
  representatives=flat[first[roots==component]];lo=representatives.min(0);hi=representatives.max(0)
  # Source matches the snapped first-weld component bounds, then creates the
  # appended SurfaceTool mesh from ORIGINAL surface arrays (not get_faces).
  vertices=rawFaces[selected].reshape(-1,3);yield dict(triangles=len(selected),low=vertices.min(0),high=vertices.max(0),center=(lo+hi)*np.float32(.5),size=hi-lo)
def native_components(resource,wanted,scopeLo,scopeHi):
 result={key:[]for key in wanted}
 for node in resource.nodes():
  match=re.fullmatch(r'Draw_(?:world|roof)_(.+)_-?\d+_-?\d+',node['name']);p=node['props']
  if not match or match[1]not in wanted or'mesh'not in p:continue
  assert p.get('transform',[1,0,0,0,1,0,0,0,1,0,0,0])==[1,0,0,0,1,0,0,0,1,0,0,0]
  ref=p['mesh'];assert ref[1]==2;mesh=resource.resources[ref[2]]
  for surface in mesh['props']['_surfaces']:
   a=np.array(surface['aabb'],np.float32)
   if np.any(a[:3]>scopeHi)or np.any(a[:3]+a[3:]<scopeLo):continue
   n=surface['vertex_count'];vertices=np.frombuffer(surface['vertex_data'],'<f4',n*3).reshape(n,3)
   ix=np.frombuffer(surface['index_data'],'<u2'if n<65536 else'<u4')if'index_data'in surface else np.arange(n)
   rawFaces=vertices[ix].reshape(-1,3,3);step=float(np.float32(.0001));faces=(np.floor(rawFaces.astype(np.float64)/step+.5)*step).astype(np.float32)
   result[match[1]].extend(components(faces,rawFaces))
 return result
try:
 for slug,config in fixtures.items():
  row=baseline.get('archive:'+slug)
  if row is None:continue
  assert config['cache_fingerprint']==row['cacheFingerprint']and config['source_sha256']==row['sourceSha256']
  # R18 uses these authoritative JSON center rows verbatim after successful
  # hash-bound marker extraction; duplicate threshold is the original .05m.
  for p in config.get('marker_recovery_r18',{}).get('warmup_spawns',[]):
   center=np.array(p['center'],np.float32)
   if any(np.linalg.norm(values(old)-center)<.05 for old in row['warmup']):continue
   row['warmup'].append(point(center));warmupAdded+=1
  assemblies=config.get('tower_gate_grates',[])
  if not assemblies:continue
  raw=pack.read('local_archive_r10/'+slug+'.res');assert hashlib.sha256(raw).hexdigest()==adapters[slug];resource=Resource(raw)
  specs=[member for opening in assemblies for part in opening['parts']for member in part.get('members',[part])]
  low=np.min([p['min']for p in specs],axis=0)-.1;high=np.max([p['max']for p in specs],axis=0)+.1
  candidates=native_components(resource,{p['key']for p in specs},low,high);boxes={};taken=set()
  for opening in assemblies:
   chosen=[]
   for part in opening['parts']:
    for spec in part.get('members',[part]):
     found=[(i,c)for i,c in enumerate(candidates[spec['key']])if c['triangles']==spec['triangles']and np.linalg.norm(c['center']-np.array(spec['center']))<.05 and np.linalg.norm(c['size']-np.array(spec['size']))<.06]
     assert len(found)==1,(slug,opening['opening_id'],spec,len(found),[(c['triangles'],c['center'].tolist(),c['size'].tolist())for c in sorted(candidates[spec['key']],key=lambda c:np.linalg.norm(c['center']-np.array(spec['center'])))[:2]]);i,c=found[0];identity=(spec['key'],i);assert identity not in taken; taken.add(identity);chosen.append(c)
   boxes[opening['opening_id']]=(np.min([c['low']for c in chosen],axis=0),np.max([c['high']for c in chosen],axis=0))
  if slug in backings:
   index=backings[slug];assert index['fingerprint']==row['cacheFingerprint'];p=source(index['path'][6:]);assert sha(p)==index['sha256'];patch=Resource(p.read_bytes());plans=patch.resources[-1]['props']['metadata/plans']
   for opening,parts in plans['attachments'].items():
    assert opening in boxes;lo,hi=boxes[opening]
    for part in parts:
     ref=part['mesh'];assert ref[1]==2
     for surface in patch.resources[ref[2]]['props']['_surfaces']:
      b=np.array(surface['aabb'],np.float32);lo=np.minimum(lo,b[:3]);hi=np.maximum(hi,b[:3]+b[3:])
     attachments+=1
    boxes[opening]=(lo,hi)
  for opening in assemblies:row['panels'].append(grow_box(*boxes[opening['opening_id']]));appended+=1
  row['postStagePanelCount']=len(boxes);print(slug,'exact native R18 panels',len(boxes),flush=True)
finally:pack.f.close()
assert appended==162 and attachments==90
data['sourceInputs'].update(used);out=HERE/'Payload/maps-post-stage.json';out.write_text(json.dumps(data,separators=(',',':'))+'\n')
(HERE/'post-stage-checks.json').write_text(json.dumps(dict(appendedPanels=appended,exactR21Attachments=attachments,additionalWarmupHints=warmupAdded,sourceInputs=used,payloadBytes=out.stat().st_size,payloadSha256=sha(out),nativeExecuted=False),indent=2)+'\n')
print('Exact post-stage completion:',appended,'R18 panels',attachments,'R21 attachments',warmupAdded,'R18 warmups; no Assets writes.')
