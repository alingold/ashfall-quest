"""Publish this disjoint art helper only; never modify a map manifest/geometry/loader."""
from pathlib import Path
import uuid,argparse,re
from analyze import ROOT,HERE
p=argparse.ArgumentParser();p.add_argument('--existing-only',action='store_true');args=p.parse_args()
existing={'classic-bigbox-vr.json','alfa-aglae-souza.json'}
pairs=[(HERE/'staged/runtime',ROOT/'unity/Assets/Ashfall/Integration/Playable/World/SourceArt'),(HERE/'staged/payload',ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt')]
for src,dst in pairs:
 dst.mkdir(parents=True,exist_ok=True)
 for path in src.rglob('*'):
  target=dst/path.relative_to(src)
  if path.is_dir():target.mkdir(parents=True,exist_ok=True)
  elif path.suffix!='.meta':
   if args.existing_only and src.name=='payload'and path.suffix=='.json'and path.name not in existing and '"mapId"'in path.read_text():continue
   data=path.read_bytes()
   if args.existing_only and path.name=='R24SourceArtMaterials.cs':data=re.sub(rb'static bool SupportedSlug\(string slug\)\{return [^}]+\}',b'static bool SupportedSlug(string slug){return slug=="classic-bigbox-vr"||slug=="alfa-aglae-souza";}',data)
   if target.is_file()and target.read_bytes()==data:continue
   target.parent.mkdir(parents=True,exist_ok=True);tmp=target.with_name(target.name+'.source-art-new');tmp.write_bytes(data);tmp.replace(target)
 for path in [dst]+list(dst.rglob('*')):
  if path.suffix=='.meta':continue
  meta=Path(str(path)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-source-art:'+str(path.relative_to(ROOT))).hex+'\n'+('folderAsset: yes\n'if path.is_dir()else''))
print('Published disjoint SourceArt runtime/shader/payload only. Original map geometry/manifests unchanged.')
