"""Decode the exact Godot 4.7.2 default UI font for Unity; no engine operation."""
from pathlib import Path
import hashlib,json,sys
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
sys.path.insert(0,str(HERE/'deps'))
from fontTools.ttLib import TTFont,woff2

source=HERE/'OpenSans_SemiBold.woff2'
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
assert sha(source)=='55809808749613ed9ef9ad4f61424a14d2aa265d74369ad43403b9761aa6bac4'
output=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R23GodotUI.ttf'
woff2.decompress(source,output)
a,b=TTFont(source),TTFont(output)
assert a.getBestCmap()==b.getBestCmap() and a['hmtx'].metrics==b['hmtx'].metrics and a.getGlyphOrder()==b.getGlyphOrder()
for name in a.getGlyphOrder():
    x,y=a['glyf'][name],b['glyf'][name]
    assert x.numberOfContours==y.numberOfContours
    if x.numberOfContours>=0:assert x.getCoordinates(a['glyf'])==y.getCoordinates(b['glyf'])
    else:assert [c.getComponentInfo() for c in x.components]==[c.getComponentInfo() for c in y.components]
assert sha(output)=='ab424b0dfe9aa67c0305a992402750431ea024254047f6e09a99c0b89955e9a1'
output.with_name('R23GodotUI-LICENSE.txt').write_bytes((HERE/'LICENSE.OpenSans.txt').read_bytes())
print('Godot default font decoded: all1150 glyph outlines, advances and cmap preserved. Unity import/render unrun.')
