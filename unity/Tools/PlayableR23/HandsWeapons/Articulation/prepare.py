"""Publish the frozen reference glove skin/poses; leave meshes/materials untouched."""
from pathlib import Path
import base64,hashlib,importlib.util,json,struct,uuid
import numpy as np
T=Path(__file__).resolve().parent;R=T.parents[4]
S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
O=R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
spec=importlib.util.spec_from_file_location('hand_resource',R/'unity/Tools/PlayableR23/Appearance/R6/NeutralPreviewPose/resource.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
manifest=S.parent/'SOURCE-MANIFEST.json'
assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={row['path']:row['sha256'] for row in json.loads(manifest.read_text())['files']};inputs={}
def source(p):
 rel=p.relative_to(S).as_posix();assert sha(p)==pins[rel];inputs[rel]=pins[rel];return p.read_bytes()
def rotation(q):
 x,y,z,w=np.array(q)/np.linalg.norm(q)
 return np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])
def build(name,publish=True):
 directory=S/'assets/hands/xrtools086';key='L' if name=='left' else 'R'
 g=json.loads(source(directory/('Hand_Glove_low_'+key+'.gltf')))
 buffers=[base64.b64decode(b['uri'].split(',')[1]) if b['uri'].startswith('data:') else source(directory/b['uri']) for b in g['buffers']]
 def acc(i):
  a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);c={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
  x=np.ndarray((a['count'],c),dt,buffers[v['buffer']],offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dt.itemsize*c),dt.itemsize)).copy()
  return x/np.iinfo(dt).max if a.get('normalized') else x
 parent={c:i for i,n in enumerate(g['nodes']) for c in n.get('children',[])}
 ordered=[]
 def add(i):
  if i in ordered:return
  if i in parent:add(parent[i])
  ordered.append(i)
 for i in range(len(g['nodes'])):add(i)
 remap={old:new for new,old in enumerate(ordered)}
 poses={}
 for label in ['Grip 5','Grip','Grip 1','Grip Shaft']:
  p=module.Resource(source(directory/(name+'_'+label+'.res'))).resources[-1]['props'];tracks={}
  for i in range(64):
   prefix='tracks/'+str(i)
   if prefix+'/path' not in p:break
   assert p[prefix+'/type']=='rotation_3d';keys=struct.unpack('<6f',p[prefix+'/keys']);assert keys[0]==0
   tracks[p[prefix+'/path']['nodepath'][-1]]=list(keys[2:])
  assert len(tracks)==19;poses[label]=tracks
 nodes=[];worlds=[]
 for i in ordered:
  n=g['nodes'][i];assert 'matrix' not in n
  label=n.get('name','');q=n.get('rotation',[0,0,0,1]);position=n.get('translation',[0,0,0]);scale=n.get('scale',[1,1,1]);par=remap[parent[i]] if i in parent else -1
  local=np.eye(4);local[:3,:3]=rotation(q)@np.diag(scale);local[:3,3]=position;worlds.append(worlds[par]@local if par>=0 else local)
  nodes.append(dict(name=label,parent=par,position=position,scale=scale,rotation=q,animated=label in poses['Grip'],index=label.startswith('Index_'),opened=poses['Grip 5'].get(label,q),grip=poses['Grip'].get(label,q),pistol=poses['Grip 1'].get(label,q),shaft=poses['Grip Shaft'].get(label,q)))
 wrist=next(i for i,n in enumerate(nodes) if n['name']=='Wrist_'+key);knuckle=next(i for i,n in enumerate(nodes) if n['name']=='Middle_Proximal_'+key)
 angle=-np.pi/2 if name=='left' else np.pi/2;c,s=np.cos(angle),np.sin(angle);bake=np.eye(4);bake[:3,:3]=[[c,-s,0],[s,c,0],[0,0,1]];bake[:3,3]=[0,.032,0]-bake[:3,:3]@((worlds[wrist][:3,3]+worlds[knuckle][:3,3])*.5);bake[2]*=-1
 assert not nodes[wrist]['animated']
 assert len(g['skins'])==1;skin=g['skins'][0];bind=acc(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
 joints=[dict(node=remap[j],inverseBind=m.reshape(-1).tolist()) for j,m in zip(skin['joints'],bind)]
 parts=[];rest=[]
 for n in g['nodes']:
  if 'mesh' not in n:continue
  assert n['skin']==0
  for primitive in g['meshes'][n['mesh']]['primitives']:
   a=primitive['attributes'];v=acc(a['POSITION']);normal=acc(a['NORMAL']);indices=acc(a['JOINTS_0']).astype(int);weights=acc(a['WEIGHTS_0'])
   assert len(v)==812 and indices.max()<len(joints) and weights.min()>=0 and np.max(abs(weights.sum(1)-1))<2e-6
   mats=np.array([worlds[j['node']]@m for j,m in zip(joints,bind)]);blend=(mats[indices]*weights[:,:,None,None]).sum(1)
   posed=np.einsum('nij,nj->ni',blend,np.c_[v,np.ones(len(v))]);rest.append((posed@bake.T)[:,:3])
   parts.append(dict(vertexCount=len(v),positions=v.reshape(-1).tolist(),normals=normal.reshape(-1).tolist(),jointIndices=indices.reshape(-1).tolist(),weights=weights.reshape(-1).tolist()))
 # Exact existing mesh vertex order/rest transport, no regeneration of its UVs,
 # material slots, indices, mount, wrist endpoints or skin textures.
 raw=(O/(name+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);at=8;max_error=0
 assert magic==0x5232334d and count==len(parts)
 for p,v in zip(parts,rest):
  nv,ni,mi=struct.unpack_from('<iii',raw,at);at+=12;actual=np.frombuffer(raw,'<f4',nv*8,at).reshape(nv,8);at+=nv*32+ni*4
  assert nv==p['vertexCount'];max_error=max(max_error,float(np.max(abs(actual[:,:3]-v))));assert max_error<1e-7
 assert at==len(raw)
 data=dict(schema=1,side=name,nodes=nodes,joints=joints,bake=bake.reshape(-1).tolist(),parts=parts,sourceMeshSha256=sha(O/(name+'.meshbin')))
 encoded=(json.dumps(data,separators=(',',':'))+'\n').encode()
 if publish:(O/(name+'-articulation.json')).write_bytes(encoded)
 return data,encoded,dict(side=name,bytes=len(encoded),vertices=sum(p['vertexCount'] for p in parts),animatedBones=sum(n['animated'] for n in nodes),joints=len(joints),restMaximumError=max_error,sha256=hashlib.sha256(encoded).hexdigest())
if __name__=='__main__':
 source(S/'scripts/grip_hand_086.gd');rows=[build(side)[2] for side in ['left','right']]
 for p in [O/(side+'-articulation.json') for side in ['left','right']]+[R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'/name for name in ['R23GlovePoseRules.cs','R23GloveArticulation.cs']]:
  meta=p.with_name(p.name+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-unity/'+p.relative_to(R/'unity').as_posix()).hex+'\n')
 (T/'source-pins.json').write_text(json.dumps(dict(sourceManifestSha256=sha(manifest),inputs=inputs,hands=rows),indent=2)+'\n');print(json.dumps(rows,indent=2))
