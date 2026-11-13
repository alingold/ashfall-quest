from pathlib import Path
import json,struct,hashlib,shutil,runpy,uuid
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';IDS=['ump','p90','mp5','mp9']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
old={p.name:sha(p)for p in OUT.iterdir()if p.is_file()and not p.name.startswith(tuple(IDS))and p.name!='audio.json'}
ns={'__file__':str(ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py')};exec(compile((ROOT/'unity/Tools/PlayableR23/HandsWeapons/prepare.py').read_text().split('OUT.mkdir(parents=True')[0],'existing_source_gltf_converter','exec'),ns);ns['SRC']=SRC;ns['OUT']=OUT
primitive=runpy.run_path(str(ROOT/'unity/Tools/PlayableR23/MagnumAssets/prepare.py'));S=np.diag([1.,1.,-1.]);records=[]
def read(name):
 raw=(OUT/(name+'.meshbin')).read_bytes();count=struct.unpack_from('<i',raw,4)[0];cursor=8;parts=[]
 for i in range(count):
  nv,ni,mi=struct.unpack_from('<iii',raw,cursor);cursor+=12;v=np.frombuffer(raw,'<f4',nv*8,cursor).reshape(nv,8).copy();cursor+=nv*32;ind=np.frombuffer(raw,'<i4',ni,cursor).copy();cursor+=ni*4;parts.append((v,ind,mi))
 assert cursor==len(raw);return parts

def write(name,parts):
 with (OUT/(name+'.meshbin')).open('wb')as f:
  f.write(struct.pack('<ii',0x5232334d,len(parts)))
  for v,i,mi in parts:f.write(struct.pack('<iii',len(v),len(i),mi));f.write(v.astype('<f4').tobytes());f.write(i.astype('<i4').tobytes())

def append(name,label,mesh,mi,at,rotation=np.eye(3)):
 v,n,uv,indices=mesh;v=(v@rotation.T+at)@S;n=(n@rotation.T)@S;uv=uv.copy();uv[:,1]=1-uv[:,1];parts=read(name);parts.append((np.c_[v,n,uv].astype('<f4'),indices.astype('<i4').ravel(),mi));write(name,parts);m=json.loads((OUT/(name+'.json')).read_text());m['parts'].append(label);(OUT/(name+'.json')).write_text(json.dumps(m))

def box(size):
 v=[];n=[];ind=[]
 for normal in [[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]]:
  normal=np.array(normal);a=np.array([1,0,0])if abs(normal[1])>.5 else np.array([0,1,0]);b=np.cross(normal,a);start=len(v)
  for x,y in [[-1,-1],[1,-1],[1,1],[-1,1]]:v.append((normal+a*x+b*y)*np.array(size)*.5);n.append(normal)
  ind.extend(start+np.array([0,2,1,0,3,2]))
 return np.array(v),np.array(n),np.zeros((len(v),2)),np.array(ind)

for name in IDS:
 folder=SRC/('assets/models/ump_aggressor'if name=='ump'else f'assets/models/download_{name}');source=folder/('ump_aggressor.glb'if name=='ump'else 'model.glb');d=json.loads((folder/'asset-provenance.json').read_text());ns['convert'](source,name)
 manifest=json.loads((OUT/(name+'.json')).read_text())
 if name=='ump':
  parts=read(name)
  for index,label in enumerate(manifest['parts']):
   if label=='AggressorMagazine':parts[index][0][:,:3]+=np.array(d['magazine_center_m'])*[1,1,-1];manifest['parts'][index]='Magazine'
   elif label=='AggressorBody':manifest['parts'][index]='Body'
  write(name,parts)
 if name=='p90':
  for material in manifest['materials']:material['alphaMode']='OPAQUE';material['color'][3]=1
 # Source still-visible authored charging components, distinct from downloaded static Body.
 steel=primitive['material']('6b7275',.7,.38)if name=='ump' else primitive['material']('515b5c',.75,.36)if name=='p90'else dict(color=[.29,.32,.33,1],metallic=.65,roughness=.36,doubleSided=False,alphaMode='OPAQUE',baseColor='',normal='',metallicMap='',occlusion='')
 mi=len(manifest['materials']);manifest['materials'].append(steel);(OUT/(name+'.json')).write_text(json.dumps(manifest))
 append(name,'Bolt',primitive['beveled']((.032,.012,.022),.003),mi,np.array([-.05 if name=='p90'else-.045,.064,-.10]))
 if name=='ump':
  manifest=json.loads((OUT/(name+'.json')).read_text());dark=len(manifest['materials']);manifest['materials'].append(primitive['material']('181b20',.2,.68));(OUT/(name+'.json')).write_text(json.dumps(manifest));append(name,'SourceChargingSlot',box((.002,.013,.10)),dark,np.array([-.041,.064,-.085]));append(name,'SourceCarrier',primitive['beveled']((.002,.016,.048),.0005),mi,np.array([.041,.05,-.11]))
 if name=='mp9':
  manifest=json.loads((OUT/(name+'.json')).read_text());steelIndex=len(manifest['materials']);manifest['materials'].append(primitive['material']('252d32',.8,.35));(OUT/(name+'.json')).write_text(json.dumps(manifest));start=d['suppressor_start'];append(name,'SourceRetainedSuppressor',primitive['cylinder'](.023,abs(-.42-start),20),steelIndex,np.array([0,.045,(start-.42)*.5]),primitive['rx'](-np.pi/2))
 parts=read(name);triangles=sum(len(i)//3 for v,i,mi in parts);vertices=sum(len(v)for v,i,mi in parts);scale={'ump':.7182,'p90':.72,'mp5':.684,'mp9':1}[name];reloadScale=1 if name=='ump'else scale;home=np.array(d['magazine_center_m'if name=='ump'else'magazine_center'])*[1,1,-1];muzzle=np.array(d['muzzle_m'if name=='ump'else'muzzle'])*[1,1,-1]
 if name=='ump':offer=(home+[0,-.168,0])*scale;seat=home*scale
 elif name=='p90':offer=home+[0,.0508/scale,-.17-.0508/scale];seat=home
 else:offer=np.array([0,-.278,home[2]]);seat=np.array([0,-.18,home[2]])
 windowHeight=.035*np.sqrt(2);foot=windowHeight*.5+.023;mount=(.2-.018/scale-foot*.25/scale)if name=='mp5'else(.13-foot*.25/scale)if name=='mp9'else 0
 fit=dict(id=name,modelScale=scale,reloadScale=reloadScale,weaponGripOrigin=[0,0,0],muzzle=muzzle.tolist(),magazineHome=home.tolist(),offer=offer.tolist(),insert=seat.tolist(),chargingGrip=[-.05 if name=='p90'else-.045,.064,.10],chargingMirrorX=name=='p90',topFeed=name=='p90',insertionAxis='Unity +Z'if name=='p90'else'Y',insertionTravel=.10 if name=='p90'else.06,chargingPull=.075,offerRadius=.25 if name=='p90'else.10,chargeRadius=.12 if name=='p90'else.18,supportPoint=[0,-.025,.36],heldMagazineGunBasis=name=='p90',opticScale=.75 if name in ['mp5','mp9']else 0,opticMountHeight=mount,worldCrosshair=name!='mp9',laserRange=180 if name=='mp9'else 0,ammoFamily='smg',reserve=1200,sourceGLBTriangles=d['source_triangles'],visibleTriangles=triangles,notes=['Actual live rack AMMO_FAMILIES overrides stale profile family label.','All model art before display scale; UMP logical reload scale remains1 because no visual_fit_scale084 metadata.','P90 actual source solid_import085 disables imported BLEND without changing textures/PBR/sidedness.'])
 if name=='ump':fit.update(rearSight=[0,.14,-.03],frontSight=[0,.14+(.045-.14)*(.03+.393)/(35+.03),.393])
 if name=='p90':fit.update(rearSight=[0,.16,.025],frontSight=[0,.16+(.045-.16)*(.36-.025)/(35.46-.025),.36])
 (OUT/f'{name}-fit.json').write_text(json.dumps(fit,indent=2));shutil.copyfile(folder/'asset-provenance.json',OUT/f'{name}-provenance.json');shutil.copyfile(folder/'ATTRIBUTION.txt',OUT/f'{name}-ATTRIBUTION.txt');records.append(dict(id=name,source=str(source.relative_to(SRC)),sha256=sha(source),vertices=vertices,triangles=triangles,sourceTriangles=d['source_triangles']))
# Actual top_feed_pdw source yellow insert/charge arrows, three8radial rods each.
for name,axis in [('p90-insert-arrow',np.array([0,1,0])),('p90-charge-arrow',np.array([0,0,1]))]:
 (OUT/(name+'.json')).write_text(json.dumps(dict(materials=[primitive['material']('ffd63d',.15,.5)],parts=[])));write(name,[])
 endpoints=[(np.zeros(3),axis*.10)]
 for side in [-1,1]:endpoints.append((axis*.10,axis*.072+np.array([side*.018,0,0])))
 for index,(a,b)in enumerate(endpoints):
  delta=b-a;direction=delta/np.linalg.norm(delta);up=np.array([0.,1.,0.]);cross=np.cross(up,direction);dot=np.dot(up,direction)
  if np.linalg.norm(cross)<1e-10:rotation=np.eye(3)if dot>0 else np.diag([1.,-1.,-1.])
  else:
   k=np.array([[0,-cross[2],cross[1]],[cross[2],0,-cross[0]],[-cross[1],cross[0],0]]);rotation=np.eye(3)+k+k@k/(1+dot)
  append(name,'ArrowRod'+str(index),primitive['cylinder'](.003,float(np.linalg.norm(delta)),8),0,(a+b)*.5,rotation)
audioPath=OUT/'audio.json';audio=json.loads(audioPath.read_text());routing=json.loads((SRC/'assets/audio/reference0992/manifest.json').read_text());audioRecords=[]
for name in IDS:
 files=[]
 for index,source in enumerate(routing['weapons'][name]['shot']):
  original=SRC/source.removeprefix('res://');file=f'audio/{name}-shot-{index}.wav';shutil.copyfile(original,OUT/file);files.append(file);audioRecords.append(dict(source=source,file=file,sha256=sha(original)))
 audio['cues']=[c for c in audio['cues']if not(c['weapon']==name and c['action']=='shot')];audio['cues'].append(dict(weapon=name,action='shot',files=files));shutil.copyfile(SRC/'assets/audio/reference097/ATTRIBUTION.txt',OUT/f'{name}-audio-ATTRIBUTION.txt')
audioPath.write_text(json.dumps(audio,indent=2)+'\n');sourceFiles=['scripts/compact_smg.gd','scripts/top_feed_pdw.gd','scripts/catalog_firearm.gd','scripts/ump_aggressor_model.gd','scripts/weapon.gd','scripts/weapon_rack.gd','scripts/model_scale_084.gd','scripts/mp9_laser_091.gd','scripts/solid_import_085.gd','assets/data/one_star_050.json','assets/data/one_star0991.json','assets/data/recoil_observed_067.json','assets/data/recoil_endpoint_experiment_071.json']
HERE.joinpath('preserved-existing.json').write_text(json.dumps(old,indent=2));assert all(sha(OUT/name)==digest for name,digest in old.items())
(OUT/'ump-p90-mp5-mp9-port.json').write_text(json.dumps(dict(baseline=str(SRC),geometry=records,audio=audioRecords,sourceInputs={p:sha(SRC/p)for p in sourceFiles},conversion='Original full GLBs + only source visible authored components, no decimation; normal inverse transpose, single Z reflection and UV/winding conversion; no Blender/engine.'),indent=2))
for p in OUT.rglob('*'):
 if p.is_file()and p.name.startswith(tuple(IDS))and not p.name.endswith('.meta'):
  meta=Path(str(p)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
print(records);print('New payload bytes',sum(p.stat().st_size for p in OUT.rglob('*')if p.is_file()and p.name.startswith(tuple(IDS))));print('Preserved',len(old),'existing top-level files')
