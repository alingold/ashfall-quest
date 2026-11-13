"""Partition only camera-witnessed family farm wedges against exact R6 faces.

The Root-owned Footprints.fragments implementation preserves uncovered kind255.
Only five architecture.json files may be published; raw render/collision bytes,
all prior face classes, Classic/ALFA, SourceArt and shared runtime are unchanged.
"""
from prepare import *

HERE=Path(__file__).resolve().parent
SLUGS=['bigbabyjesuz-classic-kiki-80','classic-bw-187norvis','cont-cl-er-ssd-cjcoops','cont-cl-ssd-cjcoops','statmaster-classic-soulsmight']
PIXELS=[(280,490),(300,510),(370,540),(440,575),(490,605)]
REFERENCES=Path('D:/ashfall-r6-staging/parity-v003/FAMILY7-REFERENCES.json')
PAIRS=Path('D:/ashfall-r6-staging/parity-family7-v001/PAIRS.json')
STAGED=HERE/'staged/farm-wedges'

def rays(view):
 eye=np.array(view['unityEye'],float);forward=np.array(view['unityTarget'])-eye;forward/=np.linalg.norm(forward);right=np.cross([0,1,0],forward);right/=np.linalg.norm(right);up=np.cross(forward,right);tan=np.tan(np.radians(58)*.5)
 return eye,[((forward+right*(2*(x+.5)/1200-1)*tan*1.5+up*(1-2*(y+.5)/800)*tan)/np.linalg.norm(forward+right*(2*(x+.5)/1200-1)*tan*1.5+up*(1-2*(y+.5)/800)*tan))for x,y in PIXELS]

def witnessed_faces(slug,view,manifest):
 eye,directions=rays(view);nearest=[None]*len(directions);geometry=OUT/slug/'geometry.bytes'
 with geometry.open('rb')as f:
  magic,nc=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and nc==manifest['chunks']
  for ci in range(nc):
   mid,collision,visible,nv,ni=struct.unpack('<iiiii',f.read(20));v=np.frombuffer(f.read(nv*48),'<f4').reshape(nv,12);ix=np.frombuffer(f.read(ni*4),'<i4').reshape(-1,3)
   if not visible:continue
   p=v[ix,:3];e1=p[:,1]-p[:,0];e2=p[:,2]-p[:,0];tv=eye-p[:,0]
   for qi,d in enumerate(directions):
    cross=np.cross(d,e2);det=(e1*cross).sum(1);valid=abs(det)>1e-10;inv=np.divide(1,det,out=np.zeros_like(det),where=valid);u=(tv*cross).sum(1)*inv;q=np.cross(tv,e1);w=(d*q).sum(1)*inv;distance=(e2*q).sum(1)*inv
    ids=np.flatnonzero(valid&(u>=0)&(w>=0)&(u+w<=1)&(distance>.01))
    if not len(ids):continue
    ti=int(ids[np.argmin(distance[ids])]);dist=float(distance[ti])
    if nearest[qi]is None or dist<nearest[qi]['distance']:
     nearest[qi]=dict(chunk=ci,triangle=ti,material=manifest['materials'][mid]['name'],vertices=nv,positions=v[ix[ti],:3].copy(),normals=v[ix[ti],3:6].copy(),colors=v[ix[ti],8:11].copy(),distance=dist,pixel=PIXELS[qi])
 assert all(row is not None for row in nearest)
 return nearest

def validate_partition(row,p,index=None,normal=None,color=None):
 weights=np.array(row['barycentric']).reshape(-1,3,3);flags=np.frombuffer(base64.b64decode(row['triangleKinds']),'u1');assert len(weights)==len(flags)<=256 and set(flags).issubset({1,255})and 1 in flags
 assert np.isfinite(weights).all()and weights.min()>=0 and weights.max()<=1 and np.allclose(weights.sum(2),1,atol=1e-9)
 q=weights@p;geometric=np.cross(p[1]-p[0],p[2]-p[0]);axes=[i for i in range(3)if i!=int(np.argmax(abs(geometric)))];area=polygon_area(p[:,axes]);areas=np.array([polygon_area(t[:,axes])for t in q]);assert abs(areas.sum()-area)<area*1e-7
 assert np.all(np.sum(np.cross(q[:,1]-q[:,0],q[:,2]-q[:,0])*geometric,axis=1)>0)
 covered=float(areas[flags==1].sum()/area);assert abs(covered-row['sourceCoverage'])<1e-7 and covered>.95
 missing=uncovered(p[:,axes],[t[:,axes]for t in q]);assert missing is not None and missing<area*1e-7,'Partition union leaves no draw-surface gap'
 if index is not None:
  groups,all_polys,target,source_area=index.collect('vc',p,normal,color);covers=[poly for kind,poly in all_polys if kind==1];origin=p[0,axes]
  for tri,kind in zip(q,flags):
   poly=tri[:,axes]-origin
   if kind==1:
    absent=uncovered(poly,covers);assert absent is not None and absent<area*1e-7,'A painted fragment exceeds actual source union'
   else:assert all(overlap_area(poly,cover)<area*1e-7 for cover in covers),'Source-covered fragment mislabeled255'
  assert index.fragments('different-material',p,normal,color)is None
  assert index.fragments('vc',p,-normal,color)is None
  assert index.fragments('vc',p,normal,color+.05)is None
  assert index.fragments('vc',p+normal[0]*.02,normal,color)is None
 return dict(renderTriangles=len(flags),sourceCoverage=covered,uncoveredKind255=1-covered,originalArea=area)

