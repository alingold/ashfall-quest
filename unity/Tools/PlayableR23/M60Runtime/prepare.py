from pathlib import Path
import hashlib,importlib.util,json,shutil,wave,struct
import numpy as np
T=Path(__file__).resolve().parent;R=T.parents[3];S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');O=T/'Payload';O.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
assert sha(S.parent/'SOURCE-MANIFEST.json')=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256']for r in json.loads((S.parent/'SOURCE-MANIFEST.json').read_text())['files']};inputs={}
def verify(p):
 p=Path(p);rel=p.relative_to(S).as_posix();h=sha(p);assert h==pins[rel],rel;inputs[rel]=h;return p
for name in ['belt_lmg','weapon','weapon_rack','downloaded_m60_087','downloaded_heavy_085','heavy_model_finish','model_scale_084','playtest_fit_088','arsenal_data','weapon_rarity_r6','reference_audio_099','audio_bank','recoil_observation','recoil_endpoint_experiment','reticle_bounce_071','reference_response','grip_hand_086','player','menu','aim_reticle','reticle_geometry']:
 verify(S/'scripts'/f'{name}.gd')
for rel in ['project.godot','assets/data/one_star_050.json','assets/data/one_star0991.json','assets/data/weapon_rarity_r6.json','assets/data/reference_062.json','assets/data/recoil_observed_067.json','assets/data/recoil_endpoint_experiment_071.json','assets/audio/reference0992/manifest.json','assets/models/download_m60/reload-fit.json','assets/ui/catalog094/weapon_8.png']:
 verify(S/rel)
for folder in ['download_m60','download_m60_reload087']:
 for file in ['model.glb','asset-provenance.json','ATTRIBUTION.txt']:verify(S/'assets/models'/folder/file)
# Actual Copy._copy uses raw mesh coordinates. Keep Cover/Bolt/Magazine home
# transforms in the real articulated view, not baked a second time by the rifle helper.
path=R/'unity/Tools/PlayableR23/HandsWeapons/prepare.py';code=path.read_text(encoding='utf-8').split('OUT.mkdir(parents=True')[0]
code=code.replace("if n.get('name')=='Magazine':","if n.get('name')=='Magazine' and not name.startswith('m60'):").replace("elif name!='spr' and n.get('name') in ['Slide','Bolt']:","elif name!='spr' and not name.startswith('m60') and n.get('name') in ['Slide','Bolt']:")
ns={'__file__':str(path)};exec(code,ns);ns.update(SRC=S,OUT=O,record=verify)
assets=[ns['convert'](S/'assets/models/download_m60/model.glb','m60-main'),ns['convert'](S/'assets/models/download_m60_reload087/model.glb','m60-donor',omit_nodes=('Body','Cover','Bolt'))]
assert assets[0]['triangles']==49822 and assets[1]['triangles']==7166
for folder,label in [('download_m60','main'),('download_m60_reload087','donor')]:
 for file,suffix in [('asset-provenance.json','provenance.json'),('ATTRIBUTION.txt','ATTRIBUTION.txt')]:shutil.copyfile(S/'assets/models'/folder/file,O/f'm60-{label}-{suffix}')
fit=json.loads((S/'assets/models/download_m60/reload-fit.json').read_text());(O/'m60-source-fit.json').write_text(json.dumps(fit,indent=2)+'\n')
# Actual reload chevron remains outside the replaced model: three8-sided rods.
spec=importlib.util.spec_from_file_location('m60_primitives',R/'unity/Tools/PlayableR23/MagnumAssets/prepare.py');pr=importlib.util.module_from_spec(spec);spec.loader.exec_module(pr);pr.OUT=O
material=pr.material('e5b646',.48,.3);arrow=pr.Asset('m60-arrow',[material])
for i,(a,b)in enumerate([([0,-.025,0],[0,.025,0]),([0,.025,0],[-.012,.009,0]),([0,.025,0],[.012,.009,0])]):
 a,b=np.array(a,float),np.array(b,float);d=(b-a)/np.linalg.norm(b-a);up=np.array([0,1.,0]);axis=np.cross(up,d);c=float(np.dot(up,d));cross=np.array([[0,-axis[2],axis[1]],[axis[2],0,-axis[0]],[-axis[1],axis[0],0]]);rotation=np.eye(3)if c>1-1e-12 else np.eye(3)+cross+cross@cross/(1+c)
 arrow.add('PhysicalReloadChevronRod'+str(i),pr.cylinder(.0025,float(np.linalg.norm(b-a)),8,1),0,rotation,(a+b)/2,recipe='belt_lmg.gd source PhysicalReloadMotionChevron')
assets.append(arrow.write());shutil.copyfile(S/'assets/ui/catalog094/weapon_8.png',O/'m60-icon.png')
routing=json.loads((S/'assets/audio/reference0992/manifest.json').read_text());cues=[];audio=[]
for action in ['shot','mag_eject','pouch','mag_insert','close','bolt','open','dry']:
 paths=routing['weapons']['m60'].get(action,[])or routing['generic'].get(action,[])
 if not paths:paths=[f'res://assets/audio/sfx/{action}_{i:02d}.wav'for i in range(2)]
 files=[]
 for i,path in enumerate(paths):
  src=verify(S/path.removeprefix('res://'));name=f'm60-source-{action}-{i}.wav';shutil.copyfile(src,O/name);files.append(name)
  with wave.open(str(src),'rb')as w:assert w.getsampwidth()==2;audio.append(dict(file=name,source=path,sha256=sha(src),channels=w.getnchannels(),rate=w.getframerate(),frames=w.getnframes()))
 cues.append(dict(weapon='m60',action=action,files=files))
for rel,name in [('assets/audio/reference097/ATTRIBUTION.txt','reference097-ATTRIBUTION.txt'),('assets/audio/reference099/ATTRIBUTION.txt','reference099-ATTRIBUTION.txt'),('assets/audio/sfx/PROVENANCE.txt','authored-PROVENANCE.txt')]:shutil.copyfile(verify(S/rel),O/('m60-audio-'+name))
(T/'m60-audio-cues.json').write_text(json.dumps(dict(cues=cues),indent=2)+'\n')
# Readonly source observed curves: current R6 selects067 before the older062 fit.
observed=json.loads((S/'assets/data/recoil_observed_067.json').read_text())['profiles']['m60'];lines=['namespace Ashfall.Integration.Playable.HandsWeapons { internal static class R24M60RecoilData {']
for k in ['one','two']:
 flat=[v for row in observed[k]['samples']for v in row];lines.append('internal static readonly float[] '+k.capitalize()+'={'+','.join(format(float(x),'.9g')+'f'for x in flat)+'};')
lines+=['} }'];(T/'Staged/R24M60RecoilData.cs').write_text('\n'.join(lines)+'\n')
(T/'source-bindings.json').write_text(json.dumps(dict(inputs=inputs,geometry=assets,audio=audio,sourceManifestSha256=sha(S.parent/'SOURCE-MANIFEST.json'),payloadBytes=sum(p.stat().st_size for p in O.iterdir()if p.is_file())),indent=2)+'\n')
print('Source M60',[(a['name'],a['triangles'])for a in assets],len(audio),'exact clips; payload bytes',sum(p.stat().st_size for p in O.iterdir()if p.is_file()),'; Tools only.')
