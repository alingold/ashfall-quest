"""CPU-only source context; textured surfaces are diagnostic gray, not missing-art evidence."""
import json,math,sys
from pathlib import Path
from PIL import Image,ImageDraw
prefix=sys.argv[1];eye=list(map(float,sys.argv[2:5]))
data=json.loads(Path(f'evidence/{prefix}-triangles.json').read_text());target=data['target']
def sub(a,b):return [x-y for x,y in zip(a,b)]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def norm(a):
 d=math.sqrt(dot(a,a));return [x/(d or 1) for x in a]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
f=norm(sub(target,eye));r=norm(cross(f,[0,1,0]));u=cross(r,f);polygons=[]
for row in data['triangles']:
 ps=[sub(v,eye) for v in row['v']];zs=[dot(p,f) for p in ps]
 if min(zs)<.1:continue
 xy=[(800+1000*dot(p,r)/z,500-1000*dot(p,u)/z) for p,z in zip(ps,zs)]
 n=norm(cross(sub(row['v'][1],row['v'][0]),sub(row['v'][2],row['v'][0])))
 shade=.5+.5*abs(dot(n,norm([.3,1,.4])))
 polygons.append((sum(zs)/3,xy,tuple(int(min(255,max(0,c*shade*255))) for c in row['c'])))
image=Image.new('RGB',(1600,1000),(150,180,190));draw=ImageDraw.Draw(image)
for _,xy,color in sorted(polygons,reverse=True):draw.polygon(xy,fill=color)
image.save(f'evidence/{prefix}-front.png')
Path(f'evidence/{prefix}-camera.json').write_text(json.dumps({'eye':eye,'target':target,'method':'CPU source geometry painter projection; textured surfaces gray'}))
