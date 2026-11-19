# MAPS workstream (campaign r12, playtest 2026-11-15): offline map-import data.
# Regenerates three runtime data files from the shipped map payloads:
#   1. World/Resources/R26MapAuthoringMarkers.json  (MAP-SPAWN-MARKERS)
#      Visible, non-colliding chunks made only of source authoring-marker triangles
#      (spawn humanoids/rings, warmup rings) listed in each map's
#      source-semantics.json marker_parts. Runtime skips building those chunks.
#   2. World/Resources/R26MapPodPolicy.json         (MAP-PODS-MAGNUM)
#      Explicit per-map pod decisions plus auto-detected "boxed" maps: a colliding
#      forcefield-lattice (pb8a19331) surface above >=90% of the match spawns.
#   3. StreamingAssets/PlayableR23/JumpPads/pads.json (MAP-JUMPPADS-MAGNUM)
#      Existing rows kept byte-for-byte; every other map gets its importer-detected
#      source-semantics jump rows (Z reflected once, as prepare.py did) plus
#      geometry-detected down-scaled launch frames, aimed at the platform they serve.
# Usage: python3 -I r26_map_data.py <repo root>   (run from any directory)
import sys,os,json,math,struct,collections,hashlib
import numpy as np

ROOT=os.path.abspath(sys.argv[1] if len(sys.argv)>1 else os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','..'))
SA=os.path.join(ROOT,'Assets/StreamingAssets/PlayableR23')
RES=os.path.join(ROOT,'Assets/Ashfall/Integration/Playable/World/Resources')
FORCEFIELD='pb8a19331'
EXPLICIT_PODS={
  'archive:magnum-chaosvr':(False,'Playtest 2026-11-15 (call 48:37-49:44): 19 small walled sky rings under a forcefield box; Team Revive spawns inside, no pods.'),
  'archive:magnum-3-chaosvr':(False,'Same Magnum-only ring arena family as archive:magnum-chaosvr (bridged rings under a forcefield box); spawn inside, no pods.'),
}
# Mirrors R24ArchiveRoundPolicy.GroundTeams (already pod-free in code).
GROUND=set('archive:'+s for s in '''beast-mode-gods-pitdevil blue-gods-quietowl candle-magic-gods-goodwitch chaotic-gods-alamo-hank
ereptile-disfunction-gods-pitdevil flash-gods-pitdevil g-spot-gods-pitdevil g0dtham-gods-poplightning high-ground-gods-thor
hildrs-echo-gods-pitdevil melipop-gods-poplightning mushy-gods-putangfreyja oppression-gods-thor plan-b-gods-pitdevil
poppy-gods-poplightning quietowl-gods-pitdevil realtree-gods-poplightning robotics-2-gods-pitdevil romulus-gods-skblong
suffering-gods-thor the-powerpuff-g0ds-gods-meli the-white-house-gods-osprey-kat thor-city-gods-thor
battlefield-paintball-gods-pitdevil skb-paintball-gods-skblong 10000-days-gods-crazytrain afterlife-gods-alamo-hank 5-levels-zyro'''.split())

# ---------------------------------------------------------------- geometry
def load(folder):
    m=json.load(open(os.path.join(folder,'map.json')))
    stride=72 if m['version']==2 else 48
    data=open(os.path.join(folder,'geometry.bytes'),'rb').read()
    magic,n=struct.unpack_from('<ii',data,0)
    if magic!=0x52323357 or n!=m['chunks']:raise ValueError('geometry header '+folder)
    off=8;chunks=[]
    for c in range(n):
        mat,flags,vis,nv,ni=struct.unpack_from('<iiiii',data,off);off+=20
        raw=np.frombuffer(data,dtype='<f4',count=nv*stride//4,offset=off).reshape(nv,stride//4);off+=nv*stride
        idx=np.frombuffer(data,dtype='<i4',count=ni,offset=off).reshape(-1,3);off+=ni*4
        chunks.append(dict(mat=m['materials'][mat]['name'],flags=flags,vis=vis,pos=raw[:,0:3].astype(np.float64),col=raw[:,8:12].astype(np.float64),idx=idx))
    if off!=len(data):raise ValueError('trailing geometry '+folder)
    return m,chunks
def maps():
    world=os.path.join(SA,'World')
    if os.path.exists(os.path.join(world,'geometry.bytes')):yield world
    for root in ('Maps','Legacy/Maps'):
        for d in sorted(os.listdir(os.path.join(SA,root))):
            p=os.path.join(SA,root,d)
            if os.path.exists(os.path.join(p,'geometry.bytes')):yield p
def semantics(folder):
    m=json.load(open(os.path.join(folder,'map.json')))
    f=os.path.join(folder,'source-semantics.json')
    if os.path.exists(f):
        data=json.load(open(f));sem=dict(data['semantics'])
        sem['marker_parts']=sem.get('marker_parts',[])+data.get('caches',{}).get('marker_parts',[])
    else:
        # Original World/Alfa/Classic exports predate the per-map sidecars.
        f=os.path.join(ROOT,'Tools/PlayableR23/SourceWorldExport/runs/prepared-r23-valley-range/reference/assets/data/map_semantics_r14.json')
        sem=json.load(open(f))['maps'].get(m['mapId'].replace('archive:',''))
    if sem and sem['source_sha256']!=m['sourceSha256']:raise ValueError('semantic source hash '+m['mapId'])
    return sem

# ---------------------------------------------------------------- 1. markers
def marker_chunks(m,ch,sem,eps=.01,partials=None):
    boxes=collections.defaultdict(list)
    for part in sem['marker_parts'] if sem else []:
        lo,hi=part['min'],part['max']  # source Godot local; reflect Z once
        boxes[part['key']].append((lo[0]-eps,lo[1]-eps,-hi[2]-eps,hi[0]+eps,hi[1]+eps,-lo[2]+eps))
    rows=[]
    for i,c in enumerate(ch):
        if c['flags']!=0 or not c['vis'] or c['mat'] not in boxes:continue
        B=np.array(boxes[c['mat']]);T=c['pos'][c['idx']]  # (t,3,3)
        lo=T.min(axis=(0,1));hi=T.max(axis=(0,1));B=B[np.all(B[:,:3]<=hi,axis=1)&np.all(B[:,3:]>=lo,axis=1)]
        inside=np.zeros(len(T),bool)
        for b in B:
            ok=np.all((T[:,:,0]>=b[0])&(T[:,:,0]<=b[3])&(T[:,:,1]>=b[1])&(T[:,:,1]<=b[4])&(T[:,:,2]>=b[2])&(T[:,:,2]<=b[5]),axis=1)
            inside|=ok
            if inside.all():break
        # Older exports retain translated copies of the exact 110-triangle
        # ring/arrow prototype outside the sparse semantic bounds. Hide a whole
        # chunk only if EVERY block is that measured prototype (0.0001 m).
        selected=np.flatnonzero(inside)
        if len(selected)==110 and np.all(np.diff(selected)==1) and len(T)%110==0:
            template=T[selected]
            if all(np.max(np.abs((T[k:k+110]-template)-(T[k:k+110]-template).mean(axis=(0,1))))<.0001 for k in range(0,len(T),110)):inside[:]=True
        if inside.all():rows.append((i,c['mat'],len(T)))
        elif inside.any() and partials is not None:
            selected=np.flatnonzero(inside);ranges=[];start=last=int(selected[0])
            for at in selected[1:]:
                at=int(at)
                if at==last+1:last=at
                else:ranges += [start,last-start+1];start=last=at
            ranges += [start,last-start+1]
            partials.append(dict(chunk=i,material=c['mat'],triangles=len(T),ranges=ranges))
    return rows

# ---------------------------------------------------------------- 2. pod policy
def collision_tris(ch):
    A=[];B=[];C=[];M=[]
    for c in ch:
        if c['flags']==0:continue
        T=c['pos'][c['idx']];A.append(T[:,0]);B.append(T[:,1]);C.append(T[:,2]);M+= [c['mat']]*len(T)
    return np.vstack(A),np.vstack(B),np.vstack(C),np.array(M)
def surface_above(T,p,rise=2.5):
    A,B,C,M=T;lo=np.minimum(np.minimum(A,B),C);hi=np.maximum(np.maximum(A,B),C)
    sel=np.where((lo[:,0]<=p[0])&(hi[:,0]>=p[0])&(lo[:,2]<=p[2])&(hi[:,2]>=p[2])&(hi[:,1]>p[1]+rise))[0]
    best=None
    for t in sel:
        a,b,c=A[t],B[t],C[t];d=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
        if abs(d)<1e-12:continue
        l1=((b[2]-c[2])*(p[0]-c[0])+(c[0]-b[0])*(p[2]-c[2]))/d;l2=((c[2]-a[2])*(p[0]-c[0])+(a[0]-c[0])*(p[2]-c[2]))/d;l3=1-l1-l2
        if min(l1,l2,l3)<-1e-6:continue
        y=l1*a[1]+l2*b[1]+l3*c[1]
        if y>p[1]+rise and (best is None or y<best[0]):best=(y,M[t])
    return best
def lattice_boxed(m,T,recorded):
    pts=[m.get('gameSpawn') or m['spawn']]+[[q['x'],q['y']+.1,-q['z']] for q in recorded]
    hits=[surface_above(T,p) for p in pts];lattice=sum(1 for h in hits if h and h[1]==FORCEFIELD)
    return lattice,len(pts)

# ---------------------------------------------------------------- 3. pads
RIM='pee49fa59';FRAME_RIM_TRIS=60;FRAME_UNIT_WIDTH=3.04  # three-support launch frame, measured on importer rows (cont-cl-ssd)
def components(T):
    key={};parent=list(range(len(T)))
    def f(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    for i,t in enumerate(T):
        for v in t:
            k=(round(v[0],4),round(v[1],4),round(v[2],4))
            if k in key:
                a,b=f(i),f(key[k])
                if a!=b:parent[a]=b
            else:key[k]=i
    g=collections.defaultdict(list)
    for i in range(len(T)):g[f(i)].append(i)
    return [T[v] for v in g.values()]
def detect_frames(ch):
    chev=[];rims=[]
    for c in ch:
        T=c['pos'][c['idx']]
        if c['mat']=='vc':
            col=c['col'][c['idx']]  # gold chevrons: every vertex gold
            g=np.all((col[:,:,0]>.6)&(col[:,:,1]>.35)&(col[:,:,2]<.35)&(col[:,:,0]>col[:,:,2]+.3),axis=1)
            for comp in components(T[g]):
                if len(comp)==52:chev.append(comp.reshape(-1,3))
        elif c['mat']==RIM:
            rims+=[(comp.reshape(-1,3),len(comp)) for comp in components(T)]
    cen=[p.mean(0) for p in chev];ext=[float(np.linalg.norm(p.max(0)-p.min(0))) for p in chev]
    used=set();out=[];rimcen=[r[0].mean(0) for r in rims]
    for i in range(len(chev)):
        if i in used:continue
        best=None
        for j in range(len(chev)):
            if j==i or j in used:continue
            d=float(np.linalg.norm(cen[i]-cen[j]))
            if d<ext[i]*1.6 and abs(ext[i]-ext[j])<.05*ext[i] and (best is None or d<best[0]):best=(d,j)
        if best is None:continue
        used|={i,best[1]};P=np.vstack([chev[i],chev[best[1]]]);c=P.mean(0);e=float(np.linalg.norm(P.max(0)-P.min(0)))
        near=[k for k,rc in enumerate(rimcen) if np.linalg.norm(rc-c)<e*1.4]
        if not near:continue
        tris=sum(rims[k][1] for k in near)
        if tris!=FRAME_RIM_TRIS:continue
        R=np.vstack([rims[k][0] for k in near]);mu=R.mean(0);n=np.linalg.svd(R-mu)[2][2]
        s=(P-mu)@n
        if s.max()<-s.min():n=-n  # chevron tip forward
        w=float(np.max(np.linalg.norm(R-mu,axis=1))*2)
        # The contact plane belongs on the front chevron surface, not the rim
        # center behind it: Flight raycasts from +0.20 to +0.12 m along normal.
        front=max(0.0,float(np.max((P-mu)@n)))+.02
        out.append(dict(center=mu+n*front,normal=n/np.linalg.norm(n),scale=w/FRAME_UNIT_WIDTH,frontOffset=front))
    return out
# Flight model mirrors FlightMachine.Launch + R26Freefall (ascent g=16.2, descent
# drag toward 25 m/s), the 0.3 s launch clearance (constant velocity),
# and no-input horizontal relaxation (0.8 m/s^2). Semi-implicit position steps.
G=16.2;VT=25.0;DT=1/72.;HEAD=1.75
def fall(vy,dt):
    rise=vy-G*dt
    if vy>0 and rise>=0:return rise
    t=dt;s=-vy
    if vy>0:t-=vy/G;s=0
    x=t*G/VT
    if s<VT:u=s/VT;s=VT*math.tanh(.5*math.log((1+u)/(1-u))+x)
    elif s>VT:u=VT/s;s=VT/math.tanh(.5*math.log((1+u)/(1-u))+x)
    return -s
def first_hit(P0,P1,A,E1,E2):
    if len(A)==0 or len(P0)==0:return None
    D=P1-P0;h=np.cross(D[:,None,:],E2[None,:,:]);a=np.einsum('tj,stj->st',E1,h)
    ok=np.abs(a)>1e-12;f=np.where(ok,1/np.where(ok,a,1),0)
    s=P0[:,None,:]-A[None,:,:];u=f*np.einsum('stj,stj->st',s,h)
    q=np.cross(s,E1[None,:,:]);w=f*np.einsum('stj,sj->st',q,D);tt=f*np.einsum('tj,stj->st',E2,q)
    M=ok&(u>=0)&(u<=1)&(w>=0)&(u+w<=1)&(tt>=0)&(tt<=1)
    if not M.any():return None
    i=int(np.where(M.any(1))[0][0]);js=np.where(M[i])[0];k=int(js[np.argmin(tt[i,js])]);return i,float(tt[i,k]),k
def simulate(center,n,speed,A,E1,E2,N,tmax=7.0,skip=1.2):
    v=n*speed;h=np.array([v[0],0,v[2]]);hs=float(np.linalg.norm(h));hd=h/hs if hs>1e-9 else h
    vy=float(v[1]);p=center.copy();t=0;trav=0;clamped=False;clear=.3
    while t<tmax and p[1]>center[1]-60:
        pts=[p.copy()]
        for _ in range(18):
            clear=max(0.0,clear-DT)
            if clear<=0:vy=fall(vy,DT);hs=max(0.0,hs-.8*DT)
            p=p+hd*hs*DT+np.array([0,vy*DT,0]);pts.append(p.copy());t+=DT
        pts=np.array(pts);seg=np.linalg.norm(np.diff(pts,axis=0),axis=1);start=trav;trav+=float(seg.sum());k0=0
        if start<skip:
            k0=int(np.searchsorted(start+np.concatenate([[0],np.cumsum(seg)]),skip))
            if k0>=len(pts)-1:continue
        if not clamped:  # head meets a ceiling: vertical speed stops, horizontal continues
            hh=first_hit(pts[k0:-1]+[0,HEAD,0],pts[k0+1:]+[0,HEAD,0],A,E1,E2)
            if hh is not None:
                i,tt,k=hh;p=pts[k0+i]+(pts[k0+i+1]-pts[k0+i])*tt;vy=min(vy,0.0);clear=0;clamped=True;trav=start+float(seg[:k0+i].sum());continue
        fh=first_hit(pts[k0:-1],pts[k0+1:],A,E1,E2)
        if fh is not None:
            i,tt,k=fh;return pts[k0+i]+(pts[k0+i+1]-pts[k0+i])*tt,abs(float(N[k][1]))>=.7
    return None,False
def floor_below(T,x,z,top,depth):
    A,B,C,N,lo,hi=T
    sel=np.where((lo[:,0]<=x)&(hi[:,0]>=x)&(lo[:,2]<=z)&(hi[:,2]>=z)&(np.abs(N[:,1])>=.7)&(lo[:,1]<=top)&(hi[:,1]>=top-depth))[0]
    for t in sel:
        a,b,c=A[t],B[t],C[t];d=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
        if abs(d)<1e-12:continue
        l1=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/d;l2=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/d;l3=1-l1-l2
        if min(l1,l2,l3)>=-1e-6 and top-depth<=l1*a[1]+l2*b[1]+l3*c[1]<=top:return True
    return False
def aim(world,center,n,fixed_speed=None):
    A,B,C,N,lo,hi=world;h=np.array([n[0],0,n[2]]);hl=float(np.linalg.norm(h))
    if hl<.2:return None,'near-vertical normal'
    hd=h/hl;end=center+hd*170;blo=np.minimum(center,end)-4;bhi=np.maximum(center,end)+4;blo[1]=center[1]-62;bhi[1]=center[1]+40
    m=(hi[:,0]>=blo[0])&(lo[:,0]<=bhi[0])&(hi[:,2]>=blo[2])&(lo[:,2]<=bhi[2])&(hi[:,1]>=blo[1])&(lo[:,1]<=bhi[1])
    mid=(lo+hi)*.5;rel=mid-center;perp=np.abs(rel[:,0]*hd[2]-rel[:,2]*hd[0]);m&=perp<=4+np.max(hi-lo,axis=1)*.5
    idx=np.where(m)[0];cA,cB,cC,cN=A[idx],B[idx],C[idx],N[idx];E1=cB-cA;E2=cC-cA;corr=(cA,cB,cC,cN,lo[idx],hi[idx])
    samples=[]
    for s in ([fixed_speed] if fixed_speed is not None else np.arange(8,45.01,.5)):
        q,walk=simulate(center,n,float(s),cA,E1,E2,cN)
        d=None if q is None else float(np.linalg.norm((q-center)[[0,2]]))
        samples.append((float(s),q,walk,d))
    if fixed_speed is not None:
        s,q,walk,d=samples[0]
        if q is not None and walk and d>=6:return dict(speed=s,distance=round(d,2),landing=[round(float(v),3) for v in q]),'fixed speed verified'
        return None,'fixed speed misses walkable landing'
    def gap(d):
        for x in np.arange(1.5,d-1.0,1.0):
            q=center+hd*x
            if not floor_below(corr,q[0],q[2],center[1]+1.0,30.0):return True
        return False
    groups=[];cur=[];miss=0
    for s,q,walk,d in samples:
        ok=q is not None and walk and d>=6
        if ok and cur and abs(d-cur[-1][3])<4 and abs(q[1]-cur[-1][1][1])<1.5:cur.append((s,q,walk,d));miss=0;continue
        if ok:
            if cur:groups.append(cur)
            cur=[(s,q,walk,d)];miss=0;continue
        miss+=1
        if cur and miss>1:groups.append(cur);cur=[]
    if cur:groups.append(cur)
    choices=[]
    for g in groups:
        if len(g)<2:continue
        dmid=(min(x[3] for x in g)+max(x[3] for x in g))*.5;pick=min(g,key=lambda x:abs(x[3]-dmid))
        choice=dict(speed=pick[0],band=[g[0][0],g[-1][0]],distance=round(pick[3],2),landing=[round(float(v),3) for v in pick[1]])
        if gap(pick[3]):return choice,'aimed across gap'
        choices.append(choice)
    # Bridged arenas still have authored launch frames: a walkable connection
    # beneath the trajectory must not disable an otherwise valid launch.
    if choices:return choices[0],'aimed over connected floor'
    return None,'no walkable landing found'

def main():
    if sys.argv[2:]==['--markers-only']:
        rows=[]
        for folder in maps():
            m,ch=load(folder);parts=[];mk=marker_chunks(m,ch,semantics(folder),partials=parts)
            if parts:raise ValueError('Mixed authoring geometry needs triangle-level review: '+m['mapId'])
            if mk:rows.append(dict(id=m['mapId'],sourceSha256=m['sourceSha256'],chunks=[r[0] for r in mk],materials=[r[1] for r in mk],triangles=[r[2] for r in mk],parts=parts))
        path=os.path.join(RES,'R26MapAuthoringMarkers.json');data=json.load(open(path));data['maps']=rows
        data['rule']='Visible non-colliding chunks whose every triangle lies inside a same-material source authoring marker AABB (+-0.01 m, Z reflected once), including cache markers and exact translated 110-triangle ring/arrow copies (0.0001 m tolerance). Semantics source hash must match.'
        open(path,'w',newline='\n').write(json.dumps(data,indent=1)+'\n')
        print('Marker maps',len(rows),'chunks',sum(len(r['chunks']) for r in rows),flush=True);return
    records=json.load(open(os.path.join(SA,'TeamLaunch/ArchiveRecovery/maps.json')))['rows'];recorded={r['id']:r['recorded'] for r in records}
    selected=set(sys.argv[2:])  # Optional map ids: update only these pad rows, leave policy/markers intact.
    padpath=os.path.join(SA,'JumpPads/pads.json');pads=json.load(open(padpath));order={r['mapId']:i for i,r in enumerate(pads['maps'])};frozen={'archive:battlefield-paintball-gods-pitdevil','archive:alfa-aglae-souza','archive:classic-bigbox-vr','range','valley'}
    if not frozen.issubset(order):raise ValueError('Five preserved baseline pad maps required')
    if selected:frozen.update(mid for mid in order if mid not in selected)
    pads['maps']=[row for row in pads['maps'] if row['mapId'] in frozen]
    marker_rows=[];pod_rows=[];pad_rows=[];report=collections.OrderedDict()
    for folder in maps():
        if selected and (json.load(open(os.path.join(folder,'map.json')))['mapId'] not in selected or os.sep+'Legacy'+os.sep in folder):continue  # r19: the Legacy copy of a restored map must not become a second row
        m,ch=load(folder);mid=m['mapId'];sem=semantics(folder);note=report.setdefault(mid,{})
        parts=[];mk=marker_chunks(m,ch,sem,partials=parts)
        if parts:raise ValueError('Mixed authoring geometry needs triangle-level review: '+mid)
        if mk:marker_rows.append(dict(id=mid,sourceSha256=m['sourceSha256'],chunks=[r[0] for r in mk],materials=[r[1] for r in mk],triangles=[r[2] for r in mk],parts=parts));note['markerChunks']=len(mk)
        if mid.startswith('archive:') and mid not in GROUND:
            T=collision_tris(ch);lat,total=lattice_boxed(m,T,recorded.get(mid,[]))
            if mid in EXPLICIT_PODS:
                pods,why=EXPLICIT_PODS[mid];pod_rows.append(dict(id=mid,pods=pods,source='explicit',evidence=why+' Lattice roof over %d/%d match spawns.'%(lat,total)))
            elif total and lat>=.9*total:
                pod_rows.append(dict(id=mid,pods=False,source='auto',evidence='Colliding forcefield lattice (%s) above %d/%d match spawns: boxed map, pods cannot enter.'%(FORCEFIELD,lat,total)))
            note['latticeRoof']='%d/%d'%(lat,total)
        if mid in frozen:continue
        rows=[]
        for i,j in enumerate(sem['jump'] if sem else []):
            c=list(j['center']);n=list(j['normal']);c[2]*=-1;n[2]*=-1
            rows.append(dict(sourceIndex=i,center=c,normal=n,radius=j['radius'],speed=j.get('speed',45),tilted=bool(j.get('tilted',False)),origin='source-semantics'))
        note['semanticsPads']=len(rows)
        A,B,C,M=collision_tris(ch);N=np.cross(B-A,C-A);L=np.linalg.norm(N,axis=1);L[L==0]=1;N=N/L[:,None]
        world=(A,B,C,N,np.minimum(np.minimum(A,B),C),np.maximum(np.maximum(A,B),C));scaled=[];skipped=[]
        for k,f in enumerate(detect_frames(ch)):
            c=f['center'];n=f['normal']
            if any(np.linalg.norm(np.array(r['center'])-c)<.15 for r in rows):continue
            if not(.2<=f['scale']<=.75):skipped.append(round(f['scale'],2));continue
            if first_hit(np.array([c+n*.2]),np.array([c]),A,B-A,C-A) is not None:
                skipped.append('launch face obstructed by source collision');continue
            r,why=aim(world,c,n)
            if r is None:skipped.append(why);continue
            scaled.append(dict(sourceIndex=1000+k,center=[round(float(v),5) for v in c],normal=[round(float(v),5) for v in n],radius=round(max(1.3*f['scale'],.6),3),speed=r['speed'],tilted=bool(abs(n[1])<.99),origin='geometry-scaled-launch-frame',scale=round(f['scale'],3),speedBand=r['band'],landingDistance=r['distance'],aim=why))
        rows+=scaled;note['scaledPads']=len(scaled)
        if skipped:note['detectedNotEnabled']=collections.Counter(map(str,skipped))
        pad_rows.append(dict(mapId=mid,sourceSha256=m['sourceSha256'],pads=rows))
        print(mid,dict(note),flush=True)
    os.makedirs(RES,exist_ok=True)
    rule='Visible non-colliding chunks whose every triangle lies inside a same-material source-semantics marker_parts AABB (+-0.01 m, Z reflected once).'
    if not selected:open(os.path.join(RES,'R26MapAuthoringMarkers.json'),'w',newline='\n').write(json.dumps(dict(version=1,generator='~/ashfall/campaign/staging/MAPS/R26MapData.py',rule=rule,maps=marker_rows),indent=1)+'\n')
    if not selected:open(os.path.join(RES,'R26MapPodPolicy.json'),'w',newline='\n').write(json.dumps(dict(version=1,generator='~/ashfall/campaign/staging/MAPS/R26MapData.py',rule='Missing row: pods unless R24ArchiveRoundPolicy.GroundTeams. Explicit rows win; auto rows mark forcefield-boxed maps.',maps=pod_rows),indent=1)+'\n')
    pads['maps']+=pad_rows
    pads['maps'].sort(key=lambda row:order.get(row['mapId'],len(order)))
    open(padpath,'w',newline='').write((json.dumps(pads,indent=2)+'\n').replace('\n','\r\n'))  # file is CRLF
    prov_path=os.path.join(SA,'JumpPads/provenance.json');prov=json.load(open(prov_path))
    prov['maps']=[dict(mapId=r['mapId'],sourceSha256=r['sourceSha256'],pads=len(r['pads']),tilted=sum(1 for p in r['pads'] if p['tilted']),scaled=sum(1 for p in r['pads'] if p.get('origin')=='geometry-scaled-launch-frame')) for r in pads['maps']]
    prov['r26']='Campaign r12 MAPS: rows after the first five come from each map\'s source-semantics.json jump list (importer rows, Z reflected once, radius/speed preserved) and from ~/ashfall/campaign/staging/MAPS/R26MapData.py geometry detection of down-scaled three-support launch frames (radius max(1.3*scale,0.6), speed aimed at walkable landing geometry including connected floors; model includes Flight 0.3s launch clearance, g=16.2 and belly terminal=25). The first five rows are unchanged.'
    open(prov_path,'w',newline='').write(json.dumps(prov,indent=2).replace('\n','\r\n'))
    json.dump(report,open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'r26_map_data_report.json'),'w'),indent=1,default=str)
if __name__=='__main__':main()
