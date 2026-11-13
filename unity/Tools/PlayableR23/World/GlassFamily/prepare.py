"""Stage genuine clear_glass_r10 pane render subsets for the nine source maps.

Tools-only. Material shader identity and exact oriented source triangles decide
membership; the panel.glass flag, RGB, bounding boxes and map titles do not.
"""
from pathlib import Path
import argparse,collections,hashlib,itertools,json,struct,sys
import numpy as np

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parent/'SourceArt'))
import analyze as source
ROOT=source.ROOT;LIVE=ROOT/'unity/Assets/StreamingAssets/PlayableR23';OUT=HERE/'staged';PERM=np.array([[0,2,1],[1,0,2],[2,1,0]])
CLEAR='res://assets/materials/clear_glass_r10.gdshader'

def sha(path):
 with Path(path).open('rb')as f:return hashlib.file_digest(f,'sha256').hexdigest()

def arrays(resource,ref):
 assert ref[1]==2
 surfaces=resource.resources[ref[2]]['props']['_surfaces'];assert len(surfaces)==1;s=surfaces[0];n=s['vertex_count'];fmt=s['format'];assert len(s['vertex_data'])==n*20
 p=np.frombuffer(s['vertex_data'],'<f4',n*3).reshape(n,3);q=np.frombuffer(s['vertex_data'][n*12:],'<u2').reshape(n,4)[:,:2].astype(np.float32)/65535*2-1;normal=np.column_stack((q,1-np.abs(q).sum(1)));t=np.maximum(-normal[:,2],0);normal[:,:2]+=np.where(normal[:,:2]>=0,-t[:,None],t[:,None]);normal/=np.linalg.norm(normal,axis=1)[:,None]
 ix=np.frombuffer(s['index_data'],'<u2'if n<65536 else'<i4').astype(np.int32)if'index_data'in s else np.arange(n,dtype=np.int32);assert len(ix)%3==0
 attr=s.get('attribute_data',b'');stride=len(attr)//n;offset=4 if fmt&8 else 0;uv=np.ndarray((n,2),'<f4',attr,offset=offset,strides=(stride,4)).copy()if fmt&16 else np.zeros((n,2),np.float32);offset+=8 if fmt&16 else 0;uv2=np.ndarray((n,2),'<f4',attr,offset=offset,strides=(stride,4)).copy()if fmt&32 else np.zeros((n,2),np.float32)
 ix=ix.reshape(-1,3);return p[ix].copy(),normal[ix],np.c_[uv,uv2[:,0]][ix]

def material(resource,ref):
 if ref[1]==3:return dict(type='external',path=resource.external[ref[2]][1],shader=None)
 assert ref[1]==2;m=resource.resources[ref[2]];p=m['props'];shader=p.get('shader');return dict(type=m['type'],name=p.get('resource_name'),shader=resource.external[shader[2]][1]if shader and shader[1]==3 else None)

def source_faces(slug):
 pack=source.Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');raw=pack.read('local_archive_r10/'+slug+'.res');pack.f.close();expected=json.loads(source.source('assets/data/archive_adapters_r10.json').read_text())['files'][slug];assert hashlib.sha256(raw).hexdigest()==expected
 r=source.Resource(raw);panels=next(v['props']['details']['panels']for v in r.resources if'details'in v['props']);faces=[];opaque=[];owners=[];counts=collections.Counter()
 for pi,panel in enumerate(panels):
  counts['allPanelAssemblies']+=1
  for ai,part in enumerate([panel]+panel.get('attachments',[])):
   mat=material(r,part['material'])
   if mat['shader']!=CLEAR:
    counts['opaqueBaseParts'if ai==0 else'opaqueAttachmentParts']+=1
    # Retain actual opaque geometry as negative controls under glass owners;
    # do not infer its transparency from the containing panel's glass flag.
    if ai>0 and panel['glass']:
     p,n,uv=arrays(r,part['mesh']);opaque.extend((mat.get('name',mat.get('path')),q)for q in p)
    continue
   assert mat['type']=='ShaderMaterial'and mat['name']=='s48'
   p,n,uv=arrays(r,part['mesh']);counts['clearBaseParts'if ai==0 else'clearAttachmentParts']+=1
   owner=dict(panel=pi,part='base'if ai==0 else'attachment'+str(ai-1),meshReference=part['mesh'],materialReference=part['material'],triangles=len(p));owners.append(owner)
   for ti,(q,qn,qu)in enumerate(zip(p,n,uv)):faces.append(dict(panel=pi,part=ai,triangle=ti,p=q,n=qn,uv=qu))
 counts['clearSourceTriangles']=len(faces);return faces,opaque,owners,dict(counts),expected

