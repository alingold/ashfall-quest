from pathlib import Path
import hashlib,json,mmap,struct,subprocess,uuid,collections
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];OUT=HERE/'StagedPayload';BASE=HERE/'OriginalPayload';ASSET=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/Destructibles'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
record=json.loads((OUT/'destructibles-provenance.json').read_text());before=json.loads((BASE/'map.json').read_text());after=json.loads((OUT/'map.json').read_text());assert record['geometrySha256']==sha(OUT/'geometry.bytes')and record['originalGeometrySha256']==sha(BASE/'geometry.bytes')
for source,digest in record['sourceInputs'].items():assert sha(Path(source))==digest
assert all(after[k]==before[k]for k in before if k!='chunks');assert after['chunks']==2144 and len(after['destructibles'])==930
assert sum(p['glass']for p in after['destructibles'])==730 and all(p['health']==1 for p in after['destructibles'])
assert len(after['chunkPanelIds'])==len(after['chunkClearGlass'])==after['chunks'];assert all(after['chunkPanelIds'][i]>=0 for i,x in enumerate(after['chunkClearGlass'])if x)
for row in before['materials']:
    for field in ['color','normal']:
        if row.get(field):assert sha(OUT/row[field])==sha(ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/alfa-aglae-souza'/row[field])
def census(folder,manifest):
    f=(folder/'geometry.bytes').open('rb');data=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ);magic,count=struct.unpack_from('<ii',data);assert magic==0x52323357 and count==manifest['chunks'];cursor=8;render={};collision={};parts=[0]*930;static=[];dynamic=[];areas={};render_tri=collision_tri=0;static_keys=[]
    for i in range(count):
        mid,col,vis,nv,ni=struct.unpack_from('<iiiii',data,cursor);cursor+=20;assert 0<=mid<len(manifest['materials'])and 0<nv<=200000 and 0<ni<=3000000 and ni%3==0
        vertex=np.ndarray((nv,12),'<f4',data,cursor);cursor+=nv*48;idx=np.ndarray((ni,),'<i4',data,cursor);cursor+=ni*4;assert np.isfinite(vertex).all()and idx.min()>=0 and idx.max()<nv
        tri=vertex[idx,:3].astype(float).reshape(-1,3,3);area=np.linalg.norm(np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]),axis=1).sum()*.5;key=manifest['materials'][mid]['name']
        if vis:render[key]=render.get(key,0)+float(area);render_tri+=ni//3
        if col:collision[key]=collision.get(key,0)+float(area);collision_tri+=ni//3
        if 'chunkPanelIds'in manifest:
            owner=manifest['chunkPanelIds'][i]
            if owner>=0:assert col==vis==1;parts[owner]+=ni//3;dynamic.append((owner,tri))
            elif col:static.append(tri);static_keys.append(key)
    assert cursor==len(data);return render,collision,parts,static,dynamic,render_tri,collision_tri,static_keys
original=census(BASE,before);changed=census(OUT,after)
for role in (0,1):
    for key,area in original[role].items():assert abs(changed[role][key]-area)<=max(.002,area*1e-7),(role,key,area,changed[role][key])
assert all(x>0 for x in changed[2]);assert sum(changed[2])==record['panelTriangles']==42917
assert changed[5]==record['staticRenderTriangles']+record['panelTriangles'];assert changed[6]==record['staticCollisionTriangles']+record['panelTriangles']
# Source aperture width/depth volume has no remaining vc collision at face centers.
static=changed[3];bb=np.array([[*x.min(axis=(0,1)),*x.max(axis=(0,1))]for x in static]);clear=blocked=0;blocked_materials=collections.Counter();blocked_ids=[]
for panel in after['destructibles']:
    p=np.array(panel['probe']);n=np.array(panel['normal']);start=p+n*.12;end=p-n*.12;motion=end-start;lo=np.minimum(start,end)-.00001;hi=np.maximum(start,end)+.00001;hit=False
    candidates=np.flatnonzero((bb[:,:3]<=hi).all(axis=1)&(bb[:,3:]>=lo).all(axis=1))
    for ci in candidates:
        t=static[ci];a=t[:,0];e1=t[:,1]-a;e2=t[:,2]-a;h=np.cross(motion,e2);det=(e1*h).sum(axis=1);valid=np.abs(det)>1e-12;inv=np.where(valid,1/np.where(valid,det,1),0);s=start-a;u=(s*h).sum(axis=1)*inv;q=np.cross(s,e1);v=(q*motion).sum(axis=1)*inv;dist=(q*e2).sum(axis=1)*inv
        if np.any(valid&(u>=-1e-6)&(v>=-1e-6)&(u+v<=1.000001)&(dist>=0)&(dist<=1)):hit=True;blocked_materials[changed[7][ci]]+=1;blocked_ids.append(panel['id']);break
    if hit:blocked+=1
    else:clear+=1
assert clear>0,(clear,blocked)
for p in [ASSET,ASSET/'R23ArchiveDestructibles.cs',ASSET/'Resources',ASSET/'Resources/R23ArchiveGlass.shader']:
    meta=Path(str(p)+'.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-archive-panels:'+str(p.relative_to(ROOT)).replace('\\','/')).hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe';exe=HERE/'mechanics.exe';r=subprocess.run([csc,'/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(exe),str(ASSET/'R23ArchiveDestructibles.cs'),str(HERE/'Mechanics.cs')],capture_output=True,text=True);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,text=True);assert r.returncode==0,r.stdout+r.stderr;print(r.stdout)
result=dict(passed=True,panels=930,glass=730,doors=200,sourceRenderAreaPreserved=True,sourceCollisionAreaPreserved=True,panelTriangles=42917,staticFaceProbeClearAfterRetirement=clear,staticFaceProbeBlockedByOtherOriginalGeometry=blocked,blockedMaterials=dict(blocked_materials),blockedPanelIds=blocked_ids,chunks=2144,geometrySha256=record['geometrySha256'],engineRun=False,deviceRun=False)
(HERE/'check-results.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
