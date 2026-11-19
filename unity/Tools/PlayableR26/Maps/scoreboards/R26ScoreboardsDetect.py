"""Detect complete Paintball-family scoreboard apertures without editing source geometry.
Run from ~/ashfall/unity: python3 ../tools/scoreboards/R26ScoreboardsDetect.py
Confidence is an evidence score, not a calibrated probability. Unknown families remain
none; complete frames outside conservative scoreboard context remain ambiguous.
"""
from pathlib import Path
import argparse, hashlib, json, sys
import numpy as np
sys.path.insert(0,str(Path(__file__).parent))
from R26ScoreboardsProbe import components, folders
sys.path.insert(0,str(Path(__file__).parents[1]/'windows'))
from r23geom import MapData
ROOT=Path('/home/al/ashfall/unity')
OUT=Path('/home/al/ashfall/campaign/scoreboards2')
RESOURCE=ROOT/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapScoreboards.json'
RING='pd959969c';TRIM='p8ad59c41'
REVIEWED=Path(__file__).with_name('R26ScoreboardsReviewed.json')

def cloud_error(a,b):
    if len(a)!=len(b):return 1.
    return float(np.sqrt(((a[:,None]-b[None,:])**2).sum(2)).min(1).max())

def planar(P):
    center=(P.min(0)+P.max(0))/2
    _,_,v=np.linalg.svd(P-center,full_matrices=False);n=v[-1]
    if abs(n[1])>.01:return None
    n[1]=0;n/=np.linalg.norm(n);right=np.cross([0,1,0],n)
    uv=np.c_[P@right,P[:,1]];lo=uv.min(0);size=uv.max(0)-lo
    if min(size)<.1 or np.ptp(P@n)>.003:return None
    return n,right,uv,lo,size

def template():
    m=MapData(str(ROOT/'Assets/StreamingAssets/PlayableR23/World'));target=np.array([-3.96,78.743,-26.237])
    for c in m.chunks:
        if m.materials[c.material]!=RING:continue
        for ids in components(c.triangles):
            T=c.triangles[ids];P=np.unique(np.round(T.reshape(-1,3),4),axis=0)
            if len(ids)==16 and np.linalg.norm(P.mean(0)-target)<3:
                n,r,uv,lo,size=planar(P);return (uv-lo)/size
    raise RuntimeError('accepted Paintball reference not found')

def ring_components(T):
    # Authored neighboring frames can share rail vertices. Split their connected
    # component in plane coordinates, then still require the exact 16-triangle
    # template and the independent 32-triangle metal trim below.
    groups=components(T)
    # Preserve existing candidate IDs; recovered touching rings are appended.
    groups.sort(key=lambda ids: len(ids)!=16)
    for ids in groups:
        if len(ids)==16:
            yield ids,False;continue
        if len(ids)<32:continue
        P=np.unique(np.round(T[ids].reshape(-1,3),4),axis=0);plane=planar(P)
        if plane is None:continue
        n,r,uv,lo,size=plane
        u=T[ids]@r;y=T[ids,:,1]
        xs=np.unique(np.round(uv[:,0],3));ys=np.unique(np.round(uv[:,1],3))
        for i in range(len(xs)-3):
            horizontal=(u.min(1)>=xs[i]-.001)&(u.max(1)<=xs[i+3]+.001)
            if horizontal.sum()<16:continue
            for j in range(len(ys)-4):
                keep=horizontal&(y.min(1)>=ys[j]-.001)&(y.max(1)<=ys[j+4]+.001)
                if keep.sum()==16:yield ids[keep],True

