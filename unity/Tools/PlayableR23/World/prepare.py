"""Bounded, read-only R23 baseline export. No Godot/Unity invocation."""
from pathlib import Path
import argparse, json, struct, hashlib, shutil, re
import numpy as np
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/World'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/World'
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps/battlefield-paintball-gods-pitdevil')
R23=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
def main(slug='battlefield-paintball-gods-pitdevil'):
 SOURCE=globals()['SOURCE'].parent/slug
 OUT=globals()['OUT'] if slug=='battlefield-paintball-gods-pitdevil' else ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/slug
 OUT.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
 src=SOURCE/'source/map.glb'; raw=src.read_bytes(); sha=hashlib.sha256(raw).hexdigest()
 sem=json.loads((R23/'assets/data/map_semantics_r14.json').read_text())['maps'][slug]
 assert sha==sem['source_sha256']
 n=struct.unpack_from('<I',raw,12)[0]; d=json.loads(raw[20:20+n]); binary=memoryview(raw)[28+n:]
 prov=json.loads((SOURCE/'source/provenance.json').read_text()); meta=json.loads((SOURCE/'converted/manifest.json').read_text())
 def acc(i):
  a=d['accessors'][i]; v=d['bufferViews'][a['bufferView']]; assert 'sparse' not in a
  dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]); c={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
  x=np.ndarray((a['count'],c),dt,buffer=binary,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',c*dt.itemsize),dt.itemsize)).astype('float64')
  if a.get('normalized'): x=np.maximum(-1,x/np.iinfo(dt).max)
  assert np.isfinite(x).all(); return x
 def local(node):
  if 'matrix' in node:return np.array(node['matrix']).reshape(4,4,order='F')
  x,y,z,w=node.get('rotation',[0,0,0,1]); m=np.eye(4)
  m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(node.get('scale',[1,1,1])); m[:3,3]=node.get('translation',[0,0,0]);return m
 materials=[]; ids={}; batches={}; counts={}; triangles=[]; align=[]; missing=[]; texture_hashes={}; semantic_removed=0; zero_normals_repaired=0
 selectors={}
 for category in ['marker_parts','decorative_collision_parts']:
  for row in sem.get(category,[]):selectors.setdefault(row['key'],[]).append(row)
 # Exact source foliage classification; marker collision follows the same original selectors on both maps.
 leaf_keys=set()
 if slug=='alfa-aglae-souza':
  for key,row in prov['materials'].items():
   tex=row.get('c','').lower()
   if row.get('alpha') and not row.get('fx') and (tex=='tex/s/demo-gamemap-2-batched/s4_c.png' or any(word in Path(tex).name for word in ['leaves','leaf','foliage'])):leaf_keys.add(key)
 def mat(key):
  if key in ids:return ids[key]
  row=prov['materials'][key]; r=dict(name=key,color='',normal='',metallic=.35 if row.get('metal') else 0,roughness=.72,alpha=bool(row.get('alpha',False)))
  for field,target in [('c','color'),('n','normal')]:
   if field not in row:continue
   p=SOURCE.parents[1]/'shared'/row[field]
   if not p.exists(): missing.append(str(p));continue
   dest=key+'_'+field+p.suffix;shutil.copyfile(p,OUT/dest);r[target]=dest;texture_hashes[dest]={'source':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
  ids[key]=len(materials);materials.append(r);return ids[key]
 def walk(i,parent):
  nonlocal semantic_removed,zero_normals_repaired
  node=d['nodes'][i]; m=parent@local(node); name=node.get('name',''); match=re.match(r'^(world|roof)_(-?\d+)_(-?\d+)_(.+)$',name)
  if 'mesh' in node:
   kind='structure' if match else name.split('_primitive')[0]
   counts[kind]=counts.get(kind,0)+1
   # Boundary is invisible solid; playfield is metadata, exactly as R23 converter.
   assert match or kind in ['boundary','playfield','false','killzone','trigger'], name
   if match or kind in ['boundary','false']:
    key=match[4] if match else 'vc'; mid=mat(key)
    for p in d['meshes'][node['mesh']]['primitives']:
     assert p.get('mode',4)==4; a=p['attributes']; pos=acc(a['POSITION']); idx=acc(p['indices']).astype(np.int64).ravel() if 'indices' in p else np.arange(len(pos))
     xyz=pos@m[:3,:3].T+m[:3,3]; xyz[:,2]*=-1 # only handedness conversion, no 4800 campaign offset
     norm=acc(a['NORMAL'])@np.linalg.inv(m[:3,:3]); norm[:,2]*=-1; norm/=np.maximum(np.linalg.norm(norm,axis=1)[:,None],1e-20)
     uv=acc(a['TEXCOORD_0']) if 'TEXCOORD_0' in a else np.zeros((len(pos),2)); uv[:,1]=1-uv[:,1]
     col=acc(a['COLOR_0']) if 'COLOR_0' in a else np.ones((len(pos),4))
     if col.shape[1]==3:col=np.column_stack((col,np.ones(len(col))))
     # glTF CCW + one reflection is Unity CW; only negative source transforms need another swap.
     if np.linalg.det(m[:3,:3])<0:idx=idx.reshape(-1,3)[:,[0,2,1]].ravel()
     ts=xyz[idx].reshape(-1,3,3); cross=np.cross(ts[:,1]-ts[:,0],ts[:,2]-ts[:,0]); dot=(cross*norm[idx].reshape(-1,3,3).mean(axis=1)).sum(axis=1);align.extend(dot[np.abs(dot)>1e-8].tolist())
     if slug=='alfa-aglae-souza':
      bad=np.linalg.norm(norm,axis=1)<.5
      if bad.any():
       geometric=np.zeros_like(norm)
       for corner in range(3):np.add.at(geometric,idx.reshape(-1,3)[:,corner],-cross)
       lengths=np.linalg.norm(geometric,axis=1);geometric/=np.maximum(lengths[:,None],1e-20);geometric[lengths<1e-12]=[0,1,0]
       norm[bad]=geometric[bad];zero_normals_repaired+=int(bad.sum())
     collision_indices=idx;removed=np.zeros(len(ts),bool)
     if key in selectors:
      tmin=ts.min(axis=1);tmax=ts.max(axis=1);meshmin=xyz.min(axis=0);meshmax=xyz.max(axis=0)
      for selector in selectors[key]:
       low=np.array(selector['min'],float);high=np.array(selector['max'],float);low[2],high[2]=-high[2],-low[2]
       if (high<meshmin-.005).any() or (low>meshmax+.005).any():continue
       inside=(tmin>=low-.0045).all(axis=1)&(tmax<=high+.0045).all(axis=1)
       # Hash-bound source selectors identify authoring-symbol geometry. A source
       # component can span several original GLB cells, so apply its exact bounds
       # to each cell rather than requiring the whole component count in one cell.
       removed|=inside
      if removed.any():collision_indices=idx.reshape(-1,3)[~removed].ravel();semantic_removed+=int(removed.sum())
     if match and key not in leaf_keys: triangles.append(xyz[collision_indices].reshape(-1,3,3))
     vertices=np.column_stack((xyz,norm,uv,col)).astype('<f4'); assert np.isfinite(vertices).all()
     cell=(int(match[2])//4,int(match[3])//4) if match else (0,0)
     if removed.any() and key not in leaf_keys:
      batches.setdefault((mid,cell,False,kind!='boundary'),[]).append((vertices,idx))
      if len(collision_indices):batches.setdefault((mid,cell,kind!='false',False),[]).append((vertices,collision_indices))
     else:
      bk=(mid,cell,kind!='false' and key not in leaf_keys,kind!='boundary');batches.setdefault(bk,[]).append((vertices,idx))
  for c in node.get('children',[]):walk(c,m)
 for i in d['scenes'][d.get('scene',0)]['nodes']:walk(i,np.eye(4))
 chunks=[]
 for (mid,cell,collision,visible),parts in batches.items():
  vs=[];ii=[];offset=0
  def flush():
   if not vs:return
   chunks.append((mid,collision,visible,np.concatenate(vs),np.concatenate(ii).astype('<i4')))
  for v,idx in parts:
   if offset+len(v)>60000 and vs:flush();vs=[];ii=[];offset=0
   vs.append(v);ii.append(idx+offset);offset+=len(v)
  flush()
 alltri=np.concatenate(triangles); spawn=np.array(meta['recorded_spawns'][0],float);spawn[2]*=-1
 if sem.get('warmup'):spawn=np.array(sem['warmup'][0]['center'],float);spawn[2]*=-1
 # Find actual supporting source triangles and four footprint samples, not a made-up platform.
 def floor(x,z,y,above=2):
  a,b,c=alltri[:,0],alltri[:,1],alltri[:,2];v0=b[:,[0,2]]-a[:,[0,2]];v1=c[:,[0,2]]-a[:,[0,2]];q=np.array([x,z])-a[:,[0,2]]
  den=v0[:,0]*v1[:,1]-v1[:,0]*v0[:,1];valid=np.abs(den)>1e-9; safe=np.where(valid,den,1)
  u=(q[:,0]*v1[:,1]-v1[:,0]*q[:,1])/safe;v=(v0[:,0]*q[:,1]-q[:,0]*v0[:,1])/safe
  h=a[:,1]+u*(b[:,1]-a[:,1])+v*(c[:,1]-a[:,1]);mask=valid&(u>=-1e-6)&(v>=-1e-6)&(u+v<=1.000001)&(h<=y+above)&(h>=y-5)
  assert mask.any(),(x,z,y);return float(h[mask].max())
 support=[floor(spawn[0]+x,spawn[2]+z,spawn[1]) for x,z in [(0,0),(.3,0),(-.3,0),(0,.3),(0,-.3)]];assert max(support)-min(support)<.3
 spawn[1]=support[0]+.12
 # Vertical headroom at center and footprint: no source triangle crossing standing volume.
 for x,z in [(0,0),(.3,0),(-.3,0),(0,.3),(0,-.3)]:
  # Same floor query from 1.9m above must still hit the floor.
  assert abs(floor(spawn[0]+x,spawn[2]+z,spawn[1]+.05)-support[0])<.3
 # Triangle/AABB SAT: reject a standing 0.5m wide, 1.95m tall spawn volume intersecting source walls.
 boxCenter=spawn+np.array([0,.975,0]); half=np.array([.25,.975,.25]); t=alltri-boxCenter
 candidates=t[(t.min(axis=1)<=half).all(axis=1)&(t.max(axis=1)>=-half).all(axis=1)]
 edges=[candidates[:,1]-candidates[:,0],candidates[:,2]-candidates[:,1],candidates[:,0]-candidates[:,2]]
 axes=[np.broadcast_to(a,(len(candidates),3)) for a in np.eye(3)]
 axes += [np.cross(edges[0],edges[1])]
 axes += [np.cross(e,a) for e in edges for a in np.eye(3)]
 overlap=np.ones(len(candidates),dtype=bool)
 for axis in axes:
  proj=(candidates*axis[:,None,:]).sum(axis=2);radius=(np.abs(axis)*half).sum(axis=1)
  overlap &= (proj.min(axis=1)<=radius+1e-7)&(proj.max(axis=1)>=-radius-1e-7)
 assert not overlap.any(), 'Spawn standing volume intersects structural triangles'
 lo=np.array(meta['bounds_min'] if slug!='alfa-aglae-souza' else meta['solid_min']); hi=np.array(meta['bounds_max'] if slug!='alfa-aglae-souza' else meta['solid_max']); boundsMin=[lo[0],lo[1],-hi[2]];boundsMax=[hi[0],hi[1],-lo[2]]
 manifest=dict(version=1,mapId=meta['map_id'],title=meta['title'],sourceSha256=sha,spawn=spawn.tolist(),boundsMin=boundsMin,boundsMax=boundsMax,materials=materials,chunks=len(chunks))
 if sem.get('warmup'):
  def settled(point):
   p=np.array(point,float);p[2]*=-1;p[1]=floor(p[0],p[2],p[1],1.5)+.12;return p.tolist()
  manifest['warmupSpawn']=spawn.tolist();manifest['gameSpawn']=settled(sem['deathmatch'][0]['center'] if sem.get('deathmatch') else meta['recorded_spawns'][0]);manifest['recordedSpawn']=settled(meta['recorded_spawns'][0])
  # Both choices use actual source support and an empty standing volume.
  for label in ['gameSpawn','recordedSpawn']:
   at=np.array(manifest[label]);sample=[floor(at[0]+x,at[2]+z,at[1],.6) for x,z in [(0,0),(.3,0),(-.3,0),(0,.3),(0,-.3)]]
   assert max(sample)-min(sample)<.3,(label,sample)
   box_lo=at+[-.25,.0,-.25];box_hi=at+[.25,1.95,.25]
   possible=alltri[(alltri.min(axis=1)<=box_hi).all(axis=1)&(alltri.max(axis=1)>=box_lo).all(axis=1)]
   t=possible-(box_lo+box_hi)*.5;h=(box_hi-box_lo)*.5;overlap_choice=np.ones(len(t),bool)
   edges=[t[:,1]-t[:,0],t[:,2]-t[:,1],t[:,0]-t[:,2]];axes=[np.broadcast_to(a,(len(t),3)) for a in np.eye(3)]+[np.cross(edges[0],edges[1])]+[np.cross(e,a) for e in edges for a in np.eye(3)]
   for axis in axes:
    proj=(t*axis[:,None,:]).sum(axis=2);radius=(np.abs(axis)*h).sum(axis=1);overlap_choice&=(proj.min(axis=1)<=radius+1e-7)&(proj.max(axis=1)>=-radius-1e-7)
   assert not overlap_choice.any(),label+' standing volume intersects source geometry'
 (OUT/'map.json').write_text(json.dumps(manifest,indent=2))
 with (OUT/'geometry.bytes').open('wb') as f:
  f.write(struct.pack('<ii',0x52323357,len(chunks)))
  for mid,collision,visible,v,idx in chunks:
   f.write(struct.pack('<iiiii',mid,int(collision),int(visible),len(v),len(idx)));f.write(v.tobytes());f.write(idx.tobytes())
 report=dict(map=meta['title'],baseline=str(R23),source=str(src),sourceSha256=sha,nodes=counts,renderTriangles=sum(len(i)//3 for _,_,vis,_,i in chunks if vis),collisionTriangles=sum(len(i)//3 for _,coll,_,_,i in chunks if coll),chunks=len(chunks),materials=len(materials),missingTextures=missing,spawn=spawn.tolist(),supportHeights=support,boundsMin=boundsMin,boundsMax=boundsMax,unityClockwiseNormalAgreement=float(np.mean(np.array(align)<0)),assetBytes=sum(p.stat().st_size for p in OUT.iterdir() if p.is_file()),limits='Original baseline geometry and provenance materials. R23 prepared destructible/passage/art repairs, fixtures and dynamic scripts are not ported; original panels remain solid. No production asset approval or device/performance claim.')
 assert report['unityClockwiseNormalAgreement']>.99
 assert report['renderTriangles']==meta['solid_triangles']
 report['standingVolumeClearance']={'width':.5,'height':1.95,'intersectingTriangles':int(overlap.sum())}
 report['leafCollisionExcluded']=sorted(leaf_keys);report['sourceSemanticCollisionTrianglesRemoved']=semantic_removed;report['sourceZeroNormalsRepaired']=zero_normals_repaired;report['textureInputs']=texture_hashes
 if slug=='alfa-aglae-souza':report['limits']='Actual source geometry and original converter/provenance material overrides. Source leaf classification removes leaf collision. R23 dynamic destructibles, fixtures and shader art repairs are not ported; no device/performance claim.'
 (DOC/('data-check.json' if slug=='battlefield-paintball-gods-pitdevil' else slug+'-data-check.json')).write_text(json.dumps(report,indent=2)); print(json.dumps({k:v for k,v in report.items() if k!='textureInputs'},indent=2))
if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--map',choices=['paintball','alpha'],default='paintball');args=parser.parse_args();main('alfa-aglae-souza' if args.map=='alpha' else 'battlefield-paintball-gods-pitdevil')
