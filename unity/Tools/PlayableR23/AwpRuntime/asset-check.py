from pathlib import Path
import hashlib,json,struct,wave
import numpy as np
from PIL import Image
T=Path(__file__).resolve().parent;S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');O=T/'Payload';proof=json.loads((T/'source-bindings.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
pins={r['path']:r['sha256']for r in json.loads((S.parent/'SOURCE-MANIFEST.json').read_text())['files']}
for rel,h in proof['inputs'].items():assert h==pins[rel]==sha(S/rel)
def read(name):
 meta=json.loads((O/(name+'.json')).read_text());raw=(O/(name+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d;at=8;rows=[]
 for i in range(count):
  nv,ni,mi=struct.unpack_from('<iii',raw,at);at+=12;v=np.frombuffer(raw,'<f4',nv*8,at).reshape(nv,8);at+=nv*32;indices=np.frombuffer(raw,'<i4',ni,at);at+=ni*4
  assert np.isfinite(v).all()and indices.min()>=0 and indices.max()<nv and ni%3==0 and 0<=mi<len(meta['materials']);assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=2e-5)
  rows.append((meta['parts'][i],v,indices,mi))
 assert at==len(raw);return meta,rows
raw=(S/'assets/models/download_awp/model.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);blob=raw[28+n:];source=json.loads((S/'assets/models/download_awp/asset-provenance.json').read_text())
def acc(i):
 a=g['accessors'][i];b=g['bufferViews'][a['bufferView']];dtype={5126:'<f4',5125:'<u4',5123:'<u2'}[a['componentType']];c={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']];dt=np.dtype(dtype)
 return np.ndarray((a['count'],c),dtype,blob,offset=b.get('byteOffset',0)+a.get('byteOffset',0),strides=(b.get('byteStride',dt.itemsize*c),dt.itemsize)).copy()
meta,rows=read('awp');row=0;counts={}
for node in g['nodes']:
 if 'mesh'not in node:continue
 for primitive in g['meshes'][node['mesh']]['primitives']:
  name,v,indices,mi=rows[row];row+=1;assert name==node['name'];home=np.array(source['magazine_center']if name=='Magazine'else source['bolt_home']if name=='Bolt'else[0,0,0]);expected=(acc(primitive['attributes']['POSITION'])+home)*[1,1,-1];assert np.allclose(v[:,:3],expected,atol=2e-7);normal=acc(primitive['attributes']['NORMAL']);normal=normal/np.linalg.norm(normal,axis=1)[:,None];normal*= [1,1,-1];assert np.allclose(v[:,3:6],normal,atol=2e-7);uv=acc(primitive['attributes']['TEXCOORD_0']);uv[:,1]=1-uv[:,1];assert np.allclose(v[:,6:8],uv,atol=1e-7);expectedIndices=(acc(primitive['indices'])if 'indices'in primitive else np.arange(len(v))).astype(np.int32).reshape(-1,3)[:,[0,2,1]].ravel();assert np.array_equal(indices,expectedIndices);counts[name]=counts.get(name,0)+len(indices)//3
assert counts=={'Body':20822,'Magazine':680,'Bolt':414},counts
for i,sourceMaterial in enumerate(g['materials']):
 pbr=sourceMaterial.get('pbrMetallicRoughness',{});actual=meta['materials'][i];assert actual['metallic']==pbr.get('metallicFactor',1)and actual['roughness']==pbr.get('roughnessFactor',1)and actual['color']==pbr.get('baseColorFactor',[1,1,1,1])
 for kind,key in [('baseColor','baseColorTexture'),('normal','normalTexture')]:
  tex=pbr.get(key)if kind=='baseColor'else sourceMaterial.get(key)
  if tex:
   im=g['images'][g['textures'][tex['index']]['source']];b=g['bufferViews'][im['bufferView']];data=blob[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']];assert (O/actual[kind]).read_bytes()==data
for arrow in ['awp-insert-arrow','awp-charge-arrow']:
 _,parts=read(arrow);assert len(parts)==3 and sum(len(r[2])//3 for r in parts)==108
_,chamber=read('awp-chamber');assert len(chamber)==1 and len(chamber[0][2])//3==12
assert (O/'awp-icon.png').read_bytes()==(S/'assets/ui/catalog094/weapon_5.png').read_bytes()
image=Image.open(O/'awp-icon.png');assert image.size[0]>0 and image.size[1]>0
for a in proof['audio']:
 p=O/a['file'];assert sha(p)==a['sha256']
 with wave.open(str(p),'rb')as w:assert w.getnframes()==a['frames']and w.getframerate()==a['rate']and w.getnchannels()==a['channels']and w.getsampwidth()==2;assert any(w.readframes(w.getnframes()))
assert sha(O/'awp-source-bolt-0.wav')==sha(S/'assets/audio/reference099/isolated_s85_bolt_00.wav')
print('PASS: every AWP GLB vertex/normal/UV/reflected triangle/material/embedded texture exact;21916source triangles +retained12 +two genuine108-triangle cue arrows; original icon, seven nonempty exact routed PCM clips and frozen pins. Engine/shader/device UNRUN.')
