from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.boundsPen import BoundsPen
import re

ROOT=Path(__file__).resolve().parents[5]
font=TTFont(ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R23GodotUI.ttf')
glyphs=font.getGlyphSet(); cmap=font.getBestCmap(); em=font['head'].unitsPerEm
metrics=font['hmtx'].metrics
line=(font['hhea'].ascent-font['hhea'].descent+font['hhea'].lineGap)*96/em
log=(ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/floor-20261105-051246/editor.log').read_text(errors='replace')
ui=(ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/R23VrUI.cs').read_text()
fixture=(ROOT/'unity/Assets/Ashfall/Integration/Playable/Gallery/R23ExpandedGalleryFixture.cs').read_text()
for caption,pixels in [('WEAPONS',22),('CX4',17),('<',46)]:
    bounds=[]
    for c in caption:
        pen=BoundsPen(glyphs);glyphs[cmap[ord(c)]].draw(pen);bounds.append(pen.bounds)
    ink=(max(b[3] for b in bounds)-min(b[1] for b in bounds))*96/em
    width=sum(metrics[cmap[ord(c)]][0] for c in caption)*96/em
    native=re.search(r'UI CAPTION '+re.escape(caption)+r' [^\r\n]*?localBounds=\(([\d.]+),\s*([\d.]+)',log)
    assert native,caption
    assert abs(float(native[1])-width*.1)<.1 and abs(float(native[2])-line*.1)<.01
    nominal=pixels*1.375/800
    corrected_scale=nominal/(96*.1)
    corrected_ink=ink*.1*corrected_scale
    assert abs(corrected_ink/nominal-ink/96)<1e-9
    old_ink=ink*.1*(nominal*.72/(line*.1))
    assert 1.88<corrected_ink/old_ink<1.90
    print(caption, 'source_em_pixels=',pixels,'ink_m=',round(corrected_ink,6),'old_ink_m=',round(old_ink,6))
assert 'label.Height/(.72f*em)' in ui and 'label.MaxWidth/bounds.x' in ui
assert '640,564,17,350' in ui
browse=ui.split('public bool BrowseWeapon(string id)',1)[1].split('// Legacy host request seam',1)[0]
assert 'Page!=R23UiPage.Weapons' in browse and 'options[i].Id==id' in browse and 'weaponChoice=i;Build();return true' in browse
assert 'WeaponSelected' not in browse and 'ui.BrowseWeapon(id)' in fixture and 'ui.BrowseWeapon(previousCaption)' in fixture
assert 'FrontButton("SELECT WEAPON"' not in ui and 'RequestWeaponSelection(string id)' in ui
print('PASS actual retained font/native line-box comparison, exact source em scaling, source selection label, browse-only capture/restoration contract. No new engine/render claim.')
