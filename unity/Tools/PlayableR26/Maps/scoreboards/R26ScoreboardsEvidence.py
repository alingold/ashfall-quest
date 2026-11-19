"""Diagnostic source-geometry plots. Uses actual vertex colour; textures shown gray."""
from pathlib import Path
import json,sys
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection
from matplotlib.patches import Rectangle
sys.path.insert(0,'/home/al/ashfall/tools/windows')
from r23geom import MapData
OUT=Path('/home/al/ashfall/campaign/scoreboards')
plt.rcParams.update({'figure.facecolor':'#101820','axes.facecolor':'#101820','text.color':'white','axes.labelcolor':'white','xtick.color':'white','ytick.color':'white','axes.edgecolor':'#64748b','font.size':11})
rows=json.load(open(OUT/'R26ScoreboardsAudit.json'))['maps']
for slug in sys.argv[1:] or ['battlefield-paintball-gods-pitdevil','badlands-gods-pitdevil','asylum-gods-pitdevil','afterlife-gods-alamo-hank','g0dtham-gods-poplightning']:
 row=next(r for r in rows if r['id']=='archive:'+slug);m=MapData(str(Path('/home/al/ashfall/unity')/Path(row['geometry']).parent))
 cand=row['candidates'];cand=sorted(cand,key=lambda c:c['lobbyDistance']);cand=cand[:6]
 fig,axs=plt.subplots(len(cand),2,figsize=(13,4*len(cand)),squeeze=False,constrained_layout=True)
 for q,aa in zip(cand,axs):
  center=np.array(q['center']);normal=np.array(q['normal']);right=np.array(q['right']);w=q['width'];h=q['height'];radius=max(w,h)*.8+2
  for ax,context in zip(aa,[False,True]):
   faces=[];colors=[]
   for c in m.chunks:
    if not c.visible:continue
    tri=c.triangles;delta=tri-center;u=delta@right;y=delta[:,:,1];d=delta@normal
    keep=(u.min(1)<radius)&(u.max(1)>-radius)&(y.min(1)<radius)&(y.max(1)>-radius)&(d.min(1)<radius)&(d.max(1)>-radius)
    if not context:keep &= (np.abs(d).max(1)<q['depth']/2+.02)
    ii=np.flatnonzero(keep);ii=ii[np.argsort(d[ii].mean(1))]
    fc=c.colors[c.indices.reshape(-1,3)[ii]].mean(1)[:,:3]
    if m.materials[c.material]!='vc':fc=np.full((len(ii),3),.42)
    faces.extend(np.stack((u[ii],y[ii]),axis=2));colors.extend(fc)
   ax.add_collection(PolyCollection(faces,facecolors=colors,edgecolors='#7b8794' if not context else 'none',linewidths=.3))
   ax.add_patch(Rectangle((-w/2,-h/2),w,h,fill=False,color='#2dd4bf',linewidth=2))
   ax.set_xlim(-radius,radius);ax.set_ylim(-radius,radius);ax.set_aspect('equal');ax.set_title(q['id']+(' | source frame plane' if not context else ' | projected context')+'\n'+('APPROVED' if q['approved'] else 'AMBIGUOUS')+f' | {w:.1f} x {h:.1f} m | lobby {q["lobbyDistance"]:.0f} m');ax.set_xlabel('Along frame right (m)');ax.set_ylabel('Above aperture center (m)')
 fig.savefig(OUT/('R26-source-'+slug+'.png'),dpi=120);plt.close(fig)
