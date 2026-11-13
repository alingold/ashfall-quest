"""Bind real missing markings, plus already-correct controls, to captured draws.

Reads untouched native PNG pixels and raw geometry. This distinguishes a label
from an actual visual defect; it performs no engine call or Assets mutation.
"""
from analyze import *
from PIL import Image

PIXELS=[('already-correct-upper',(600,240)),('already-correct-center',(600,360)),('already-correct-lower',(600,520)),('missing-left-marking',(500,360)),('missing-upper-left-marking',(530,240)),('missing-upper-right-marking',(650,240)),('flat-right-flank',(700,480))]

def resolve(folder,path):
 candidate=folder/'source'/path
 if candidate.is_file():return candidate
 metadata=json.loads((folder/'inputs.json').read_text());return resolve(Path(metadata['sourceBase']),path)

def main():
 pairs=json.loads(Path('D:/ashfall-unity-builds/parity-r6-roads-next/comparison/pairs.json').read_text());pair=next(r for r in pairs['pairs']if r['id']=='archive:alfa-aglae-souza');sp=Path(pair['godot']);up=ROOT/pair['unity'];assert sha(sp)==pair['godotSha256']and sha(up)==pair['unitySha256'];source_image=Image.open(sp);unity_image=Image.open(up)
 captured=resolve(Path('D:/ashfall-unity-builds/parity-r6-roads-next/freeze'),'Assets/StreamingAssets/PlayableR23/SourceArt/alfa-aglae-souza.json');old=json.loads(captured.read_text());current_path=ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt/alfa-aglae-souza.json';new=json.loads(current_path.read_text());roads=json.loads((HERE/'alpha-road-checks.json').read_text());road_owners={(r['chunk'],r['triangle']):r['owner']for r in roads['matches']};geometry=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/alfa-aglae-souza/geometry.bytes';assert sha(geometry)==old['geometrySha256']==new['geometrySha256']==roads['geometrySha256']
 old_kinds={(p['chunk'],t):part['kind']for p in old['patches']for part in p['parts']for t in part['triangles']};new_kinds={(p['chunk'],t):part['kind']for p in new['patches']for part in p['parts']for t in part['triangles']}
 eye=np.array(pair['eye']);forward=np.array(pair['target'])-eye;forward/=np.linalg.norm(forward);right=np.cross(forward,[0,1,0]);right/=np.linalg.norm(right);up_axis=np.cross(right,forward);tan=math.tan(math.radians(58)/2);nearest=[None]*len(PIXELS)
 with geometry.open('rb')as f:
  magic,nc=struct.unpack('<ii',f.read(8));assert magic==0x52323357
  for ci in range(nc):
   mid,co,visible,nv,ni=struct.unpack('<iiiii',f.read(20));v=np.frombuffer(f.read(nv*48),'<f4').reshape(nv,12);ix=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3)
   if not visible:continue
   p=v[:,:3].copy();p[:,2]*=-1;p=p[ix];e1=p[:,1]-p[:,0];e2=p[:,2]-p[:,0];tv=eye-p[:,0]
   for qi,(label,(x,y))in enumerate(PIXELS):
    d=forward+right*(2*(x+.5)/1200-1)*tan*1.5+up_axis*(1-2*(y+.5)/800)*tan;d/=np.linalg.norm(d);cross=np.cross(d,e2);det=(e1*cross).sum(1);valid=abs(det)>1e-10;inv=np.divide(1,det,out=np.zeros_like(det),where=valid);u=(tv*cross).sum(1)*inv;q=np.cross(tv,e1);w=(d*q).sum(1)*inv;distance=(e2*q).sum(1)*inv;ids=np.flatnonzero(valid&(u>=0)&(w>=0)&(u+w<=1)&(distance>.01))
    if not len(ids):continue
    ti=int(ids[np.argmin(distance[ids])]);dist=float(distance[ti])
    if nearest[qi]is None or dist<nearest[qi]['distance']:nearest[qi]=dict(chunk=ci,triangle=ti,distance=dist,point=(eye+d*dist).tolist())
 rows=[]
 for(label,pixel),hit in zip(PIXELS,nearest):
  assert hit is not None;key=(hit['chunk'],hit['triangle']);s=list(source_image.getpixel(pixel))[:3];u=list(unity_image.getpixel(pixel))[:3];row=dict(label=label,pixel=pixel,sourceRgb=s,capturedUnityRgb=u,capturedKind=old_kinds.get(key),currentKind=new_kinds.get(key),exactAdditionalSourceOwner=road_owners.get(key),**hit);rows.append(row)
  if label.startswith('already-correct'):
   assert key==(277,1360)and old_kinds[key]==new_kinds[key]=='junction'
   if label.endswith('upper'):assert min(s)>180 and min(u)>180,'The alleged upper gray sample is actually the correct white marking'
   else:assert max(abs(a-b)for a,b in zip(s,u))<=3,'The alleged gray center/lower is already source-matched asphalt'
  else:
   assert key in road_owners and old_kinds.get(key)is None and new_kinds[key]=='junction','Actual defective flank must bind the added source road owner'
   if 'marking'in label:assert min(s)>180 and max(u)<170,'Actual missing white marking must be visible in untouched PNGs'
 report=dict(passed=True,sourceImage=str(sp),sourceImageSha256=sha(sp),capturedUnityImage=str(up),capturedUnityImageSha256=sha(up),capturedPlan=str(captured),capturedPlanSha256=sha(captured),currentPlanSha256=sha(current_path),geometrySha256=sha(geometry),samples=rows,finding='Raw277/1360 is already correctly drawn at the three formerly mislabeled samples. Actual flat gray flanks and missing white markings bind to the additional28 ALFA source road faces, absent in this capture and present in the released candidate. No projection/mask/material fix to277/1360 is justified.',nativeCandidateVisualConfirmationPending=True,assetsWritten=False)
 (HERE/'alpha-actual-pixel-checks.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
