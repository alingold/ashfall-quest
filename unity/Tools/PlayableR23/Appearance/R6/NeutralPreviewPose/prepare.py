from pathlib import Path
import base64,hashlib,json,struct
import numpy as np
from resource import Resource
T=Path(__file__).resolve().parent;R=T.parents[5];S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');O=R/'unity/Assets/StreamingAssets/PlayableR23/Appearance';manifest=S.parent/'SOURCE-MANIFEST.json';sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={x['path']:x['sha256']for x in json.loads(manifest.read_text())['files']};inputs={}
def source(p):
 p=Path(p);h=sha(p);assert h==pins[p.relative_to(S).as_posix()];inputs[p.relative_to(S).as_posix()]=h;return p.read_bytes()
def rot(q):
 q=np.asarray(q,float);q=q/np.linalg.norm(q);x,y,z,w=q;return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def rz(a):return np.array([[np.cos(a),-np.sin(a),0],[np.sin(a),np.cos(a),0],[0,0,1.]])
def from_to(a,b):
 a=a/np.linalg.norm(a);b=b/np.linalg.norm(b);c=float(a@b);assert c>-.999999;k=np.cross(a,b);x,y,z=k;K=np.array([[0,-z,y],[z,0,-x],[-y,x,0]]);return np.eye(3)+K+K@K/(1+c)
