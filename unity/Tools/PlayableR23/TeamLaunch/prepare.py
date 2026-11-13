from pathlib import Path
import hashlib,json,math,re,shutil,struct
import numpy as np

T=Path(__file__).resolve().parent
S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
O=T/'Payload';O.mkdir(exist_ok=True)
M=S.parent/'SOURCE-MANIFEST.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(M)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={x['path']:x['sha256']for x in json.loads(M.read_text())['files']};inputs={}
def source(name):
 p=S/name;assert sha(p)==pins[name],name;inputs[name]=sha(p);return p
for name in ['team_launch_r12','launch_art_r12','net_session1000','net_bridge1000']:
 source('scripts/'+name+'.gd')
text=(S/'scripts/launch_art_r12.gd').read_text()
class Vector:
 def __call__(self,*a):return np.array(a,dtype=float)
V=Vector();V.RIGHT=np.array([1.,0,0]);V.UP=np.array([0.,1,0]);V.FORWARD=np.array([0.,0,-1]);V.BACK=-V.FORWARD
class Basis:
 IDENTITY=np.eye(3)
 def __new__(cls,axis,angle):
  a=np.array(axis)/np.linalg.norm(axis);x,y,z=a;k=np.array([[0,-z,y],[z,0,-x],[-y,x,0]])
  return np.eye(3)*math.cos(angle)+(1-math.cos(angle))*np.outer(a,a)+k*math.sin(angle)
def looking(direction,up):
 z=-direction/np.linalg.norm(direction);x=np.cross(up,z);x/=np.linalg.norm(x);return np.column_stack([x,np.cross(z,x),z])
def box(parts,at,size,color,basis=None,solid=True):parts.append(dict(at=at,size=size,color=color,basis=np.eye(3)if basis is None else basis,solid=solid))
def beam(parts,a,b,width,color,solid=True):
 direction=b-a;axis=V.RIGHT if abs(np.dot(direction/np.linalg.norm(direction),V.UP))>.98 else V.UP
 box(parts,(a+b)*.5,V(width,width,np.linalg.norm(direction)),color,looking(direction,axis),solid)
def profile(parts,at,outline,thickness,color,solid=True):parts.append(dict(at=at,profile=np.array(outline),thickness=thickness,color=color,basis=np.eye(3),solid=solid))
def run(function):
 body=text.split('static func '+function+'()->Array:\n')[1].split('\nstatic func ')[0]
 code=[]
 for line in body.splitlines():
  if not line.strip()or line.lstrip().startswith('#'):continue
  line=re.sub(r'\bvar (\w+)(?::\w+)?(?:\s*:?=)',r'\1=',line)
  line=re.sub(r'for (\w+) in (\d+):',r'for \1 in range(\2):',line)
  line=line.replace('return p','RESULT=p').replace('true','True').replace('false','False')
  line=line.replace(';var ',';').replace('; var ',';')
  code.append(line[1:])
 env=dict(Vector3=V,Vector2=V,Basis=Basis,box=box,beam=beam,profile=profile,absf=abs)
 exec('\n'.join(code),env)
 return env['RESULT']
R=np.diag([1.,1.,-1.])
def triangulate(poly):
 signed=sum(poly[i,0]*poly[(i+1)%len(poly),1]-poly[(i+1)%len(poly),0]*poly[i,1]for i in range(len(poly)))
 ids=list(range(len(poly)));out=[];sign=1 if signed>0 else -1
 def cross(a,b,c):
  u=b-a;v=c-a;return float(u[0]*v[1]-u[1]*v[0])
 while len(ids)>3:
  for j in range(len(ids)):
   a,b,c=ids[j-1],ids[j],ids[(j+1)%len(ids)]
   if cross(poly[a],poly[b],poly[c])*sign<=1e-10:continue
   if any(all(cross(poly[u],poly[v],poly[k])*sign>=-1e-10 for u,v in [(a,b),(b,c),(c,a)])for k in ids if k not in [a,b,c]):continue
   out.append([a,b,c]);ids.pop(j);break
  else:raise ValueError('source profile ear clipping failed')
 out.append(ids);return out if signed>0 else [list(reversed(t))for t in out]
