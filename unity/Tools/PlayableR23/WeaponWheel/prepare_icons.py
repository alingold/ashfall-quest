from pathlib import Path
import base64,hashlib,json
from backpack_icons import rasterize
ROOT=Path(__file__).resolve().parents[4];SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUT=ROOT/'unity/Assets/Ashfall/Integration/Playable/Input/R23WeaponWheelIcons.cs'
rows={};lines=['#if UNITY_6000_0_OR_NEWER','using System;','namespace Ashfall.Integration.Playable.Input { internal static class R23WeaponWheelIcons { internal static byte[] Read(string id) { switch(id) {']
for key,index in [('mk18',3),('akm',21),('spr',13),('magnum',7),('cx4',0),('px4',2),('m1014',9),('ump',6),('p90',10),('mp5',17),('mp9',18),('1911',19),('tec9',20),('rfb',23),('fal',24),('s85',25),('frag',26),('zone',28)]:
    path=next(SRC/f'assets/ui/{folder}/weapon_{index}.png' for folder in ['catalog0991','catalog094'] if(SRC/f'assets/ui/{folder}/weapon_{index}.png').exists());raw=path.read_bytes()
    rows[key]={'source':str(path.relative_to(SRC)),'sourceCatalogIndex':index,'label':'SILENCED MP9' if key=='mp9' else 'ZONE GRENADE' if key=='zone' else key.upper(),'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw)}
    lines.append('case "'+key+'": return Convert.FromBase64String("'+base64.b64encode(raw).decode()+'");')
for key,name in [(x,'item_'+x)for x in ['banana','soda','shaker']]+[('supply/'+x,'backpack_r23_'+x)for x in ['pistol','rifle','shells','sniper','smg','builds']]:
    path=(Path(__file__).parent/'UnityIcons'/(name+'.svg'))if key=='shaker'else SRC/'assets/ui'/(name+'.svg');raw=rasterize(name,path.parent)
    rows[key]={'source':str(path.relative_to(ROOT if key=='shaker'else SRC)),'sourceOwner':'Unity requested shaker update'if key=='shaker'else 'Godot reference','sourceSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw),'conversion':'existing CPU quadratic path parser24 samples, Pillow4x antialias, rect/ellipse retained'}
    lines.append('case "'+key+'": return Convert.FromBase64String("'+base64.b64encode(raw).decode()+'");')
lines+=['default:return null; } } } }','#endif'];OUT.write_text('\n'.join(lines));Path(__file__).with_name('icons.json').write_text(json.dumps(rows,indent=2))
