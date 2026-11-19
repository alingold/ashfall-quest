"""Render every aperture with local source context. Source textures represented by mean color."""
from pathlib import Path
import json,sys,math
import numpy as np
from PIL import Image,ImageDraw,ImageFont
sys.path.insert(0,'/home/al/ashfall/tools/windows')
from r23geom import MapData
OUT=Path('/home/al/ashfall/campaign/scoreboards3');ROOT=Path('/home/al/ashfall/unity')
FONT=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',15)

def prepare(m):
 T=[];C=[];MI=[]
 for c in m.chunks:
  if not c.visible or m.materials[c.material]=='sg':continue
  t=c.triangles;rgb=c.colors[c.indices.reshape(-1,3)].mean(1)[:,:3]
  mat=m.manifest['materials'][c.material]
  if m.materials[c.material]!='vc':
   tex=next((p for p in Path(m.folder).glob(m.materials[c.material]+'_c.*') if p.suffix in ['.jpg','.png']),None)
   col=np.array(Image.open(tex).convert('RGB').resize((1,1)))[0,0]/255 if tex else np.array([.5,.5,.5])
   rgb=rgb*col
  norms=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]);norms/=np.maximum(np.linalg.norm(norms,axis=1)[:,None],1e-10)
  shade=.6+.4*np.abs(norms@np.array([.3,.8,.5]));rgb=np.clip(rgb*shade[:,None],0,1)
  T.append(t);C.append((rgb*255).astype('uint8'));MI.extend([m.materials[c.material]]*len(t))
 return np.concatenate(T),np.concatenate(C),np.array(MI)

def render(data,q,back=False):
 T,C,MI=data;center=np.array(q.get('frameCenter',q['center']));n=np.array(q['normal'])*(-1 if back else 1);u=np.array(q['right'])*(-1 if back else 1);up=np.array(q.get('up',[0,1,0]));w=q.get('frameWidth',q['width']);h=q.get('frameHeight',q['height']);D=T-center;x=D@u;y=D@up;z=D@n
 # Exclude geometry between viewer and board; show its backing and nearby objects.
 keep=(x.min(1)<w*.85)&(x.max(1)>-w*.85)&(y.min(1)<h*.85)&(y.max(1)>-h*.85)&(z.min(1)<max(w,h)*.3)&(z.max(1)>-max(w,h)*.6)
 ids=np.flatnonzero(keep);W=400;H=350;s=min(W/(w*1.7),H/(h*1.7));im=Image.new('RGB',(W,H),'#152430');d=ImageDraw.Draw(im)
 # Slight lateral projection reveals thickness and source backing.
 xx=x+z*.10; yy=y+z*.04
 for i in ids[np.argsort(z[ids].mean(1))]:
  xy=np.c_[W/2+xx[i]*s,H/2-yy[i]*s]
  if np.ptp(xy[:,0])*np.ptp(xy[:,1])<.15:continue
  d.polygon([tuple(v) for v in xy],fill=tuple(C[i]),outline=tuple(np.minimum(C[i].astype(int)+12,255)))
 d.rectangle([W/2-w*s/2,H/2-h*s/2,W/2+w*s/2,H/2+h*s/2],outline='#2dd4bf',width=1)
 d.text((5,4),'BACK' if back else 'FRONT',font=FONT,fill='white')
 return im

def main():
 rows=json.load(open(OUT/'R26Candidates.json'))['maps'];records=[];sheetitems=[]
 (OUT/'source').mkdir(exist_ok=True);(OUT/'review').mkdir(exist_ok=True)
 for r in rows:
  if not r['candidates']:continue
  m=MapData(str(ROOT/Path(r['geometry']).parent));data=prepare(m);slug=r['id'].replace('archive:','')
  for q in r['candidates']:
   path=OUT/'source'/f"R26-{slug}-{q['id']}.png"
   if path.exists() and q['id'].startswith('R26-frame'):
    records.append(dict(map=r['id'],frame=q['id'],path=str(path),existing=q['approved']))
    if not q['approved']:sheetitems.append(path)
    continue
   im=Image.new('RGB',(800,410),'#101820');im.paste(render(data,q),(0,60));im.paste(render(data,q,True),(400,60));d=ImageDraw.Draw(im)
   d.text((6,4),slug+' | '+q['id'],font=FONT,fill='white');d.text((6,24),f"{q['width']:.2f} x {q['height']:.2f} m | lobby {q['lobbyDistance']:.1f} m | "+('EXISTING' if q['approved'] else 'REVIEW'),font=FONT,fill='white');im.save(path)
   records.append(dict(map=r['id'],frame=q['id'],path=str(path),existing=q['approved']))
   if not q['approved']:sheetitems.append(path)
  print(slug,len(r['candidates']),flush=True)
 # Every new placement candidate, without cherry-picking or size exclusion.
 for k in range(0,len(sheetitems),12):
  page=Image.new('RGB',(2400,1640),'#101820')
  for j,p in enumerate(sheetitems[k:k+12]):page.paste(Image.open(p),((j%3)*800,(j//3)*410))
  page.save(OUT/'review'/f'R26-review-{k//12:02}.png')
 (OUT/'R26RenderManifest.json').write_text(json.dumps(records,indent=2)+'\n');print('DONE',len(records),len(sheetitems),flush=True)
if __name__=='__main__':main()
