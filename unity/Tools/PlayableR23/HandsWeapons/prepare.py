import base64,json,struct,hashlib,shutil,io
from pathlib import Path
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[4]
SRC=ROOT/'quest-r24/source'
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
records=[]
def record(p):
 records.append({'source':str(p.relative_to(SRC)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
def convert(path,name,hand=False,omit_nodes=()):
 record(path); raw=path.read_bytes(); blob=None
 if path.suffix=='.glb':
  length=struct.unpack_from('<I',raw,12)[0]; g=json.loads(raw[20:20+length]); blob=raw[28+length:]
 else:g=json.loads(raw)
 buffers=[base64.b64decode(b['uri'].split(',')[1]) if 'uri' in b and b['uri'].startswith('data:') else (path.parent/b['uri']).read_bytes() if 'uri' in b else blob for b in g['buffers']]
 def acc(i):
  a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];typ={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]; c={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];dt=np.dtype(typ)
  x=np.ndarray((a['count'],c),dtype=dt,buffer=buffers[v['buffer']],offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dt.itemsize*c),dt.itemsize)).copy()
  if a.get('normalized'):x=x/np.iinfo(dt).max
  return x
 def local(n):
  if 'matrix' in n:return np.array(n['matrix']).reshape(4,4).T
  x,y,z,w=n.get('rotation',[0,0,0,1]);r=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])
  m=np.eye(4);m[:3,:3]=r@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0]);return m
 parents={c:i for i,n in enumerate(g['nodes']) for c in n.get('children',[])}
 def world(i):return world(parents[i])@local(g['nodes'][i]) if i in parents else local(g['nodes'][i])
 extra=np.eye(4)
 if hand:
  suffix='_L' if name=='left' else '_R';wrist=next(i for i,n in enumerate(g['nodes']) if n.get('name')=='Wrist'+suffix);knuckle=next(i for i,n in enumerate(g['nodes']) if n.get('name')=='Middle_Proximal'+suffix)
  angle=-np.pi/2 if name=='left' else np.pi/2;c,s=np.cos(angle),np.sin(angle);extra[:3,:3]=[[c,-s,0],[s,c,0],[0,0,1]];extra[:3,3]=[0,.032,0]-extra[:3,:3]@((world(wrist)[:3,3]+world(knuckle)[:3,3])*.5)
 materials=[]
 def image_data(index):
  im=g['images'][g['textures'][index]['source']]
  if 'bufferView' in im:
   v=g['bufferViews'][im['bufferView']];return buffers[v['buffer']][v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]
  if im['uri'].startswith('data:'):return base64.b64decode(im['uri'].split(',')[1])
  p=path.parent/im['uri'];record(p);return p.read_bytes()
 for i,m in enumerate(g.get('materials',[{}])):
  p=m.get('pbrMetallicRoughness',{});entry={'color':p.get('baseColorFactor',[1,1,1,1]),'metallic':p.get('metallicFactor',1),'roughness':p.get('roughnessFactor',1),'doubleSided':m.get('doubleSided',False),'alphaMode':m.get('alphaMode','OPAQUE'),'baseColor':'','normal':'','metallicMap':'','occlusion':''}
  for key,src in [('baseColor',p.get('baseColorTexture')),('normal',m.get('normalTexture'))]:
   if src:
    fn=f'{name}-{i}-{key}.png';(OUT/fn).write_bytes(image_data(src['index']));entry[key]=fn
  strength=m.get('extensions',{}).get('KHR_materials_emissive_strength',{}).get('emissiveStrength',1)
  entry['emissionColor']=[float(c)*strength for c in m.get('emissiveFactor',[0,0,0])]
  entry['emission']=''
  if m.get('emissiveTexture'):
   fn=f'{name}-{i}-emission.png';(OUT/fn).write_bytes(image_data(m['emissiveTexture']['index']));entry['emission']=fn
  if p.get('metallicRoughnessTexture'):
   tex=Image.open(io.BytesIO(image_data(p['metallicRoughnessTexture']['index']))).convert('RGB');r,green,b=tex.split();one=Image.new('L',tex.size,255)
   fn=f'{name}-{i}-metallic.png';Image.merge('RGBA',(b,one,one,green.point(lambda x:255-x))).save(OUT/fn);entry['metallicMap']=fn
  spec=m.get('extensions',{}).get('KHR_materials_pbrSpecularGlossiness')
  if spec:
   entry.update(color=spec.get('diffuseFactor',[1,1,1,1]),metallic=0,roughness=1-spec.get('glossinessFactor',1),specularWorkflow=True,specularColor=spec.get('specularFactor',[1,1,1]),glossiness=spec.get('glossinessFactor',1),specularGlossiness='')
   # Current source Matadors has unit specularFactor: preserve embedded bytes.
   # URP's map workflow ignores _SpecColor, so reject nonunit textured factors
   # until that source case is explicitly transported rather than silently lost.
   if spec.get('specularGlossinessTexture') and entry['specularColor']!=[1,1,1]:raise ValueError('Textured specularFactor requires explicit linear-factor bake')
   for key,texture in [('baseColor',spec.get('diffuseTexture')),('specularGlossiness',spec.get('specularGlossinessTexture'))]:
    if texture:
     fn=f'{name}-{i}-'+('baseColor' if key=='baseColor' else 'specGloss')+'.png';(OUT/fn).write_bytes(image_data(texture['index']));entry[key]=fn
  if m.get('occlusionTexture'):
   tex=Image.open(io.BytesIO(image_data(m['occlusionTexture']['index']))).convert('RGB');r=tex.split()[0];fn=f'{name}-{i}-occlusion.png';Image.merge('RGB',(r,r,r)).save(OUT/fn);entry['occlusion']=fn
  materials.append(entry)
 if hand:
  tex=SRC/'assets/hands/xrtools086/textures_glove_caucasian_dark_camo.png';record(tex);shutil.copyfile(tex,OUT/'glove.png');materials=[{'color':[1,1,1,1],'metallic':0,'roughness':.85,'doubleSided':False,'baseColor':'glove.png','normal':''}]
 parts=[];part_names=[]
 for ni,n in enumerate(g['nodes']):
  if 'mesh' not in n or n.get('name') in omit_nodes:continue
  for p in g['meshes'][n['mesh']]['primitives']:
   assert p.get('mode',4)==4
   a=p['attributes'];v=acc(a['POSITION']);norm=acc(a['NORMAL']);uv=acc(a['TEXCOORD_0']);mat=world(ni)
   if 'skin' in n:
    skin=g['skins'][n['skin']];ib=acc(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1);j=acc(a['JOINTS_0']).astype(int);weights=acc(a['WEIGHTS_0']);mats=np.array([world(k)@b for k,b in zip(skin['joints'],ib)]);blend=(mats[j]*weights[:,:,None,None]).sum(axis=1);v=np.einsum('nij,nj->ni',blend,np.c_[v,np.ones(len(v))])[:,:3];norm=np.einsum('nij,nj->ni',np.linalg.inv(blend[:,:3,:3]).transpose(0,2,1),norm);mat=np.eye(4)
   if not hand and (path.parent/'asset-provenance.json').exists():
    data=json.loads((path.parent/'asset-provenance.json').read_text())
    if n.get('name')=='Magazine':mat[:3,3]+=np.array(data['magazine_center'])
    elif name!='spr' and n.get('name') in ['Slide','Bolt']:
     mat[:3,3]+=np.array(data['slide_home' if n.get('name')=='Slide' else 'bolt_home'])
   mat=extra@mat;v=(np.c_[v,np.ones(len(v))]@mat.T)[:,:3];norm=norm@np.linalg.inv(mat[:3,:3]);norm/=np.linalg.norm(norm,axis=1)[:,None];v[:,2]*=-1;norm[:,2]*=-1
   ind=(acc(p['indices']) if 'indices' in p else np.arange(len(v))).astype(np.int32).reshape(-1,3)[:,[0,2,1]];assert ind.max()<len(v)
   uv[:,1]=1-uv[:,1]
   parts.append((np.c_[v,norm,uv].astype('<f4'),ind.astype('<i4'),0 if hand else p.get('material',0)))
   part_names.append(n.get('name',f'Node{ni}'))
 with (OUT/(name+'.meshbin')).open('wb') as f:
  f.write(struct.pack('<ii',0x5232334d,len(parts)))
  for v,ind,mi in parts:f.write(struct.pack('<iii',len(v),ind.size,mi));f.write(v.tobytes());f.write(ind.tobytes())
 (OUT/(name+'.json')).write_text(json.dumps({'materials':materials,'parts':part_names}))
 return {'name':name,'vertices':sum(len(v) for v,i,m in parts),'triangles':sum(i.size//3 for v,i,m in parts)}
OUT.mkdir(parents=True,exist_ok=True)
counts=[convert(SRC/'assets/hands/xrtools086'/('Hand_Glove_low_'+side+'.gltf'),name,True) for side,name in [('L','left'),('R','right')]]
counts.append(convert(SRC/'assets/models/download_mk18/model.glb','mk18'))
counts.append(convert(SRC/'assets/models/download_akm/model.glb','akm'))
counts.append(convert(SRC/'assets/models/download_spr/model.glb','spr'))
counts.append(convert(SRC/'assets/models/gemini_scope099/spr_scope_pcvr.glb','spr-scope',omit_nodes=('ReticleViewPlane',)))
for name in ['mk18','akm','spr']:
 p=SRC/f'assets/models/download_{name}/asset-provenance.json';record(p);shutil.copyfile(p,OUT/f'{name}-provenance.json')
for p in [SRC/'assets/models/download_mk18/asset-provenance.json',SRC/'assets/hands/xrtools086/ATTRIBUTION.txt',SRC/'assets/hands/xrtools086/LICENSE.txt']:
 record(p);shutil.copyfile(p,OUT/p.name)
for name in ['grip_hand_086.gd','player.gd','playtest_fit_088.gd','downloaded_firearm_model.gd','model_scale_084.gd','common_reflex_090.gd','authored_reflex_sight.gd','authored_ring_dot_sight.gd','practice_dummy.gd','audio_bank.gd','reference_audio_099.gd','weapon.gd','weapon_rack.gd','burst_carbine.gd','catalog_firearm.gd','arsenal_data.gd','weapon_rarity_r6.gd','spr.gd','spr_scope.gd','longwatch_scope.gd','gemini_scope099.gd']:record(SRC/'scripts'/name)
for name in ['provenance.json','ATTRIBUTION.txt']:
 p=SRC/'assets/models/gemini_scope099'/name;record(p);shutil.copyfile(p,OUT/('spr-scope-'+name))
audio_manifest=SRC/'assets/audio/reference0992/manifest.json';record(audio_manifest);routing=json.loads(audio_manifest.read_text());cues=[]
(OUT/'audio').mkdir(exist_ok=True)
for weapon,actions in [('', ['mag_eject','pouch','mag_insert','bolt','dry','hit','hurt']),('mk18',['shot']),('akm',['shot','mag_insert','bolt']),('spr',['shot','bolt','mag_eject','mag_insert'])]:
 for action in actions:
  paths=routing['weapons'].get(weapon,{}).get(action,[]) if weapon else routing['generic'].get(action,[])
  if not paths:paths=[f'res://assets/audio/sfx/{action}_{i:02d}.wav' for i in range(2)]
  files=[]
  for i,path in enumerate(paths):
   source=SRC/path.removeprefix('res://');record(source);file=f'audio/{weapon or "generic"}-{action}-{i}.wav';shutil.copyfile(source,OUT/file);files.append(file)
  cues.append({'weapon':weapon,'action':action,'files':files})
(OUT/'audio.json').write_text(json.dumps({'cues':cues},indent=2))
record(SRC/'assets/data/one_star0991.json')
for name in ['one_star_050.json','weapon_rarity_r6.json']:record(SRC/'assets/data'/name)
(OUT/'provenance.json').write_text(json.dumps({'baseline':'quest-r24 snapshot of actual Opus R23','inputs':records,'geometry':counts,'conversion':'Full source primitives, default skin pose baked, normals inverse transpose, Z reflection + winding reversal. Magazine and moving bolt homes restored from current source runtime; SPR bolt rest ZERO overrides stale provenance. No decimation. Runtime fits MK18 .92 / AKM .945 / SPR1.32. Gemini scope ReticleViewPlane omitted because live stereo aperture owns it.','mechanics':{'mode':'Three persistent first-rarity local loadout instances; selector equips existing instances. Explicit round restart regrants starter loadout.','mk18':{'capacity':18,'mode':'committed three-round burst','intra_seconds':.08,'recovery_seconds':.24},'akm':{'capacity':20,'mode':'automatic','rounds_per_second':11},'spr':{'capacity':4,'mode':'semi','rounds_per_second':3.125,'rearm_trigger_below':.2,'body':55,'head':92,'ammo_family':'sniper','scope':'Accepted Gemini PCVR housing; per-eye520x307 source angular2x image with fixed1m virtual reticle; source eye-relief/alignment/occlusion gates.'},'reserves':{'rifle':{'shared_by':['mk18','akm'],'initial':2400},'sniper':{'shared_by':['spr'],'initial':2400},'gameplay_cap':None},'reload':'Source eject/refund -> grab/upward insert/debit -> release/regrip/rearward charge; button advances one source desktop step. Native WeaponHandling owns all loaded/seated/chamber/ammo mutations.','limits':'First rarity only; existing Unity recoil presentation retained. Source-specific configuration is not supported by generic schema1 weapon checkpoint restore. Dropped props use explicit supplied poses, no gravity simulation. Scope shader/GPU/stereo acceptance not run in this CPU-only port lane.'}},indent=2))
print(counts)

