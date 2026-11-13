"""Actual fitted dual GLBs, source icons/audio and immutable profile data; CPU only."""
from source import *
import numpy as np,struct,shutil,wave
from PIL import Image
OUT=HERE/'Payload';STAGED=HERE/'Staged';OUT.mkdir(exist_ok=True);STAGED.mkdir(exist_ok=True)

def main():
 path=ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py';prefix=path.read_text().split('OUT.mkdir(parents=True')[0];ns={'__file__':str(path)};exec(compile(prefix,'read_only_existing_gltf_converter','exec'),ns);ns['SRC']=SRC;ns['OUT']=OUT
 profiles=json.loads(source('assets/data/one_star_050.json').read_text())['profiles'];rarity=json.loads(source('assets/data/weapon_rarity_r6.json').read_text())['weapons'];damage=json.loads(source('assets/data/one_star0991.json').read_text())['weapons'];routing=json.loads(source('assets/audio/reference0992/manifest.json').read_text());records=[];cues=[];audio=[]
 for id,folder,scale,index in [('uzis','download_uzi',.65436,11),('matadors','download_matadors',.9,12)]:
  relative='assets/models/'+folder+'/';model=source(relative+'model.glb');provenance=json.loads(source(relative+'asset-provenance.json').read_text());name='dual-'+id;result=ns['convert'](model,name);shutil.copyfile(source(relative+'ATTRIBUTION.txt'),OUT/(name+'-ATTRIBUTION.txt'));shutil.copyfile(source(relative+'asset-provenance.json'),OUT/(name+'-provenance.json'))
  profile=next(r for r in profiles if r['index']==index);r=rarity[id];d=damage[id];interval=1/(profile['rounds_per_second']*(1.2 if id=='matadors'else 1));muzzle=np.array([0,.045,-.37 if id=='matadors'else-.46])*[1,1,-1]
  fit=dict(id=id,catalogIndex=index,catalogId='twin_matadors'if id=='matadors'else'twin_meridian',modelName=name,modelScale=scale,weaponGripOrigin=[0,0,0],primaryGrip=[0,-.057,-.025],spinPivot=[0,-.057,-.025],muzzle=muzzle.tolist(),spinSeconds=.36,roundsPerHand=r['magazine'],bodyPerVolley=r['body'],headPerVolley=r['head'],pellets=r['pellets'],interval=interval,ammoFamily='shells'if id=='matadors'else'smg',initialReserve=480 if id=='matadors'else 1200,physicalGloveOffset=[0,.1524-.1016*np.cos(np.radians(35)),-.1016*np.sin(np.radians(35))]if id=='matadors'else[0,0,-.0127],glovePitchDegrees=35 if id=='matadors'else 0,sourcePerHandOnly=True,reticleLaser=id=='uzis',sourceTriangles=result['triangles'])
  (OUT/(name+'-fit.json')).write_text(json.dumps(fit,indent=2)+'\n');icon=source(next(p for p in ['assets/ui/catalog0991/weapon_'+str(index)+'.png','assets/ui/catalog094/weapon_'+str(index)+'.png']if p in PINS));shutil.copyfile(icon,OUT/(name+'-icon.png'))
  for action in ['shot','mag_insert']:
   paths=routing['weapons'][id].get(action,routing['generic'].get(action,[]));assert paths
   files=[]
   for i,original in enumerate(paths):
    p=source(original.removeprefix('res://'));file=name+'-'+action+'-'+str(i)+'.wav';shutil.copyfile(p,OUT/file);files.append(file)
    with wave.open(str(p),'rb')as w:assert w.getsampwidth()==2;pcm=w.readframes(w.getnframes());audio.append(dict(file=file,source=original,sha256=sha(p),pcmSha256=hashlib.sha256(pcm).hexdigest(),channels=w.getnchannels(),rate=w.getframerate(),frames=w.getnframes()))
   cues.append(dict(weapon=id,action=action,files=files))
  for ref in ['reference096','reference097','reference099']:shutil.copyfile(source('assets/audio/'+ref+'/ATTRIBUTION.txt'),OUT/(name+'-'+ref+'-ATTRIBUTION.txt'))
  records.append(dict(id=id,geometry=result,source=relative+'model.glb',sha256=sha(model),legacyProvenanceOutputMatches=sha(model)==provenance['output_sha256'],fit=fit,profile=profile,damage=d))
 for p in ns['records']:
  relative=str(p['source']).replace('\\','/');source(relative)
 paths=['scripts/dual_pistols.gd','scripts/dual_matadors.gd','scripts/compact_smg.gd','scripts/weapon.gd','scripts/downloaded_pair_model.gd','scripts/model_scale_084.gd','scripts/playtest_fit_088.gd','scripts/weapon_rack.gd','scripts/arsenal_data.gd','scripts/one_star0991.gd','scripts/weapon_rarity_r6.gd','scripts/recoil_endpoint_experiment.gd','scripts/uzi_vertical_prefix.gd','scripts/reticle_bounce_071.gd','scripts/matador_secret_094.gd','scripts/audio_bank.gd','scripts/reference_audio_099.gd','assets/data/recoil_endpoint_experiment_071.json']
 for p in paths:source(p)
 (OUT/'dual-audio-cues.json').write_text(json.dumps(dict(cues=cues),indent=2)+'\n');(HERE/'source-port.json').write_text(json.dumps(dict(contentId='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520',inputs=USED,geometry=records,audio=audio,converterSha256=sha(path),conversion='Full authentic fitted source GLBs; no decimation. Node transforms, source magazine/bolt homes, inverse-transpose normals, Z/winding/UV reflection once. Display scale is applied once by the view.',assetsWritten=False),indent=2)+'\n')
 endpoint=json.loads(source('assets/data/recoil_endpoint_experiment_071.json').read_text())['profiles']['uzis']['one']['samples_pitch_yaw_deg'];values=','.join(format(float(p[0]),'.9g')+'f'for p in endpoint)
 (STAGED/'R24DualSourceData.cs').write_text('namespace Ashfall.Integration.Playable.HandsWeapons { internal static class R24DualSourceData { internal static readonly float[] UziPitch={'+values+'}; } }\n')
 assert sum(p.stat().st_size for p in OUT.iterdir()if p.is_file())<60000000
 print([(r['id'],r['geometry'])for r in records]);print('Copied',len(audio),'actualWAVs and2sourceicons; Tools-only payload bytes',sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()))

if __name__=='__main__':main()
