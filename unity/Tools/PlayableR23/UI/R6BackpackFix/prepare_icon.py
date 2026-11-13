"""Use the exact frozen R6 revive utility SVG and existing CPU rasterizer."""
from pathlib import Path
import hashlib,importlib.util,json,uuid,re
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
manifest=FROZEN.parent/'SOURCE-MANIFEST.json';assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
table={r['path']:r for r in json.loads(manifest.read_text())['files']};relative='assets/ui/backpack_r23_revive.svg';svg=FROZEN/relative;assert sha(svg)==table[relative]['sha256']
spec=importlib.util.spec_from_file_location('source_backpack_svg',ROOT/'unity/Tools/PlayableR23/WeaponWheel/backpack_icons.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);module.SOURCE=FROZEN/'assets/ui'
out=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R23SourceUI/backpack_r23_revive.png';out.write_bytes(module.rasterize('backpack_r23_revive'))
template=(out.parent/'settings_0.png.meta').read_text();template=template.replace('nPOTScale: 1','nPOTScale: 0');guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-source-ui/backpack_r23_revive').hex
Path(str(out)+'.meta').write_text(re.sub(r'guid: [a-f0-9]{32}','guid: '+guid,template,count=1),encoding='utf-8')
(HERE/'icon-provenance.json').write_text(json.dumps(dict(source=str(svg),sourceSha256=sha(svg),output=str(out),outputSha256=sha(out),renderer='existing bounded source SVG Pillow parser; preserves white outlines/cyan center/RGBA',engineRun=False),indent=2)+'\n',encoding='utf-8')
print('Published genuine source revive glyph and source-aspect NPOT-preserving texture import settings.')
