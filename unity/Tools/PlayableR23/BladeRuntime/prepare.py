from pathlib import Path
import hashlib,json,math,shutil
import numpy as np
T=Path(__file__).resolve().parent;R=T.parents[3];F=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');S=F/'source';O=T/'Payload';manifest=F/'SOURCE-MANIFEST.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={r['path']:r['sha256']for r in json.loads(manifest.read_text())['files']};inputs={}
def verify(name):
 p=S/name;h=sha(p);assert h==pins[name],name;inputs[name]=h;return p
for script in ['melee_sword','speed_sword','energy_sword','blade_assets','downloaded_blade_model','model_variants','measurement_calibration','directional_shield','blade_sweep099','playtest_fit_088','weapon_rarity_r6','reference_audio_099','audio_bank']:
 verify('scripts/'+script+'.gd')
verify('assets/materials/field_guard_064.gdshader');verify('assets/audio/reference0992/manifest.json')
def unit(v):return v/np.linalg.norm(v)
def axis_angle(axis,angle):
 x,y,z=unit(axis);c=math.cos(angle);s=math.sin(angle);k=np.array([[0,-z,y],[z,0,-x],[-y,x,0]]);return np.eye(3)*c+(1-c)*np.outer([x,y,z],[x,y,z])+k*s
def from_to(a,b):
 a=unit(a);b=unit(b);cross=np.cross(a,b);c=np.dot(a,b);k=np.array([[0,-cross[2],cross[1]],[cross[2],0,-cross[0]],[-cross[1],cross[0],0]]);return np.eye(3)+k+k@k/(1+c)
FACT=48/41.5;bottom=-.057-.2032*FACT;length=1.2192*FACT;fits={};extras={};rows=[]
for name,variant,catalog,label in [('katana','katana_alt',14,'ASH KATANA'),('excalibur','excalibur',15,'RIDGE EXCALIBUR'),('cyberblade','katana',16,'ARC CYBERBLADE')]:
 folder='assets/models/download_'+variant
 for file in ['model.glb','asset-provenance.json','ATTRIBUTION.txt']:verify(folder+'/'+file)
 data=json.loads((S/folder/'asset-provenance.json').read_text());curve={'katana':.075/.83,'excalibur':0,'cyberblade':.012/.79}[name];square=1+curve*curve;a=.085-bottom;span=(-a+math.sqrt(a*a+square*(length*length-a*a)))/square;tip=np.array([0,.085+span,.025-curve*span]);base=np.array([0,.125,.025]);pommel=np.array(data.get('target_pommel',data['target']));originalTip=np.array(data['contact_tip']);rot=np.eye(3)
 if name in ['katana','cyberblade']:
  rot=axis_angle(originalTip-pommel,0 if name=='katana'else math.pi)
  if name=='cyberblade':rot=from_to(originalTip-pommel,tip-pommel)@rot
 extra=np.eye(4);extra[:3,:3]=rot;extra[:3,3]=pommel-rot@pommel;extras[name]=extra
 fit=dict(version=1,id=name,catalog=catalog,displayName=label,variant=variant,modelScale=1,grip=[0,-.057,-.025],bladeBase=(base*[1,1,-1]).tolist(),bladeTip=(tip*[1,1,-1]).tolist(),renderedHandPhysicalOffset=[0,0,-.0381],supportPoint=[0,-.190,-.025],contactRadius=.026,exitHysteresis=.008,fullDamageHandSpeed=2,damageRange=[25,75],movementUnsupported=1.15 if name=='katana' else 1.25,movementShield=.90 if name=='katana' else 1.25,shieldCentre=[0,.34,.08],shieldRadii=[.48768*FACT,.6096*FACT],sourceBodyTransform=extra.tolist())
 fits[name]=fit;(O/(name+'-fit.json')).write_text(json.dumps(fit,indent=2)+'\n');shutil.copyfile(S/folder/'ATTRIBUTION.txt',O/(name+'-ATTRIBUTION.txt'));shutil.copyfile(verify('assets/ui/catalog094/weapon_'+str(catalog)+'.png'),O/(name+'-icon.png'));rows.append((name,folder))
code=(R/'unity/Tools/PlayableR23/HandsWeapons/prepare.py').read_text().split('OUT.mkdir(parents=True')[0].replace('extra=np.eye(4)','extra=EXTRAS.get(name,np.eye(4)).copy()');ns={'__file__':str(R/'unity/Tools/PlayableR23/HandsWeapons/prepare.py'),'EXTRAS':extras};exec(code,ns);ns['SRC']=S;ns['OUT']=O;ns['record']=lambda p:verify(str(p.relative_to(S)).replace('\\','/'));assets=[]
for name,folder in rows:assets.append(ns['convert'](S/folder/'model.glb',name))
routes=json.loads((S/'assets/audio/reference0992/manifest.json').read_text());audio={}
for key,kind in [('blade-swing','sword_swing'),('katana-shield','katana_shield'),('katana-raise','katana_shield_raise')]:
 original=routes['generic'][kind][0].removeprefix('res://');shutil.copyfile(verify(original),O/(key+'.wav'));audio[key]=original
for family in ['reference099','reference097','reference096']:shutil.copyfile(verify('assets/audio/'+family+'/ATTRIBUTION.txt'),O/('blade-'+family+'-audio-ATTRIBUTION.txt'))
# Source exact profiles and real marker positions; knife backward compatibility remains separate.
def vec(a):return 'new Float3('+','.join(format(float(x),'.10g')+'f'for x in a)+')'
cs='using System;using Ashfall.Core;namespace Ashfall.Integration.Playable.HandsWeapons {public sealed class R24MeleeProfile {public readonly string Id,Name;public readonly int Catalog;public readonly Float3 Base,Tip;public R24MeleeProfile(string id,string name,int catalog,Float3 a,Float3 b){Id=id;Name=name;Catalog=catalog;Base=a;Tip=b;}} public static class R24MeleeProfiles {public static readonly R24MeleeProfile Knife=new R24MeleeProfile("knife","FIELD KNIFE",29,new Float3(0,.025f,-.025f),new Float3(0,.246f,-.025f));'
for name,fit in fits.items():cs+='public static readonly R24MeleeProfile '+name.title()+'=new R24MeleeProfile("'+name+'","'+fit['displayName']+'",'+str(fit['catalog'])+','+vec(fit['bladeBase'])+','+vec(fit['bladeTip'])+');'
cs+='public static R24MeleeProfile ForId(string id){return id=="knife"?Knife:id=="katana"?Katana:id=="excalibur"?Excalibur:id=="cyberblade"?Cyberblade:null;}}}\n';(T/'Staged/R24MeleeProfiles.cs').write_text(cs)
(O/'blade-provenance.json').write_text(json.dumps(dict(manifestSha256=sha(manifest),inputs=inputs,assets=assets,audio=audio,sourceMeasurementFactor=FACT,sourceShieldRadii=[.48768*FACT,.6096*FACT],engine=False,gpu=False,device=False),indent=2)+'\n');print('STAGED full genuine sword models',assets,'source inputs',len(inputs),'outsideAssets')