def faces(part):
 if 'profile'in part:
  poly=part['profile'];n=len(poly);v=np.array([[x,y,z]for x in [-part['thickness']*.5,part['thickness']*.5]for z,y in poly]);tri=[]
  for a,b,c in triangulate(poly):tri.extend([[a,b,c],[n+c,n+b,n+a]])
  for i in range(n):j=(i+1)%n;tri.extend([[i,i+n,j+n],[i,j+n,j]])
  f=v[np.array(tri)[:,[2,1,0]]]
 else:
  f=[];h=part['size']*.5
  for axis in range(3):
   for sign in [-1,1]:
    u=(axis+1)%3;w=(axis+2)%3;q=[]
    for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]:
     p=np.zeros(3);p[axis]=h[axis]*sign;p[u]=h[u]*a;p[w]=h[w]*b;q.append(p)
    for t in ([[0,2,1],[0,3,2]]if sign>0 else[[0,1,2],[0,2,3]]):f.append(np.array(q)[t])
  f=np.array(f)
 return f@part['basis'].T+part['at']
def collision(part):
 out=dict(at=(R@part['at']).tolist(),basis=(R@part['basis']@R).reshape(-1).tolist(),solid=part['solid'])
 if 'profile'in part:out['vertices']=(np.array([[x,y,z]for x in [-part['thickness']*.5,part['thickness']*.5]for z,y in part['profile']])@R).reshape(-1).tolist()
 else:out['size']=part['size'].tolist()
 return out
summary={};allparts={}
for name,function in [('cabin','cabin_parts_r14'),('deck','deck_parts_r14')]:
 parts=run(function);allparts[name]=parts;surfaces=[]
 for color in range(8):
  blocks=[faces(p)for p in parts if p['color']==color]
  if not blocks:continue
  f=np.concatenate(blocks);n=np.cross(f[:,2]-f[:,0],f[:,1]-f[:,0]);n/=np.linalg.norm(n,axis=1)[:,None]
  # Godot CW normals use e2 x e1. Reflect position/normal once; reverse
  # source triangle order for Unity's CW-facing convention, matching other ports.
  f=f[:,[0,2,1]]@R;n=n@R;v=f.reshape(-1,3);normal=np.repeat(n,3,axis=0)
  surfaces.append((color,np.column_stack([v,normal]).astype('<f4')))
 with (O/('launch-'+name+'.meshbin')).open('wb')as out:
  out.write(struct.pack('<ii',0x32314c54,len(surfaces)))
  for color,v in surfaces:out.write(struct.pack('<ii',color,len(v)));out.write(v.tobytes())
 summary[name]=dict(parts=len(parts),triangles=sum(len(v)//3 for _,v in surfaces),solidParts=sum(p['solid']for p in parts))
palette=['26343f','c53b40','b8c4c6','e8bb52','101b24','68dbe2','465b68','79292e']
hardware=dict(version=1,palette=palette,metallic=.5,roughness=.76,emissionIndex=5,emissionEnergy=.7,cabin=[collision(p)for p in allparts['cabin']],summary=summary)
(O/'launch-hardware.json').write_text(json.dumps(hardware,separators=(',',':'))+'\n')
for cue in ['pod_launch','pod_engine','pod_land','pod_exit']:shutil.copyfile(source('assets/audio/sfx/r18/'+cue+'.wav'),O/(cue+'.wav'))
(O/'SOURCE.txt').write_text('Original frozen R6 launch_art_r12.gd procedural deck/cabin and original assets/audio/sfx/r18 pod WAVs. No substitute model, image, synthesized cue or external licensed asset was introduced. Audio copied byte-identically. No engine/render/native acceptance claimed.\n')
(O/'source-pins.json').write_text(json.dumps(dict(manifestSha256=sha(M),contentId='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520',inputs=inputs,geometry=summary),indent=2)+'\n')
print(summary,'payload bytes',sum(p.stat().st_size for p in O.iterdir()))
