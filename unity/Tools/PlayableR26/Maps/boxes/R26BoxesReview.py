"""BOXES: reproducible collision/marker inspection of every shipped launch payload.
Geometry is Unity local (Y up); semantic/recovery points are source local (Z reflected).
Run analytics first, then render [slugs]. No archived raw payload is modified.
"""
import sys,json,hashlib,importlib.util,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ROOT=Path('/home/al/ashfall/unity'); OUT=Path('/home/al/ashfall/campaign/boxes'); SA=ROOT/'Assets/StreamingAssets/PlayableR23'
spec=importlib.util.spec_from_file_location('maps',Path(__file__).with_name('R26MapData.py')); maps=importlib.util.module_from_spec(spec);spec.loader.exec_module(maps);maps.ROOT=str(ROOT);maps.SA=str(SA);maps.RES=str(ROOT/"Assets/Ashfall/Integration/Playable/World/Resources")
REC={r['id']:r for r in json.loads((SA/'TeamLaunch/ArchiveRecovery/maps.json').read_bytes())['rows']}
CAT=json.loads((SA/'TeamLaunch/launch-maps.json').read_bytes())['rows']
PAYLOADS={json.loads(p.read_bytes())['mapId']:str(p.parent.relative_to(SA)) for p in SA.rglob('map.json') if (p.parent/'geometry.bytes').exists()}
for row in CAT: row['payloadFolder']=PAYLOADS[row['id']]
FONT='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
def pts(rows):return np.array([[p['x'],p['y'],-p['z']] for p in rows],float).reshape(-1,3)
def ray(T,p,above=False):
 a,b,c,mat,normal,lo,hi=T
 sel=np.flatnonzero((lo[:,0]<=p[0])&(hi[:,0]>=p[0])&(lo[:,2]<=p[2])&(hi[:,2]>=p[2])&(np.abs(normal[:,1])>(1e-5 if above else .7))&((hi[:,1]>p[1]) if above else (lo[:,1]<=p[1])))
 if not len(sel):return None
 A,B,C=a[sel],b[sel],c[sel];d=(B[:,2]-C[:,2])*(A[:,0]-C[:,0])+(C[:,0]-B[:,0])*(A[:,2]-C[:,2]);ok=np.abs(d)>1e-10;d=np.where(ok,d,1)
 u=((B[:,2]-C[:,2])*(p[0]-C[:,0])+(C[:,0]-B[:,0])*(p[2]-C[:,2]))/d;v=((C[:,2]-A[:,2])*(p[0]-C[:,0])+(A[:,0]-C[:,0])*(p[2]-C[:,2]))/d
 y=u*A[:,1]+v*B[:,1]+(1-u-v)*C[:,1];ok&=(u>=-1e-6)&(v>=-1e-6)&(u+v<=1+1e-6)&((y>p[1]+.01) if above else (y<=p[1]+.001))
 if not ok.any():return None
 ids=np.flatnonzero(ok);j=ids[np.argmin(y[ids]) if above else np.argmax(y[ids])];return float(y[j]),str(mat[sel[j]])
def world(ch):
 a,b,c,m=maps.collision_tris(ch if any(c['flags'] for c in ch) else [dict(c,flags=1) for c in ch]);n=np.cross(b-a,c-a);n/=np.maximum(1e-12,np.linalg.norm(n,axis=1))[:,None];return a,b,c,m,n,np.minimum(np.minimum(a,b),c),np.maximum(np.maximum(a,b),c)
