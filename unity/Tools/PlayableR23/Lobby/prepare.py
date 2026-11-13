"""CPU-only actual lobby_map_091 recipe; stages outside Assets during native jobs."""
from pathlib import Path
import ast,hashlib,json,math,struct
import numpy as np
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent;OUT=HERE/'StagedPayload';OUT.mkdir(exist_ok=True)
SRC=ROOT/'quest-r24/source';TAU=math.tau;parts=[];materials=[]
# Reuse the already checked source primitive translator without executing its builds.
helper=ast.parse((HERE.parent/'Consumables/prepare.py').read_text())
for node in helper.body:
    if isinstance(node,ast.FunctionDef) and node.name in ('unit','add','rounded','cylinder','torus'):
        exec(compile(ast.Module(body=[node],type_ignores=[]),'source-primitives','exec'))
class SourcePCG:
    """Godot RandomPCG/PCG32 default increment, CLZ32 randf, single-real build."""
    def __init__(self,seed):
        self.state=0;self.inc=(1442695040888963407<<1|1)&((1<<64)-1);self.raw();self.state=(self.state+seed)&((1<<64)-1);self.raw()
    def raw(self):
        old=self.state;self.state=(old*6364136223846793005+self.inc)&((1<<64)-1)
        x=(((old>>18)^old)>>27)&0xffffffff;r=old>>59
        return ((x>>r)|(x<<((-r)&31)))&0xffffffff
    def value(self):
        exponent=self.raw()
        return np.float32(0) if exponent==0 else np.float32(math.ldexp(float(np.float32(self.raw()|0x80000001)),-32-(32-exponent.bit_length())))
    def between(self,a,b):return float(np.float32(np.float32(self.value()*np.float32(b-a))+np.float32(a)))
def box(label,size,at,mi):rounded(label,size,at,mi,.025)
box('LobbyDeck',(22,.30,18),(0,-.15,-2),0)
for x in [-8.,8.]:
    box('DeckPerimeter',(.12,.12,16),(x,.12,-2),1);box('DeckLight',(.035,.035,16),(x,.19,-2),2)
for z in [-8.,-4.,0.,4.]:box('DeckSeam',(16,.005,.018),(0,.005,z),1)
cylinder('DisplayPedestal',.83,.3,(1.35,.15,-.65),1);torus('CyanPedestalRim',.76,.018,(1.35,.31,-.65),2)
rng=SourcePCG(91091);towers=[]
for i in range(28):
    x,z,height,width=rng.between(-40,40),rng.between(-55,-18),rng.between(5,25),rng.between(2,5)
    towers.append(dict(index=i,x=x,z=z,height=height,width=width))
    box('SkylineTower'+str(i),(width,height,width),(x,float(np.float32(height*.5-2)),z),4)
    for band in range(4):box('TowerNeonBand',(width+.025,.10,width+.025),(x,float(np.float32(height*(.2+band*.19)-2)),z),2 if i%3 else 3)
authored=[p[0] for p in parts];groups={}
# Godot authored primitive order is clockwise with outward normals. Reflection
# alone converts that to Unity's front faces; undo the glTF-style swap in helper.add.
for name,v,indices,mi,cap in parts:groups.setdefault(mi,[]).append((v,indices.reshape(-1,3)[:,[0,2,1]].ravel()))
with (OUT/'lobby.meshbin').open('wb') as f:
    f.write(struct.pack('<ii',0x5232334c,len(groups)))
    for mi,members in groups.items():
        vertices=[];indices=[];offset=0
        for v,i in members:vertices.append(v);indices.append(i+offset);offset+=len(v)
        v=np.concatenate(vertices);indices=np.concatenate(indices).astype('<i4')
        f.write(struct.pack('<iii',mi,len(v),len(indices)));f.write(v.tobytes());f.write(indices.tobytes())
colliders=[dict(name='LobbyDeck',at=[0,-.15,2],size=[22,.30,18])]
for at,size in [([-10.75,5.8,-2],[.25,12,18]),([10.75,5.8,-2],[.25,12,18]),([0,5.8,-10.75],[22,12,.25]),([0,5.8,6.75],[22,12,.25])]:
    at[2]*=-1;colliders.append(dict(name='InvisibleLobbyForceField',at=at,size=size))
manifest=dict(version=1,origin=[1000,0,-1000],spawn=[0,.3,-3.8],menu=[.1,1.3,-1.55],preview=[1.35,.31,.65],colliders=colliders,towers=towers,authoredParts=authored,triangles=sum(len(p[2])//3 for p in parts),surfaces=len(groups))
(OUT/'lobby.json').write_text(json.dumps(manifest,indent=2))
inputs={str(p.relative_to(SRC)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in [SRC/'scripts/lobby_map_091.gd',SRC/'scripts/front_menu_091.gd',SRC/'scripts/field_item_models.gd',SRC/'assets/materials/lobby_facade_091.gdshader']}
(OUT/'provenance.json').write_text(json.dumps(dict(baseline='quest-r24/source actual R23 snapshot',inputs=inputs,recipe='Source rounded boxes/cylinder/torus, 28 PCG32 seed91091 towers and 112 bands. One Z and winding reflection. Source procedural colors/shader; no external textures.',rngReferences=['https://github.com/godotengine/godot/blob/master/core/math/random_pcg.h','https://github.com/godotengine/godot/blob/master/thirdparty/misc/pcg.cpp'],fire='No lobby fire recipe or asset found; original Godot lobby capture contains skyline/deck and no fire.'),indent=2))
print('source lobby:',len(authored),'authored parts;',manifest['triangles'],'triangles;',manifest['surfaces'],'surfaces;',len(colliders),'source colliders; staged',OUT)
