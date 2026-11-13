"""Transfer exact prepared R10 / staged R14-R19 art to unchanged Unity faces."""
from analyze import *
import argparse,itertools,shutil
from PIL import Image
sys.path.insert(0,str(HERE/'vendor'));import texture2ddecoder
OUT=HERE/'staged/payload';PERM=np.array([[0,2,1],[1,0,2],[2,1,0]]) # undo Unity's single winding reflection; cyclic source order only
KINDS={'container_r10':0,'van_r10':1,'tire_r10':2,'container_end_panels':3,'wood_panels':4,'van':5,'truck':6,'tire':7,'wood':8,'sign':9,'banner':10,'junction':11}
def pipeline(slug):
 stage=prepared(slug);details=[];counts=collections.Counter();digests=[]
 def detail(name,row,ids,kind,uv):
  if kind not in KINDS:return
  ix=row['indices'].reshape(-1,3)[ids];uv=np.asarray(uv,np.float32).reshape(-1,3,3);assert ix.shape==uv.shape[:2]
  details.append(dict(name=name,kind=kind,faces=row['vertices'][ix],normals=row['normals'][ix],colors=row['colors'][ix],uv=uv));counts[kind]+=len(ids)
 for name,row in stage.items():
  if not row['artKind']:continue
  ids=np.arange(len(row['indices'])//3);detail(name,row,ids,row['artKind'],row['uv'][row['indices']])
 audit=json.loads(source('assets/data/map_art_audit_r14.json').read_text());plans=next(r['repair_selectors']for r in audit['maps']if r['id']=='archive:'+slug)
 def erase(row,ids):row['indices']=row['indices'].reshape(-1,3)[[i for i in range(len(row['indices'])//3)if i not in ids]].ravel();row['faces']=row['vertices'][row['indices']].reshape(-1,3,3)
 for plan in plans:
  name=plan['node'];row=stage[name];assert len(row['vertices'])==plan['vertex_count']and len(row['indices'])==plan['index_count'];removed=set()
  for part in plan['parts']:
   uv=np.asarray(part['uv'],np.float32);uv=np.c_[uv,np.zeros(len(uv),np.float32)];detail(name,row,part['triangles'],part['kind'],uv);removed.update(part['triangles'])
  erase(row,removed)
 for phase in ['r17','r19']:
  binding=PLANS[slug][phase];path=binding['resource'].removeprefix('res://');assert sha(source(path))==binding['sha256'];finger=binding['sourceBinding'].get('fingerprint',binding['sourceBinding'].get('cache_fingerprint'));assert finger==json.loads(source('assets/data/map_semantics_r14.json').read_text())['maps'][slug]['cache_fingerprint']
  for name,plan in binding['plans'].items():
   row=stage[name];actual=face_digest(row['faces']);assert actual==plan['sha256'],(slug,phase,name);digests.append(dict(stage=phase,node=name,sha256=actual));removed=set()
   for part in plan['parts']:
    uv=np.asarray(part['uv'],np.float32)
    if phase=='r19':uv[:,1]=1-uv[:,1]
    detail(name,row,part['triangles'],part['kind'],uv);removed.update(part['triangles'])
   erase(row,removed)
 # Verify complete final native base arrays where available, independently of the plan hash.
 spec=importlib.util.spec_from_file_location('architecture_source_binding',ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6/prepare.py');architecture=importlib.util.module_from_spec(spec);spec.loader.exec_module(architecture)
 config=source('assets/data/architecture_r24.json');assert sha(config)=='1cb3f22b3267a0d825eadbd3cc2b132f17740fdd0061a74ed5ff44e6e133644d'
 architecture_plan=json.loads(config.read_text())['maps']['archive:'+slug];folder,_=architecture.source_export(slug,architecture_plan);native=json.loads((folder/'result.json').read_text());comparisons=0
 junctions=[p for p in details if p['kind']=='junction']
 if junctions:
  if slug=='alfa-aglae-souza':
   guard=json.loads(source('assets/data/alpha_junction_r24.json').read_text());assert len(guard['projections'])==7
  else:
   family=architecture_plan['family_details_r6'];assert family['repair_r17_junction_material']and family['expected_r17_junction_parts']==7
  assert len(junctions)==7,(slug,'actual source R17 junction part census')
 for m in native['meshes']:
  name=m['path'];channels=m.get('channels',{})
  if name not in stage or not all(k in channels for k in ['vertices','indices']):continue
  # Semantics later extracts doors/glass from some base nodes; compare unmodified survivors only.
  p=np.fromfile(folder/channels['vertices'],'<f4').reshape(-1,3);ix=np.fromfile(folder/channels['indices'],'<i4');a=p[ix].reshape(-1,3,3);b=stage[name]['faces']
  if a.shape==b.shape:assert np.array_equal(a,b),(slug,name,'final native arrays');comparisons+=1
 return details,dict(stageDigests=digests,sourceTriangles=dict(counts),preparedR10Owners={k:sum(row['artKind']==k for row in stage.values())for k in ['container_r10','van_r10','tire_r10']},finalNativeArraysCompared=comparisons,exactNativeExport=str(folder))
def transfer(slug):
 details,report=pipeline(slug);lookup=collections.defaultdict(list);source_triangles=[]
 for part in details:
  for face,normal,color,uv in zip(part['faces'],part['normals'],part['colors'],part['uv']):
   center=face.mean(0);lo=np.rint((center-.005)*100).astype(int);hi=np.rint((center+.005)*100).astype(int);index=len(source_triangles);source_triangles.append((part['kind'],face,normal,color,uv))
   for key in itertools.product(*(range(lo[j],hi[j]+1)for j in range(3))):lookup[key].append(index)
 folder=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/slug;manifest=json.loads((folder/'map.json').read_text());gpath=folder/'geometry.bytes';geometry_sha=sha(gpath);patches=[];counts=collections.Counter();matched=set();ambiguous=0;max_error=0
 with gpath.open('rb')as f:
  magic,chunks=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and chunks==manifest['chunks']and manifest['version']==1
  for chunk in range(chunks):
   mid,collision,visible,nv,ni=struct.unpack('<iiiii',f.read(20));vb=f.read(nv*48);ib=f.read(ni*4)
   if not visible or manifest['materials'][mid]['name']!='vc':continue
   v=np.frombuffer(vb,'<f4').reshape(nv,12);ix=np.frombuffer(ib,'<i4').reshape(-1,3);p=v[:,:3].copy();p[:,2]*=-1;normal=v[:,3:6].copy();normal[:,2]*=-1;faces=p[ix];centers=np.rint(faces.mean(1)*100).astype(int);groups=collections.defaultdict(list)
   for ti,key in enumerate(map(tuple,centers.tolist())):
    candidates=lookup.get(key)
    if not candidates:continue
    hits=[];face=faces[ti]
    for si in candidates:
     kind,known,qn,qc,uv=source_triangles[si];errors=np.linalg.norm(face[None,:,:]-known[PERM],axis=-1).max(-1);perm=int(np.argmin(errors));error=float(errors[perm]);order=PERM[perm]
     # R10 SurfaceTool never sets COLOR; the exact source shader owns paint.
     # All later details retain original per-corner RGB and must match it.
     r10=kind.endswith('_r10');a=np.cross(face[1]-face[0],face[2]-face[0]);b=np.cross(known[1]-known[0],known[2]-known[0]);oriented=float(a@b)<-.9999*float(np.linalg.norm(a)*np.linalg.norm(b))
     if error<=.005 and (oriented if r10 else float(a@b)<0) and (r10 or (np.min(np.sum(normal[ix[ti]]*qn[order],axis=1))>.9995 and np.max(abs(v[ix[ti],8:11]-qc[order,:3]))<.005)):hits.append((error,kind,uv[order],si,qn[order]))
    if not hits:continue
    best=min(x[0]for x in hits);near=[x for x in hits if x[0]<=best+.00002];first=near[0]
    if any(x[1]!=first[1]or not np.allclose(x[2],first[2],atol=.0003)or (x[1].endswith('_r10')and not np.allclose(x[4],first[4],atol=.002))for x in near[1:]):ambiguous+=1;continue
    error,kind,uv,si,qn=first;groups[kind].append((ti,uv,qn));counts[kind]+=1;matched.add(si);max_error=max(max_error,error)
   if groups:
    parts=[]
    for kind,rows in groups.items():
     part=dict(kind=kind,triangles=[t for t,u,n in rows],projected=[float(x)for t,u,n in rows for x in u.ravel()])
     if kind.endswith('_r10'):part['normals']=[float(x)for t,u,n in rows for x in (n*np.array([1,1,-1],np.float32)).ravel()]
     parts.append(part)
    patches.append(dict(chunk=chunk,vertices=nv,indices=ni,vertexSha256=hashlib.sha256(vb).hexdigest(),indexSha256=hashlib.sha256(ib).hexdigest(),parts=parts))
 assert sha(gpath)==geometry_sha
 report.update(mapId=manifest['mapId'],transferredTriangles=dict(counts),uniqueSourceTrianglesMatched=len(matched),unmatchedSourceTriangles=len(source_triangles)-len(matched),ambiguousUnityTrianglesUnchanged=ambiguous,maxCornerDistance=max_error,patches=len(patches),geometryUntouched=True)
 data=dict(version=1,mapId=manifest['mapId'],chunks=chunks,geometrySha256=geometry_sha,patches=patches,textures=['freight.png','northline-portrait.png'])
 OUT.mkdir(parents=True,exist_ok=True);(OUT/(slug+'.json')).write_text(json.dumps(data,separators=(',',':'))+'\n');(HERE/(slug+'-result.json')).write_text(json.dumps(report,indent=2)+'\n')
 images=[]
 for name,path in [('freight.png','assets/art_r19/freight_texture.res'),('northline-portrait.png','assets/art_r21/northline-portrait_texture.res')]:
  resource=Resource(source(path).read_bytes());image=next(r['props']['data']for r in resource.resources if r['type']=='Image');assert image['format']=='ETC2_RGB8';w,h=image['width'],image['height'];length=((w+3)//4)*((h+3)//4)*8;base=image['data'][:length];bgra=texture2ddecoder.decode_etc2(base,w,h);decoded=Image.frombytes('RGBA',(w,h),bgra,'raw','BGRA');decoded.save(OUT/name)
  images.append(dict(file=name,sourceResource=path,sourceSha256=sha(source(path)),dimensions=[w,h],format='ETC2_RGB8',baseMipSha256=hashlib.sha256(base).hexdigest(),decodedRgbaSha256=hashlib.sha256(decoded.tobytes()).hexdigest(),pngSha256=sha(OUT/name),sourceMipmapsRegeneratedByUnity=True))
 (OUT/'images.json').write_text(json.dumps(images,indent=2)+'\n')
 for folder in ['art_r19','art_r21']:shutil.copyfile(source('assets/'+folder+'/ATTRIBUTION.json'),OUT/(folder+'-ATTRIBUTION.json'))
 print(slug,'source',report['sourceTriangles'],'transferred',dict(counts),'unmatched',report['unmatchedSourceTriangles'],'ambiguous',ambiguous,'maxerror',max_error,'patches',len(patches))
 return report
def main():
 p=argparse.ArgumentParser();p.add_argument('--map',choices=list(PLANS));args=p.parse_args();results=[transfer(slug)for slug in([args.map]if args.map else list(PLANS))];(HERE/'results.json').write_text(json.dumps(results,indent=2)+'\n')
 paths=['scripts/archive_prop_materials_r10.gd','scripts/map_art_repair_r14.gd','scripts/map_art_r17.gd','scripts/map_art_r19.gd','scripts/architecture_r24.gd','scripts/alpha_junctions_r24.gd','assets/data/alpha_junction_r24.json','assets/data/architecture_r24.json','assets/data/archive_adapters_r10.json','assets/data/map_art_audit_r14.json','assets/materials/alpha_junction_r24.gdshader','assets/materials/archive_container_r10.gdshader','assets/materials/archive_van_r10.gdshader','assets/materials/vehicle_r17.gdshader','assets/materials/surface_r17.gdshader','assets/materials/assembly_r19.gdshader']
 (OUT/'source.json').write_text(json.dumps(dict(contentId='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520',manifestSha256=sha(FROZEN/'SOURCE-MANIFEST.json'),inputs={p:sha(source(p))for p in paths},exportedPlansSha256=sha(PLAN),family7PlansSha256=sha(FAMILY_PLAN),preparedR10Resources={s:json.loads(source('assets/data/archive_adapters_r10.json').read_text())['files'][s]for s in PLANS},geometryUntouched=True,engineRun=False),indent=2)+'\n')
if __name__=='__main__':main()
