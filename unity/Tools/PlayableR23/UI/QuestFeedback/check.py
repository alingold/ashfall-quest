from pathlib import Path
import base64,hashlib,io,json,re
from fontTools.ttLib import TTFont
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];ASSETS=ROOT/'unity/Assets/Ashfall/Integration/Playable';RES=ASSETS/'UI/Resources'
font=json.loads((HERE/'font.json').read_text());original=TTFont(RES/'R23GodotUI.ttf');variant=TTFont(RES/'R23InventoryUI.ttf')
assert hashlib.sha256((RES/'R23GodotUI.ttf').read_bytes()).hexdigest()==font['originalSha256']
assert hashlib.sha256((RES/'R23InventoryUI.ttf').read_bytes()).hexdigest()==font['outputSha256']
assert 0x2605 not in original.getBestCmap() and 0x2605 in variant.getBestCmap()
for glyph in original.getGlyphOrder():
 assert original['hmtx'].metrics[glyph]==variant['hmtx'].metrics[glyph]
 assert original['glyf'][glyph].getCoordinates(original['glyf'])==variant['glyf'][glyph].getCoordinates(variant['glyf'])
ui=(ASSETS/'UI/R23VrUI.cs').read_text();wheel=(ASSETS/'Input/R23WeaponWheel.cs').read_text();icons=(ASSETS/'Input/R23WeaponWheelIcons.cs').read_text()
assert 'MenuCard("WEAPONS",-.09f,"settings_2",OpenWeaponSettings)' in ui
assert 'FLING POWER' in ui and 'Settings.FlingPower;},0,5,.05f' in ui
assert 'Settings.MotionDamping;},0,2,.01f' in ui and 'Settings.OffhandInfluence;},0,1,.01f' in ui
backpack=ui.split('void BuildLoadout()',1)[1].split('void BuildAppearance()',1)[0]
assert 'CATALOG' not in backpack and 'int[] inventoryCells={0,1,4,5,8,9}' in backpack and 'resourceCells={2,3,6,7,10,11}' in backpack
assert 'option.ProfileId' in backpack and 'option.Stars' in backpack and 'CloseBackpack();if(EquipRequested' in ui
assert 'FrontButton("OUTFITS",970,712' in ui
assert wheel.count('=Label(')==1 and 'Label(row.Label' not in wheel and 'row.Detail' not in wheel
assert '.03024f' in wheel and 'R23InventoryUI' in wheel and 'R23InventoryUI' in ui
embedded=dict(re.findall(r'case "(.*?)": return Convert.FromBase64String\("(.*?)"\)',icons))
for key in ['mk18','px4','m1014','frag','zone','banana','soda','shaker','supply/pistol','supply/rifle','supply/shells','supply/sniper','supply/smg','supply/builds']:
 im=Image.open(io.BytesIO(base64.b64decode(embedded[key]))).convert('RGBA');assert im.getbbox() and im.getextrema()[3][1]>0
print('PASS retained1150 source glyphs unchanged +explicit star, source settings/navigation/12tile backpack/action-profile contract, top-only wheel text,14real visible picture payloads; no engine/render/device run')
