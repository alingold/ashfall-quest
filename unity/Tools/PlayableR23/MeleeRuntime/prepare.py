from pathlib import Path
import hashlib,json,shutil
T=Path(__file__).resolve().parent;R=T.parents[3];F=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');S=F/'source';O=T/'Payload';manifest=F/'SOURCE-MANIFEST.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={row['path']:row['sha256']for row in json.loads(manifest.read_text())['files']};inputs={}
def verify(name):
 p=S/name;h=sha(p);assert h==pins[name],name;inputs[name]=h;return p
for name in ['short_knife','melee_sword','blade_assets','downloaded_blade_model','playtest_fit_088','model_variants','weapon_rarity_r6','reference_audio_099','audio_bank']:
 verify('scripts/'+name+'.gd')
for name in ['assets/models/download_knife/model.glb','assets/models/download_knife/asset-provenance.json','assets/models/download_knife/ATTRIBUTION.txt','assets/ui/catalog094/weapon_29.png','assets/audio/reference0992/manifest.json','assets/audio/reference099/ATTRIBUTION.txt']:
 verify(name)
code=(R/'unity/Tools/PlayableR23/HandsWeapons/prepare.py').read_text().split('OUT.mkdir(parents=True')[0];ns={'__file__':str(R/'unity/Tools/PlayableR23/HandsWeapons/prepare.py')};exec(code,ns);ns['SRC']=S;ns['OUT']=O;ns['record']=lambda p:verify(str(p.relative_to(S)).replace('\\','/'));mesh=ns['convert'](S/'assets/models/download_knife/model.glb','knife');assert mesh['triangles']==1142
routing=json.loads((S/'assets/audio/reference0992/manifest.json').read_text());sound=routing['generic']['sword_swing'][0].removeprefix('res://');shutil.copyfile(verify(sound),O/'knife-swing.wav');shutil.copyfile(S/'assets/models/download_knife/ATTRIBUTION.txt',O/'knife-ATTRIBUTION.txt');shutil.copyfile(S/'assets/audio/reference099/ATTRIBUTION.txt',O/'knife-audio-ATTRIBUTION.txt');shutil.copyfile(S/'assets/ui/catalog094/weapon_29.png',O/'knife-icon.png')
fit=dict(version=1,id='knife',catalog=29,displayName='FIELD KNIFE',modelScale=1,grip=[0,-.057,-.025],bladeBase=[0,.025,-.025],bladeTip=[0,.246,-.025],renderedHandPhysicalOffset=[0,0,-.0508],sourceBladeRadius=.026,exitHysteresis=.008,flatDamage=35,minimumBladeSpeed=.2,teleportTravelLimit=1,maximumStepSeconds=.15,movementMultiplier=1.15,sourceWhoosh=dict(minimumSpeed=1.4,minimumTravel=.12,rearmMilliseconds=180,source=sound,gainDb=-13))
(O/'knife-fit.json').write_text(json.dumps(fit,indent=2)+'\n');(O/'knife-provenance.json').write_text(json.dumps(dict(manifestSha256=sha(manifest),sourceRoot=str(S),inputs=inputs,mesh=mesh,sourceWhoosh=sound,outputs={p.name:sha(p)for p in O.iterdir()if p.is_file()and p.name!='knife-provenance.json'},engine=False,device=False,gpu=False),indent=2)+'\n');print('STAGED whole source knife',mesh,'verified inputs',len(inputs),'outsideAssets',O)