def run(publish=False):
 config_path=SOURCE/'assets/data/architecture_r24.json';assert sha(config_path)==PINS['assets/data/architecture_r24.json'];config=json.loads(config_path.read_text())['maps'];refs=json.loads(REFERENCES.read_text())['maps'];pairs=json.loads(PAIRS.read_text())['pairs'];reports=[];STAGED.mkdir(parents=True,exist_ok=True)
 protected=[OUT/s/'architecture.json'for s in export_folders()if s not in SLUGS]+[OUT/s/'map.json'for s in SLUGS]+[ROOT/'unity/Assets/Ashfall/Integration/Playable/World/R23PlayableMap.cs']+list((ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt').glob('*'))
 protected={p:sha(p)for p in protected if p.is_file()};candidates=[]
 for slug in SLUGS:
  manifest=json.loads((OUT/slug/'map.json').read_text());path=OUT/slug/'architecture.json';before=json.loads(path.read_text());data=json.loads(json.dumps(before));assert data['geometrySha256']==sha(OUT/slug/'geometry.bytes')and data['sourcePins']==PINS
  view=next(v for r in refs if r['mapId']=='archive:'+slug for v in r['views']if v['name']in['farm-4','farm-4-lean-to']);pair=next(p for p in pairs if p['mapId']=='archive:'+slug and p['view']==view['name']);assert pair['eye']==view['eye']and pair['target']==view['target'];assert sha(pair['unity'])==pair['unitySha256']and sha(pair['godot'])==pair['godotSha256']
  rows=witnessed_faces(slug,view,manifest);selected={(r['chunk'],r['triangle']):r for r in rows};plan=config['archive:'+slug];_,source_count,surfaces=source_faces(slug,plan);index=Footprints(surfaces);patches={p['chunk']:p for p in data['patches']};changes=[];controls=[]
  for(ci,ti),r in selected.items():
   patch=patches.get(ci);existing={}if patch is None else dict(zip(patch['triangles'],base64.b64decode(patch['triangleKinds'])));fragments=[]if patch is None else patch.get('fragments',[])
   if ti in existing or any(f['triangle']==ti for f in fragments):controls.append(dict(chunk=ci,triangle=ti,alreadyClassified=existing.get(ti),existingFragment=any(f['triangle']==ti for f in fragments)));continue
   assert r['material']=='vc','Camera witness hit another material; do not broaden selection'
   split=index.fragments(r['material'],r['positions'],r['normals'],r['colors'])
   if split is None:controls.append(dict(chunk=ci,triangle=ti,sourcePartitionUnavailable=True));continue
   check=validate_partition(split,r['positions'],index,r['normals'],r['colors']);assert set(base64.b64decode(split['triangleKinds'])).issubset({1,255})
   if patch is None:patch=dict(chunk=ci,vertices=r['vertices'],triangles=[],triangleKinds='',fragments=[]);patches[ci]=patch;data['patches'].append(patch)
   patch.setdefault('fragments',[]).append(dict(triangle=ti,**split));changes.append(dict(chunk=ci,triangle=ti,pixels=[x['pixel']for x in rows if x['chunk']==ci and x['triangle']==ti],**check))
  assert changes,(slug,'No useful camera-bound partition')
  # Every original selector and fragment remains exactly present. Only the
  # whitelisted new fragment records may differ; no whole-face kind is added.
  restored=json.loads(json.dumps(data));added={(r['chunk'],r['triangle'])for r in changes}
  original_patches={p['chunk']:p for p in before['patches']}
  for p in restored['patches']:
   if'fragments'in p:
    p['fragments']=[f for f in p['fragments']if(p['chunk'],f['triangle'])not in added]
    if not p['fragments']and'fragments'not in original_patches.get(p['chunk'],{}):del p['fragments']
  restored['patches']=[p for p in restored['patches']if p['triangles']or p.get('fragments')]
  assert restored==before,(slug,'Previous face classification changed')
  target=STAGED/(slug+'-architecture.json');target.write_text(json.dumps(data,separators=(',',':'))+'\n');candidates.append((path,target,sha(path)))
  report=dict(map=slug,geometrySha256=data['geometrySha256'],sourcePlanSha256=PINS['assets/data/architecture_r24.json'],sourceClassifiedFaces=source_count,witnessPixels=PIXELS,changes=changes,unchangedControls=controls,previousAllFaceClassesPreserved=True,sourcePartitionsOnly=True);reports.append(report);print(json.dumps(report,indent=2),flush=True)
 if publish:
  for path,target,digest in candidates:
   assert sha(path)==digest,'Concurrent architecture edit';temporary=path.with_name(path.name+'.farm-wedge-new');temporary.write_bytes(target.read_bytes());temporary.replace(path)
 assert all(sha(p)==digest for p,digest in protected.items()),'Protected map/source art/runtime changed'
 result=dict(published=publish,maps=reports,protectedFilesUnchanged=len(protected),sourceReferencesSha256=sha(REFERENCES),nativePairsSha256=sha(PAIRS),sourceGeometryAndCollisionUnchanged=True,engineRun=False);(HERE/'farm-wedge-checks.json').write_text(json.dumps(result,indent=2)+'\n');return result

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--publish',action='store_true');run(p.parse_args().publish)
