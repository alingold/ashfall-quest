from pathlib import Path
import hashlib,json,shutil,wave,struct,importlib.util,math
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');O=HERE/'Payload';O.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
assert sha(S.parent/'SOURCE-MANIFEST.json')=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256']for r in json.loads((S.parent/'SOURCE-MANIFEST.json').read_text())['files']};inputs={}
def verify(path):
 path=Path(path);rel=path.relative_to(S).as_posix();h=sha(path);assert pins[rel]==h,rel;inputs[rel]=h;return path
for name in ['precision_rifle','weapon','weapon_rack','awp_scope','longwatch_scope','downloaded_awp_model','downloaded_firearm_model','model_scale_084','playtest_fit_088','arsenal_data','weapon_rarity_r6','reference_audio_099','audio_bank','reticle_bounce_071','reference_response','grip_hand_086','player','menu']:
 verify(S/'scripts'/f'{name}.gd')
for rel in ['project.godot','assets/data/one_star_050.json','assets/data/one_star0991.json','assets/data/weapon_rarity_r6.json','assets/data/reference_062.json','assets/data/recoil_observed_067.json','assets/data/recoil_endpoint_experiment_071.json','assets/audio/reference0992/manifest.json','assets/models/download_awp/model.glb','assets/models/download_awp/asset-provenance.json','assets/models/download_awp/ATTRIBUTION.txt','assets/ui/catalog094/weapon_5.png']:verify(S/rel)
code=(ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py').read_text(encoding='utf-8').split('OUT.mkdir(parents=True')[0];ns={'__file__':str(ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py')};exec(code,ns);ns.update(SRC=S,OUT=O,record=verify)
asset=ns['convert'](S/'assets/models/download_awp/model.glb','awp');assert asset['triangles']==21916
# Exact retained weapon.gd six-sided/one-ring three-rod functional arrows.
spec=importlib.util.spec_from_file_location('awp_source_primitives',ROOT/'unity/Tools/PlayableR23/MagnumAssets/prepare.py');pr=importlib.util.module_from_spec(spec);spec.loader.exec_module(pr);pr.OUT=O
cyan=pr.material('ffffff',.2,.4);cyan['color']=[.05,.72,.75,1]
arrows=[]
for name,segments in [('awp-insert-arrow',[([0,0,0],[0,.09,0]),([0,.09,0],[-.012,.066,0]),([0,.09,0],[.012,.066,0])]),('awp-charge-arrow',[([0,0,0],[0,0,.105]),([0,0,.105],[-.014,0,.079]),([0,0,.105],[.014,0,.079])])]:
 a=pr.Asset(name,[cyan])
 for i,(start,end)in enumerate(segments):
  start,end=np.array(start,float),np.array(end,float);direction=(end-start)/np.linalg.norm(end-start);up=np.array([0,1,0.]);axis=np.cross(up,direction);c=float(np.dot(up,direction));cross=np.array([[0,-axis[2],axis[1]],[axis[2],0,-axis[0]],[-axis[1],axis[0],0]]);rotation=np.eye(3)if c>1-1e-12 else np.eye(3)+cross+cross@cross/(1+c)
  a.add('SourceRod'+str(i),pr.cylinder(.0018,float(np.linalg.norm(end-start)),6,1),0,rotation,(start+end)/2,recipe='weapon.gd exact functional arrow .0018radius/6radial/1ring')
 arrows.append(a.write())
# DownloadedAWP intentionally retains precision_rifle's brass chamber window.
def box(size):
 ext=np.array(size)*.5;vertices=[];normals=[];uv=[];indices=[]
 for axis,sign in [(0,-1),(0,1),(1,-1),(1,1),(2,-1),(2,1)]:
  other=[a for a in range(3)if a!=axis];start=len(vertices);normal=np.zeros(3);normal[axis]=sign
  for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]:
   point=np.zeros(3);point[axis]=sign*ext[axis];point[other[0]]=x*ext[other[0]];point[other[1]]=y*ext[other[1]];vertices.append(point);normals.append(normal);uv.append([(x+1)/2,(y+1)/2])
  tri=np.array([0,1,2,0,2,3])+start
  if np.dot(np.cross(vertices[start+1]-vertices[start],vertices[start+2]-vertices[start]),normal)>0:tri=tri.reshape(-1,3)[:,[0,2,1]].ravel()
  indices.extend(tri)
 return np.array(vertices),np.array(normals),np.array(uv),np.array(indices)
