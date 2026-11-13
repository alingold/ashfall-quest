"""Diagnostic geometry rasterization, no GPU. Usage: python tools/render_prop_context_r18.py PREFIX [DISTANCE]."""
import json, math, sys
from pathlib import Path
from PIL import Image, ImageDraw
prefix=sys.argv[1]
if not prefix.replace('_','').replace('-','').isalnum():raise ValueError('invalid prefix')
data=json.loads(Path(f'evidence/{prefix}-triangles.json').read_text());rows=data['triangles'];target=data['target'];distance=float(sys.argv[2]) if len(sys.argv)>2 else 7

def sub(a,b):return [x-y for x,y in zip(a,b)]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def norm(a):
 length=math.sqrt(dot(a,a))
 return [x/length for x in a] if length>1e-12 else [0,1,0]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
for index,offset in enumerate([(distance*.43,distance*.15,-distance),(-distance*.43,distance*.15,distance)]):
 eye=[a+b for a,b in zip(target,offset)];f=norm(sub(target,eye));r=norm(cross(f,[0,1,0]));u=cross(r,f);polygons=[]
 for row in rows:
  ps=[sub(v,eye) for v in row['v']];zs=[dot(p,f) for p in ps]
  if min(zs)<.1:continue
  xy=[(800+900*dot(p,r)/z,500-900*dot(p,u)/z) for p,z in zip(ps,zs)]
  n=norm(cross(sub(row['v'][1],row['v'][0]),sub(row['v'][2],row['v'][0])))
  shade=.5+.5*abs(dot(n,norm([.3,1,.4])))
  polygons.append((sum(zs)/3,xy,tuple(int(min(255,max(0,c*shade*255))) for c in row['c'])))
 image=Image.new('RGB',(1600,1000),(150,180,190));draw=ImageDraw.Draw(image)
 for _,xy,color in sorted(polygons,reverse=True):draw.polygon(xy,fill=color)
 image.save(f'evidence/{prefix}-view-{index}.png')
