"""Read existing pinned metadata only; never decode/re-export a map mesh."""
from pathlib import Path
import hashlib,json
T=Path(__file__).resolve().parent;R=T.parents[3];S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');L=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive');U=R/'unity/Assets/StreamingAssets/PlayableR23'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
manifest=S.parent/'SOURCE-MANIFEST.json';assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={x['path']:x['sha256']for x in read(manifest)['files']};inputs={}
for name in ['scripts/map_catalog1000.gd','scripts/team_launch_r12.gd','scripts/archive_catalog1003.gd','scripts/archive_map1003.gd','scripts/cyber_bird.gd','assets/data/map_semantics_r14.json','assets/data/archive_semantics_r9.json','assets/data/archive_adapters_r10.json']:
 assert sha(S/name)==pins[name],name;inputs[name]=pins[name]
sem=read(S/'assets/data/map_semantics_r14.json')['maps'];arc=read(S/'assets/data/archive_semantics_r9.json')['maps'];adapters=read(S/'assets/data/archive_adapters_r10.json')['files'];catalog=read(L/'catalog.json');archive={x['slug']:x for x in catalog['maps']};available=read(R/'unity/Assets/Ashfall/Integration/Playable/World/Resources/R24ArchiveCatalog.json')['maps'];selected={x['id']:x['relativeFolder']for x in available};selected.update({'archive:battlefield-paintball-gods-pitdevil':'World','archive:alfa-aglae-souza':'Maps/alfa-aglae-souza','archive:classic-bigbox-vr':'Maps/classic-bigbox-vr','valley':'Maps/valley','range':'Maps/range','cyber_bird':'Maps/cyber_bird'})
rows=[];checks=0
for id,folder in sorted(selected.items()):
 payload=read(U/folder/'map.json');assert payload['mapId']==id;row=dict(id=id,payloadFolder=folder,sourceSha256=payload['sourceSha256'],sourceOrigin=[0,0,0],removedOrigin=[0,0,0],hasArchiveMetadata=False,solidMinY=0,solidMaxY=0,hasDeploymentTop=False,deploymentTop=0,manifestSha256='',cacheFingerprint='',adapterSha256='')
 if id.startswith('archive:'):
  slug=id[8:];p=L/'maps'/slug/'converted/manifest.json';m=read(p);assert m['source_sha256']==sem[slug]['source_sha256']==arc[id]['source_sha256']==payload['sourceSha256'];assert m['cache_fingerprint']==sem[slug]['cache_fingerprint']==archive[slug]['cache_fingerprint'];checks+=2
  provenance=U/folder/'provenance.json'
  if provenance.exists():
   d=read(provenance)
   if 'archiveInputs'in d:
    lookup={k.replace('\\','/'):v for k,v in d['archiveInputs'].items()};assert lookup[p.as_posix()]==sha(p);checks+=1
  row.update(sourceOrigin=[0,0,4800],removedOrigin=[0,0,4800],catalogMinX=m['bounds_min'][0],catalogMinZ=m['bounds_min'][2],catalogMaxX=m['bounds_max'][0],catalogMaxZ=m['bounds_max'][2],hasArchiveMetadata=True,solidMinY=m['solid_min'][1],solidMaxY=m['solid_max'][1],hasDeploymentTop=True,deploymentTop=m['solid_max'][1],manifestSha256=sha(p),cacheFingerprint=m['cache_fingerprint'],adapterSha256=adapters[slug])
 else:
  bounds={'valley':[-100,-130,100,70],'range':[-50,-50,50,50],'cyber_bird':[-400,-400,400,400]}[id];row.update(catalogMinX=bounds[0],catalogMinZ=bounds[1],catalogMaxX=bounds[2],catalogMaxZ=bounds[3],sourceOrigin=[0,0,{'valley':0,'range':600,'cyber_bird':2400}[id]])
  if id=='cyber_bird':
   city=read(U/folder/'city.json');p=read(U/folder/'provenance.json');assert p['sourceInputs']['scripts/cyber_bird.gd']==pins['scripts/cyber_bird.gd']==payload['sourceSha256'];assert city['originGodot']==row['sourceOrigin'];row.update(hasDeploymentTop=True,deploymentTop=city['deploymentTop'],manifestSha256=sha(U/folder/'city.json'));checks+=2
 rows.append(row)
assert len(rows)==108
out=T/'Payload/launch-maps.json';out.write_text(json.dumps(dict(version=1,contentId='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520',sourceManifestSha256=sha(manifest),sourceInputs=inputs,archiveCatalogSha256=sha(L/'catalog.json'),rows=rows),separators=(',',':'))+'\n',encoding='utf-8')
print('Prepared exact108 source catalog/solid-top/origin rows',out.stat().st_size,'bytes;',checks,'source checkpoint bindings; no geometry read')
