from pathlib import Path
import json,hashlib,re,struct,subprocess
import numpy as np
import prepare
T=Path(__file__).resolve().parent;R=T.parents[5];O=R/'unity/Assets/StreamingAssets/PlayableR23/Appearance';S=prepare.S;sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();d=json.loads((T/'receipt.json').read_text());before=json.loads((T.parent/'ColorDomainFix/payload-before.json').read_text());assert all(sha(O/k)==v for k,v in before.items());registry=(S/'scripts/gemini_player_models_099.gd').read_text();keys=json.loads(re.search(r'const STYLE_KEYS := (\[.*?\])',registry).group(1));extra=json.loads(re.search(r'const EXTRA_KITS := (\{.*\})',registry).group(1));assert len(keys)==115 and set(extra)==set(keys[8:]);cases=115
runtime=(R/'unity/Assets/Ashfall/Integration/Playable/Appearance/R23MenuGlovePreview.cs').read_text()
for side,stored in zip([-1,1],d['hands']):
 row,expected=prepare.build(side,publish=False);actual=(O/row['file']).read_bytes();assert expected==actual and row['sha256']==stored['sha256'];assert row['vertices']==812 and row['triangles']==1160 and row['openPoseBoneCount']==19 and 14.4<row['maxOpenVsRestDegrees']<14.5
 raw=memoryview(actual);magic,count=struct.unpack_from('<ii',raw);at=8
 for part in range(count):
  nv,ni,material=struct.unpack_from('<iii',raw,at);at+=12;v=np.frombuffer(raw,'<f4',nv*8,at).reshape(nv,8);at+=nv*32;indices=np.frombuffer(raw,'<i4',ni,at);at+=ni*4;assert np.isfinite(v).all()and np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-6)and indices.min()>=0 and indices.max()<nv;cases+=3
 f=np.array(row['relaxedForward']);u=np.array(row['relaxedUp']);U=np.column_stack([np.cross(u,f),u,f]);w=np.array(row['bakedWrist']);k=np.array(row['bakedKnuckle']);pos=np.array(row['relaxedPosition']);assert np.allclose(U.T@U,np.eye(3),atol=1e-12)and abs(np.linalg.det(U)-1)<1e-12;assert np.allclose(pos+U@w,[side*.26,.97,.04],atol=1e-12);direction=U@(k-w);direction/=np.linalg.norm(direction);assert np.allclose(direction,np.array([0,-1,.08])/np.linalg.norm([0,-1,.08]),atol=1e-12);cases+=3
 # Execute Unity LookRotation's orthonormal forward/up reconstruction on emitted
 # runtime constants, then check palm-side and wrist/knuckle axes (not just down).
 values=re.findall(r'hand.localPosition=new Vector3\(([^)]+)\);hand.localRotation=Quaternion.LookRotation\(new Vector3\(([^)]+)\),new Vector3\(([^)]+)\)\)',runtime);p,f0,u0=[np.array([float(x.removesuffix('f'))for x in text.split(',')])for text in values[0 if side<0 else 1]];f0/=np.linalg.norm(f0);r0=np.cross(u0,f0);r0/=np.linalg.norm(r0);u0=np.cross(f0,r0);actual_basis=np.column_stack([r0,u0,f0]);assert np.allclose(actual_basis,U,atol=2e-8)and np.allclose(p,pos,atol=2e-8);assert np.allclose(actual_basis[:,0],U[:,0],atol=2e-8);cases+=3
 assert row['weightSumMaxError']<2e-6;assert at==len(raw)