def analyze(row):
 folder=SA/row['payloadFolder'];m,ch=maps.load(str(folder));T=world(ch);mid=m['mapId'];rec=REC.get(mid);sem=maps.semantics(str(folder));marks=[]
 # Native deathmatch rings are verified authoring markers; recorded points are
 # archived source recorded spawn hints, kept separate from elevated warmup rings.
 native=[q['center'] for q in (sem or {}).get('deathmatch',[])];marks=np.array(native,float).reshape(-1,3);marks[:,2]*=-1
 recorded=pts(rec['recorded']) if rec else np.empty((0,3));warm=pts(rec['warmup']) if rec else np.empty((0,3))
 hints=marks if len(marks) else recorded
 lo=np.array(m['boundsMin'],float);hi=np.array(m['boundsMax'],float)
 floorh=[]
 for p in hints:
  hit=ray(T,p+[0,1.5,0]);
  if hit and p[1]-8<=hit[0]<=p[1]+1:floorh.append([p[0],hit[0]+.12,p[2]])
 # Source play bounds, not the enormous imported backdrop, define arena coverage.
 base=float(np.median(hints[:,1])) if len(hints) else float(m.get('gameSpawn',m['spawn'])[1]);grid=[];ceil=[];force=0
 for z in np.linspace(lo[2]+1,hi[2]-1,13):
  for x in np.linspace(lo[0]+1,hi[0]-1,13):
   roof=ray(T,[x,base+2.5,z],True);ceil.append(roof)
   if roof and roof[1]==maps.FORCEFIELD:force+=1
   hit=ray(T,[x,base+1.5,z]);
   if hit and base-30<=hit[0]<=base+1.5:grid.append([x,hit[0]+.12,z])
 coverage=sum(q is not None for q in ceil)/len(ceil);fc=force/len(ceil)
 # Forcefield material alone is insufficient: require a broad roof; upright fences
 # or a small local building are not an arena box.
 verdict='BOXED' if fc>=.55 or coverage>=.92 else 'UNSURE' if fc>.12 or coverage>.62 else 'OPEN'
 roofs=[q[0] for q in ceil if q];ceiling=float(np.median(roofs)) if roofs else None
 proposed=np.array(floorh if floorh else grid,float).reshape(-1,3)
 if verdict=='BOXED' and ceiling is not None:proposed=proposed[proposed[:,1]<ceiling-2] if len(proposed) else proposed
 current=[]
 # Mirror existing plan: recorded-floor candidates if valid; otherwise top-down
 # grid. This is a static candidate overlay, not a claimed runtime spawn trace.
 if floorh:current=floorh
 else:
  for z in np.linspace(lo[2]+1,hi[2]-1,9):
   for x in np.linspace(lo[0]+1,hi[0]-1,9):
    hit=ray(T,[x,T[6][:,1].max()+5,z]);
    if hit:current.append([x,hit[0]+.12,z])
 current=np.concatenate([np.array(current,float).reshape(-1,3),warm])
 problem='roof-candidates' if verdict=='BOXED' and len(current) and ceiling is not None and (current[:,1]>=ceiling-1).any() else 'ok'
 reason=f'Roof above {coverage:.0%} of 169 arena probes; forcefield roof {fc:.0%}; {len(marks)} native match markers, {len(recorded)} recorded spawn hints, {len(floorh)} marker/figure floors.'
 return dict(mapId=mid,title=m['title'],folder=row['payloadFolder'],verdict=verdict,pods=False if verdict=='BOXED' else mid not in maps.GROUND,spawnMode=('authored-markers' if floorh else 'interior-floor') if verdict=='BOXED' else 'top-down',confidence=.97 if fc>.9 or coverage<.2 else .8,reason=reason,currentSpawnProblem=problem,roofCoverage=coverage,forcefieldCoverage=fc,ceiling=ceiling,base=base,sourceSha256=m['sourceSha256'],geometrySha256=hashlib.sha256((folder/'geometry.bytes').read_bytes()).hexdigest(),markers=marks.tolist(),figureHints=recorded.tolist(),warmupMarkers=warm.tolist(),currentCandidates=current.tolist(),proposedSpawns=proposed.tolist(),boundsMin=lo.tolist(),boundsMax=hi.tolist()),ch