for rel in ['scripts/grip_hand_086.gd','scripts/gemini_player_models_099.gd','scripts/lobby_map_091.gd']:source(S/rel)
rows=[]
def build(side,publish=True):
 key='L'if side<0 else'R';name='left'if side<0 else'right';directory=S/'assets/hands/xrtools086';g=json.loads(source(directory/('Hand_Glove_low_'+key+'.gltf')));buffers=[]
 for b in g['buffers']:
  if b['uri'].startswith('data:'):buffers.append(base64.b64decode(b['uri'].split(',')[1]))
  else:buffers.append(source(directory/b['uri']))
 def acc(index):
  a=g['accessors'][index];v=g['bufferViews'][a['bufferView']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);c={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];x=np.ndarray((a['count'],c),dt,buffers[v['buffer']],offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dt.itemsize*c),dt.itemsize)).copy();return x/np.iinfo(dt).max if a.get('normalized')else x
 parents={c:i for i,n in enumerate(g['nodes'])for c in n.get('children',[])}
 def worlds(poses):
  cache={}
  def world(i):
   if i in cache:return cache[i]
   n=g['nodes'][i];assert'matrix'not in n;m=np.eye(4);m[:3,:3]=rot(poses.get(n.get('name'),n.get('rotation',[0,0,0,1])))@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0]);cache[i]=(world(parents[i])@m)if i in parents else m;return cache[i]
  return [world(i)for i in range(len(g['nodes']))]
 def tracks(kind):
  p=Resource(source(directory/(name+'_'+kind+'.res'))).resources[-1]['props'];result={}
  for i in range(64):
   prefix='tracks/'+str(i)
   if prefix+'/path'not in p:break
   assert p[prefix+'/type']=='rotation_3d';keys=struct.unpack('<6f',p[prefix+'/keys']);assert keys[0]==0;result[p[prefix+'/path']['nodepath'][-1]]=np.array(keys[2:])
  return result
 opened=tracks('Grip 5');closed=tracks('Grip');rest=worlds({});pose=worlds({bone:opened.get(bone,next(n for n in g['nodes']if n.get('name')==bone).get('rotation',[0,0,0,1]))for bone in closed});node=lambda name:next(i for i,n in enumerate(g['nodes'])if n.get('name')==name);wrist=node('Wrist_'+key);knuckle=node('Middle_Proximal_'+key);B=rz(side*np.pi/2);t=np.array([0,.032,0])-B@((rest[wrist][:3,3]+rest[knuckle][:3,3])*.5);B4=np.eye(4);B4[:3,:3]=B;B4[:3,3]=t;F=np.diag([1.,1.,-1.]);w=B@pose[wrist][:3,3]+t;k=B@pose[knuckle][:3,3]+t;canonical=rz(-side*.2);relaxed=np.array([0.,-1.,-.08]);relaxed/=np.linalg.norm(relaxed);G=from_to(canonical@(k-w),relaxed)@canonical;position=np.array([side*.26,.97,-.04])-G@w;U=F@G@F;uposition=F@position
 # Source seed0..7 has canonical mount; ExtraKits8..114 have relaxed wrist.
 seed=F@canonical@F;seedposition=F@np.array([side*.26,.94,-.04]);parts=[];weight_error=0;max_rotation=0
 for bone,q in opened.items():
  restq=np.array(next(n for n in g['nodes']if n.get('name')==bone).get('rotation',[0,0,0,1]));max_rotation=max(max_rotation,float(2*np.arccos(min(1,abs(q@restq/np.linalg.norm(q)/np.linalg.norm(restq))))*180/np.pi))
 for n in g['nodes']:
  if'mesh'not in n:continue
  for p in g['meshes'][n['mesh']]['primitives']:
   a=p['attributes'];assert'COLOR_0'not in a;vertices=acc(a['POSITION']);normal=acc(a['NORMAL']);uv=acc(a['TEXCOORD_0']);joints=acc(a['JOINTS_0']).astype(int);weights=acc(a['WEIGHTS_0']);skin=g['skins'][n['skin']];ib=acc(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1);assert len(skin['joints'])==26 and joints.max()<26;weight_error=max(weight_error,float(np.max(np.abs(weights.sum(1)-1))));assert weight_error<2e-6 and weights.min()>=0
   mats=np.array([pose[j]@bind for j,bind in zip(skin['joints'],ib)]);blend=(mats[joints]*weights[:,:,None,None]).sum(1);posed=np.einsum('nij,nj->ni',blend,np.c_[vertices,np.ones(len(vertices))])[:,:3];posed_normal=np.einsum('nij,nj->ni',blend[:,:3,:3],normal);v=(posed@B.T+t)@F;nrm=posed_normal@B.T@F;nrm/=np.linalg.norm(nrm,axis=1)[:,None];uv[:,1]=1-uv[:,1];indices=acc(p['indices']).astype('<i4').reshape(-1,3)[:,[0,2,1]];assert np.isfinite(v).all()and np.isfinite(nrm).all();parts.append((np.c_[v,nrm,uv].astype('<f4'),indices.astype('<i4')))
 raw=bytearray(struct.pack('<ii',0x5232334d,len(parts)))
 for v,indices in parts:raw.extend(struct.pack('<iii',len(v),indices.size,0));raw.extend(v.tobytes());raw.extend(indices.tobytes())
 file='preview-menu-'+name+'.meshbin';
 if publish:(O/file).write_bytes(raw)
 # Independent direct-composition oracle: world source == Unity runtime mount.
 for v,indices in parts:
  actual=v[:,:3]@U.T+uposition;source_world=(v[:,:3]@F@G.T+position)@F;assert np.allclose(actual,source_world,atol=1e-7)
 assert np.linalg.norm(uposition+U@(F@w)-F@np.array([side*.26,.97,-.04]))<1e-12
 assert np.dot(U@(F@(k-w))/np.linalg.norm(k-w),F@relaxed)>1-1e-12
 row=dict(side=name,file=file,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest(),vertices=sum(len(v)for v,_ in parts),triangles=sum(i.size//3 for _,i in parts),weightSumMaxError=weight_error,maxOpenVsRestDegrees=max_rotation,bakedWrist=(F@w).tolist(),bakedKnuckle=(F@k).tolist(),relaxedPosition=uposition.tolist(),relaxedForward=U[:,2].tolist(),relaxedUp=U[:,1].tolist(),canonicalPosition=seedposition.tolist(),canonicalForward=seed[:,2].tolist(),canonicalUp=seed[:,1].tolist(),openPoseBoneCount=len(opened),sourceWrist=(F@np.array([side*.26,.97,-.04])).tolist())
 return (row,bytes(raw))if not publish else row
if __name__=='__main__':
 rows=[build(-1),build(1)];receipt=dict(sourceManifestSha256=sha(manifest),inputs=inputs,hands=rows,normalContract='Godot Compatibility skeleton.glsl blended bone matrix normal direction, normalized, skin basis then one Z reflection; no bind-rest gameplay mesh replacement.',policy='Seed0..7 canonical; all ExtraKits8..114 source relaxed pose; actual Grip5 skeleton pose, no controller/gameplay/bore changes.');(T/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(rows,indent=2))
