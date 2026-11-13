"""CPU implementation of preserved archive_map1003/community_map1002 floor fallback."""
import numpy as np

class TriangleScene:
    def __init__(self,parts):
        self.parts=parts
        self.bounds=np.array([[*p.min(axis=(0,1)),*p.max(axis=(0,1))] for p in parts])
    def floor(self,x,z,top,depth):
        boxes=self.bounds;ids=np.flatnonzero((boxes[:,0]<=x+1e-5)&(boxes[:,3]>=x-1e-5)&(boxes[:,2]<=z+1e-5)&(boxes[:,5]>=z-1e-5)&(boxes[:,1]<=top)&(boxes[:,4]>=top-depth))
        highest=-np.inf;normal=None;witness=None
        for index in ids:
            t=self.parts[index].astype(float);a,b,c=t[:,0],t[:,1],t[:,2];ab=b-a;ac=c-a
            den=ab[:,0]*ac[:,2]-ac[:,0]*ab[:,2];valid=np.abs(den)>1e-10;safe=np.where(valid,den,1)
            dx=x-a[:,0];dz=z-a[:,2];u=(dx*ac[:,2]-ac[:,0]*dz)/safe;v=(ab[:,0]*dz-dx*ab[:,2])/safe
            h=a[:,1]+u*ab[:,1]+v*ac[:,1];valid&=(u>=-1e-7)&(v>=-1e-7)&(u+v<=1.0000001)&(h<=top+1e-7)&(h>=top-depth-1e-7)
            if not valid.any():continue
            tri=int(np.argmax(np.where(valid,h,-np.inf)))
            if h[tri]>highest:
                highest=h[tri];normal=-np.cross(ab[tri],ac[tri]);normal/=max(np.linalg.norm(normal),1e-30);witness=(int(index),tri)
        return (np.array([x,highest,z]),normal,witness) if highest>-np.inf and normal[1]>.7 else None
    def near(self,low,high):
        b=self.bounds;ids=np.flatnonzero((b[:,:3]<=high).all(axis=1)&(b[:,3:]>=low).all(axis=1));pieces=[]
        for i in ids:
            t=self.parts[i];mask=(t.min(axis=1)<=high).all(axis=1)&(t.max(axis=1)>=low).all(axis=1)
            if mask.any():pieces.append(t[mask])
        return np.concatenate(pieces).astype(float)if pieces else np.empty((0,3,3))

def point_segment(p,a,b):
    ab=b-a;t=np.clip(np.sum((p-a)*ab,axis=-1)/np.maximum(np.sum(ab*ab,axis=-1),1e-30),0,1)
    q=a+ab*t[...,None];return np.sum((p-q)**2,axis=-1)
def point_triangle(p,t):
    a,b,c=t[:,0],t[:,1],t[:,2];ab=b-a;ac=c-a;n=np.cross(ab,ac);nn=np.sum(n*n,axis=1);h=np.sum((p-a)*n,axis=1)/np.maximum(nn,1e-30);q=p-h[:,None]*n
    d00=np.sum(ab*ab,axis=1);d01=np.sum(ab*ac,axis=1);d11=np.sum(ac*ac,axis=1);d20=np.sum((q-a)*ab,axis=1);d21=np.sum((q-a)*ac,axis=1);den=d00*d11-d01*d01
    u=(d11*d20-d01*d21)/np.maximum(den,1e-30);v=(d00*d21-d01*d20)/np.maximum(den,1e-30)
    interior=(den>1e-20)&(u>=0)&(v>=0)&(u+v<=1)
    return np.minimum.reduce([np.where(interior,h*h*nn,np.inf),point_segment(p,a,b),point_segment(p,b,c),point_segment(p,c,a)])
def segment_segment(p,q,a,b):
    d=q-p;e=b-a;r=p-a;aa=np.dot(d,d);ee=np.dot(e,e);de=np.dot(d,e);dr=np.dot(d,r);er=np.dot(e,r)
    if ee<1e-20:return float(point_segment(a,p,q))
    den=aa*ee-de*de;s=np.clip((de*er-dr*ee)/den,0,1)if den>1e-20 else 0;t=(de*s+er)/ee
    if t<0:t=0;s=np.clip(-dr/aa,0,1)
    elif t>1:t=1;s=np.clip((de-dr)/aa,0,1)
    return float(np.dot((p+s*d)-(a+t*e),(p+s*d)-(a+t*e)))
