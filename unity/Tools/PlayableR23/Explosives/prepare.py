from pathlib import Path
import hashlib,json,math,shutil,struct,uuid,wave,io
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent;SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Explosives'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 OUT.mkdir(parents=True,exist_ok=True);path=ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py';ns={'__file__':str(path)};exec(compile(path.read_text().split('OUT.mkdir(parents=True')[0],'existing_full_gltf','exec'),ns);ns['SRC']=SRC;ns['OUT']=OUT
 pinned={p['path']:p['sha256']for p in json.loads((ROOT/'docs/agent-runs/20261104-three-chats/world/r24-integration/R23-BASELINE.json').read_text())['actualFiles']};geometry=[]
 for kind,folder,scale in [('frag','download_frag_alt',.9),('zone','download_frag',2.0)]:
  src=SRC/'assets/models'/folder/'model.glb';assert sha(src)==pinned[str(src.relative_to(SRC)).replace('\\','/')];row=ns['convert'](src,kind);row.update(source=str(src.relative_to(SRC)),sha256=sha(src),heldScale=scale,flightScale=scale*.035/.047);geometry.append(row)
  raw=src.read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);blob=raw[28+n:];manifest=json.loads((OUT/(kind+'.json')).read_text())
  def image(index):
   im=g['images'][g['textures'][index]['source']];b=g['bufferViews'][im['bufferView']];return blob[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']]
  for i,m in enumerate(g['materials']):
   p=m.get('pbrMetallicRoughness',{});a=manifest['materials'][i];a['emissionColor']=m.get('emissiveFactor',[0,0,0]);a['emissionStrength']=m.get('extensions',{}).get('KHR_materials_emissive_strength',{}).get('emissiveStrength',1);a['occlusionStrength']=m.get('occlusionTexture',{}).get('strength',1)
   if m.get('emissiveTexture'):
    name=f'{kind}-{i}-emission.png';(OUT/name).write_bytes(image(m['emissiveTexture']['index']));a['emission']=name
   if p.get('metallicRoughnessTexture'):
    pixels=np.array(Image.open(io.BytesIO(image(p['metallicRoughnessTexture']['index']))).convert('RGB'));packed=np.full((*pixels.shape[:2],4),255,np.uint8);packed[:,:,0]=np.rint(pixels[:,:,2]*p.get('metallicFactor',1)).astype(np.uint8);packed[:,:,3]=np.rint(255-pixels[:,:,1]*p.get('roughnessFactor',1)).astype(np.uint8);Image.fromarray(packed).save(OUT/a['metallicMap'])
   a['sourceExtensions']=m.get('extensions',{})
  (OUT/(kind+'.json')).write_text(json.dumps(manifest)+'\n')
  for name in ['asset-provenance.json','ATTRIBUTION.txt']:shutil.copyfile(src.parent/name,OUT/(kind+'-'+name))
 routing=json.loads((SRC/'assets/audio/reference0992/manifest.json').read_text());audio=[];cues=[]
 for action in ['grenade_throw','frag_blast','zone_bloom','zone_arm']:
  paths=routing['generic'].get(action)or['res://assets/audio/sfx/'+action+'_00.wav'];files=[]
  for i,path in enumerate(paths):
   src=SRC/path.removeprefix('res://');name=action+'-'+str(i)+'.wav';shutil.copyfile(src,OUT/name);files.append(name)
   with wave.open(str(src),'rb')as w:pcm=w.readframes(w.getnframes());frames=w.getnframes();rate=w.getframerate();assert w.getsampwidth()==2
   audio.append(dict(source=path,file=name,sha256=sha(src),pcmSha256=hashlib.sha256(pcm).hexdigest(),frames=frames,rate=rate))
  cues.append(dict(action=action,files=files))
 for kind,freq in [('frag',1210),('zone',1570)]:
  rate=24000;seconds=.065;n=int(seconds*rate);t=np.arange(n)/rate;envelope=np.minimum(1,t/.004)*np.minimum(1,(seconds-t)/.012);samples=((np.sin(math.tau*freq*t)+.16*np.sin(math.tau*freq*2*t))*.45*envelope*32767).astype('<i2');name=kind+'-warning.wav'
  with wave.open(str(OUT/name),'wb')as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes(samples.tobytes())
  audio.append(dict(source='scripts/grenade_warning.gd::tone('+kind+')',file=name,sha256=sha(OUT/name),pcmSha256=hashlib.sha256(samples.tobytes()).hexdigest(),frames=n,rate=rate,sourceRecipe=True));cues.append(dict(action=kind+'_warning',files=[name]))
 (OUT/'audio.json').write_text(json.dumps(dict(cues=cues),indent=2)+'\n')
 for folder in ['reference099','reference097']:shutil.copyfile(SRC/('assets/audio/'+folder+'/ATTRIBUTION.txt'),OUT/(folder+'-audio-ATTRIBUTION.txt'))
 shutil.copyfile(SRC/'assets/audio/sfx/PROVENANCE.txt',OUT/'authored-audio-PROVENANCE.txt')
 inputs=['scripts/explosive_weapon.gd','scripts/explosive_projectile.gd','scripts/grenade_ballistics.gd','scripts/grenade_warning.gd','scripts/explosive_finish_064.gd','scripts/downloaded_grenade_model.gd','scripts/explosive_effect.gd','scripts/main.gd','scripts/weapon_inventory.gd','scripts/weapon_rack.gd','scripts/arsenal_data.gd','assets/data/one_star_050.json','assets/audio/reference0992/manifest.json'];assert all(sha(SRC/p)==pinned[p]for p in inputs)
 record=dict(originalSource=str(SRC),inputs={p:sha(SRC/p)for p in inputs},geometry=geometry,audio=audio,notes=['Actual original grenade variant selection is frag_alt for FRAG and frag for ZONE; source held scales.9/2.0, projectile ratio.035/.047.','Current original frag_alt asset already contains its historical authored source decimation; this port adds no decimation.','Only source head-to-launch LOS blocks throws. Actual source radial damage has no wall LOS exclusion.','No engine/GPU/build/device or listening run.'])
 (OUT/'provenance.json').write_text(json.dumps(record,indent=2)+'\n')
 assets=ROOT/'unity/Assets/Ashfall/Integration/Playable/Explosives'
 for p in [OUT,assets]+list(OUT.iterdir())+list(assets.glob('*.cs')):
  if p.name.endswith('.meta'):continue
  meta=Path(str(p)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-explosives:'+str(p.relative_to(ROOT)).replace('\\','/')).hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
 print('Original grenade assets',geometry,'audio',len(audio),'payloadBytes',sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()))
if __name__=='__main__':main()