def build(slug):
 known,opaque,owners,census,prepared_sha=source_faces(slug);lookup=collections.defaultdict(list)
 for si,row in enumerate(known):
  center=row['p'].mean(0);lo=np.floor((center-.005)/.05).astype(int);hi=np.floor((center+.005)/.05).astype(int)
  for key in itertools.product(*(range(lo[j],hi[j]+1)for j in range(3))):lookup[key].append(si)
 base=LIVE/'SourceArt'/(slug+'.json');before=base.read_bytes();data=json.loads(before);original=json.loads(before);patches={p['chunk']:p for p in data['patches']};mf=LIVE/'Maps'/slug/'map.json';manifest=json.loads(mf.read_text());geometry=mf.with_name('geometry.bytes');assert data['geometrySha256']==sha(geometry)and manifest['chunks']==data['chunks'];matched=set();added=[];already_art=[];already_native=[];max_error=0.;unmatched_raw=0;source_bindings=[]
 with geometry.open('rb')as f:
  magic,nc=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and nc==data['chunks']
  for ci in range(nc):
   mid,collision,visible,nv,ni=struct.unpack('<iiiii',f.read(20));vb=f.read(nv*48);ib=f.read(ni*4)
   if not visible or manifest['materials'][mid]['name']!='s48':continue
   v=np.frombuffer(vb,'<f4').reshape(nv,12);ix=np.frombuffer(ib,'<i4').reshape(-1,3);p=v[:,:3].copy();p[:,2]*=-1;n=v[:,3:6].copy();n[:,2]*=-1;faces=p[ix];centers=np.floor(faces.mean(1)/.05).astype(int);new=[];patch=patches.get(ci);existing={}if patch is None else{t:part['kind']for part in patch['parts']for t in part['triangles']}
   for ti,key in enumerate(map(tuple,centers.tolist())):
    hits=[]
    for si in lookup.get(key,[]):
     row=known[si];errors=np.linalg.norm(faces[ti][None,:,:]-row['p'][PERM],axis=-1).max(-1);which=int(np.argmin(errors));error=float(errors[which]);order=PERM[which]
     if error<=.005 and np.min(np.sum(n[ix[ti]]*row['n'][order],axis=1))>.9995:hits.append((error,si,order))
    if not hits:unmatched_raw+=1;continue
    hits.sort(key=lambda x:x[0]);error,si,order=hits[0];row=known[si];matched.update(h[1]for h in hits);max_error=max(max_error,error);source_bindings.append(dict(chunk=ci,triangle=ti,sourcePanel=row['panel'],sourcePart=row['part'],sourceTriangle=row['triangle'],maxCornerDistance=error))
    if ti in existing:
     assert existing[ti]=='clear_glass','An actual pane overlaps a different existing source-art owner';already_art.append((ci,ti));continue
    native=manifest.get('chunkClearGlass')
    if native is not None and native[ci]:already_native.append((ci,ti));continue
    new.append((ti,row['uv'][order]));added.append((ci,ti))
   if not new:continue
   if patch is None:patch=dict(chunk=ci,vertices=nv,indices=ni,vertexSha256=hashlib.sha256(vb).hexdigest(),indexSha256=hashlib.sha256(ib).hexdigest(),parts=[]);patches[ci]=patch;data['patches'].append(patch)
   patch['parts'].append(dict(kind='clear_glass',triangles=[t for t,u in new],projected=[float(x)for t,u in new for x in u.ravel()]))
 # All prior art records, including real ALFA28/junctions/four Classic panes,
 # survive identically. Removing only new records recovers the original JSON.
 restored=json.loads(json.dumps(data));new_keys=set(added)
 for patch in restored['patches']:patch['parts']=[p for p in patch['parts']if not p['triangles']or not all((patch['chunk'],ti)in new_keys for ti in p['triangles'])]
 restored['patches']=[p for p in restored['patches']if p['parts']];assert restored==original
 # Material-domain control: no vc/wood/door/frame face can enter the new set.
 assert len(set(added))==len(added)and all(m['maxCornerDistance']<=.005 for m in source_bindings)
 OUT.mkdir(exist_ok=True);output=OUT/(slug+'.json');output.write_bytes(before if not added else(json.dumps(data,separators=(',',':'))+'\n').encode())
 report=dict(mapId='archive:'+slug,preparedR10Sha256=prepared_sha,sourcePaneShader=CLEAR,sourcePaneShaderSha256=sha(source.source('assets/materials/clear_glass_r10.gdshader')),basePlanSha256=hashlib.sha256(before).hexdigest(),stagedPlanSha256=sha(output),mapManifestSha256=sha(mf),geometrySha256=data['geometrySha256'],sourceCensus=census,sourceClearOwners=owners,sourceTrianglesMatched=len(matched),sourceTrianglesUnmatched=len(known)-len(matched),newClearGlassTriangles=len(added),alreadySourceArtGlassTriangles=len(already_art),alreadyNativeClearGlassTriangles=len(already_native),unmatchedRawS48TrianglesUnchanged=unmatched_raw,maxCornerDistance=max_error,addedRawFaces=added,sourceBindings=source_bindings,opaqueAttachmentsRetained=len(opaque),allPriorArtPartsPreserved=True,originalCollisionAndOwnershipUnchanged=True,engineRun=False)
 (OUT/(slug+'-checks.json')).write_text(json.dumps(report,indent=2)+'\n');print(slug,'source',census['clearSourceTriangles'],'matched',len(matched),'new',len(added),'already',len(already_art),'+',len(already_native),'unmatched',len(known)-len(matched),flush=True);return report

def main():
 p=argparse.ArgumentParser();p.add_argument('--map',choices=list(source.PLANS));args=p.parse_args();slugs=[args.map]if args.map else list(source.PLANS);protected={p:sha(p)for p in (LIVE/'SourceArt').rglob('*')if p.is_file()};reports=[build(s)for s in slugs];assert all(sha(p)==h for p,h in protected.items()),'Live SourceArt changed during Tools-only preparation';(HERE/'results.json').write_text(json.dumps(dict(maps=[{k:v for k,v in r.items()if k not in['sourceClearOwners','sourceBindings','addedRawFaces']}for r in reports],liveSourceArtFilesUntouched=len(protected),assetsWritten=False),indent=2)+'\n')

if __name__=='__main__':main()