def render(r,ch):
 W,H=1800,730;im=Image.new('RGB',(W,H),'#101722');d=ImageDraw.Draw(im);font=lambda n:ImageFont.truetype(FONT,n)
 d.text((20,12),r['title']+' | '+('EXCLUDED / ' if r.get('excluded') else '')+r['verdict']+' | pods '+str(r['pods'])+' | '+r['spawnMode'],font=font(23),fill='white')
 d.text((20,45),r['mapId']+' | confidence '+str(r['confidence']),font=font(17),fill='#cbd5e1')
 d.text((20,72),'Arena roof %.0f%% | Marker/figure ceiling %.0f%% (forcefield %.0f%%) | %d match rings + %d figure hints | Review geometry below'%(100*r['roofCoverage'],100*r.get('markerRoofCoverage',0),100*r.get('markerForcefieldCoverage',0),len(r['markers']),len(r['figureHints'])),font=font(16),fill='#cbd5e1')
 lo=np.array(r['boundsMin']);hi=np.array(r['boundsMax']);base=r['base'];ceil=r['ceiling'];cut=(ceil-1 if ceil is not None and r['verdict']!='OPEN' else base+18)
 triangles=[];colors=[];isforce=[]
 for c in ch:
  if not c['vis']:continue
  t=c['pos'][c['idx']];inside=(t.max(1)[:,0]>=lo[0]-8)&(t.min(1)[:,0]<=hi[0]+8)&(t.max(1)[:,2]>=lo[2]-8)&(t.min(1)[:,2]<=hi[2]+8)&(t.min(1)[:,1]<max(base+450,(ceil or base+40)+5))&(t.max(1)[:,1]>base-50)
  t=t[inside];col=c['col'][c['idx']].mean(1)[inside,:3];f=c['mat']==maps.FORCEFIELD
  if not len(t):continue
  if not col.any():col[:]=.55
  
  tex=next((q.get('color') for q in m['materials'] if q['name']==c['mat']),None) if False else None
  digest=hashlib.sha256(c['mat'].encode()).digest();tint=np.array([.35+v/255*.45 for v in digest[:3]])
  n=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]);n/=np.maximum(1e-9,np.linalg.norm(n,axis=1))[:,None];shade=.45+.55*np.abs(n@np.array([.25,.85,.45]));col=np.clip((col*.2+tint*.8)*shade[:,None],0,1)
  if f:col[:]=[.15,.8,.9]
  triangles.append(t);colors.append(col);isforce.extend([f]*len(t))
 ts=np.concatenate(triangles);cs=np.concatenate(colors);ff=np.array(isforce)
 for view in range(3):
  x0=view*600;d.text((x0+18,107),['OVERHEAD (roof transparent cyan)','OBLIQUE OUTSIDE','CUT-AWAY (roof + near wall removed)'][view],font=font(17),fill='white')
  tt=ts.copy();cc=cs.copy();force=ff.copy()
  if view==2:
   sel=(tt.max(1)[:,1]<cut)&(tt.mean(1)[:,2]<(lo[2]+hi[2])/2+(hi[2]-lo[2])*.4);tt=tt[sel];cc=cc[sel];force=force[sel]
  if view==0:U=np.array([1,0,0]);V=np.array([0,0,-1]);D=np.array([0,1,0])
  else:U=np.array([.8,0,-.6]);V=np.array([-.25,.91,-.33]);D=np.cross(U,V)
  uv=np.stack([tt@U,-tt@V],axis=-1);dep=tt.mean(1)@D
  bounds=uv.reshape(-1,2);mn=bounds.min(0);mx=bounds.max(0);scale=min(560/max(1,mx[0]-mn[0]),490/max(1,mx[1]-mn[1]));offset=np.array([x0+300,390])-(mn+mx)*.5*scale
  area=np.abs(np.cross(uv[:,1]-uv[:,0],uv[:,2]-uv[:,0]))*scale**2;sel=np.flatnonzero(area>.7);sel=sel[np.argsort(dep[sel])]
  draw=ImageDraw.Draw(im,'RGBA')
  for i in sel:
   p=[tuple(v) for v in uv[i]*scale+offset];col=tuple((cc[i]*255).astype(int))
   if force[i]:draw.polygon(p,fill=col+(35 if view==0 else 65,),outline=col+(125,))
   else:draw.polygon(p,fill=col+(255,),outline=tuple(int(v*.75) for v in col)+(255,) if area[i]>200 else None)
  d=ImageDraw.Draw(im)
  for key,col,rad in [('currentCandidates','#ff4545',8),('figureHints','#61ee70',5),('markers','#61ee70',5),('warmupMarkers','#61ee70',5),('proposedSpawns','#369aff',2)]:
   for p in np.array(r[key]).reshape(-1,3):
    q=np.array([p@U,-p@V])*scale+offset
    if x0+5<q[0]<x0+595 and 130<q[1]<650:
     if key=='proposedSpawns':d.rectangle((q[0]-rad,q[1]-rad,q[0]+rad,q[1]+rad),outline=col,width=2)
     else:d.ellipse((q[0]-rad,q[1]-rad,q[0]+rad,q[1]+rad),outline=col,width=2)
  d.text((x0+18,650),'X/Z metres; Y is height' if view==0 else f'Floor seed Y={base:.1f}; ceiling '+(f'{ceil:.1f}' if ceil is not None else 'none'),font=font(15),fill='#cbd5e1')
 d.text((20,685),'RED: existing candidate sites   GREEN: authored match/warmup markers + recorded spawn hints   BLUE squares: proposed floor sites',font=font(19),fill='white')
 d.text((20,711),'Static candidate reconstruction, including elevated warmup sites; not a runtime trace. Blue sites are resnapped by native physics.',font=font(13),fill='#cbd5e1')
 im.save(OUT/'review'/(r['mapId'].replace('archive:','')+'.png'))

def sheets(rows):
 for start in range(0,len(rows),12):
  page=Image.new('RGB',(1800,4*300),'#101722')
  for i,r in enumerate(rows[start:start+12]):
   p=OUT/'review'/(r['mapId'].replace('archive:','')+'.png')
   if p.exists():
    im=Image.open(p);im.thumbnail((600,270));page.paste(im,((i%3)*600,(i//3)*300))
    ImageDraw.Draw(page).text(((i%3)*600+5,(i//3)*300+270),r['mapId'].replace('archive:','')+' '+r['verdict'],font=ImageFont.truetype(FONT,14),fill='white')
  page.save(OUT/'review'/f'overview-{start//12+1:02}.png')
if __name__=='__main__':
 args=sys.argv[2:];mode=sys.argv[1] if len(sys.argv)>1 else 'analyze'
 if mode=='analyze':
  rows=[]
  for row in CAT:
   r,ch=analyze(row);rows.append(r);(OUT/'analysis.json').write_text(json.dumps(rows,indent=1)+'\n');print(r['mapId'],r['verdict'],round(r['roofCoverage'],2),round(r['forcefieldCoverage'],2),len(r['proposedSpawns']),flush=True)
  (OUT/'analysis.json').write_text(json.dumps(rows,indent=1)+'\n')
 else:
  rows=json.loads((OUT/'analysis.json').read_bytes())
  for r in rows:
   if args and r['mapId'].replace('archive:','') not in args:continue
   m,ch=maps.load(str(SA/r['folder']));render(r,ch);print('render',r['mapId'],flush=True)
  sheets(rows)
