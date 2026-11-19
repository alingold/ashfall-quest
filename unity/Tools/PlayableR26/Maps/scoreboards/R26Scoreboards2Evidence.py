"""Read source geometry and render frame planes, context, and spawn-relative locations."""
from pathlib import Path
import json,sys
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection
sys.path.insert(0,'/home/al/ashfall/tools/windows')
from r23geom import MapData
ROOT=Path('/home/al/ashfall/unity');OUT=Path('/home/al/ashfall/campaign/scoreboards2')
plt.rcParams.update({'figure.facecolor':'#101820','axes.facecolor':'#101820','text.color':'white','axes.labelcolor':'white','xtick.color':'white','ytick.color':'white','axes.edgecolor':'#64748b','font.size':11})
def draw(ax,m,center,normal,right,w,h,depth=None):
 faces=[];colors=[];z=[]
 for c in m.chunks:
  if not c.visible:continue
  t=c.triangles;delta=t-center;u=delta@right;y=delta[:,:,1];d=delta@normal
  keep=(u.min(1)<w)&(u.max(1)>-w)&(y.min(1)<h)&(y.max(1)>-h)&(d.min(1)<max(w,h))&(d.max(1)>-max(w,h))
  if depth is not None:keep&=np.abs(d).max(1)<depth
  else:keep&=(d.min(1)>-.5)&(d.max(1)<min(w,h)*.2)
  ii=np.flatnonzero(keep);fc=c.colors[c.indices.reshape(-1,3)[ii]].mean(1)[:,:3]
  if m.materials[c.material]!='vc':fc=np.full((len(ii),3),.42 if m.materials[c.material]=='p8ad59c41' else .23)
  faces.extend(np.stack((u[ii],y[ii]),axis=2));colors.extend(fc);z.extend(d[ii].mean(1))
 ix=np.argsort(z);ax.add_collection(PolyCollection(np.array(faces)[ix],facecolors=np.array(colors)[ix],edgecolors='#78818a' if depth else 'none',linewidths=.3));ax.set_xlim(-w,w);ax.set_ylim(-h,h);ax.set_aspect('equal');ax.set_xlabel('Frame horizontal (m)');ax.set_ylabel('Height relative to center (m)')
def main():
 audit=json.load(open(OUT/('R26ScoreboardsAudit.json' if '--after' in sys.argv else 'R26BeforeScoreboardsAudit.json')))
 for slug in [a for a in sys.argv[1:] if not a.startswith('--')] or ['bi-polar-gods-trashpandakiller','chill-gods-pitdevil','flash-gods-pitdevil','badlands-gods-pitdevil']:
  row=next(r for r in audit['maps'] if r['id']=='archive:'+slug);m=MapData(str(ROOT/Path(row['geometry']).parent));qs=row['candidates']
  if not qs:qs=[dict(id='four touching outlines',center=[230.9225,193.602,7.618],normal=[0,0,1],right=[1,0,0],width=26,height=7.205,depth=.236,approved=False)]
  # one image per frame so every aperture can be inspected at full resolution
  for i,q in enumerate(qs):
   center=np.array(q.get('frameCenter',q['center']));normal=np.array(q['normal']);right=np.array(q['right']);w=q.get('frameWidth',q['width']);h=q.get('frameHeight',q['height'])
   fig,aa=plt.subplots(1,2,figsize=(13,6),constrained_layout=True)
   for ax,dep in zip(aa,[q['depth']/2+.02,None]):draw(ax,m,center,normal,right,w*.65,h*.7,dep)
   aa[0].set_title('Source outline geometry (textures represented in gray)');aa[1].set_title('Source geometry with nearby context')
   fig.suptitle(slug+' | '+q['id']+f' | {w:.2f} x {h:.2f} m | '+('accepted' if q['approved'] else 'not accepted'))
   fig.savefig(OUT/f'R26-source-{slug}-{i:02}.png',dpi=120);plt.close(fig)
  fig,ax=plt.subplots(figsize=(9,8),constrained_layout=True)
  for c in m.chunks:
   if c.visible and m.materials[c.material] not in ['sg','source-walkthrough']:
    t=c.triangles;ax.add_collection(PolyCollection(t[:,:,[0,2]],facecolors='#263643',edgecolors='none'))
  for i,q in enumerate(qs):
   p=np.array(q['center']);r=np.array(q['right'])*q['width']/2;a=p-r;b=p+r;ax.plot([a[0],b[0]],[a[2],b[2]],color='#2dd4bf',lw=2);ax.text(p[0],p[2],str(i),color='white')
  p=np.array(row['lobbySpawn']);ax.scatter(p[0],p[2],c='#ffb454',s=90,marker='*',label='Lobby spawn');ax.legend(loc="upper right");pts=np.array([q['center'] for q in qs]+[row['lobbySpawn']]);lo=pts.min(0);hi=pts.max(0);margin=max(20,float(np.max(hi-lo))*.2);ax.set_xlim(lo[0]-margin,hi[0]+margin);ax.set_ylim(lo[2]-margin,hi[2]+margin);ax.set_aspect('equal');ax.set_xlabel('Map X (m)');ax.set_ylabel('Map Z (m)');ax.set_title(slug+' | source plan and frame indices');fig.savefig(OUT/f'R26-plan-{slug}.png',dpi=120);plt.close(fig)
if __name__=='__main__':main()
