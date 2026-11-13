"""Original CYBER BIRD avian traffic models. Python standard library only.
Coordinates: metres, Y up, forward -Z; wheel pivots rotate around local X.
All triangle winding is outward CCW, all materials opaque, no external textures.
"""
import json, math, struct, hashlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE if HERE.name == 'traffic_r5' else HERE.parents[2] / 'outputs/CYBER-BIRD-R5-Source/assets/cyber_bird/traffic_r5'
OUT.mkdir(parents=True, exist_ok=True)

PALETTE = [
 ('Paint_Obsidian',(.036,.046,.073),.72,.29,None),
 ('Paint_Plum',(.20,.055,.20),.65,.31,None),
 ('Paint_Ivory',(.68,.73,.69),.5,.34,None),
 ('Paint_Amber',(.80,.26,.045),.65,.32,None),
 ('Metal_Chassis',(.12,.15,.20),.8,.30,None),
 ('Rubber',(.017,.021,.029),.0,.86,None),
 ('Glass_Opaque',(.025,.10,.15),.85,.13,None),
 ('Neon_Cyan',(.04,.8,.9),.4,.26,(.04,2.8,3.6)),
 ('Neon_Magenta',(.7,.045,.42),.4,.26,(2.8,.04,1.4)),
 ('Lamp_Amber',(.95,.60,.16),.3,.29,(3.4,1.5,.18)),
 ('Lamp_Red',(.7,.025,.025),.3,.3,(2.3,.015,.015)),
]
MAT={x[0]:i for i,x in enumerate(PALETTE)}

