"""Copy the three real source catalog thumbnails, no engine or image synthesis.
The retained original pack is read-only and selected payloads are MD5 checked.
"""
from pathlib import Path
import ast,hashlib,struct,json,re,uuid
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
ARCHIVE=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive')
DEST=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R23MapThumbnails'
PACK=ROOT/'builds/ASHFALL-Local-Maps.pck'
SLUGS=['battlefield-paintball-gods-pitdevil','alfa-aglae-souza','classic-bigbox-vr']
# Existing bounded seek-only PCK reader, extracted without module entrypoints,
# unrelated writes or cache generation.
reader=ROOT/'unity/Tools/Modules/Inventory/inventory.py';tree=ast.parse(reader.read_text())
node=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Pck4')
exec(compile(ast.Module(body=[node],type_ignores=[]),str(reader),'exec'))
pack=Pck4(PACK);catalog_bytes=pack.payload('local_archive/catalog.json');catalog=json.loads(catalog_bytes)
template=(DEST/'range.png.meta').read_text()
entries=[]
for slug in SLUGS:
    row=next(r for r in catalog['maps'] if r['id']=='archive:'+slug)
    preview='local_archive/maps/'+slug+'/converted/preview.png'
    # Follow original archive_catalog1003.image's exact preferred/fallback order.
    key=preview if preview in pack.entries else 'local_archive/maps/'+slug+'/source/thumb.jpg'
    assert key.endswith('/source/thumb.jpg'), 'New preferred preview found; explicitly verify before updating existing JPEG binding'
    content=pack.payload(key);source=ARCHIVE/'maps'/slug/'source/thumb.jpg'
    receipt=json.loads(source.with_suffix('.jpg.receipt.json').read_text())
    digest=hashlib.sha256(content).hexdigest();assert content==source.read_bytes() and digest==receipt['sha256']
    path=DEST/(slug+'.jpg');path.write_bytes(content)
    meta=path.with_suffix('.jpg.meta')
    if not meta.exists():
        text=re.sub(r'^guid: .*$', 'guid: '+uuid.uuid4().hex,template,flags=re.M)
        text=text.replace('nPOTScale: 1','nPOTScale: 0').replace('alphaIsTransparency: 1','alphaIsTransparency: 0')
        # Preserve the exact800x450 source image and full color on both targets.
        text=text.replace('buildTarget: Android\n    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: -1','buildTarget: Android\n    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: 4')
        text=text.replace('buildTarget: Android\n    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: 4\n    textureCompression: 0\n    compressionQuality: 50\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: 0','buildTarget: Android\n    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: 4\n    textureCompression: 0\n    compressionQuality: 50\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: 1')
        meta.write_text(text)
    with Image.open(path) as im:assert im.size==(800,450) and im.mode=='RGB'
    entries.append(dict(id=row['id'],title=row['title'],creator=row['creator'],official=row['official'],cache_fingerprint=row['cache_fingerprint'],resource='R23MapThumbnails/'+slug,asset=str(path.relative_to(ROOT)),source_path=str(source),pack_entry=key,preferred_preview_present=False,sha256=digest,bytes=len(content),dimensions=[800,450],receipt=receipt))
scripts=[SOURCE/'scripts/map_catalog1000.gd',SOURCE/'scripts/archive_catalog1003.gd']
data=dict(source_baseline=str(SOURCE),catalog=dict(pack=str(PACK),entry='local_archive/catalog.json',sha256=hashlib.sha256(catalog_bytes).hexdigest(),entry_md5_verified=True),resolver_sources=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in scripts],entries=entries,conversion='None: byte-identical original source JPEGs. Existing Unity UI applies source card center-crop.',missing_genuine_previews=[],engine_or_device_run=False)
(HERE/'provenance.json').write_text(json.dumps(data,indent=2))
print('Copied3 byte-identical original archive JPEGs; PCK payload MD5 + original download receipts verified. No engine.')
