from pathlib import Path
import hashlib,importlib.util,json,math,struct,wave
import numpy as np
T=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('launch_source_recipe',T/'prepare.py');recipe=importlib.util.module_from_spec(spec);spec.loader.exec_module(recipe)
O=recipe.O;S=recipe.S;n=0
def need(value,why):
 global n;n+=1;assert value,why
report=json.loads((O/'source-pins.json').read_text());manifest=S.parent/'SOURCE-MANIFEST.json';need(hashlib.sha256(manifest.read_bytes()).hexdigest()==report['manifestSha256'],'frozen R6 exact manifest')
for name,digest in report['inputs'].items():need(hashlib.sha256((S/name).read_bytes()).hexdigest()==digest,'actual frozen input '+name)
hardware=json.loads((O/'launch-hardware.json').read_text());need(hardware['palette']==['26343f','c53b40','b8c4c6','e8bb52','101b24','68dbe2','465b68','79292e'],'all actual source palette routes');need(hardware['metallic']==.5 and hardware['roughness']==.76 and hardware['emissionIndex']==5 and hardware['emissionEnergy']==.7,'actual source material recipe')
for kind,part_count,tris,solid in [('deck',254,3048,101),('cabin',71,884,27)]:
 parts=recipe.allparts[kind];need(len(parts)==part_count and sum(p['solid']for p in parts)==solid,'all actual '+kind+' source parts/solid flags')
 raw=(O/('launch-'+kind+'.meshbin')).read_bytes();magic,surfaces=struct.unpack_from('<ii',raw);need(magic==0x32314c54 and surfaces==len(set(p['color']for p in parts)),'actual complete source material surfaces '+kind);at=8;seen=[];vertices=[]
 for i in range(surfaces):
  material,count=struct.unpack_from('<ii',raw,at);at+=8;v=np.frombuffer(raw,'<f4',count*6,at).reshape(-1,6);at+=count*24;need(material==sorted(set(p['color']for p in parts))[i] and np.isfinite(v).all()and count%3==0,'finite indexed actual material '+kind+str(i));need(np.allclose(np.linalg.norm(v[:,3:],axis=1),1,atol=1e-6),'source unit normals '+kind+str(i))
  expected=np.concatenate([recipe.faces(p)for p in parts if p['color']==material]);normal=np.cross(expected[:,2]-expected[:,0],expected[:,1]-expected[:,0]);normal/=np.linalg.norm(normal,axis=1)[:,None]
  need(np.allclose(v[:,:3],(expected[:,[0,2,1]]@recipe.R).reshape(-1,3),atol=2e-6),'every original part triangle reflected once '+kind+str(i));need(np.allclose(v[:,3:],np.repeat(normal@recipe.R,3,axis=0),atol=1e-6),'authored Godot winding/normal reflection '+kind+str(i));vertices.extend(v[:,:3]);seen.append(count//3)
 need(at==len(raw)and sum(seen)==tris,'complete untruncated source geometry '+kind)
for p,out in zip(recipe.allparts['cabin'],hardware['cabin']):
 need(out['solid']==p['solid'] and np.allclose(out['at'],recipe.R@p['at'])and np.allclose(np.reshape(out['basis'],(3,3)),recipe.R@p['basis']@recipe.R),'exact source cabin collision transform/flag')
 if 'profile'in p:need(len(out['vertices'])==len(p['profile'])*6 and np.allclose(np.array(out['vertices']).reshape(-1,3),np.array([[x,y,z]for x in [-p['thickness']*.5,p['thickness']*.5]for z,y in p['profile']])@recipe.R),'all genuine profile prism hull points, no box surrogate')
 else:need(np.allclose(out['size'],p['size']),'source collision box dimensions')
def hull2(points):
 pts=sorted(set(tuple(v)for v in points))
 def cross(o,a,b):return(a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
 lo=[];hi=[]
 for p in pts:
  while len(lo)>1 and cross(lo[-2],lo[-1],p)<=0:lo.pop()
  lo.append(p)
 for p in reversed(pts):
  while len(hi)>1 and cross(hi[-2],hi[-1],p)<=0:hi.pop()
  hi.append(p)
 return lo[:-1]+hi[:-1]
def inside(part,point):
 q=(np.array(point)-part['at'])@part['basis']
 if 'profile'not in part:return bool(np.all(np.abs(q)<=part['size']*.5+1e-8))
 if abs(q[0])>part['thickness']*.5+1e-8:return False
 poly=hull2(part['profile']);p=[q[2],q[1]]
 return all((b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])>=-1e-8 for a,b in zip(poly,poly[1:]+poly[:1]))
for part in recipe.allparts['cabin']:
 if 'profile'not in part:continue
 poly=part['profile'];area=abs(sum(poly[i,0]*poly[(i+1)%len(poly),1]-poly[(i+1)%len(poly),0]*poly[i,1]for i in range(len(poly))))*.5
 triangles=recipe.triangulate(poly);cap=0
 for a,b,c in triangles:
  u=poly[b]-poly[a];v=poly[c]-poly[a];cross=u[0]*v[1]-u[1]*v[0];need(cross>0,'Godot Geometry2D alwaysCCW output cap orientation');cap+=cross*.5
 need(abs(cap-area)<1e-8,'all actual profile cap area preserved independently of triangulation')
 f=recipe.faces(part);surface=sum(np.linalg.norm(np.cross(tri[1]-tri[0],tri[2]-tri[0]))*.5 for tri in f);perimeter=sum(np.linalg.norm(poly[(i+1)%len(poly)]-poly[i])for i in range(len(poly)))
 need(abs(surface-(area*2+perimeter*part['thickness']))<1e-8,'actual closed panel surface area preserved; no fill or omission')
def occupied(kind,point):return any(p['solid']and inside(p,point)for p in recipe.allparts[kind])
for side in [-1,1]:
 need(not occupied('cabin',(side*1.035,1.55,0)),'genuine source side window remains empty')
 need(occupied('cabin',(side*1.035,.5,0)),'source lower shell remains solid')
 need(not occupied('deck',(side*24,.8,-4.5))and not occupied('deck',(side*24,.8,1)),'actual source side exit gaps stay open')
 need(not occupied('deck',(side*13.5,.8,8.73)),'actual source rear deck exit gap stays open')
need(not occupied('cabin',(0,1.2,1.4)),'actual source rear ejection route remains open')
for cue in ['pod_launch','pod_engine','pod_land','pod_exit']:
 need((O/(cue+'.wav')).read_bytes()==(S/'assets/audio/sfx/r18'/(cue+'.wav')).read_bytes(),'byte-identical original '+cue)
 with wave.open(str(O/(cue+'.wav')))as audio:need(audio.getnchannels()==1 and audio.getnframes()>0,'actual original mono source waveform '+cue)
need(sum(p.stat().st_size for p in O.iterdir())<1024*1024,'bounded genuine hardware/audio payload under1MiB')
(T/'asset-results.json').write_text(json.dumps(dict(checks=n,geometry=recipe.summary,bytes=sum(p.stat().st_size for p in O.iterdir()),native=False,engine=False,render=False),indent=2)+'\n')
print('PASS',n,'frozen-source full geometry/collision/openings/palette/original audio bindings; native/render unrun')