def extract(m,ref):
    ring=[];trim=[]
    for c in m.chunks:
        if not c.visible:continue
        mat=m.materials[c.material]
        if mat==RING:ring.append((c.index,c.triangles))
        if mat==TRIM:trim.append((c.index,c.triangles))
    if not ring:return []
    T=np.concatenate([t for _,t in ring]);chunks=np.concatenate([np.full(len(t),i) for i,t in ring]);parts=[]
    for ids,split in ring_components(T):
        if len(ids)!=16:continue
        P=np.unique(np.round(T[ids].reshape(-1,3),4),axis=0)
        if len(P)!=16:continue
        plane=planar(P)
        if plane is None:continue
        n,r,uv,lo,size=plane;norm=(uv-lo)/size
        error=min(cloud_error(norm,ref*np.array([sx,sy])+np.array([sx<0,sy<0])) for sx in [1,-1] for sy in [1,-1])
        if error>.001:continue
        center=r*(lo[0]+size[0]/2)+[0,lo[1]+size[1]/2,0]+n*np.mean(P@n)
        parts.append(dict(split=split,P=P,n=n,r=r,center=center,size=size,error=error,chunks=sorted(set(chunks[ids].tolist()))))
    used=set();results=[]
    for i,a in enumerate(parts):
        if i in used:continue
        options=[]
        for j,b in enumerate(parts):
            if j<=i or j in used or abs(a['n']@b['n'])<.99999:continue
            delta=b['center']-a['center'];depth=abs(delta@a['n'])
            if depth<.015 or depth>min(a['size'])*.25:continue
            if np.linalg.norm(delta-a['n']*(delta@a['n']))>.003:continue
            if np.max(np.abs(a['size']-b['size']))>.003:continue
            options.append((depth,j))
        if not options:continue
        depth,j=min(options);b=parts[j];used|={i,j};center=(a['center']+b['center'])/2;n=a['n'];r=a['r'];size=a['size']
        # Independently verify the metal joins the ring faces, including inner rails.
        gray=[];graychunks=[]
        for ci,TT in trim:
            q=TT-center;xyz=np.stack((q@r,q[:,:,1],q@n),axis=2)
            keep=np.all(np.abs(xyz)<=np.array([size[0]/2+.002,size[1]/2+.002,depth/2+.002]),axis=(1,2))
            if keep.any():gray.append(TT[keep]);graychunks.append(ci)
        gray=np.concatenate(gray) if gray else np.empty((0,3,3))
        # 20 outer-rail triangles + 12 inner-rail triangles, with all vertices shared
        # by the two independently matched rings. No bounding-box-only acceptance.
        refpoints=np.vstack((a['P'],b['P']))
        gray_error=1 if not len(gray) else float(np.linalg.norm(gray.reshape(-1,1,3)-refpoints[None,:,:],axis=2).min(1).max())
        # Touching frames duplicate the shared outer-rail triangles. Count
        # geometric triangles once, without waiving missing inner/outer rails.
        unique_trim={tuple(sorted(tuple(p) for p in tri)) for tri in np.round(gray,3)}
        complete=(len(gray)==32 or len(unique_trim)==32) and gray_error<.003
        if not complete:continue
        uv=np.c_[a['P']@r,a['P'][:,1]];ux=np.unique(np.round(uv[:,0],3));uy=np.unique(np.round(uv[:,1],3))
        if len(ux)!=4 or len(uy)!=5:continue
        width=float(ux[2]-ux[1]);height=float(uy[3]-uy[1]);center+=r*((ux[1]+ux[2])/2-center@r);center[1]=(uy[1]+uy[3])/2
        warm=np.array(m.manifest.get('warmupSpawn',m.manifest['spawn']));game=np.array(m.manifest.get('gameSpawn',m.manifest['spawn']))
        if (warm-center)@n<0:n=-n;r=-r
        distance=float(np.linalg.norm(warm-center));game_distance=float(np.linalg.norm(game-center));ratio=width/height
        # Small landscape signs, remote/giant boundary frames and wildly stretched
        # panels are retained in the audit, never automatically filled.
        reasons=[]
        if a['split'] or b['split']:reasons.append('touching frame role requires source review')
        if width<2 or height<3:reasons.append('small sign-sized aperture')
        if max(width,height)>40:reasons.append('oversized/boundary frame')
        if not .45<=ratio<=1.4:reasons.append('landscape or stretched signage')
        if distance>110:reasons.append('remote from lobby')
        accepted=not reasons
        conf=.98 if accepted and distance<45 else .92 if accepted else .55
        results.append(dict(id='R26-frame-'+str(len(results)),center=center.tolist(),normal=n.tolist(),right=np.cross(n,[0,1,0]).tolist(),width=width-.006,height=height-.006,depth=depth,frontOffset=depth/2+.015,confidence=conf,approved=accepted,reason='complete 64-triangle frame near lobby' if accepted else '; '.join(reasons),lobbyDistance=distance,gameSpawnDistance=game_distance,shapeError=max(a['error'],b['error']),trimError=gray_error,materials=[RING,TRIM],chunks=sorted(set(a['chunks']+b['chunks']+graychunks))))
    # A lobby neighborhood is stronger evidence than the generic frame family.
    # Beyond 40 m accept only a coherent large multi-face display (three or more
    # equal-height faces, including perpendicular normals), never isolated windows.
    for q in results:
        if not q['approved'] or q['lobbyDistance']<=40:continue
        cluster=[x for x in results if x['approved'] and min(x['width'],x['height'])>=6 and abs(x['center'][1]-q['center'][1])<.15 and abs(x['height']-q['height'])<.15 and np.linalg.norm(np.array(x['center'])-q['center'])<max(q['width'],q['height'])*1.6]
        coherent=len(cluster)>=3 and any(abs(np.dot(x['normal'],q['normal']))<.1 for x in cluster)
        if coherent:
            q['reason']='complete frame in coherent large multi-face display near lobby'
            centroid=np.mean([x['center'] for x in cluster],axis=0)
            if np.dot(np.array(q['center'])-centroid,q['normal'])<0:
                q['normal']=(-np.array(q['normal'])).tolist();q['right']=(-np.array(q['right'])).tolist()
        else:q.update(approved=False,confidence=.65,reason='complete frame outside immediate lobby; display role ambiguous')
    # Author-confirmed distant displays, verified against rendered source geometry.
    # A review can waive context gates only: exact source hashes, complete topology,
    # aperture location and dimensions must still match. Unknown geometry stays gated.
    reviewed=json.loads(REVIEWED.read_text())
    for row in reviewed['maps']:
        if row['id']!=m.map_id or row['sourceSha256']!=m.manifest['sourceSha256'] or row['geometrySha256']!=m.geometry_sha256:continue
        for f in row['frames']:
            matches=[q for q in results if np.linalg.norm(np.array(q['center'])-f['center'])<.005 and abs(q['width']-f['width'])<.005 and abs(q['height']-f['height'])<.005]
            if len(matches)!=1:raise RuntimeError('reviewed frame topology/location changed: '+m.map_id)
            q=matches[0];n=np.array(f['normal'])
            if abs(np.dot(n,q['normal']))<.9999:raise RuntimeError('reviewed frame plane changed: '+m.map_id)
            q.update(approved=True,confidence=.98,normal=n.tolist(),right=np.cross(n,[0,1,0]).tolist(),reason=f['reason'])
    for q in results:
        if not q['approved']:continue
        # Some authors layer extruded KILLS lettering across the upper aperture.
        # Keep that source art and fit the live title below its actual lower edge.
        # Restrict to dense, protruding geometry entirely inside the top quarter;
        # broad architecture, backing walls and the four frame rails do not qualify.
        center=np.array(q['center']);n=np.array(q['normal']);right=np.array(q['right']);w=q['width'];h=q['height'];bottom=[]
        for c in m.chunks:
            if not c.visible or m.materials[c.material] in (RING,TRIM):continue
            delta=c.triangles-center;u=delta@right;y=delta[:,:,1];d=delta@n
            mask=(u.min(1)>-w*.49)&(u.max(1)<w*.49)&(y.min(1)>h*.25)&(y.min(1)<h*.5)&(d.min(1)>q['frontOffset']+.03)&(d.max(1)<min(w,h)*.15)
            if mask.sum()>=16:bottom.append(float(y[mask].min()))
        if bottom:
            inset=h/2-min(bottom)+.025
            if .03<inset<h*.25:
                q['frameCenter']=q['center'][:];q['frameWidth']=w;q['frameHeight']=h;q['topInset']=inset
                q['center'][1]-=inset/2;q['height']-=inset;q['reason']+='; fitted below protruding source header'
    return results

