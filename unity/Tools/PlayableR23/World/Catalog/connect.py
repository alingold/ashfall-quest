"""Connect checked archive payloads and genuine source previews to the runtime UI."""
from pathlib import Path
import hashlib,json,re,shutil,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
data=json.loads((HERE/'catalog-delta.json').read_text());assert len(data['maps'])==102 and not data['refused']
resources=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/Resources';output=resources/'R24ArchiveCatalog.json'
cards=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R24MapThumbnails';cards.mkdir(exist_ok=True)
template=(cards.parent/'R23MapThumbnails/range.png.meta').read_text();rows=[]
def meta(path,kind='DefaultImporter',template=None):
 m=Path(str(path)+'.meta')
 if m.exists():return
 guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6:'+path.relative_to(ROOT).as_posix()).hex
 if template:m.write_text(re.sub(r'(?m)^guid: .*$', 'guid: '+guid,template));return
 m.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+('folderAsset: yes\n'if path.is_dir()else'')+kind+':\n  externalObjects: {}\n')
meta(cards)
for row in data['maps']:
 assert row['published']and row['id']=='archive:'+row['slug']
 folder=ROOT/'unity/Assets/StreamingAssets/PlayableR23'/row['relativeFolder'];manifest=json.loads((folder/'map.json').read_text())
 assert manifest['mapId']==row['id']and manifest['sourceSha256']==row['sourceSha256']
 source=folder/row['preview'];original=Path(row['sourcePreview']);assert source.read_bytes()==original.read_bytes()
 target=cards/(row['slug']+source.suffix);shutil.copyfile(source,target);meta(target,template=template)
 rows.append({key:row[key]for key in ['id','title','relativeFolder']})
output.write_text(json.dumps(dict(sourceContentId=data['sourceContentId'],maps=rows),indent=2)+'\n');meta(output,'TextScriptImporter')
print('Connected genuine source map choices/previews:',len(rows),'; existing5 remain, total107.')
