"""Execute the frozen Cyber Bird's finite geometry-math subset without an engine.

This is not a general GDScript runtime. Unsupported syntax fails. Node/resource
creation is replaced only by explicit source-equivalent geometry/data sinks.
"""
import ast,collections,math,re
import numpy as np

class Vec:
    def __init__(self,*v):self.a=np.array(v[0].a if len(v)==1 and isinstance(v[0],Vec)else v[0]if len(v)==1 else v,dtype=np.float32)
    @property
    def x(self):return float(self.a[0])
    @property
    def y(self):return float(self.a[1])
    @property
    def z(self):return float(self.a[2])
    def __add__(self,v):return Vec(self.a+v.a)
    def __sub__(self,v):return Vec(self.a-v.a)
    def __neg__(self):return Vec(-self.a)
    def __mul__(self,v):return Vec(self.a*np.float32(v))
    def __rmul__(self,v):return self*v
    def __truediv__(self,v):return Vec(self.a/np.float32(v))
    def length(self):return float(np.sqrt(np.dot(self.a,self.a)))
    def normalized(self):return self/max(self.length(),1e-30)
    def cross(self,v):return Vec(np.cross(self.a,v.a))
    def dot(self,v):return float(np.dot(self.a,v.a))
    def distance_to(self,v):return (self-v).length()
    def lerp(self,v,t):return self+(v-self)*t
    def __repr__(self):return 'Vec('+repr(self.a.tolist())+')'
Vec.ZERO=Vec(0,0,0);Vec.ONE=Vec(1,1,1);Vec.UP=Vec(0,1,0);Vec.FORWARD=Vec(0,0,-1);Vec.RIGHT=Vec(1,0,0)

class Basis:
    def __init__(self,axis=None,angle=0,matrix=None):
        if matrix is not None:self.a=np.array(matrix,np.float32);return
        if axis is None:self.a=np.eye(3,dtype=np.float32);return
        x,y,z=axis.normalized().a.astype(float);c=math.cos(angle);s=math.sin(angle);t=1-c
        self.a=np.array([[t*x*x+c,t*x*y-s*z,t*x*z+s*y],[t*x*y+s*z,t*y*y+c,t*y*z-s*x],[t*x*z-s*y,t*y*z+s*x,t*z*z+c]],np.float32)
    @classmethod
    def looking_at(cls,target,up):
        z=-target.normalized();x=up.cross(z).normalized();y=z.cross(x)
        return cls(matrix=np.column_stack((x.a,y.a,z.a)))
    @property
    def x(self):return Vec(self.a[:,0])
    def __mul__(self,v):return Vec(self.a@v.a)
Basis.IDENTITY=Basis()

class Row(dict):
    def __getattr__(self,key):return self[key]
    def __setattr__(self,key,value):self[key]=value
class Array(list):
    def size(self):return len(self)
    def filter(self,predicate):return Array(v for v in self if predicate(v))
def gd_iter(value):return range(value)if isinstance(value,int)else value
def gd_div(a,b):return int(a/b)if isinstance(a,int)and isinstance(b,int)else a/b
def gd_mod(a,b):return a%tuple(b)if isinstance(a,str)and isinstance(b,list)else a%b
def rgba(value):return [int(value[i:i+2],16)/255 for i in (0,2,4)]+[1.]
def color(*args):return rgba(args[0])if len(args)==1 and isinstance(args[0],str)else list(args)+([1.]if len(args)==3 else[])

class SourceOperators(ast.NodeTransformer):
    def visit_BinOp(self,node):
        self.generic_visit(node)
        if isinstance(node.op,(ast.Div,ast.Mod)):
            return ast.copy_location(ast.Call(func=ast.Name(id='gd_div'if isinstance(node.op,ast.Div)else'gd_mod',ctx=ast.Load()),args=[node.left,node.right],keywords=[]),node)
        return node
    def visit_For(self,node):
        self.generic_visit(node);node.iter=ast.Call(func=ast.Name(id='gd_iter',ctx=ast.Load()),args=[node.iter],keywords=[]);return node
    def visit_Dict(self,node):
        self.generic_visit(node);return ast.copy_location(ast.Call(func=ast.Name(id='Row',ctx=ast.Load()),args=[node],keywords=[]),node)
    def visit_List(self,node):
        self.generic_visit(node);return ast.copy_location(ast.Call(func=ast.Name(id='Array',ctx=ast.Load()),args=[node],keywords=[]),node)

FUNCTIONS=['_city_ground','_building','_facade','_interior_program','_swap','_stairs','_nest','_feather','_street_furniture','_bridge','_sky_routes','_landmarks','_bird','_boundary','_infill_identity','_loot_offset_r6','_weapon_mounts_r6','_beam']

