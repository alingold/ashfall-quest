from pathlib import Path
import argparse,hashlib,json,struct,io
import numpy as np
from PIL import Image
import prepare as p
args=argparse.ArgumentParser();args.add_argument('--receipt',default='receipt.json');args=args.parse_args()
receipt=json.loads((p.HERE/args.receipt).read_text());out=p.OUT
for relative,digest in receipt['inputs'].items():assert p.known[relative]==digest==hashlib.sha256(p.resolve(relative).read_bytes()).hexdigest()
digests=set();payload=set()
for row in receipt['styles']:
 key=row['id'];manifest=json.loads((out/(key+'.json')).read_text());raw=(out/(key+'.meshbin')).read_bytes();digests.add(hashlib.sha256(raw).hexdigest());g,acc,image=p.document(p.resolve(row['sourceModel']))
 assert manifest['sourceIndex']==p.keys.index(key)==row['sourceIndex'] and manifest['label']==p.labels[row['sourceIndex']]
 assert 8<=row['sourceIndex']<len(p.keys)
 if row['tier']=='pcvr-fallback':assert row['sourceModel'].replace('_pcvr.glb','_light.glb')not in p.known
 else:assert row['tier']=='light'and row['sourceModel'].endswith('_light.glb')
 magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323341;cursor=8;surface=0;triangles=0;parts=set()
 for node in g['nodes']:
  if 'mesh'not in node:continue
  for pr in g['meshes'][node['mesh']]['primitives']:
   length=struct.unpack_from('<i',raw,cursor)[0];cursor+=4;name=raw[cursor:cursor+length].decode();cursor+=length;nv,ni,mi=struct.unpack_from('<iii',raw,cursor);cursor+=12
   assert name==node['name']and nv<=100000 and ni<=300000 and mi==pr.get('material',0);parts.add(name)
   v=np.frombuffer(raw,'<f4',nv*8,cursor).reshape(nv,8);cursor+=nv*32;idx=np.frombuffer(raw,'<i4',ni,cursor);cursor+=ni*4
   a=pr['attributes'];pos=acc(a['POSITION']);norm=acc(a['NORMAL']);uv=acc(a['TEXCOORD_0']);pos[:,2]*=-1;norm[:,2]*=-1;uv[:,1]=1-uv[:,1]
   assert np.array_equal(v,np.c_[pos,norm,uv].astype('<f4'))and np.array_equal(idx,acc(pr['indices']).astype('<i4').reshape(-1,3)[:,[0,2,1]].ravel())
   assert np.isfinite(v).all()and np.linalg.norm(v[:,3:6],axis=1).min()>.5 and idx.min()>=0 and idx.max()<nv
   triangles+=ni//3;surface+=1
 assert cursor==len(raw)and surface==count and triangles==row['triangles']and len(parts)==14
 assert all(name in parts for name in ['AvatarHead','Jacket','ChestPlate','Belt','LForearm','RForearm','LUpperArm','RUpperArm','LThigh','RThigh','LShin','RShin','LBoot','RBoot'])
 assert 0<=manifest['gloveMaterialIndex']<len(manifest['materials'])
 assert manifest.get('chestplateFollowsJacket',False)==p.extra[key].get('chestplate_follow_jacket',False)
 assert manifest.get('stabilizePreviewLegs',False)==(key in ['anna_tabby','anna_rainbow','anna'])
 for mi,source in enumerate(g['materials']):
  actual=manifest['materials'][mi];m=source.get('pbrMetallicRoughness',{})
  assert actual['color']==m.get('baseColorFactor',[1,1,1,1])and actual['metallic']==m.get('metallicFactor',1)and actual['roughness']==m.get('roughnessFactor',1)
  assert actual['doubleSided']==source.get('doubleSided',False)
  ext=source.get('extensions',{})
  if row['sourceIndex']not in [53,74,75,76]:
   assert actual.get('emissionEnergy',1)==ext.get('KHR_materials_emissive_strength',{}).get('emissiveStrength',1)
   assert actual.get('specularFactor',1)==ext.get('KHR_materials_specular',{}).get('specularFactor',1)
  for field,slot,kind in [('baseColor',m.get('baseColorTexture'),'albedo'),('normal',source.get('normalTexture'),'normal'),('emission',source.get('emissiveTexture'),'emission'),('metallicMap',m.get('metallicRoughnessTexture'),'metallic'),('occlusion',source.get('occlusionTexture'),'occlusion')]:
   if not slot:assert not actual[field];continue
   expected=Image.open(io.BytesIO(image(slot['index']))).convert('RGBA')
   if max(expected.size)>1024:expected.thumbnail((1024,1024),Image.Resampling.LANCZOS)
   if kind=='metallic':
    _,green,blue,_=expected.split();zero=Image.new('L',expected.size,0)
    if actual['metallic']!=1:blue=blue.point(lambda x:round(x*actual['metallic']))
    alpha=green.point(lambda x:255-x)if actual['roughness']==1 else green.point(lambda x:round(255-x*actual['roughness']))
    expected=Image.merge('RGBA',(blue,zero,zero,alpha))
   elif kind=='occlusion':
    red,_,_,_=expected.split();zero=Image.new('L',expected.size,0);expected=Image.merge('RGB',(zero,red,zero))
   decoded=Image.open(out/actual[field]);assert decoded.mode==expected.mode and decoded.size==expected.size and decoded.tobytes()==expected.tobytes(),(key,mi,field)
 for material in manifest['materials']:
  for field in ['baseColor','normal','emission','metallicMap','occlusion']:
   if material[field]:
    path=out/material[field];payload.add(path);im=Image.open(path);assert max(im.size)<=1024
 if 'glove_from_body_part'in p.extra[key]:
  node=next(n for n in g['nodes']if n.get('name')==p.extra[key]['glove_from_body_part']);assert manifest['gloveMaterialIndex']==g['meshes'][node['mesh']]['primitives'][0].get('material',0)
 else:
  assert manifest['gloveMaterialIndex']==len(g['materials'])
  scalar=p.extra[key].get('glove_material_parameters',{}).get('metallic_specular',.5)*2
  assert manifest['materials'][manifest['gloveMaterialIndex']].get('specularFactor',1)==scalar
 payload.update([out/(key+'.json'),out/(key+'.meshbin')]);print('PASS',key,'sourceID',row['sourceIndex'],'exact complete source triangles',triangles,'surfaces',surface)
assert sum(x.stat().st_size for x in payload)<=p.BUDGET_BYTES
print('PASS',len(receipt['styles']),'actual source outfits;',len(digests),'mesh fingerprints; exact bound source inputs, complete attributes/indexes/parts and material pixels/factors, shared1024 textures, payload',sum(x.stat().st_size for x in payload),'bytes; no engine/import/device run')
