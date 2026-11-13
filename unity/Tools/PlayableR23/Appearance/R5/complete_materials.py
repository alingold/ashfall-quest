"""Complete source scalar extensions in newly staged outfits, never existing12."""
import json,struct,uuid
from pathlib import Path
import prepare as p
receipt=json.loads((p.HERE/'full-r5-receipt.json').read_text());patched=[]
for row in receipt['styles']:
 key=row['id'];assert row['sourceIndex']not in list(range(8))+[53,74,75,76]
 path=p.resolve(row['sourceModel'])
 with path.open('rb')as f:f.seek(12);n=struct.unpack('<I',f.read(4))[0];f.seek(20);g=json.loads(f.read(n))
 manifest_path=p.OUT/(key+'.json');manifest=json.loads(manifest_path.read_text());changes=[]
 for mi,source in enumerate(g['materials']):
  ext=source.get('extensions',{})
  for field,extension,prop in [('emissionEnergy','KHR_materials_emissive_strength','emissiveStrength'),('specularFactor','KHR_materials_specular','specularFactor')]:
   value=ext.get(extension,{}).get(prop,1)
   if value!=1:manifest['materials'][mi][field]=value;changes.append((mi,field,value))
 config=p.extra[key]
 if 'glove_from_body_part'not in config and 'metallic_specular'in config.get('glove_material_parameters',{}):
  value=config['glove_material_parameters']['metallic_specular']*2
  if value!=1:manifest['materials'][manifest['gloveMaterialIndex']]['specularFactor']=value;changes.append((manifest['gloveMaterialIndex'],'gloveSpecularFactor',value))
 manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
 if changes:patched.append(dict(id=key,scalars=changes))
resources=p.ROOT/'unity/Assets/Ashfall/Integration/Playable/Appearance/Resources'
for path in [resources,resources/'R23AppearanceSpecular.shader']:
 meta=Path(str(path)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n'if path.is_dir()else''))
(p.HERE/'material-corrections.json').write_text(json.dumps(patched,indent=2)+'\n')
print('Completed actual source material extension scalars in',len(patched),'new outfits; existing12 unchanged')