def translate(source):
    spans=list(re.finditer(r'^func (\w+)\(',source,re.M));result=[]
    for i,match in enumerate(spans):
        name=match.group(1)
        if name not in FUNCTIONS:continue
        block=source[match.start():spans[i+1].start()if i+1<len(spans)else len(source)]
        if name=='_building':block=block.split('\t# A solid service partition provides conservative occlusion')[0]
        if name=='_boundary':block=block.split('\tfor view in _chunk.get_children():')[0]
        # Function parameter types are limited to the actual scalar/vector/dict types.
        header,body=block.split('\n',1)
        header=re.sub(r'\s*->\s*\w+','',header.replace('func ','def ',1))
        header=re.sub(r':\s*(?:Vector3|Vector2|float|int|bool|String|Dictionary)\b','',header)
        header=header.replace(':=','=')
        lines=[header]
        match_indent=None
        for line in body.splitlines():
            line=line.split('#',1)[0].rstrip()
            if not line.strip():continue
            indent=len(line)-len(line.lstrip('\t'));text=line.lstrip('\t')
            text=re.sub(r'\bvar\s+(\w+)\s*(?::\s*(?:Vector3|Vector2|float|int|bool|String|Dictionary))?\s*:?=',r'\1 =',text)
            text=re.sub(r'\s+as\s+StandardMaterial3D\b','',text)
            text=re.sub(r'\bfunc\((\w+)(?::\s*Dictionary)?\):\s*return\s+',r'lambda \1: ',text)
            text=re.sub(r'\btrue\b','True',text);text=re.sub(r'\bfalse\b','False',text)
            if text.startswith('match '):match_indent=indent
            elif match_indent is not None and indent==match_indent+1 and re.match(r'\d+:',text):text='case '+text
            lines.append('    '*indent+text)
        result.append('\n'.join(lines))
    assert len(result)==len(FUNCTIONS)
    # Source's build orchestrator, only its geometry calls/loops.
    build=source[source.index('\t_city_ground()'):source.index('\t_make_environment()')]
    result.append('def build_geometry():\n'+build)
    text='\n\n'.join(result).replace('\t','    ')
    text=re.sub(r'\bvar\s+(\w+)\s*(?::\s*(?:Vector3|Vector2|float|int|bool|String|Dictionary))?\s*:?=',r'\1 =',text)
    text=re.sub(r'\btrue\b','True',text);text=re.sub(r'\bfalse\b','False',text)
    tree=SourceOperators().visit(ast.parse(text));ast.fix_missing_locations(tree)
    return ast.unparse(tree),compile(tree,'frozen-cyber-bird-geometry','exec')

class Geometry:
    def __init__(self,source):
        self.boxes=[];self.ramps=[];self.signs=[];self.chunks=[];self.current=None
        self.env=dict(Vector3=Vec,Vector2=Vec,Basis=Basis,Color=color,Row=Row,Array=Array,gd_iter=gd_iter,gd_div=gd_div,gd_mod=gd_mod,PI=math.pi,TAU=math.tau,cos=math.cos,sin=math.sin,minf=min,maxf=max,mini=min,maxi=max,absf=abs,floori=math.floor)
        self.env.update(global_position=Vec(0,0,2400),STOREY=4.2,DISTRICTS=Array(['HERON QUAY','FEATHER MARKET','ROOK FOUNDRY','CROWN NESTS']),buildings=Array(),windows=Array(),routes=Array(),loot_points=Array(),statistics=Row(boxes=0,collision_triangles=0,batches=0,floors=0,stairs=0,bridges=0),_light_sites=Array(),_neon_sites=Array(),_street_sites=Array())
        self.env['materials']={k:Row(albedo_color=rgba(c))for k,c in [('cyan','21e9ef'),('pink','fb458e'),('gold','ffd17d'),('violet','9a7bff'),('white','e6f6ff')]}
        self.env.update(_begin_chunk=self.begin,_finish_chunk=self.finish,_box=self.box,_sign=self.sign,_foot_ramp_r5=self.ramp)
        self.python,code=translate(source);exec(code,self.env);self.env['build_geometry']()
        assert self.current is None and len(self.env['buildings'])==144
    def begin(self,title,at=Vec.ZERO):
        assert self.current is None
        self.current=Row(name=title,position=Vec(at),startBox=len(self.boxes));self.env['_chunk']=self.current
    def finish(self,terrain=False):
        assert self.current is not None
        self.current.update(endBox=len(self.boxes),terrain=terrain);self.chunks.append(self.current);self.current=None
    def box(self,at,size,material,solid=True,detail=False,basis=Basis.IDENTITY):
        assert self.current is not None and np.isfinite(size.a).all()and(size.a>0).all()
        self.boxes.append(dict(chunk=self.current.name,chunkPosition=self.current.position.a.copy(),position=at.a.copy(),size=size.a.copy(),basis=basis.a.copy(),material=material,solid=solid,detail=detail,terrain=False))
        self.env['statistics'].boxes+=1
    def sign(self,text,at,color,size=.045,rotation=0):
        self.signs.append(dict(text=text,position=self.env['global_position'].a+self.current.position.a+at.a,color=color,pixelSize=size,fontSize=96,rotation=rotation,outlineSize=4,outlineColor='07101b',sourceChunk=self.current.name))
    def ramp(self,x,y,start_z,end_z):
        a=Vec(x-1.15,y+.008,start_z);b=Vec(x-1.15,y+2.108,end_z);c=Vec(x+1.15,y+2.108,end_z);d=Vec(x+1.15,y+.008,start_z)
        faces=[a,b,c,a,c,d]if(b-a).cross(c-a).y<0 else[a,c,b,a,d,c]
        origin=self.env['global_position'].a+self.current.position.a
        self.ramps.append(dict(faces=np.array([v.a+origin for v in faces],np.float32),sourceLayer=16,backface=False,chunk=self.current.name))
def serial(value):
    if isinstance(value,Vec):return value.a.tolist()
    if isinstance(value,np.ndarray):return value.tolist()
    if isinstance(value,dict):return {k:serial(v)for k,v in value.items()}
    if isinstance(value,(list,tuple)):return [serial(v)for v in value]
    if isinstance(value,np.generic):return value.item()
    return value