def capsule_clear(scene,floor,height=1.9288,radius=.3):
    low=floor+[-radius,.02,-radius];high=floor+[radius,height+.02,radius];tri=scene.near(low,high)
    if not len(tri):return True,dict(candidateTriangles=0,minimumDistance=None)
    p=floor+[0,.02+radius,0];q=floor+[0,.02+height-radius,0];dist=np.minimum(point_triangle(p,tri),point_triangle(q,tri))
    for i,t in enumerate(tri):
        a,b,c=t;n=np.cross(b-a,c-a);den=np.dot(n,q-p)
        if abs(den)>1e-20:
            s=np.dot(n,a-p)/den
            if 0<=s<=1 and point_triangle(p+(q-p)*s,tri[i:i+1])[0]<1e-16:dist[i]=0
        for a,b in [(a,b),(b,c),(c,a)]:dist[i]=min(dist[i],segment_segment(p,q,a,b))
    minimum=float(np.sqrt(max(0,dist.min())))
    return minimum>radius+1e-7,dict(candidateTriangles=len(tri),minimumDistance=minimum)

def room(scene,floor):
    support=[]
    for x,z in [(.4,0),(-.4,0),(0,.4),(0,-.4)]:
        hit=scene.floor(floor[0]+x,floor[2]+z,floor[1]+.6,1.2)
        if hit is None or abs(hit[0][1]-floor[1])>.3:return None
        support.append(dict(offset=[x,z],height=float(hit[0][1]),normal=hit[1].tolist(),witness=hit[2]))
    clear,clearance=capsule_clear(scene,floor)
    return dict(footprint=support,capsule=clearance)if clear else None

def fallback(scene,meta):
    lo=np.array(meta['solid_min']);hi=np.array(meta['solid_max']);blo=np.array(meta['bounds_min']);bhi=np.array(meta['bounds_max']);focus=np.array(meta['center']);focus[2]*=-1
    minx=max(lo[0],blo[0]+.6);maxx=min(hi[0],bhi[0]-.6);minz=max(lo[2],blo[2]+.6);maxz=min(hi[2],bhi[2]-.6);top=hi[1]+5;depth=top-(lo[1]-5)
    valid=[]
    for z in range(19):
        for x in range(19):
            sx=minx+(maxx-minx)*(x+.5)/19;sz=minz+(maxz-minz)*(z+.5)/19
            hit=scene.floor(sx,-sz,top,depth)
            if hit is None:continue
            point,normal,witness=hit
            if point[1]<lo[1]-25 or point[1]>hi[1]+100:continue
            proof=room(scene,point)
            if proof is not None:valid.append(dict(grid=[x,z],floor=point.tolist(),normal=normal.tolist(),witness=witness,proof=proof))
    assert valid,'No source 19x19 fallback candidates have actual support/headroom'
    # Source sorts loot floor+.6 relative to source center before deduplicating 16 spawns.
    preferred=sorted(valid,key=lambda row:float(np.sum((np.array(row['floor'])+[0,.6,0]-focus)**2)))
    selected=[]
    for row in preferred:
        p=np.array(row['floor'])+[0,.12,0]
        if any(np.linalg.norm(p-np.array(v['spawn']))<2 for v in selected):continue
        selected.append(dict(row,spawn=p.tolist()))
        if len(selected)>=16:break
    ordered=sorted(selected,key=lambda row:row['spawn'][1]);median=ordered[len(ordered)//2]['spawn'][1]
    match=[r for r in ordered if r['spawn'][1]<=median+20]
    # Existing room proof and +/- .4 footprint also establish safe_spawn's .6/1m floor query.
    assert match;game=match[0];warmup=min(match,key=lambda row:float(np.sum((np.array(row['spawn'])-focus)**2)))
    return game['spawn'],warmup['spawn'],dict(recipe='archive_map1003._settle 19x19 floor-grid fallback + community_map1002._room + _separate_spawns_r8',gridCandidates=361,validFloorRoomCandidates=len(valid),selectedFallbackCandidates=selected,game=game,warmup=warmup,standingClearance=1.9288,capsuleRadius=.3,footprintOffsets=.4,capsuleBottomGap=.02,recordedSpawns=0,authoredDeathmatchSpawns=0,authoredWarmupSpawns=0,spawnFloorsAdded=False)
