"""Port exact ALFA-only AlphaJunctionRoadFacesR24_0/1 render subsets.

The frozen alpha_junctions_r24 controller extracts 21+7 ordered road faces in
addition to the R17 junction meshes. No other map, geometry or collider changes.
"""
from analyze import *
import argparse,itertools,uuid

SLUG='alfa-aglae-souza';PERM=np.array([[0,2,1],[1,0,2],[2,1,0]])
STAGED=HERE/'staged/alpha-roads';STAGED.mkdir(exist_ok=True)

def main(publish=False):
 spec=importlib.util.spec_from_file_location('alpha_road_source_binding',ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6/prepare.py');architecture=importlib.util.module_from_spec(spec);spec.loader.exec_module(architecture)
 guard_path=source('assets/data/alpha_junction_r24.json');guard=json.loads(guard_path.read_text());plan=json.loads(source('assets/data/architecture_r24.json').read_text())['maps']['archive:'+SLUG];folder,meshes=architecture.source_export(SLUG,plan);prepared_rows=prepared(SLUG);known=[];owners=[]
 for ri,road in enumerate(guard['roads']):
  assert road['material']=='vc';mesh=meshes[road['path']];assert mesh['transform']==[[1,0,0],[0,1,0],[0,0,1],[0,0,0]];ch=mesh['channels'];assert sha(folder/ch['vertices'])==road['vertex_sha256']
  positions=np.fromfile(folder/ch['vertices'],'<f4').reshape(-1,3);normals=np.fromfile(folder/ch['normals'],'<f4').reshape(-1,3);colors=np.fromfile(folder/ch['colors'],'<f4').reshape(-1,4);indices=np.fromfile(folder/ch['indices'],'<i4').reshape(-1,3);remaining={tuple(x)for x in indices.tolist()};ordered=set()
  prepared_row=prepared_rows[road['path']]
  for row in road['triangles']:
   ix=np.array(row['indices'],np.int32);key=tuple(ix.tolist());assert key not in remaining and key not in ordered,'Source alpha controller must already have detached this unique ordered face';ordered.add(key)
   p=positions[ix];n=normals[ix];c=colors[ix];assert np.array_equal(p,np.array(row['positions'],np.float32))and np.allclose(c,np.array(row['colors'],np.float32),atol=1e-7)
   # R14/R17 compact the vertex arrays. Recover the unchanged original UVs
   # using exact prepared positions, not provisional pre-compaction indices.
   uv=[]
   for point in p:
    at=np.flatnonzero(np.all(prepared_row['vertices']==point,axis=1));assert len(at)>0
    values=prepared_row['uv'][at];assert np.all(values==values[0]),'Ambiguous source UV at road corner';uv.append(values[0])
   uv=np.array(uv,np.float32);known.append(dict(owner='AlphaJunctionRoadFacesR24_'+str(ri),sourceNode=road['path'],sourceIndices=key,sourceTriangle=row['triangle'],junction=row['junction'],p=p,n=n,c=c,uv=uv))
  owners.append(dict(owner='AlphaJunctionRoadFacesR24_'+str(ri),sourceNode=road['path'],sourceVertexSha256=road['vertex_sha256'],triangles=len(road['triangles'])))
 assert len(known)==28 and [r['triangles']for r in owners]==[21,7]
 live=ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt';base_path=live/(SLUG+'.json');before=base_path.read_bytes();data=json.loads(before);patches={p['chunk']:p for p in data['patches']};mapfolder=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/SLUG;manifest=json.loads((mapfolder/'map.json').read_text());geometry=mapfolder/'geometry.bytes';assert sha(geometry)==data['geometrySha256'];matched=set();matches=[]
 with geometry.open('rb')as f:
  magic,nc=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and nc==data['chunks']
  for ci in range(nc):
   mid,collision,visible,nv,ni=struct.unpack('<iiiii',f.read(20));vb=f.read(nv*48);ib=f.read(ni*4)
   if not visible or manifest['materials'][mid]['name']!='vc':continue
   v=np.frombuffer(vb,'<f4').reshape(nv,12);ix=np.frombuffer(ib,'<i4').reshape(-1,3);p=v[:,:3].copy();p[:,2]*=-1;n=v[:,3:6].copy();n[:,2]*=-1;faces=p[ix];centers=faces.mean(1);new=[]
   for si,row in enumerate(known):
    for ti in np.flatnonzero(np.linalg.norm(centers-row['p'].mean(0),axis=1)<=.005):
     errors=np.linalg.norm(faces[ti][None,:,:]-row['p'][PERM],axis=-1).max(-1);which=int(np.argmin(errors));order=PERM[which];error=float(errors[which])
     if error>.005 or np.min(np.sum(n[ix[ti]]*row['n'][order],axis=1))<=.9995 or np.max(abs(v[ix[ti],8:11]-row['c'][order,:3]))>=.005:continue
     assert si not in matched,'Ambiguous duplicate raw road face';matched.add(si);new.append((int(ti),row['uv'][order]));matches.append(dict(owner=row['owner'],sourceNode=row['sourceNode'],sourceIndices=row['sourceIndices'],sourceTriangle=row['sourceTriangle'],chunk=ci,triangle=int(ti),maxCornerDistance=error))
   if not new:continue
   patch=patches.get(ci)
   if patch is None:patch=dict(chunk=ci,vertices=nv,indices=ni,vertexSha256=hashlib.sha256(vb).hexdigest(),indexSha256=hashlib.sha256(ib).hexdigest(),parts=[]);patches[ci]=patch;data['patches'].append(patch)
   old={t for part in patch['parts']for t in part['triangles']};assert not old.intersection(t for t,uv in new),'Previous SourceArt owner overlap'
   patch['parts'].append(dict(kind='junction',triangles=[t for t,uv in new],projected=[float(x)for t,uv in new for x in uv.ravel()]))
 assert len(matched)==len(matches)==28
 # Recover every previous part exactly by dropping only these added records.
 restored=json.loads(json.dumps(data));added={(m['chunk'],m['triangle'])for m in matches}
 for patch in restored['patches']:patch['parts']=[part for part in patch['parts']if not part['triangles']or not all((patch['chunk'],ti)in added for ti in part['triangles'])]
 restored['patches']=[p for p in restored['patches']if p['parts']];assert restored==json.loads(before)
 target=STAGED/(SLUG+'.json');target.write_text(json.dumps(data,separators=(',',':'))+'\n');report=dict(sourceGuardSha256=sha(guard_path),sourceControllerSha256=sha(source('scripts/alpha_junctions_r24.gd')),sourceShaderSha256=sha(source('assets/materials/alpha_junction_r24.gdshader')),sourceOwners=owners,matches=matches,sourceFaces=28,oldAlphaPlanSha256=hashlib.sha256(before).hexdigest(),geometrySha256=data['geometrySha256'],previousAllArtPartsPreserved=True,otherEightMapsUntouched=True,collisionUntouched=True,published=publish,engineRun=False)
 witnesses=HERE/'alpha-road-witnesses.json'
 if witnesses.exists():
  bound=json.loads(witnesses.read_text());assert bound['geometrySha256']==report['geometrySha256'];report['capturedCameraWitnesses']=bound['capturedCameraWitnesses']
 if publish:
  assert base_path.read_bytes()==before
  temp=base_path.with_name(base_path.name+'.alpha-roads-new');temp.write_bytes(target.read_bytes());temp.replace(base_path);(HERE/'staged/payload'/base_path.name).write_bytes(target.read_bytes())
  provenance=live/'alpha-roads-source.json';tmp=provenance.with_name(provenance.name+'.new');tmp.write_text(json.dumps(report,indent=2)+'\n');tmp.replace(provenance)
  meta=Path(str(provenance)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-source-art:'+str(provenance.relative_to(ROOT))).hex+'\n')
 (HERE/'alpha-road-checks.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(dict(sourceOwners=owners,sourceFaces=28,matchedRawFaces=len(matches),maxCornerDistance=max(m['maxCornerDistance']for m in matches),previousAllArtPartsPreserved=True,published=publish),indent=2));return report

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--publish',action='store_true');main(p.parse_args().publish)
