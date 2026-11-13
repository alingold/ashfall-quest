"""Stage two verified Classic pane materials; no geometry/collision/Assets writes.

Opaque attachments are negative controls, never inferred transparent from their
panel owner's `glass` flag. Only the actual pane ShaderMaterial is authority.
"""
from analyze import *
import itertools,shutil

SLUG='classic-bigbox-vr';OUT=HERE/'staged/window-candidate';PERM=np.array([[0,2,1],[1,0,2],[2,1,0]])

def main():
 pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');raw=pack.read('local_archive_r10/'+SLUG+'.res');pack.f.close()
 expected=json.loads(source('assets/data/archive_adapters_r10.json').read_text())['files'][SLUG];assert hashlib.sha256(raw).hexdigest()==expected
 resource=Resource(raw);panels=next(r['props']['details']['panels']for r in resource.resources if'details'in r['props'])
 known=[];bindings=[]
 for pi in [433,616]:
  row=panels[pi];assert row['glass'];material=resource.resources[row['material'][2]];shader=material['props']['shader'];assert resource.external[shader[2]][1]=='res://assets/materials/clear_glass_r10.gdshader'
  mesh=resource.resources[row['mesh'][2]];surface=mesh['props']['_surfaces'][0];n=surface['vertex_count'];v=np.frombuffer(surface['vertex_data'],'<f4',n*3).reshape(n,3);ix=np.frombuffer(surface['index_data'],'<u2'if n<65536 else'<i4')if'index_data'in surface else np.arange(n)
  faces=v[ix].reshape(-1,3,3);assert len(faces)==2
  known.extend((pi,ti,p.copy())for ti,p in enumerate(faces));bindings.append(dict(panelId=pi,meshReference=row['mesh'],materialReference=row['material'],sourceFacesSha256=hashlib.sha256(faces.tobytes()).hexdigest(),opaqueAttachmentCount=len(row.get('attachments',[]))))
 data=json.loads((HERE/'staged/payload'/f'{SLUG}.json').read_text())
 for patch in data['patches']:patch['parts']=[p for p in patch['parts']if p['kind']!='clear_glass']
 data['patches']=[p for p in data['patches']if p['parts']]
 patches={p['chunk']:p for p in data['patches']};manifest_path=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/SLUG/'map.json';manifest=json.loads(manifest_path.read_text());geometry=manifest_path.with_name('geometry.bytes');assert sha(geometry)==data['geometrySha256'];matches=[];matched=set()
 with geometry.open('rb')as f:
  magic,nc=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and nc==data['chunks']
  for ci in range(nc):
   mid,collision,visible,nv,ni=struct.unpack('<iiiii',f.read(20));vb=f.read(nv*48);ib=f.read(ni*4)
   # The actual pane's material identity is s48. Never apply panel transparency
   # to vc frames, shutters, cut trim or neighbouring geometry.
   if not visible or manifest['materials'][mid]['name']!='s48':continue
   v=np.frombuffer(vb,'<f4').reshape(nv,12);ix=np.frombuffer(ib,'<i4').reshape(-1,3);p=v[:,:3].copy();p[:,2]*=-1;faces=p[ix];selected=[]
   for pi,sti,known_face in known:
    near=np.flatnonzero(np.linalg.norm(faces.mean(1)-known_face.mean(0),axis=1)<=.005)
    for ti in near:
     errors=np.linalg.norm(faces[ti][None,:,:]-known_face[PERM],axis=-1).max(-1);error=float(errors.min())
     if error>.005:continue
     assert (pi,sti)not in matched,'Ambiguous duplicate pane';matched.add((pi,sti));selected.append(int(ti));matches.append(dict(panelId=pi,sourceTriangle=sti,chunk=ci,triangle=int(ti),maxCornerDistance=error))
   if not selected:continue
   patch=patches.get(ci)
   if patch is None:
    patch=dict(chunk=ci,vertices=nv,indices=ni,vertexSha256=hashlib.sha256(vb).hexdigest(),indexSha256=hashlib.sha256(ib).hexdigest(),parts=[]);data['patches'].append(patch);patches[ci]=patch
   owned={t for part in patch['parts']for t in part['triangles']};assert not owned.intersection(selected),'Existing art ownership overlap'
   patch['parts'].append(dict(kind='clear_glass',triangles=selected,projected=[0.]*(len(selected)*9)))
 assert len(matched)==4 and len(matches)==4
 assert any(m['chunk']==185 and m['triangle']==351 for m in matches)and any(m['chunk']==186 and m['triangle']==284 for m in matches)
 assert all(m['chunk']not in[266,270]for m in matches),'Opaque attachment was made glass'
 assert sha(geometry)==data['geometrySha256'];payload=OUT/'payload';runtime=OUT/'runtime';payload.mkdir(parents=True,exist_ok=True);runtime.mkdir(parents=True,exist_ok=True)
 (payload/f'{SLUG}.json').write_text(json.dumps(data,separators=(',',':'))+'\n')
 for path in (HERE/'staged/runtime').rglob('*'):
  if path.is_file():target=runtime/path.relative_to(HERE/'staged/runtime');target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(path,target)
 p=runtime/'R24SourceArtPlan.cs';s=p.read_text()
 if 'UsesClearGlass'not in s:s=s.replace('kind=="banner";','kind=="banner"||kind=="clear_glass";').replace('public string MapId {','public bool UsesClearGlass {get;private set;}public string MapId {').replace('foreach(int tri in part.triangles)','if(part.kind=="clear_glass")UsesClearGlass=true;foreach(int tri in part.triangles)')
 p.write_text(s)
 p=runtime/'R24SourceArtMaterials.cs';s=p.read_text();at='materials.Add(kinds[i],m);}Ready=true;'
 if 'Plan.UsesClearGlass'not in s:
  assert at in s;s=s.replace(at,'materials.Add(kinds[i],m);}if(Plan.UsesClearGlass){var glassShader=Resources.Load<Shader>("R23ArchiveGlass");if(glassShader==null){Failure="Original archive clear glass shader missing";yield break;}var glass=new Material(glassShader);owned.Add(glass);glass.name="Actual source clear_glass_r10 panes";materials.Add("clear_glass",glass);}Ready=true;')
 p.write_text(s)
 for name in ['images.json','freight.png','northline-portrait.png']:shutil.copyfile(HERE/'staged/payload'/name,payload/name)
 report=dict(mapId='archive:'+SLUG,preparedR10Sha256=expected,sourceShader='assets/materials/clear_glass_r10.gdshader',sourceShaderSha256=sha(source('assets/materials/clear_glass_r10.gdshader')),panels=bindings,matches=matches,originalGeometrySha256=data['geometrySha256'],originalCollisionUntouched=True,opaqueAttachmentsUnchanged=True,scope='Only four actual source pane triangles of panels433/616. No new frame geometry, collider, gameplay owner, or opacity on vc attachments. Raw266/4977 does not match these pane meshes and is unchanged.',engineRun=False)
 (payload/'windows-source.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
