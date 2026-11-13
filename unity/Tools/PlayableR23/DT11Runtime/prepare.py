from pathlib import Path
import base64,hashlib,importlib.util,io,json,math,shutil,struct,wave
import numpy as np
T=Path(__file__).resolve().parent;R=T.parents[3];S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');O=T/'Payload';O.mkdir(exist_ok=True);F=S.parent/'SOURCE-MANIFEST.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(F)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={v['path']:v['sha256']for v in json.loads(F.read_text())['files']};inputs={}
def verify(p):
 q=S/p;h=sha(q);assert h==pins[p],p;inputs[p]=h;return q
for name in ['dt11','shotgun','break_action_state','closure_flick','weapon','player','solid_import_085','common_reflex_090','downloaded_dt11_model','model_variants','model_scale_084','heavy_model_finish','playtest_fit_088','arsenal_data','one_star0991','weapon_rarity_r6','reference_audio_099','audio_bank','reticle_bounce_071']:
 verify('scripts/'+name+'.gd')
for path in ['project.godot','assets/data/one_star_050.json','assets/data/one_star0991.json','assets/data/weapon_rarity_r6.json','assets/data/reference_062.json','assets/audio/reference0992/manifest.json','assets/models/download_dt11/model.glb','assets/models/download_dt11/asset-provenance.json','assets/models/download_dt11/ATTRIBUTION.txt','assets/ui/catalog094/weapon_1.png']:verify(path)
code=(R/'unity/Tools/PlayableR23/HandsWeapons/prepare.py').read_text().split('OUT.mkdir(parents=True')[0];ns={'__file__':str(R/'unity/Tools/PlayableR23/HandsWeapons/prepare.py')};exec(code,ns);ns['SRC']=S;ns['OUT']=O;ns['record']=lambda p:verify(str(p.relative_to(S)).replace('\\','/'))
assets=[ns['convert'](S/'assets/models/download_dt11/model.glb','dt11',omit_nodes=('Barrels',)),ns['convert'](S/'assets/models/download_dt11/model.glb','dt11-barrels',omit_nodes=('Body',))]
# Compact only unused material records/textures, preserving every used source surface.
for name in ['dt11','dt11-barrels']:
 p=O/(name+'.meshbin');raw=bytearray(p.read_bytes());count=struct.unpack_from('<i',raw,4)[0];at=8;locations=[];used=[]
 for i in range(count):
  nv,ni,mat=struct.unpack_from('<iii',raw,at);locations.append((at+8,mat));at+=12+nv*32+ni*4
  if mat not in used:used.append(mat)
 meta=json.loads((O/(name+'.json')).read_text());meta['materials']=[meta['materials'][i]for i in used]
 for material in meta['materials']:material['alphaMode']='OPAQUE';material['color'][3]=1.0
 for at,mat in locations:struct.pack_into('<i',raw,at,used.index(mat))
 p.write_bytes(raw);(O/(name+'.json')).write_text(json.dumps(meta))
needed=set()
for p in O.glob('dt11*.json'):
 if p.name.endswith('-fit.json'):continue
 try:
  j=json.loads(p.read_text())
  for m in j.get('materials',[]):needed.update(m[k]for k in ['baseColor','normal','metallicMap','occlusion']if m.get(k))
 except:pass
for p in O.glob('dt11-*.png'):
 if p.name not in needed:p.unlink() # Own newly generated, resolved task payload only.
spec=importlib.util.spec_from_file_location('dt11_source_primitives',R/'unity/Tools/PlayableR23/MagnumAssets/prepare.py');pr=importlib.util.module_from_spec(spec);spec.loader.exec_module(pr);pr.OUT=O
factor=.4269203091911859;rx=pr.rx;material=pr.material;steel=material('ffffff',.75,.42);steel['color']=[.32,.34,.31,1];copper=material('ffffff',.7,1);copper['color']=[.45,.24,.10,1];charcoal=material('ffffff',.65,.44);charcoal['color']=[.06,.075,.085,1];cyan=material('ffffff',0,1);cyan['color']=[.1,.8,.8,1]
# Exact visible functional chambers retained by downloaded_dt11_model. All relative hinge.
ports=pr.Asset('dt11-chambers',[steel,copper,charcoal])
for i,x in enumerate([-.027*factor,.027*factor]):
 ports.add('OpenChamberMouth'+str(i),pr.torus(.017*factor,.025*factor,16,8),0,rx(math.pi/2),(x,.175,.014),recipe='shotgun.gd retained mouth, fitted default factor and+.02y')
 ports.add('LiveShellCap'+str(i),pr.cylinder(.016*factor,.006,12),1,rx(math.pi/2),(x,.175,.017),recipe='source live chamber cap source radiusfit only')
 ports.add('SpentShellCap'+str(i),pr.cylinder(.015*factor,.007,12),2,rx(math.pi/2),(x,.175,.0175),recipe='source spent cap distinct visual')
assets.append(ports.write());pair=pr.Asset('dt11-pair',[copper,cyan])
for i,x in enumerate([-.024*factor,.024*factor]):
 pair.add('Shell'+str(i)+'.Hull',pr.cylinder(.016*factor,.07,12),0,at=(x,0,0),recipe='source shell_offer/shell_pair hull12radial/rings1')
 pair.add('Shell'+str(i)+'.Cap',pr.cylinder(.018*factor,.01,12),1,at=(x,.035,0),recipe='source pair source cap, x/radius fit only')
