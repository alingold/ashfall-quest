from pathlib import Path
import hashlib,json
from fontTools.ttLib import TTFont
ROOT=Path(__file__).resolve().parents[5];HERE=Path(__file__).resolve().parent
SRC=Path(r'D:/ashfall-r6-staging/frozen-source-r6-v001/source');mf=SRC.parent/'SOURCE-MANIFEST.json'
assert hashlib.sha256(mf.read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
rows={x['path']:x for x in json.loads(mf.read_text())['files']}
for name in ['scripts/match_hud_r3.gd','scripts/net_bridge1000.gd']:
 assert hashlib.sha256((SRC/name).read_bytes()).hexdigest()==rows[name]['sha256']
source=(SRC/'scripts/match_hud_r3.gd').read_text()
assert 'quad(Vector2(.34,.013),Vector3(-.02,-.333,-.803),Color("17212a"))' in source
assert 'label("",Vector3(-.02,-.333,-.798),.0095)' in source
assert 'view.font_size=32;view.pixel_size=height/32' in source
assert 'actor.input_enabled=not game.front_menu.session_overlay and p.life in ["alive","downed","eliminated"]' in (SRC/'scripts/net_bridge1000.gd').read_text()
base=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources'
a,b=TTFont(base/'R23GodotUI.ttf'),TTFont(base/'R23InventoryUI.ttf')
chars=set('YOU ARE A GHOST · WAITING FOR REVIVEYOU DIED · RESPAWN 0123456789.sELIMINATED · WAITING FOR ROUND')
for char in chars:
 ga,gb=a.getBestCmap()[ord(char)],b.getBestCmap()[ord(char)]
 assert a['hmtx'].metrics[ga]==b['hmtx'].metrics[gb]
 assert a['glyf'][ga].getCoordinates(a['glyf'])==b['glyf'][gb].getCoordinates(b['glyf'])
result={'sourceContent':'23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520','noticeGlyphs':len(chars),'sourceGeometryAndMotionVerified':True,'engineExecuted':False,'sourceFiles':{name:rows[name]['sha256'] for name in ['scripts/match_hud_r3.gd','scripts/net_bridge1000.gd']}}
(HERE/'checks.json').write_text(json.dumps(result,indent=2)+'\n');print('PASS frozen source compact HUD geometry/ghost motion +',len(chars),'actual notice glyph outlines/advances')
