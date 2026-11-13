"""Actual immutable source witnesses and safe publication for seven central maps."""
from pathlib import Path
import argparse,base64,collections,json,struct
import numpy as np
from prepare import ROOT,SOURCE,OUT,EXISTING,PINS,sha,source_export,export_folders,publish_staged
HERE=Path(__file__).resolve().parent
def check(publish=False):
 assert sha(SOURCE/'assets/data/architecture_r24.json')==PINS['assets/data/architecture_r24.json']
 summary=json.loads((HERE/'central7-results.json').read_text());protected=summary['protectedAlfaClassic'];assert all(sha(Path(path))==digest for path,digest in protected.items())
 config=json.loads((SOURCE/'assets/data/architecture_r24.json').read_text())['maps'];slugs=sorted(set(export_folders())-EXISTING);reports=[]
 for slug in slugs:
  folder=OUT/slug;before=json.loads((folder/'map.json').read_text());data=json.loads((HERE/'staged'/(slug+'-architecture.json')).read_text());report=json.loads((HERE/(slug+'-transfer.json')).read_text());plan=config['archive:'+slug];assert data['geometrySha256']==sha(folder/'geometry.bytes')and data['family']==plan.get('family_details_r6',{})and data['sourcePins']==PINS
  export,meshes=source_export(slug,plan);source_rows={row['path']:row for row in plan['meshes']};witnesses=collections.defaultdict(list)
  for w in report['directSourceWitnesses']:witnesses[w['chunk']].append(w)
  count=0;actual=[]
  with(folder/'geometry.bytes').open('rb')as stream:
   magic,chunks=struct.unpack('<ii',stream.read(8));assert magic==0x52323357 and chunks==data['chunks']==before['chunks'];patches={p['chunk']:p for p in data['patches']}
   for chunk in range(chunks):
    mid,collision,visible,nv,ni=struct.unpack('<iiiii',stream.read(20));vertices=np.frombuffer(stream.read(nv*48),'<f4').reshape(nv,12);indices=np.frombuffer(stream.read(ni*4),'<i4').reshape(-1,3);patch=patches.get(chunk)
    if patch is None:continue
    assert visible and patch['vertices']==nv;flags=base64.b64decode(patch['triangleKinds']);assert len(flags)==len(patch['triangles'])and len(set(patch['triangles']))==len(flags);lookup=dict(zip(patch['triangles'],flags));assert all(0<=tri<len(indices)for tri in lookup);count+=len(flags)
    for w in witnesses.get(chunk,[]):
     assert lookup[w['triangle']]==w['kind'];source=source_rows[w['sourceNode']];channels=meshes[w['sourceNode']]['channels'];sx=np.memmap(export/channels['indices'],dtype='<i4',mode='r').reshape(-1,3);source_indices=sx[w['sourceTriangle']];source_flags=np.frombuffer(base64.b64decode(source['flags']),np.uint8);assert int(source_flags[source_indices[2]])==w['kind'];at=source_indices[[0,2,1]]
     sv=np.memmap(export/channels['vertices'],dtype='<f4',mode='r').reshape(-1,3)[at].copy();sn=np.memmap(export/channels['normals'],dtype='<f4',mode='r').reshape(-1,3)[at].copy();sc=np.memmap(export/channels['colors'],dtype='<f4',mode='r').reshape(-1,4)[at,:3].copy();sv[:,2]*=-1;sn[:,2]*=-1;v=vertices[indices[w['triangle']]];found=False
     for shift in range(3):
      distance=float(np.linalg.norm(v[:,:3]-np.roll(sv,shift,axis=0),axis=1).max())
      if distance<=.005 and np.max(abs(v[:,8:11]-np.roll(sc,shift,axis=0)))<.005 and np.min(np.sum(v[:,3:6]*np.roll(sn,shift,axis=0),axis=1))>.9995:found=True;break
     assert found and before['materials'][mid]['name']==source['material']==w['material'];actual.append(dict(kind=w['kind'],chunk=chunk,triangle=w['triangle'],sourceNode=w['sourceNode'],sourceTriangle=w['sourceTriangle'],maxCornerDistance=distance))
  assert count==sum(report['transferredUnityFaces'].values())and count>1000
  family=data['family'];assert len(family.get('junction_projections',[]))<=16 and len(family.get('trailers',[]))<=8 and len(family.get('wheels',[]))<=16
  if publish:
   publish_staged(slug,data,before);after=json.loads((folder/'map.json').read_text());assert after['architectureFile']=='architecture.json'and {k:v for k,v in before.items()if k!='architectureFile'}=={k:v for k,v in after.items()if k!='architectureFile'}
   expected=json.loads((SOURCE.parent/'SOURCE-MANIFEST.json').read_text());pins={r['path']:r['sha256']for r in expected['files']}
   for name in data['textures']:
    target=folder/'architecture'/name;relative=('assets/art_r19/'if name=='freight.png'else'assets/art_r24/')+name;assert sha(target)==pins[relative]and target.stat().st_nlink==1,'Local architecture texture must be independent and exact'
  reports.append(dict(map=slug,chunks=len(data['patches']),sourceClassifiedFaces=report['sourceClassifiedFaces'],transferredFaces=count,kinds=report['transferredUnityFaces'],boundedFootprintFaces=report['coplanarFootprintFaces'],actualSourceWitnesses=actual,geometrySha256=data['geometrySha256'],published=publish,geometryAndOtherManifestFieldsUnchanged=True))
 assert all(sha(Path(path))==digest for path,digest in protected.items()),'Do not change finished ALFA/Classic'
 (HERE/'central7-checks.json').write_text(json.dumps(dict(maps=reports,protectedAlfaClassic=protected,sourcePlanSha256=PINS['assets/data/architecture_r24.json'],engineRun=False,deviceRun=False),indent=2)+'\n')
 for r in reports:print('PASS',r['map'],r['transferredFaces'],'faces',r['chunks'],'chunks',len(r['actualSourceWitnesses']),'actual source-kind witnesses','published'if publish else'staged')
 return reports
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--publish',action='store_true');check(p.parse_args().publish)