assets.append(pair.write());ejected=pr.Asset('dt11-ejected',[copper]);ejected.add('SpentCasing',pr.cylinder(.016*factor,.07,10,4),0,recipe='source8pool projectile cylinder10radial/default4rings');assets.append(ejected.write())
# Source swipe arrow three box faces/36 actual triangles.
def box(size):
 x,y,z=np.array(size)*.5;v=[];n=[];uv=[];idx=[]
 for axis,sign in [(0,-1),(0,1),(1,-1),(1,1),(2,-1),(2,1)]:
  others=[a for a in range(3)if a!=axis];first=len(v)
  for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]:
   p=np.zeros(3);p[axis]=sign*[x,y,z][axis];p[others[0]]=a*[x,y,z][others[0]];p[others[1]]=b*[x,y,z][others[1]];v.append(p);norm=np.zeros(3);norm[axis]=sign;n.append(norm);uv.append([(a+1)/2,(b+1)/2])
  tri=np.array([0,1,2,0,2,3])+first
  if np.dot(np.cross(np.array(v[first+1])-v[first],np.array(v[first+2])-v[first]),norm)>0:tri=tri.reshape(-1,3)[:,[0,2,1]].ravel()
  idx.extend(tri)
 return np.array(v),np.array(n),np.array(uv),np.array(idx)
arrowMaterial=material('ffffff',.15,.5);arrowMaterial['color']=[1,.72,.16,1];arrow=pr.Asset('dt11-arrow',[arrowMaterial]);arrow.add('UpCloseShaft',box((.009,.09,.006)),0,at=(0,.045,0),recipe='dt11.gd source UpCloseOnly shaft')
for side in [-1,1]:arrow.add('UpCloseArm'+str(side),box((.009,.035,.006)),0,pr.rz(side*.65),(side*.01,.077,0),recipe='source original three-box UP-only arrow')
assets.append(arrow.write())
shutil.copyfile(S/'assets/models/download_dt11/ATTRIBUTION.txt',O/'dt11-ATTRIBUTION.txt');shutil.copyfile(S/'assets/ui/catalog094/weapon_1.png',O/'dt11-icon.png')
# Source shotgun._bake_cue is the actual DT11 shot AND mechanism route.
# Keep recordings separate; do not substitute them for this source contract.
spec=importlib.util.spec_from_file_location('dt11_source_cues',T/'source_cues.py');source_cues=importlib.util.module_from_spec(spec);spec.loader.exec_module(source_cues);source_cues.verify_source(S)
cues=[];audio=[];h=source_cues.seed_for('blast')
for action,kind in [('shot','blast'),('mag_eject','open'),('mag_insert','insert'),('bolt','close')]:
 filename='dt11-authored-'+kind+'.wav';record=source_cues.write_cue(O/filename,kind);cues.append(dict(weapon='dt11',action=action,files=[filename]));audio.append(dict(record,source='scripts/shotgun.gd::_bake_cue',sourceSha256=inputs['scripts/shotgun.gd']))
for family in ['reference099']:
 shutil.copyfile(verify('assets/audio/'+family+'/ATTRIBUTION.txt'),O/('dt11-'+family+'-ATTRIBUTION.txt'))
(O/'dt11-audio-cues.json').write_text(json.dumps(dict(cues=cues),indent=2))
fit=dict(id='dt11',catalog=1,displayName='DT11',modelScale=1.6,reloadScale=1.6,loadNames=['dt11','dt11-barrels','dt11-chambers','dt11-pair','dt11-ejected','dt11-arrow'],grip=[0,-.057,-.025],renderedHandPhysicalOffset=[0,.127*math.cos(math.radians(35)),.127*math.sin(math.radians(35))-.0127],renderedHandPitchDegreesUnity=35,muzzle=[0,.04,.65],hinge=[0,-.135,.13],barrelsVerticesRelativeHinge=True,chamberMarkerRelativeHinge=[0,.175,-.017],foreEndRelativeHinge=[0,.09,.27],shellOfferVerticalOffset=.11-.0127/1.6,closeGrabBackOffset=-.06,hingeOpenRadians=.72,physicalDownStroke=.045*.8,physicalUpStroke=.05*.8,sourceDefaultModel='download_dt11',sourceOptionalAlternate='download_dt11_alt (not selected by default)',sourceVisibleBodyTriangles=5846,sourceOmittedTriangles=242,sourceOmission='downloaded model source omits fore-end; runtime hides retained authored fore-end085; no invented replacement',capacity=2,roundsPerSecond=5,pellets=8,pelletDamage=12)
(O/'dt11-fit.json').write_text(json.dumps(fit,indent=2));(O/'dt11-provenance.json').write_text(json.dumps(dict(manifestSha256=sha(F),inputs=inputs,assets=assets,audio=audio,blast=dict(kind='source procedural shotgun._bake_cue',source='scripts/shotgun.gd',seed=h,samples=20800,rate=32000,rng='Godot4.6 PCG32 source algorithm',primaryReferences=['https://github.com/godotengine/godot/blob/4.6.2-stable/core/math/random_pcg.h','https://github.com/godotengine/godot/blob/4.6.2-stable/thirdparty/misc/pcg.cpp'],limits='Recipe port, CPU deterministic verification only; no native Godot PCM byte-comparison. Reference0992 DT11 recorded shot exists but actual shotgun._mechanisms.blast bypasses it.'),engine=False,gpu=False,device=False),indent=2));print('STAGED genuine default DT11 full5846tris +source moving chambers/pair/ejection/arrow, actual authored blast formula and routed reloadWAVs; no Assets')
