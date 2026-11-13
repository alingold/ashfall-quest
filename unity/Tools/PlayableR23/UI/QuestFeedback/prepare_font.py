"""Preserve original R23 font; add only missing U+2605 in a separate OFL variant."""
from pathlib import Path
import hashlib,json,urllib.request,uuid
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
RES=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources'
BASE=RES/'R23GodotUI.ttf';EXPECTED='ab424b0dfe9aa67c0305a992402750431ea024254047f6e09a99c0b89955e9a1'
assert hashlib.sha256(BASE.read_bytes()).hexdigest()==EXPECTED
URL='https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssymbols2/'
for name in ['NotoSansSymbols2-Regular.ttf','OFL.txt']:
    path=HERE/name
    if not path.exists():
        with urllib.request.urlopen(URL+name,timeout=30)as response:raw=response.read(3*1024*1024)
        assert len(raw)<3*1024*1024;path.write_bytes(raw)
base=TTFont(BASE);fallback=TTFont(HERE/'NotoSansSymbols2-Regular.ttf');assert 0x2605 not in base.getBestCmap()
name=fallback.getBestCmap()[0x2605];ratio=base['head'].unitsPerEm/fallback['head'].unitsPerEm
pen=TTGlyphPen(None);fallback.getGlyphSet()[name].draw(TransformPen(pen,(ratio,0,0,ratio,0,0)))
star='ashfallInventoryBlackStar';order=base.getGlyphOrder()[:];base['glyf'][star]=pen.glyph();base['hmtx'].metrics[star]=tuple(round(x*ratio)for x in fallback['hmtx'].metrics[name]);base.setGlyphOrder(order+[star])
for table in base['cmap'].tables:
    if table.isUnicode()and table.format in (4,12):table.cmap[0x2605]=star
for record in base['name'].names:
    if record.nameID in (1,4,6,16):record.string=('AshfallInventoryUI'if record.nameID==6 else'Ashfall Inventory UI').encode(record.getEncoding())
base.recalcTimestamp=False;out=RES/'R23InventoryUI.ttf';base.save(out)
original=TTFont(BASE);result=TTFont(out)
assert result.getBestCmap()[0x2605]==star
for code,glyph in original.getBestCmap().items():assert result.getBestCmap()[code]==glyph
for glyph in original.getGlyphOrder():
    assert original['hmtx'].metrics[glyph]==result['hmtx'].metrics[glyph]
    assert original['glyf'][glyph].getCoordinates(original['glyf'])==result['glyf'][glyph].getCoordinates(result['glyf'])
meta=Path(str(out)+'.meta')
if not meta.exists():meta.write_text(Path(str(BASE)+'.meta').read_text().replace('ffd14bc32cf24f98b773b7b50e10afa5',uuid.uuid4().hex).replace('Open Sans SemiBold','Ashfall Inventory UI'))
license=RES/'R23InventoryUI-LICENSE.txt';license.write_text((HERE.parent/'SourceFont/LICENSE.OpenSans.txt').read_text()+'\n\nStar fallback: Noto Sans Symbols 2, U+2605 only.\n'+(HERE/'OFL.txt').read_text())
if not Path(str(license)+'.meta').exists():Path(str(license)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
(HERE/'font.json').write_text(json.dumps(dict(originalSha256=EXPECTED,outputSha256=hashlib.sha256(out.read_bytes()).hexdigest(),addedUnicode='U+2605 BLACK STAR',originalGlyphsUnchanged=len(original.getGlyphOrder()),fallbackURL=URL+'NotoSansSymbols2-Regular.ttf',fallbackSha256=hashlib.sha256((HERE/'NotoSansSymbols2-Regular.ttf').read_bytes()).hexdigest(),note='Explicit OFL symbol fallback for Unity TextMesh; original Godot font has no star. Original OS fallback shape is platform-dependent; not claiming identical source system fallback outline.'),indent=2))
print('PASS original1150 glyph outlines/metrics unchanged, separate font has actual filled-star glyph; no import/render run')