def main():
    OUT.mkdir(parents=True,exist_ok=True);ref=template();rows=[]
    for p in folders():
        p=p.parent if p.name=='map.json' else p;m=MapData(str(p));candidates=extract(m,ref);approved=[r for r in candidates if r['approved']]
        row=dict(id=m.map_id,title=m.manifest['title'],sourceSha256=m.manifest['sourceSha256'],geometrySha256=m.geometry_sha256,geometry=str(p/'geometry.bytes'),status='found' if approved else 'ambiguous' if candidates else 'none',confidence=max((r['confidence'] for r in candidates),default=0),lobbySpawn=m.manifest.get('warmupSpawn',m.manifest['spawn']),rectangles=approved,candidates=candidates)
        rows.append(row);print(row['id'],row['status'],len(approved),'/',len(candidates),flush=True)
    script=Path(__file__)
    report=dict(version=1,method='Complete Paintball rings including touching outlines, deduplicated shared trim; conservative context gates plus source-bound reviewed displays',reviewedSha256=hashlib.sha256(REVIEWED.read_bytes()).hexdigest(),detectorSha256=hashlib.sha256(script.read_bytes()+REVIEWED.read_bytes()).hexdigest(),referenceMap='archive:battlefield-paintball-gods-pitdevil',referenceSourceSha256=json.load(open(ROOT/'Assets/StreamingAssets/PlayableR23/World/map.json'))['sourceSha256'],maps=rows)
    (OUT/'R26ScoreboardsAudit.json').write_text(json.dumps(report,indent=2)+'\n')
    runtime=dict(version=1,detectorSha256=report['detectorSha256'],maps=[{k:r[k] for k in ('id','sourceSha256','geometrySha256','status','rectangles')} for r in rows])
    baseline=json.load(open(ROOT/'Assets/StreamingAssets/PlayableR23/WorldScores/boards.json'))
    for row in runtime['maps']:
        old=next((x for x in baseline['maps'] if x['mapId']==row['id']),None)
        if old and old['sourceSha256']==row['sourceSha256']:row['retainedFaces']=old['faces']
    RESOURCE.write_bytes((json.dumps(runtime,indent=2)+'\n').replace('\n','\r\n').encode())
    import csv
    with (OUT/'R26ScoreboardsPerMap.csv').open('w',newline='') as f:
        w=csv.writer(f);w.writerow(['map','status','approved','complete candidates','confidence','nearest lobby metres'])
        for r in rows:w.writerow([r['id'],r['status'],len(r['rectangles']),len(r['candidates']),r['confidence'],round(min((c['lobbyDistance'] for c in r['candidates']),default=-1),2)])
if __name__=='__main__':main()
