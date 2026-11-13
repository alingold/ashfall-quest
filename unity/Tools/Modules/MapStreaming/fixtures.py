"""Tiny owned GLBs cover transform/layout cases absent from the actual maps."""
import json,struct,math
import inventory as inv
base=inv.OUT
paths=[]
for it,fmt in [(5121,'B'),(5123,'H'),(5125,'I')]:
 for nt,nfmt,normal in [(5120,'b',127),(5122,'h',32767),(5126,'f',1)]:
  blob=bytearray();views=[];accessors=[]
  def add(raw,ctype,kind,count,stride=None,normalized=False):
   while len(blob)%4:blob.append(0)
   off=len(blob);blob.extend(raw);v=dict(buffer=0,byteOffset=off,byteLength=len(raw))
   if stride:v['byteStride']=stride
   views.append(v);a=dict(bufferView=len(views)-1,componentType=ctype,type=kind,count=count)
   if normalized:a['normalized']=True
   accessors.append(a);return len(accessors)-1
  # Interleaved positions include padding sentinel; face is asymmetric and not origin centered.
  pos=add(b''.join(struct.pack('<ffff',*p,999) for p in [(1,2,3),(4,2,3),(1,7,3)]),5126,'VEC3',3,16)
  norm=add(struct.pack('<'+nfmt*9,*([0,0,normal]*3)),nt,'VEC3',3,normalized=nt!=5126)
  uv=add(bytes([0,0,255,0,0,255]),5121,'VEC2',3,normalized=True)
  idx=add(struct.pack('<'+fmt*3,2,0,1),it,'SCALAR',3)
  d=dict(asset=dict(version='2.0'),scene=0,scenes=[dict(nodes=[0])],nodes=[dict(translation=[5,-3,9],rotation=[0,math.sin(.37),0,math.cos(.37)],scale=[-2,3,.5],children=[1]),dict(mesh=0,matrix=[1,0,0,0,0,1,0,0,0,0,1,0,2,4,6,1])],meshes=[dict(primitives=[dict(attributes=dict(POSITION=pos,NORMAL=norm,TEXCOORD_0=uv),indices=idx)])],buffers=[dict(byteLength=len(blob))],bufferViews=views,accessors=accessors)
  raw=json.dumps(d,separators=(',',':')).encode();raw+=b' '*((-len(raw))%4);blob.extend(b'\0'*((-len(blob))%4));glb=struct.pack('<III',0x46546c67,2,28+len(raw)+len(blob))+struct.pack('<II',len(raw),0x4e4f534a)+raw+struct.pack('<II',len(blob),0x004e4942)+blob
  path=base/f'fixture-{it}-{nt}.glb';path.write_bytes(glb)
  inv.generate(path,f'fixture:{path.stem}',inv.digest(path),dict(indoor=True,capsules=False,collisionRole='UNKNOWN',playableApproval=False));paths.append(str(path.with_suffix('.mapref.json')))
inv.write(inv.DOC/'fixture-paths.json',paths)
print('Generated',len(paths),'tiny owned analytic fixtures')
