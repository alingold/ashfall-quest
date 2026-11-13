from pathlib import Path
import json,struct,hashlib,shutil,runpy,uuid
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
old={p.name:sha(p) for p in OUT.iterdir() if p.is_file() and not p.name.startswith(('cx4','px4','m1014')) and p.name!='audio.json'}
# Reuse the actual project's NumPy/Pillow glTF conversion, without executing its old main/export paths.
namespace={'__file__':str(ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py')};prefix=(ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py').read_text().split('OUT.mkdir(parents=True')[0];exec(compile(prefix,'existing_glTF_conversion','exec'),namespace);namespace['SRC']=SRC;namespace['OUT']=OUT
counts=[namespace['convert'](SRC/f'assets/models/download_{name}/model.glb',name) for name in ['cx4','px4']]
# Append ONLY the actual still-visible source extension/charging handle; downloaded body stays complete.
primitive=runpy.run_path(str(ROOT/'unity/Tools/PlayableR23/MagnumAssets/prepare.py'));S=np.diag([1.,1.,-1.])
def append(name,part,mesh,mi,at,rotation):
 v,n,uv,indices=mesh;v=(v@rotation.T+at)@S;n=(n@rotation.T)@S;uv=uv.copy();uv[:,1]=1-uv[:,1];vertices=np.c_[v,n,uv].astype('<f4');indices=indices.astype('<i4').ravel();p=OUT/(name+'.meshbin');raw=bytearray(p.read_bytes());num=struct.unpack_from('<i',raw,4)[0];struct.pack_into('<i',raw,4,num+1);raw+=struct.pack('<iii',len(v),len(indices),mi)+vertices.tobytes()+indices.tobytes();p.write_bytes(raw);m=json.loads((OUT/(name+'.json')).read_text());m['parts'].append(part);(OUT/(name+'.json')).write_text(json.dumps(m));return len(indices)//3
m=json.loads((OUT/'cx4.json').read_text());m['materials'].extend([primitive['material']('454f55',.78,.32),dict(color=[.29,.32,.33,1],metallic=.65,roughness=.36,doubleSided=False,alphaMode='OPAQUE',baseColor='',normal='',metallicMap='',occlusion='')]);(OUT/'cx4.json').write_text(json.dumps(m));mi=len(m['materials'])-2
extension=append('cx4','SourceBarrelExtension',primitive['cylinder'](.0125,.62-.522377260389049,16),mi,np.array([0,.045,-(.62+.522377260389049)/2]),primitive['rx'](-np.pi/2))
handle=append('cx4','Bolt',primitive['beveled']((.032,.012,.022),.003),mi+1,np.array([-.045,.064,-.10]),np.eye(3));counts[0]['triangles']+=extension+handle
counts[0]['vertices']+=len(primitive['cylinder'](.0125,.62-.522377260389049,16)[0])+len(primitive['beveled']((.032,.012,.022),.003)[0])
for name in ['cx4','px4']:
 folder=SRC/f'assets/models/download_{name}';shutil.copyfile(folder/'asset-provenance.json',OUT/f'{name}-provenance.json');shutil.copyfile(folder/'ATTRIBUTION.txt',OUT/f'{name}-ATTRIBUTION.txt')
 d=json.loads((folder/'asset-provenance.json').read_text());scale=.855 if name=='cx4' else .7;home=(np.array(d['magazine_center'])*[1,1,-1]).tolist();charge=[-.045,.064,.10] if name=='cx4' else [0,.03602747788280249,.15576319806277752];rear=.135 if name=='cx4' else .083;frontZ=.465 if name=='cx4' else .245;zero=80 if name=='cx4' else 35;rearZ=.015;front=rear+(.045-rear)*(frontZ-rearZ)/(zero+float(-d['muzzle'][2])-rearZ)
 fit=dict(id=name,modelScale=scale,weaponGripOrigin=[0,0,0],muzzle=(np.array(d['muzzle'])*[1,1,-1]).tolist(),magazineHome=home,offer=[0,-.278,home[2]],insert=[0,-.18,home[2]],chargingGrip=charge,chargingPullUnityZ=-.075,chargingMaximum=.09,supportPoint=[0,-.065,.095] if name=='px4' else [0,-.025,.36],rearSight=[0,rear,rearZ],frontSight=[0,front,frontZ],zeroDistance=zero,capacity=25 if name=='cx4' else 16,ammoFamily='rifle' if name=='cx4' else 'pistol',sourceVisibleTriangles=counts[0 if name=='cx4' else 1]['triangles'],sourceGLBTriangles=d['source_triangles'],slideCycleDistance=.035 if name=='px4' else 0,slideCycleSeconds=.095 if name=='px4' else 0,notes=['Coordinates already reflected Unity+Zforward. Model scale is NOT baked.','Downloaded Magazine and Slide homes restored exactly once.','CX4 retains original barrel extension and beveled charging handle; R5 collimated dot supplied by runtime, not an MK18 optic.'] if name=='cx4' else ['Pistol inherited downloaded magazine Z; physical magazine up .06, release/regrip then slide back .075.','PX4 uses source semi rearm at released .65 fire threshold; SPR-only .2 latch is not applied.'])
 (OUT/f'{name}-fit.json').write_text(json.dumps(fit,indent=2))
manifest=json.loads((SRC/'assets/audio/reference0992/manifest.json').read_text());p=OUT/'audio.json';audio=json.loads(p.read_text());new=[]
for name,actions in [('cx4',['shot','mag_insert','bolt']),('px4',['shot'])]:
 for action in actions:
  files=[]
  for index,source in enumerate(manifest['weapons'][name][action]):
   sourcePath=SRC/source.removeprefix('res://');file=f'audio/{name}-{action}-{index}.wav';shutil.copyfile(sourcePath,OUT/file);files.append(file);new.append(dict(source=source,file=file,sha256=sha(sourcePath)))
  audio['cues']=[c for c in audio['cues'] if not(c['weapon']==name and c['action']==action)];audio['cues'].append(dict(weapon=name,action=action,files=files))
 shutil.copyfile(SRC/'assets/audio/reference097/ATTRIBUTION.txt',OUT/f'{name}-audio-ATTRIBUTION.txt')
p.write_text(json.dumps(audio,indent=2)+'\n')
sourceFiles=['scripts/catalog_firearm.gd','scripts/pistol.gd','scripts/weapon.gd','scripts/model_scale_084.gd','scripts/downloaded_firearm_model.gd','scripts/cx4_sight_r5.gd','scripts/authored_ring_dot_sight.gd','assets/data/one_star_050.json','assets/data/one_star0991.json','assets/data/recoil_observed_067.json','scripts/reticle_bounce_071.gd']
record=dict(baseline=str(SRC),geometry=counts,sourceInputs={p:sha(SRC/p)for p in sourceFiles},audio=new,notes='Full original fitted GLBs; no decimation or substitute rifle meshes. CX4 + source rod/charging bevel only. NumPy/Pillow, no engine/Blender.')
(OUT/'cx4-px4-port.json').write_text(json.dumps(record,indent=2));(HERE/'preserved-existing.json').write_text(json.dumps(old,indent=2));assert all(sha(OUT/name)==digest for name,digest in old.items())
for p in OUT.rglob('*'):
 if p.is_file() and (p.name.startswith(('cx4','px4')) or p.parent.name=='audio' and p.name.startswith(('cx4','px4'))):
  meta=Path(str(p)+'.meta')
  if not p.name.endswith('.meta') and not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
print(counts);print('Preserved existing top-level payload files',len(old));print('Exact new audio files',len(new))
