# Independent scalar oracle: extract source RSRC key quaternion from each track,
# apply source shortest-path slerp closure and SRS, retain source glTF translation/scale.
# Production C# decoder/math are not imported.
from pathlib import Path
import re,struct,json,math
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/AvatarR4'
for side,letter in [('left','L'),('right','R')]:
 b=(S/(side+'_Grip Shaft.res')).read_bytes();p=106;names=[]
 for _ in range(struct.unpack_from('<I',b,102)[0]):
  n=struct.unpack_from('<I',b,p)[0];p+=4;names.append(b[p:p+n-1].decode());p+=n
 def tracks(pose):
  raw=(S/(side+'_'+pose+'.res')).read_bytes();out={};offset=106;table=[]
  for _ in range(struct.unpack_from('<I',raw,102)[0]):
   length=struct.unpack_from('<I',raw,offset)[0];offset+=4;table.append(raw[offset:offset+length-1].decode());offset+=length
  # independent locate key arrays by packed-float tag+6 and single time0/transition1,
  # then bind preceding NodePath subname string table index.
  needle=struct.pack('<IIff',33,6,0,1)
  for m in re.finditer(re.escape(needle),raw):
   start=m.start();bone=struct.unpack_from('<I',raw,start-32)[0];out[table[bone]]=struct.unpack_from('<4f',raw,start+16)
  assert len(out)==19
  return out
 opened=tracks('Grip 5');closed=tracks('Grip Shaft');doc=json.loads((S/(letter+'.gltf')).read_bytes())
 results=[]
 for i,node in enumerate(doc['nodes']):
  name=node['name']
  if name not in closed:continue
  a=opened[name];c=closed[name];t=.86 if name.startswith('Index_') else 1.12
  dot=sum(x*y for x,y in zip(a,c));sign=-1 if dot<0 else 1;dot=min(1,abs(dot))
  if dot>.9995:x,y=1-t,t
  else:angle=math.acos(dot);x=math.sin((1-t)*angle)/math.sin(angle);y=math.sin(t*angle)/math.sin(angle)
  q=[x*aa+y*sign*cc for aa,cc in zip(a,c)];norm=math.sqrt(sum(v*v for v in q));x,y,z,w=[v/norm for v in q];x=-x;y=-y
  rot=[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]
  scale=node.get('scale',[1,1,1]);matrix=[0.0]*16;matrix[15]=1
  for r in range(3):
   for c in range(3):matrix[r*4+c]=rot[r*3+c]*scale[c]
  trans=node.get('translation',[0,0,0]);matrix[3],matrix[7],matrix[11]=trans[0],trans[1],-trans[2]
  results.append((i,matrix))
 with (S/(letter+'-source-grip-oracle.bin')).open('wb') as f:
  f.write(struct.pack('<i',len(results)))
  for i,m in results:f.write(struct.pack('<i16f',i,*m))
 print(side,len(results),'source shaft matrices independently extracted')
