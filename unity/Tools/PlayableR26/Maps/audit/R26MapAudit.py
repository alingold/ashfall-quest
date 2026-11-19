"""Audit represented Unity feet against packed, original-winding geometry, independently of PhysX."""
import os,sys,struct,ctypes as ct,json,hashlib,collections
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation
sys.dont_write_bytecode=True
root=Path('/home/al/ashfall/unity');out=Path(__file__).resolve().parent
sys.path.insert(0,str(root/'Tools/PlayableR23/World/Classic'));import spawn
lib=ct.CDLL(str(root.parent/'campaign/tmp/MAPS2/R26MapsTree.so'))
ptr=lambda x:ct.c_void_p(x.ctypes.data)
sa=root/'Assets/StreamingAssets/PlayableR23';res=root/'Assets/Ashfall/Integration/Playable/World/Resources'
def read(p):return json.loads(p.read_bytes())
def index(rows):return {r['id']:r for r in rows}
launch=index(read(sa/'TeamLaunch/launch-maps.json')['rows']);rec=index(read(sa/'TeamLaunch/ArchiveRecovery/maps.json')['rows']);policy=index(read(res/'R26MapPodPolicy.json')['maps'])
catalog={'archive:battlefield-paintball-gods-pitdevil':'World',**{k:'Maps/'+k for k in ('range','valley','cyber_bird')}}
for r in read(res/'R24ArchiveCatalog.json')['maps']+read(sa/'Legacy/manifest.json')['maps']:catalog.setdefault(r['id'],r['relativeFolder'])
if '"archive:cold-hearted-gods-pitdevil"' in (res.parent/'R23MapCatalog.cs').read_text().split('Excluded=')[1].split(';')[0]:catalog.pop('archive:cold-hearted-gods-pitdevil',None)
# Include the r17 Gotham payload even when concurrently withheld in the r18 picker.
catalog.setdefault('archive:g0dtham-gods-poplightning','Maps/g0dtham-gods-poplightning')
boards=index(read(res/'R26MapScoreboards.json')['maps'])
cube=np.array([[-1,-1,-1],[-1,-1,1],[-1,1,-1],[-1,1,1],[1,-1,-1],[1,-1,1],[1,1,-1],[1,1,1]])
faces=np.array([[0,1,3],[0,3,2],[4,6,7],[4,7,5],[0,4,5],[0,5,1],[2,3,7],[2,7,6],[0,2,6],[0,6,4],[1,5,7],[1,7,3]])[:,::-1]
class Scene:
 def __init__(self,folder,m):
  raw=(folder/'geometry.bytes').read_bytes();self.sha=hashlib.sha256(raw).hexdigest();magic,n=struct.unpack_from('<ii',raw);assert magic==0x52323357 and n==m['chunks'];offset=8;ts=[];ids=[];self.names={};self.layers={}
  chunks=read(folder/'source-chunks.json') if (folder/'source-chunks.json').exists() else []
  def add(t,i,mat,layer):
   if len(t):ts.append(t);ids.append(np.full(len(t),i,np.int32));self.names[i]=mat;self.layers[i]=layer
  for i in range(n):
   mat,col,vis,nv,ni=struct.unpack_from('<5i',raw,offset);offset+=20;stride=18 if m['version']==2 else 12
   pos=np.frombuffer(raw,'<f4',nv*stride,offset).reshape(nv,stride)[:,:3];offset+=nv*stride*4
   ix=np.frombuffer(raw,'<i4',ni,offset).reshape(-1,3);offset+=ni*4;layer=m.get('chunkSourceLayers',[]);layer=layer[i] if layer else 1
   if col and (layer&1 or layer&4):
    # v2 Cyber Bird emits reflected Godot BoxMesh winding opposite the archive converter.
    tri=pos[ix[:,::-1] if m['mapId']=='cyber_bird' else ix];add(tri,i,m['materials'][mat]['name'],layer)
  assert offset==len(raw)
  for i,b in enumerate(m.get('boxes',[]),n):
   layer=b.get('sourceLayer',1)
   if layer&1 or layer&4:add((Rotation.from_quat(b.get('rotation',[0,0,0,1])).apply(cube*np.array(b['size'])/2)+b['center'])[faces],i,'box',layer)
  active=boards.get(m['mapId'],{});active=active if active.get('sourceSha256')==m['sourceSha256'] else {}
  for j,row in enumerate(active.get('rectangles',[])+active.get('solidPanels',[]),n+len(m.get('boxes',[]))):
   if not row.get('approved',True) or row.get('confidence',1)<.9:continue
   right=np.array(row['right']);normal=np.array(row['normal']);up=np.cross(right,normal)
   # This basis is reflected, so reverse the box winding once.
   basis=np.array([right,up,normal]);vertices=(cube*np.array([row.get('frameWidth',row['width'])/2,row.get('frameHeight',row['height'])/2,row['frontOffset']]))@basis+row.get('frameCenter',row['center']);add(vertices[faces[:,::-1]],j,'billboard'+str(j),1)
  rows=active.get('rectangles',[])
  for j,f in enumerate(active.get('retainedFaces',[]),n+len(m.get('boxes',[]))+len(rows)+len(active.get('solidPanels',[]))):
   vs=np.array(f.get('vertices',[])).reshape(-1,3)
   if len(vs)<3:continue
   center=vs.mean(0)
   if any(np.linalg.norm(center-row['center'])<min(row['width'],row['height'])*.15 for row in rows):continue
   normal=np.cross(vs[1]-vs[0],vs[2]-vs[0]);length=np.linalg.norm(normal)
   if length<1e-8:continue
   normal/=length;right=np.cross([0,1,0],normal);right=right/np.linalg.norm(right) if np.linalg.norm(right)>1e-7 else np.array([1,0,0]);basis=np.array([right,np.cross(normal,right),normal]);local=(vs-center)@basis.T
   size=np.ptp(local,axis=0);size[2]=max(.06,size[2]);verts=(cube*size/2+(local.max(0)+local.min(0))/2)@basis+center
   add(verts[faces],j,'retained'+str(j),1)
  self.t=np.concatenate(ts).astype('f');self.ids=np.concatenate(ids);self.b=np.ascontiguousarray(np.c_[self.t.min(1),self.t.max(1)]);self.buf=np.empty(len(self.t),'i');lib.index_triangles(ptr(self.b),len(self.t));self.upOnly={i for i in self.names if self.layers[i]&1}
 def query(self,lo,hi):
  q=np.ascontiguousarray(np.r_[lo,hi],dtype='f');n=lib.query(ptr(q),ptr(self.buf));return self.buf[:n].copy()
 def near(self,lo,hi):return self.t[self.query(lo,hi)].astype(float)
 def column(self,x,z):
  ids=self.query([x-.0001,-1e6,z-.0001],[x+.0001,1e6,z+.0001]);t=self.t[ids].astype(float);a=t[:,0];e=t[:,1]-a;f=t[:,2]-a
  den=e[:,0]*f[:,2]-e[:,2]*f[:,0];d=np.where(abs(den)>1e-9,den,1);dx=x-a[:,0];dz=z-a[:,2];u=(dx*f[:,2]-dz*f[:,0])/d;v=(e[:,0]*dz-e[:,2]*dx)/d
  mask=(abs(den)>1e-9)&(u>=-1e-7)&(v>=-1e-7)&(u+v<1.0000001)
  heights=a[:,1]+u*e[:,1]+v*f[:,1];norm=den/np.maximum(1e-20,np.linalg.norm(np.cross(e,f),axis=1))
  return sorted([(float(heights[i]),float(norm[i]),int(self.ids[ids[i]])) for i in np.flatnonzero(mask)],reverse=True)
 def test(self,p,up=.6,down=1):
  x,y,z=p;hits=self.column(x,z);floor=[h for h in hits if y+up-down<=h[0]<=y+up and h[2] in self.upOnly]
  if not floor:return ['unsupported',None]
  h=floor[0][0];tops=[a for a in floor if h-a[0]<.015];norm=max(a[1] for a in tops);floor=np.array([x,h,z]);feet=(floor+[0,.12,0]).round(4).tolist()
  if norm<=.7:return ['underside' if norm<-.7 else 'steep',feet]
  # The top of a single-valued terrain sheet above the standing point can hide a supported lower mesh.
  # For other materials demand an outward top above AND a downward underside below the feet.
  # Report roofed locations separately from these directed solid intervals.
  for mat in {self.names[a[2]] for a in hits}:
   above=[a for a in hits if self.names[a[2]]==mat and a[0]>h+.2];below=[a for a in hits if self.names[a[2]]==mat and a[0]<=h+.2]
   if not above:continue
   upper=above[-1]
   if min(a[1] for a in above if a[0]<upper[0]+.015)>.15 and ((below and max(a[1] for a in below if a[0]>below[0][0]-.015)<-.15) or mat=='sg'):
    return ['inside:'+mat,feet]
  for dx,dz in ((.4,0),(-.4,0),(0,.4),(0,-.4)):
   hs=[a for a in self.column(x+dx,z+dz) if h-.6<=a[0]<=h+.6 and a[2] in self.upOnly]
   if not hs or abs(hs[0][0]-h)>.3 or max(a[1] for a in hs if hs[0][0]-a[0]<.015)<=.7:return ['edge',feet]
  if not spawn.capsule_clear(self,floor)[0]:return ['obstructed',feet]
  return ['clear',feet]