# Independent scalar per-influence skin oracle for every original vertex, not
# the vectorized converter's matrix accumulation. Check full UV/index transport.
import base64
from resource import Resource
for side,key,name in [(-1,'L','left'),(1,'R','right')]:
 path=S/'assets/hands/xrtools086'/('Hand_Glove_low_'+key+'.gltf');g=json.loads(path.read_text());buffers=[base64.b64decode(b['uri'].split(',')[1])for b in g['buffers']]
 def read_acc(i):
  a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];data=np.ndarray((a['count'],n),dt,buffers[v['buffer']],offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dt.itemsize*n),dt.itemsize)).copy();return data/np.iinfo(dt).max if a.get('normalized')else data
 anim=Resource((path.parent/(name+'_Grip 5.res')).read_bytes()).resources[-1]['props'];poses={}
 for i in range(19):poses[anim['tracks/'+str(i)+'/path']['nodepath'][-1]]=np.array(struct.unpack('<6f',anim['tracks/'+str(i)+'/keys'])[2:])
 parent={child:i for i,n in enumerate(g['nodes'])for child in n.get('children',[])}
 def world(i,opened):
  n=g['nodes'][i];q=np.array(poses.get(n.get('name'),n.get('rotation',[0,0,0,1]))if opened else n.get('rotation',[0,0,0,1]),dtype=float);q/=np.linalg.norm(q);v=q[:3];w=q[3];x,y,z=v;K=np.array([[0,-z,y],[z,0,-x],[-y,x,0]]);m=np.eye(4);m[:3,:3]=((w*w-v@v)*np.eye(3)+2*np.outer(v,v)+2*w*K)@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0]);return world(parent[i],opened)@m if i in parent else m
 node=lambda label:next(i for i,n in enumerate(g['nodes'])if n.get('name')==label);W=world(node('Wrist_'+key),False)[:3,3];M=world(node('Middle_Proximal_'+key),False)[:3,3];a=side*np.pi/2;B=np.array([[np.cos(a),-np.sin(a),0],[np.sin(a),np.cos(a),0],[0,0,1]]);offset=np.array([0,.032,0])-B@(W+M)*.5;F=np.diag([1,1,-1]);packed=(O/('preview-menu-'+name+'.meshbin')).read_bytes();at=8
 for n in g['nodes']:
  if'mesh'not in n:continue
  skin=g['skins'][n['skin']];bind=read_acc(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1);mats=[world(j,True)@b for j,b in zip(skin['joints'],bind)]
  for primitive in g['meshes'][n['mesh']]['primitives']:
   attrs=primitive['attributes'];v=read_acc(attrs['POSITION']);norm=read_acc(attrs['NORMAL']);weights=read_acc(attrs['WEIGHTS_0']);joints=read_acc(attrs['JOINTS_0']).astype(int);nv,ni,_=struct.unpack_from('<iii',packed,at);at+=12;actual=np.frombuffer(packed,'<f4',nv*8,at).reshape(nv,8);at+=nv*32;indices=np.frombuffer(packed,'<i4',ni,at);at+=ni*4;expected=[];normals=[]
   for vi in range(nv):
    position=np.zeros(4);normal=np.zeros(3)
    for ji,weight in zip(joints[vi],weights[vi]):position+=weight*(mats[ji]@np.r_[v[vi],1]);normal+=weight*(mats[ji][:3,:3]@norm[vi])
    expected.append(F@(B@position[:3]+offset));normal=F@B@normal;normals.append(normal/np.linalg.norm(normal))
   assert np.allclose(actual[:,:3],expected,atol=5e-8)and np.allclose(actual[:,3:6],normals,atol=1e-7)
   uv=read_acc(attrs['TEXCOORD_0']);uv[:,1]=1-uv[:,1];assert np.allclose(actual[:,6:8],uv,atol=1e-7);assert np.array_equal(indices,read_acc(primitive['indices']).astype(int).reshape(-1,3)[:,[0,2,1]].ravel());cases+=4
assert all(sha(S/rel)==h for rel,h in d['inputs'].items());assert (O/'preview-menu-left.meshbin').stat().st_size+(O/'preview-menu-right.meshbin').stat().st_size==79848
print('PASS'+str(cases)+' source policy/matrix/wrist/palm/knuckle/pose-skin/normal cases; all source vertices,UVs,indices and weights recomposed;'+str(len(before))+' original115 payload/meta files preserved;79848 new preview-only bytes.')
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');args=[]
for row in (R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/R23Playable.BuildAndSmoke.rsp').read_text(encoding='utf-8-sig').splitlines():
 a=row.strip().strip('"')
 if a.startswith('/out:'):a='/out:'+str(T/'NeutralPreview.BuildAndSmoke.dll')
 args.append(a)
for file in ['R23MenuGlovePreview.cs','R23AppearanceColorDomain.cs']:
 p=R/'unity/Assets/Ashfall/Integration/Playable/Appearance'/file
 if not any(Path(a).name==file for a in args):args.append(str(p))
rsp=T/'api.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));q=subprocess.run([str(E/'NetCoreRuntime/dotnet.exe'),str(E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/'api.log').write_text(q.stdout+q.stderr);print(q.stdout+q.stderr);assert q.returncode==0;print('PASS preview-only source neutral hand runtime +full installed BuildAndSmoke API; native UNRUN.')