brass=pr.material('a88c54',.8,.35);chamber=pr.Asset('awp-chamber',[brass]);chamber.add('SourceRetainedBrassChamber',box((.006,.023,.105)),0,at=(.022,.05,-.12),recipe='precision_rifle.gd retained chamber_window; downloaded_awp_model.gd moves only x=.022');retained=chamber.write()
provenance=json.loads((S/'assets/models/download_awp/asset-provenance.json').read_text());home=provenance['magazine_center'];home=[home[0],home[1],-home[2]]
fit=dict(id='awp',catalogIndex=5,modelScale=1,reloadScale=1,weaponGripOrigin=[0,0,0],muzzle=[0,.047,1.01],muzzleCalibrationPitchUnityDegrees=2.5,magazineHome=home,chargingRest=[.072,.045,.07],chargingGrip=[.072,.045,.07],chargingPull=.115,chargingMaximum=.115,offer=[0,-.1764,home[2]],insert=[0,-.18,home[2]],offerRadius=.25,insertRadius=.25,boltEllipsoid=[.24,.20,.30,.22],boltBound=.42,insertionTravel=.06,ammoFamily='sniper',reserve=2400,capacity=3,interval=.95,maximumRange=180,primaryGrip=[0,-.057,-.025],renderedHandOffsetPhysical=[0,0,-.0381],scope=dict(resolution=[768,768],hz=30,magnifications=[4,8],viewDistance=.45,viewRadius=.15,angularScale=1/3,sourceSharedBinocularImage=True),sourceTriangles=21916,sourceBodyTriangles=20822,sourceMagazineTriangles=680,sourceBoltTriangles=414)
(O/'awp-fit.json').write_text(json.dumps(fit,indent=2)+'\n');shutil.copyfile(S/'assets/models/download_awp/asset-provenance.json',O/'awp-provenance.json');shutil.copyfile(S/'assets/models/download_awp/ATTRIBUTION.txt',O/'awp-ATTRIBUTION.txt');shutil.copyfile(S/'assets/ui/catalog094/weapon_5.png',O/'awp-icon.png')
routing=json.loads((S/'assets/audio/reference0992/manifest.json').read_text());cues=[];audio=[]
for action in ['shot','bolt','mag_eject','pouch','mag_insert','dry']:
 # Actual reference_audio_099.gd explicitly routes AWP bolt to GENERIC S85, even
 # though the weapons.awp object contains a separate unused recording.
 paths=routing['generic'].get('bolt',[]) if action=='bolt'else routing['weapons']['awp'].get(action,[])
 if not paths and action in ['mag_eject','pouch','mag_insert']:paths=routing['generic'].get(action,[])
 if not paths:paths=routing['generic'].get(action,[])
 if not paths:paths=[f'res://assets/audio/sfx/{action}_{i:02d}.wav'for i in range(2)]
 files=[]
 for i,path in enumerate(paths):
  source=verify(S/path.removeprefix('res://'));name=f'awp-source-{action}-{i}.wav';shutil.copyfile(source,O/name);files.append(name)
  with wave.open(str(source),'rb')as w:assert w.getsampwidth()==2;audio.append(dict(file=name,sha256=sha(source),channels=w.getnchannels(),rate=w.getframerate(),frames=w.getnframes()))
 cues.append(dict(weapon='awp',action=action,files=files))
for rel,label in [('assets/audio/reference099/ATTRIBUTION.txt','reference099-ATTRIBUTION.txt'),('assets/audio/sfx/PROVENANCE.txt','authored-sfx-PROVENANCE.txt')]:
 source=verify(S/rel);shutil.copyfile(source,O/('awp-audio-'+label))
(HERE/'awp-audio-cues.json').write_text(json.dumps(dict(cues=cues),indent=2)+'\n');(HERE/'source-bindings.json').write_text(json.dumps(dict(inputs=inputs,geometry=asset,arrows=arrows,retained=retained,audio=audio,sourceManifestSha256=sha(S.parent/'SOURCE-MANIFEST.json'),newPayloadBytes=sum(p.stat().st_size for p in O.iterdir()if p.is_file())),indent=2)+'\n')
print('Source-bound AWP:',asset,'clips',len(audio),'payload bytes',sum(p.stat().st_size for p in O.iterdir()if p.is_file()),'; Tools only; no engine.')
