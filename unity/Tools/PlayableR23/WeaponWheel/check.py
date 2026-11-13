from pathlib import Path
import base64,hashlib,json,re,subprocess
from backpack_icons import rasterize
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent;SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
CODE=ROOT/'unity/Assets/Ashfall/Integration/Playable/Input';CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
def compile(name,files,extra):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(TOOLS/name)]+extra+[str(p) for p in files];rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/(name+'.log')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
compile('WheelCases.exe',[CODE/'R23WeaponWheelState.cs',TOOLS/'Cases.cs'],['/target:exe']);r=subprocess.run([str(TOOLS/'WheelCases.exe')],capture_output=True,text=True);(TOOLS/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
icons=json.loads((TOOLS/'icons.json').read_text());text=(CODE/'R23WeaponWheelIcons.cs').read_text();embedded=dict(re.findall(r'case "(.*?)": return Convert.FromBase64String\("(.*?)"\)',text))
for key,row in icons.items():
 raw=base64.b64decode(embedded[key]);source=SRC/row['source'];assert hashlib.sha256(raw).hexdigest()==row['sha256']
 if source.suffix=='.svg':assert raw==rasterize(source.stem) and hashlib.sha256(source.read_bytes()).hexdigest()==row['sourceSha256']
 else:assert raw==source.read_bytes()
wheel=(SRC/'scripts/weapon_wheel.gd').read_text();rack=(SRC/'scripts/weapon_rack.gd').read_text();assert 'const ITEM_SLOTS := [2,1,0,7,6,5]' in wheel and 'const QUICK_A_TAP_SECONDS := 0.10' in rack and 'wheel.point(ray)' in rack and 'backpack_requested.emit()' in rack
(TOOLS/'source-inputs.json').write_text(json.dumps({name:hashlib.sha256((SRC/name).read_bytes()).hexdigest() for name in ['scripts/weapon_wheel.gd','scripts/weapon_rack.gd','scripts/menu.gd','scripts/weapon_icons_094.gd','scripts/upright_ui.gd']},indent=2))
EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((EDITOR/'Managed/UnityEngine').glob('*.dll'))+list((EDITOR/'NetStandard/ref/2.1.0').glob('*.dll'))
assert set(icons)=={'mk18','akm','spr','magnum','cx4','px4','m1014','ump','p90','mp5','mp9','1911','tec9','rfb','fal','s85','frag','zone','banana','soda','shaker'}|{'supply/'+x for x in ['pistol','rifle','shells','sniper','smg','builds']}
for key,index in [('cx4',0),('px4',2),('m1014',9),('ump',6),('p90',10),('mp5',17),('mp9',18),('1911',19),('tec9',20),('rfb',23),('fal',24),('s85',25),('frag',26),('zone',28)]:assert icons[key]['sourceCatalogIndex']==index and icons[key]['label']==('SILENCED MP9' if key=='mp9' else 'ZONE GRENADE'if key=='zone'else key.upper())
compile('Wheel.Native.dll',sorted(CODE.glob('R23WeaponWheel*.cs')),['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER,UNITY_EDITOR']+['/reference:'+str(p) for p in refs]);print('PASS eighteen exact source PNGs +nine original SVG CPU rasterizations; installed Unity API + Editor fixture compile, no engine/GPU/device run')