def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def cross(a,b):return(a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def norm(a):
 d=math.sqrt(sum(x*x for x in a));return tuple(x/d for x in a)

class Car:
 def __init__(self,name):self.name=name;self.parts=[]
 def mesh(self,name,vertices,faces,mat,position=(0,0,0)):
  self.parts.append(dict(name=name,vertices=vertices,faces=faces,mat=MAT[mat],position=position))
 def box(self,name,c,size,mat,bevel=.0):
  x,y,z=size
  if bevel:
   b=min(bevel,x*.2,y*.2)
   ring=[(-x/2+b,-y/2),(x/2-b,-y/2),(x/2,-y/2+b),(x/2,y/2-b),(x/2-b,y/2),(-x/2+b,y/2),(-x/2,y/2-b),(-x/2,-y/2+b)]
   self.loft(name,[(-z/2,ring),(z/2,ring)],mat,c)
  else:self.loft(name,[(-z/2,[(-x/2,-y/2),(x/2,-y/2),(x/2,y/2),(-x/2,y/2)]),(z/2,[(-x/2,-y/2),(x/2,-y/2),(x/2,y/2),(-x/2,y/2)])],mat,c)
 def loft(self,name,sections,mat,position=(0,0,0)):
  n=len(sections[0][1]);v=[(x,y,z) for z,ring in sections for x,y in ring];f=[]
  for j in range(len(sections)-1):
   for i in range(n):
    a=j*n+i;b=j*n+(i+1)%n;c=(j+1)*n+(i+1)%n;d=(j+1)*n+i
    f.extend([(a,b,c),(a,c,d)])
  for i in range(1,n-1):f.append((0,i+1,i));f.append(((len(sections)-1)*n,(len(sections)-1)*n+i,(len(sections)-1)*n+i+1))
  self.mesh(name,v,f,mat,position)
 def beam(self,name,a,b,width,mat,depth=None):
  # Rectangular beam between any two endpoints, with rigid local frame baked.
  d=norm(sub(b,a));ref=(0,1,0) if abs(d[1])<.9 else (0,0,1)
  u=norm(cross(d,ref));w=cross(d,u);r=width/2;s=(depth or width)/2
  verts=[]
  for c in [a,b]:
   for q,t in [(-1,-1),(1,-1),(1,1),(-1,1)]:verts.append(tuple(c[k]+q*r*u[k]+t*s*w[k] for k in range(3)))
  faces=[(0,2,1),(0,3,2),(4,5,6),(4,6,7)]
  for i in range(4):j=(i+1)%4;faces.extend([(i,j,j+4),(i,j+4,i+4)])
  self.mesh(name,verts,faces,mat)
 def wheel(self,name,c,r=.33,width=.22):
  # Group all pieces under a single pivot via a shared part-name prefix.
  n=16;v=[]
  for x,rad in [(-width/2,r*.90),(-width*.32,r),(width*.32,r),(width/2,r*.90)]:
   v.extend((x,rad*math.cos(2*math.pi*i/n),rad*math.sin(2*math.pi*i/n)) for i in range(n))
  f=[]
  for j in range(3):
   for i in range(n):a=j*n+i;b=j*n+(i+1)%n;d=(j+1)*n+i;e=(j+1)*n+(i+1)%n;f.extend([(a,b,e),(a,e,d)])
  for i in range(1,n-1):f.extend([(0,i+1,i),(3*n,3*n+i,3*n+i+1)])
  self.mesh(name+'/Tire',v,f,'Rubber',c)
  # Faceted hub cap and five aerodynamic spokes, on both sides.
  for side in [-1,1]:
   x=side*(width/2+.008)
   ring=[(x,0,0)]+[(x,r*.68*math.cos(2*math.pi*i/n),r*.68*math.sin(2*math.pi*i/n)) for i in range(n)]
   faces=[(0,1+i,1+(i+1)%n) if side>0 else (0,1+(i+1)%n,1+i) for i in range(n)]
   self.mesh(name+f'/Hub{side}',ring,faces,'Metal_Chassis',c)
   for i in range(5):
    t=i*2*math.pi/5
    a=(c[0]+x,c[1]+r*.1*math.cos(t),c[2]+r*.1*math.sin(t));b=(c[0]+x,c[1]+r*.57*math.cos(t+.24),c[2]+r*.57*math.sin(t+.24))
    # geometry in wheel-local coordinates so a common node can animate everything.
    a=sub(a,c);b=sub(b,c)
    before=len(self.parts);self.beam(name+f'/Spoke{side}_{i}',a,b,.035,'Paint_Ivory',.018);self.parts[before]['position']=c
 def feather(self,name,side,x,y,z,scale,mat):
  # Three swept illuminated feather vanes, following the vehicle flank.
  for j in range(3):
   a=(side*x,y+j*.09*scale,z+j*.14*scale);b=(side*x,y+.22*scale+j*.09*scale,z+.52*scale+j*.14*scale)
   self.beam(name+str(j),a,b,.045*scale,mat,.018)
 def export(self):
  # Merge by material within the rigid body and each wheel. This keeps draw calls
  # bounded (and preserves the four independent wheel-animation pivots).
  grouped={}
  for part in self.parts:
   group=part['name'].split('/')[0] if '/' in part['name'] else 'Body'
   key=(group,part['mat'])
   if key not in grouped:grouped[key]={'name':group+'/'+PALETTE[part['mat']][0] if group!='Body' else 'Body_'+PALETTE[part['mat']][0],'vertices':[],'faces':[],'mat':part['mat'],'position':part['position'] if group!='Body' else (0,0,0)}
   merged=grouped[key];offset=len(merged['vertices'])
   merged['vertices'].extend(tuple(v[k]+part['position'][k]-merged['position'][k] for k in range(3)) for v in part['vertices'])
   merged['faces'].extend(tuple(v+offset for v in face) for face in part['faces'])
  self.parts=list(grouped.values())
  g={'asset':{'version':'2.0','generator':'Original Cyber Bird traffic generator R5'},'scene':0,'scenes':[{'nodes':[0]}], 'nodes':[{'name':self.name,'children':[]}], 'meshes':[],'materials':[], 'buffers':[],'bufferViews':[],'accessors':[]}
  for name,rgb,metal,rough,em in PALETTE:
   m={'name':name,'pbrMetallicRoughness':{'baseColorFactor':[*rgb,1],'metallicFactor':metal,'roughnessFactor':rough},'alphaMode':'OPAQUE','doubleSided':False}
   if em:m['emissiveFactor']=[v/max(em) for v in em];m['extensions']={'KHR_materials_emissive_strength':{'emissiveStrength':max(em)}}
   g['materials'].append(m)
  g['extensionsUsed']=['KHR_materials_emissive_strength'];data=bytearray();triangles=0;mins=[1e9]*3;maxs=[-1e9]*3;groups={}
  def accessor(values,components,typ,minmax=False):
   while len(data)%4:data.append(0)
   start=len(data);flat=[t for v in values for t in v] if components>1 else values
   data.extend(struct.pack('<'+('f' if typ==5126 else 'I')*len(flat),*flat));bv=len(g['bufferViews']);g['bufferViews'].append({'buffer':0,'byteOffset':start,'byteLength':len(data)-start})
   a={'bufferView':bv,'componentType':typ,'count':len(values),'type':{1:'SCALAR',3:'VEC3'}[components]}
   if minmax:a['min']=[min(v[k] for v in values) for k in range(3)];a['max']=[max(v[k] for v in values) for k in range(3)]
   ai=len(g['accessors']);g['accessors'].append(a);return ai
  for part in self.parts:
   vertices=[];normals=[]
   for face in part['faces']:
    a,b,c=[part['vertices'][i] for i in face];normal=norm(cross(sub(b,a),sub(c,a)))
    for v in [a,b,c]:
     vertices.append(v);normals.append(normal)
     for k in range(3):mins[k]=min(mins[k],v[k]+part['position'][k]);maxs[k]=max(maxs[k],v[k]+part['position'][k])
   triangles+=len(part['faces']);p=accessor(vertices,3,5126,True);n=accessor(normals,3,5126);ind=accessor(list(range(len(vertices))),1,5125)
   mi=len(g['meshes']);g['meshes'].append({'name':part['name'],'primitives':[{'attributes':{'POSITION':p,'NORMAL':n},'indices':ind,'material':part['mat']}]})
   node={'name':part['name'].split('/')[-1],'mesh':mi};parent=0
   if '/' in part['name']:
    group=part['name'].split('/')[0]
    if group not in groups:
     groups[group]=len(g['nodes']);g['nodes'].append({'name':group,'translation':list(part['position']),'children':[]});g['nodes'][0]['children'].append(groups[group])
    parent=groups[group]
   else:node['translation']=list(part['position'])
   idx=len(g['nodes']);g['nodes'].append(node);g['nodes'][parent]['children'].append(idx)
  g['buffers']=[{'byteLength':len(data)}]
  js=json.dumps(g,separators=(',',':')).encode();js+=b' '*((-len(js))%4);data+=b'\0'*((-len(data))%4)
  path=OUT/(self.name+'.glb');path.write_bytes(struct.pack('<III',0x46546c67,2,12+8+len(js)+8+len(data))+struct.pack('<II',len(js),0x4E4F534A)+js+struct.pack('<II',len(data),0x004E4942)+data)
  return {'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'triangles':triangles,'mesh_parts':len(self.parts),'dimensions_m':[round(maxs[k]-mins[k],4) for k in range(3)],'bounds_min':mins,'bounds_max':maxs,'wheel_pivots':['Wheel_FL','Wheel_FR','Wheel_RL','Wheel_RR'],'bytes':path.stat().st_size}

def ring(w,y0,y1,bevel=.10):return[(-w/2+bevel,y0),(w/2-bevel,y0),(w/2,y0+bevel),(w/2,y1-bevel),(w/2-bevel,y1),(-w/2+bevel,y1),(-w/2,y1-bevel),(-w/2,y0+bevel)]
def base(car,length,width,paint,wheelz,r=.33):
 car.box('Undertray',(0,.37,.05),(width-.16,.16,length-.32),'Metal_Chassis',.04)
 car.loft('SculptedBody',[(-length/2,ring(width*.70,.37,.74)),(-length*.35,ring(width,.40,.91)),(length*.30,ring(width,.40,.94)),(length/2,ring(width*.86,.44,.79))],paint)
 for side in [-1,1]:
  car.beam('SillL' if side<0 else 'SillR',(side*(width/2-.03),.48,-length*.30),(side*(width/2-.03),.48,length*.32),.075,'Metal_Chassis',.10)
  for axle,z in [('F',-wheelz),('R',wheelz)]:car.wheel('Wheel_'+axle+('L' if side<0 else 'R'),(side*(width/2-.14),r,z),r)
  car.box('FrontLamp'+str(side),(side*width*.26,.73,-length/2-.012),(width*.28,.065,.025),'Lamp_Amber',.02)
  car.box('RearLamp'+str(side),(side*width*.29,.73,length/2+.012),(width*.25,.062,.025),'Lamp_Red',.02)
 car.box('FrontGrille',(0,.49,-length/2-.025),(width*.45,.13,.04),'Rubber',.02)
 for i in range(7):car.box('GrilleVane'+str(i),((i-3)*.09,.49,-length/2-.05),(.026,.12,.027),'Metal_Chassis')

def sedan():
 c=Car('raven_taxi');l=4.72;w=1.84;base(c,l,w,'Paint_Plum',1.48)
 c.loft('OpaqueCanopy',[(-1.25,[(-.78,.90),(.78,.90),(.75,.95),(-.75,.95)]),(-.62,[(-.78,.91),(.78,.91),(.64,1.49),(-.64,1.49)]),(.92,[(-.78,.92),(.78,.92),(.60,1.43),(-.60,1.43)]),(1.43,[(-.74,.93),(.74,.93),(.62,.99),(-.62,.99)])],'Glass_Opaque')
 c.box('Roof',(0,1.46,.13),(1.27,.09,1.64),'Paint_Obsidian',.045)
 for side in [-1,1]:
  for i,z in enumerate([-.66,.20,.94]):c.beam('WindowPillar'+str(side)+str(i),(side*.78,.90,z),(side*.64,1.44,z-.04),.055,'Paint_Obsidian',.075)
  c.beam('ShoulderNeon'+str(side),(side*.925,.85,-1.47),(side*.925,.86,1.54),.024,'Neon_Cyan')
  c.feather('RavenWing'+str(side),side,.928,.60,.45,.90,'Lamp_Amber')
  for j in range(3):c.box('HoodFeather'+str(side)+str(j),(side*(.25+j*.14),.91,-1.70+j*.06),(.075,.022,.37-j*.05),'Paint_Obsidian')
  c.beam('MirrorStem'+str(side),(side*.76,1.08,-.74),(side*.90,1.08,-.81),.035,'Metal_Chassis');c.box('Mirror'+str(side),(side*.90,1.10,-.81),(.09,.12,.24),'Paint_Obsidian',.02)
  c.beam('TailWing'+str(side),(side*.57,.90,1.66),(side*.88,1.18,2.03),.055,'Paint_Obsidian',.17)
 c.box('TaxiSign_Base',(0,1.56,.12),(.65,.08,.38),'Metal_Chassis',.03);c.box('TaxiSign_Glow',(0,1.65,.12),(.60,.15,.30),'Lamp_Amber',.025)
 for side in [-1,1]:c.beam('TaxiWing'+str(side),(side*.20,1.69,.12),(side*.48,1.62,.25),.035,'Neon_Cyan',.09)
 return c

def van():
 c=Car('heron_van');l=4.68;w=1.88;base(c,l,w,'Paint_Ivory',1.47,.35)
 c.loft('CargoMonocoque',[(-.35,ring(1.80,.80,2.12,.13)),(2.08,ring(1.80,.80,2.12,.13))],'Paint_Ivory')
 c.loft('CabGlass',[(-1.92,[(-.78,.87),(.78,.87),(.76,.94),(-.76,.94)]),(-1.15,[(-.85,.91),(.85,.91),(.69,1.94),(-.69,1.94)]),(-.38,[(-.85,.90),(.85,.90),(.73,1.95),(-.73,1.95)])],'Glass_Opaque')
 c.box('CabRoof',(0,1.96,-.74),(1.48,.09,.96),'Paint_Obsidian',.03)
 for side in [-1,1]:
  c.beam('CabPillar'+str(side),(side*.86,.91,-.40),(side*.74,1.96,-.42),.075,'Paint_Ivory',.09)
  c.box('CargoWingPanel'+str(side),(side*.909,1.49,.75),(.028,.75,1.69),'Paint_Obsidian',.01)
  c.feather('HeronFlank'+str(side),side,.915,1.28,.17,1.5,'Neon_Cyan')
  c.beam('HeronNeck'+str(side),(side*.930,1.39,1.30),(side*.930,1.87,.94),.035,'Paint_Ivory')
  c.beam('HeronBeak'+str(side),(side*.930,1.87,.94),(side*.930,1.82,.61),.035,'Lamp_Amber')
  c.box('CargoRunner'+str(side),(side*.75,2.18,.65),(.10,.08,2.55),'Metal_Chassis',.03)
  c.box('Step'+str(side),(side*.88,.42,-.73),(.12,.12,.86),'Metal_Chassis')
  c.box('Mirror'+str(side),(side*.90,1.48,-1.10),(.095,.25,.20),'Paint_Obsidian',.02)
  c.beam('DoorSeam'+str(side),(side*.915,1.01,-.20),(side*.915,1.95,-.20),.018,'Metal_Chassis')
 c.box('RearDoorInset',(0,1.47,2.10),(1.51,1.06,.028),'Paint_Obsidian',.05)
 for side in [-1,1]:
  c.box('RearCargoDoor'+str(side),(side*.385,1.47,2.13),(.71,.95,.045),'Paint_Ivory',.035)
  c.box('RearNeon'+str(side),(side*.70,1.47,2.164),(.028,.72,.028),'Neon_Cyan')
  c.box('CargoHandle'+str(side),(side*.10,1.38,2.17),(.045,.22,.04),'Metal_Chassis')
 for i in range(4):c.box('RoofCoolingFin'+str(i),(0,2.15,.05+i*.28),(1.12,.07,.075),'Metal_Chassis',.02)
 return c

def compact():
 c=Car('kestrel_compact');l=3.66;w=1.72;base(c,l,w,'Paint_Amber',1.10,.31)
 c.loft('OpaqueCanopy',[(-1.02,[(-.72,.87),(.72,.87),(.70,.92),(-.70,.92)]),(-.42,[(-.74,.89),(.74,.89),(.57,1.42),(-.57,1.42)]),(.55,[(-.73,.90),(.73,.90),(.58,1.38),(-.58,1.38)]),(1.17,[(-.65,.89),(.65,.89),(.49,.98),(-.49,.98)])],'Glass_Opaque')
 c.box('Roof',(0,1.40,.01),(1.17,.06,.98),'Paint_Obsidian',.03)
 for side in [-1,1]:
  c.beam('CanopySpine'+str(side),(side*.57,1.43,-.42),(side*.58,1.39,.55),.04,'Neon_Magenta')
  c.beam('WindowPillar'+str(side),(side*.74,.91,.35),(side*.58,1.39,.34),.055,'Paint_Obsidian')
  c.feather('KestrelFlash'+str(side),side,.87,.59,-.22,.8,'Neon_Magenta')
  c.box('SportSill'+str(side),(side*.87,.45,.05),(.08,.12,1.52),'Paint_Obsidian',.03)
  c.beam('MirrorArm'+str(side),(side*.73,1.05,-.51),(side*.88,1.08,-.60),.04,'Metal_Chassis');c.box('Mirror'+str(side),(side*.88,1.10,-.60),(.12,.10,.20),'Paint_Obsidian',.02)
  c.beam('SpoilerMount'+str(side),(side*.54,.91,1.35),(side*.54,1.11,1.40),.045,'Metal_Chassis')
 c.box('RearWing',(0,1.12,1.42),(1.57,.055,.25),'Paint_Obsidian',.02)
 c.box('WingNeon',(0,1.115,1.55),(1.45,.028,.024),'Neon_Magenta')
 c.box('HoodBeak',(0,.89,-1.40),(.14,.06,.59),'Paint_Obsidian',.025)
 for side in [-1,1]:c.beam('HoodFeather'+str(side),(side*.12,.925,-1.14),(side*.55,.86,-1.62),.045,'Neon_Magenta')
 return c

manifest={'title':'CYBER BIRD original avian traffic assets R5','author':'Codex original procedural geometry','license':'CC0-1.0','coordinate_contract':{'units':'metres','up':'+Y','forward':'-Z','ground_y':0,'wheel_rotation_axis':'+X'},'provider':{'requested':'Gemini Argon via T3 Code','used':'local original Python procedural geometry','reason':'These fallback models were authored locally before T3 orchestration access was verified. A later isolated read/prompt bridge test successfully reached Gemini 4 Argon (High) through Antigravity; these particular models remain original Codex procedural geometry. No originals modified.'},'materials':{'count':len(PALETTE),'textures':0,'opaque':True,'double_sided':False,'dynamic_lights':0,'reuse_by_name':[p[0] for p in PALETTE]},'models':[x.export() for x in [sedan(),van(),compact()]]}
(OUT/'PROVENANCE.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf8')
(OUT/'LICENSE.txt').write_text('CC0 1.0 Universal\n\nOriginal CYBER BIRD R5 car geometry and generator by Codex, 2026.\nTo the extent possible under law, the author has waived all copyright and related or neighboring rights to these original assets and generation source under CC0 1.0 Universal.\nhttps://creativecommons.org/publicdomain/zero/1.0/\n\nNo third-party geometry or textures. These are locally generated original assets; they were not generated by Gemini Argon. Existing Gemini Argon assets were read only references and were not modified or incorporated.\n',encoding='utf8')
print(json.dumps(manifest['models'],indent=2))
