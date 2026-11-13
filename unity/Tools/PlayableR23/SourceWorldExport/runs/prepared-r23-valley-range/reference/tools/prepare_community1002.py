"""Download only one selected map's referenced textures; strip unused GLB materials.
Asset bytes and derived data remain local, excluded from source and distribution.
"""
from pathlib import Path
import json,struct,hashlib,urllib.request,urllib.parse,sys
D=Path(sys.argv[1]).resolve()
blob=(D/'map.glb').read_bytes();n,kind=struct.unpack_from('<II',blob,12)
j=json.loads(blob[20:20+n]);meta=json.loads((D/'map.json').read_text());tex=json.loads((D/'tex.json').read_text())
keys=sorted({m['name'].split('_',3)[3] for m in j['meshes'] if m['name'].startswith(('world_','roof_'))})
materials={};dependencies=set()
for key in keys:
 if key in meta['materials']:row=meta['materials'][key].copy()
 elif key.startswith('t') and key[1:].isdigit():
  i=int(key[1:]);t=tex[i];row={'c':f'tex/{i}_c.jpg','name':t['name']}
  if t.get('n'):row['n']=f'tex/{i}_n.jpg'
 elif key=='vc':row={'vertex_color':True}
 else:raise ValueError('unknown material '+key)
 for k in ['c','n']:
  if k in row:
   uri=row[k];p=Path(uri)
   assert not p.is_absolute() and '..' not in p.parts and uri.startswith('tex/') and not urllib.parse.urlsplit(uri).scheme
   dependencies.add(uri)
 materials[key]=row
for uri in sorted(dependencies):
 p=D/uri;p.parent.mkdir(parents=True,exist_ok=True)
 if not p.exists():
  with urllib.request.urlopen('https://maps.deliciouswines.org/'+urllib.parse.quote(uri,safe='/'),timeout=45) as response:p.write_bytes(response.read())
 print(uri,p.stat().st_size)
binary_start=20+n;binary_len,binary_type=struct.unpack_from('<II',blob,binary_start)
assert binary_type==0x004e4942
binary=bytearray(blob[binary_start+8:binary_start+8+binary_len])
converted=0
for accessor in j['accessors']:
 if accessor['componentType']!=5120:continue
 assert accessor.get('normalized') and accessor['type']=='VEC3' and 'sparse' not in accessor
 view=j['bufferViews'][accessor['bufferView']];start=view.get('byteOffset',0)+accessor.get('byteOffset',0);stride=view.get('byteStride',3)
 values=[]
 for i in range(accessor['count']):values.extend(max(-1.0,v/127.0) for v in struct.unpack_from('<3b',binary,start+i*stride))
 binary.extend(b'\x00'*((-len(binary))%4));offset=len(binary);data=struct.pack('<'+'f'*len(values),*values);binary.extend(data)
 accessor.update({'bufferView':len(j['bufferViews']),'componentType':5126,'byteOffset':0});accessor.pop('normalized',None)
 j['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(data),'target':34962});converted+=1
j['buffers']=[{'byteLength':len(binary)}]
for key in ['extensionsRequired','extensionsUsed']:
 if key in j:j[key]=[v for v in j[key] if v!='KHR_mesh_quantization']
for key in ['images','textures','materials','samplers']:j.pop(key,None)
for m in j['meshes']:
 for primitive in m['primitives']:primitive.pop('material',None)
header=json.dumps(j,separators=(',',':')).encode();header+=b' '*((-len(header))%4)
binary.extend(b'\x00'*((-len(binary))%4));tail=struct.pack('<II',len(binary),0x004e4942)+binary
result=struct.pack('<III',0x46546C67,2,20+len(header)+len(tail))+struct.pack('<II',len(header),0x4E4F534A)+header+tail
(D/'geometry.glb').write_bytes(result)
files=[{'path':str(p.relative_to(D)).replace('\\','/'),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(D.rglob('*')) if p.is_file() and p.name!='provenance.json']
receipt={'title':meta['name'],'creator':meta['creator'],'slug':'hildrs-echo-gods-pitdevil','url':'https://maps.deliciouswines.org/maps/hildrs-echo-gods-pitdevil/','retrieved':'2026-10-27','license':'Unknown; local technical candidate only. No redistribution grant established. Not covered by ASHFALL Apache-2.0.','materials':materials,'files':files,'units':'glTF metres, right-handed Y-up; no Babylon mirror','rationale':'GODS-attributed community Sandbox map, moderate 678420 world triangles, layered combat structures and explicit boundaries. Only this map and referenced texture dependencies downloaded.'}
(D/'provenance.json').write_text(json.dumps(receipt,indent=2)+'\n')
print('DOWNLOAD_DONE',len(dependencies),'textures',sum(f['bytes'] for f in files),'bytes local total')