def vec(v):return np.array([v['x'],v['y'],-v['z']])
def audit(k):
 folder=sa/catalog[k];m=read(folder/'map.json');s=Scene(folder,m);assert m['mapId']==k
 l=launch[k];origin=(np.array(l['sourceOrigin'])-l['removedOrigin'])*[1,1,-1];groups={};r=rec.get(k,{});pol=policy.get(k,{})
 def add(name,p,up=.6,down=1):
  result,feet=s.test(p,up,down);groups.setdefault(name,[]).append([np.round(p,4).tolist(),result,feet])
 for n in ('gameSpawn','warmupSpawn'):
  field='gameLocal' if n=='gameSpawn' else 'warmupLocal';override=pol.get(field) if pol.get('sourceSha256')==m['sourceSha256'] and pol.get('has'+field[0].upper()+field[1:]) else None;p=np.array([override[axis] for axis in 'xyz'] if override else m.get(n,m['spawn']));add(n,p,.6,2 if k=='valley' else 1)
 for n,up,depth in [('warmup',1.5,8),('deathmatch',1.5,8),('recorded',2,5),('boxed',.6,3)]:
  for p in (pol.get('spawns',[]) if n=='boxed' and r and pol.get('verdict')=='BOXED' else r.get(n,[])):add(n,origin+vec(p),up,depth)
 if not r:
  for i in range(24):
   p=(origin+[-55+(i%4)*35,1,-330+((i//4)%2)*660+(i//8)*1.5] if k=='cyber_bird' else np.array(m.get('gameSpawn',m['spawn']))+[-15+(i%4)*10,2,6+((i//4)%2)*30+(i//8)*1.5]);add('builtinArena',p,20,50)
 if k.startswith('archive:') and r:
  a,b=vec(r['solidMin']),vec(r['solidMax']);u,v=vec(r['boundsMin']),vec(r['boundsMax'])
  lo=np.maximum(a[[0,2]]*[1,-1],u[[0,2]]*[1,-1]+.6);hi=np.minimum(b[[0,2]]*[1,-1],v[[0,2]]*[1,-1]-.6)
  for iz in range(19):
   for ix in range(19):
    x,z=lo+(hi-lo)*np.array([ix+.5,iz+.5])/19;add('grid',origin+[x,b[1]+5,-z],0,b[1]-a[1]+10)
 summary={}
 if os.environ.get('MAPS2_FULL'):
  # Cover the live warm-up grid on each authored lobby anchor. Markerless maps
  # can use an arena anchor, so include all certified candidate match anchors.
  warmed=[f for _,state,f in groups.get('warmup',[]) if state=='clear']
  if not warmed:
   warmed=[groups['warmupSpawn'][0][2]]
   if not pol.get('hasWarmupLocal') and r:
    warmed += [f for n in ('boxed','recorded','deathmatch','grid') for _,state,f in groups.get(n,[]) if state=='clear' and n!='grid'] 
   for ceiling in r.get('spawnFloors',[]):
    if 'position' in ceiling and 'size' in ceiling:
     c=vec(ceiling['position']);size=vec(ceiling['size']);warmed.append(origin+c+size*[.5,1,.5]+[0,.12,0])
  counted=collections.Counter();seen=set()
  for anchor in warmed:
   if anchor is None:continue
   for iz in range(-4,5):
    for ix in range(-4,5):
     q=np.array(anchor)+[ix*.9,0,iz*.9];key=tuple(q.round(3))
     if key in seen:continue
     seen.add(key);state,feet=s.test(q,2,6)
     if state=='clear' and abs(feet[1]-q[1])>1.5:state='level'
     counted[state]+=1
  summary['warmupGrid']=dict(counted)
  if pol.get('pods',True) and pol.get('verdict')!='BOXED':
   top=l['deploymentTop']-l['removedOrigin'][1] if l['hasDeploymentTop'] else max(origin[1]+40,float(s.b[:,4].max()))
   xmin=origin[0]+l['catalogMinX'];xmax=origin[0]+l['catalogMaxX'];zmin=origin[2]-l['catalogMaxZ'];zmax=origin[2]-l['catalogMinZ']
   mx=min(25.5,(xmax-xmin)/2);mz=min(10,(zmax-zmin)/2);pod=[]
   for team in range(2):
    sign=2*team-1
    center=np.array([xmax-mx if team else xmin+mx,top+65+(18*team if min(xmax-xmin,zmax-zmin)<100 else 0),zmin+mz if team else zmax-mz])
    for slot in range(12):
     feet=center+[sign*(slot-5.5)*3.6,.12,-sign*3];col=s.column(feet[0],feet[2]);gap=feet[1]-max((y for y,_,_ in col),default=-1e6)
     pod.append([feet.round(4).tolist(),bool(gap>0 and spawn.capsule_clear(s,feet)[0]),round(gap,3)])
   summary['podSeats']=pod
 return dict(id=k,folder=catalog[k],geometry=s.sha,groups=groups,**summary)
if __name__=='__main__':
 for k in (sys.argv[1:] or sorted(catalog)):
  a=audit(k);dest=out/os.environ.get('MAPS2_AUDIT_DIR','sites');dest.mkdir(exist_ok=True);(dest/(k.replace('archive:','')+'.json')).write_text(json.dumps(a,separators=(',',':')));print(k,{n:dict(collections.Counter(i[1] for i in v)) for n,v in a['groups'].items()},flush=True)
