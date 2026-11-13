from pathlib import Path
import hashlib,json,struct,wave
import numpy as np
from PIL import Image
T=Path(__file__).resolve().parent;S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');O=T/'Payload';proof=json.loads((T/'source-bindings.json').read_text());sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();pins={r['path']:r['sha256']for r in json.loads((S.parent/'SOURCE-MANIFEST.json').read_text())['files']}
for rel,h in proof['inputs'].items():assert h==pins[rel]==sha(S/rel)
def read(name):
 meta=json.loads((O/(name+'.json')).read_text());raw=(O/(name+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d;at=8;rows=[]
 for i in range(count):
  nv,ni,mi=struct.unpack_from('<iii',raw,at);at+=12;v=np.frombuffer(raw,'<f4',nv*8,at).reshape(nv,8);at+=nv*32;ind=np.frombuffer(raw,'<i4',ni,at);at+=ni*4;assert np.isfinite(v).all()and ind.min()>=0 and ind.max()<nv and ni%3==0 and 0<=mi<len(meta['materials']);assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=2e-5);rows.append((meta['parts'][i],v,ind,mi))
 assert at==len(raw);return meta,rows
counts={}
for folder,name,used in [('download_m60','m60-main',{'Body','Cover','Bolt'}),('download_m60_reload087','m60-donor',{'Magazine','Belt'})]:
 raw=(S/'assets/models'/folder/'model.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);blob=raw[28+n:]
 def acc(i):
  a=g['accessors'][i];b=g['bufferViews'][a['bufferView']];dtype={5126:'<f4',5125:'<u4',5123:'<u2'}[a['componentType']];c={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']];dt=np.dtype(dtype);return np.ndarray((a['count'],c),dtype,blob,offset=b.get('byteOffset',0)+a.get('byteOffset',0),strides=(b.get('byteStride',dt.itemsize*c),dt.itemsize)).copy()
 meta,rows=read(name);row=0
 for node in g['nodes']:
  if 'mesh'not in node or node['name']not in used:continue
  assert not any(k in node for k in ['translation','rotation','matrix','scale'])
  for primitive in g['meshes'][node['mesh']]['primitives']:
   part,v,ind,mi=rows[row];row+=1;assert part==node['name'];assert np.allclose(v[:,:3],acc(primitive['attributes']['POSITION'])*[1,1,-1],atol=2e-7)
   norm=acc(primitive['attributes']['NORMAL']);norm=norm/np.linalg.norm(norm,axis=1)[:,None];assert np.allclose(v[:,3:6],norm*[1,1,-1],atol=2e-7);uv=acc(primitive['attributes']['TEXCOORD_0']);uv[:,1]=1-uv[:,1];assert np.allclose(v[:,6:8],uv,atol=1e-7);expected=(acc(primitive['indices'])if'indices'in primitive else np.arange(len(v))).astype(np.int32).reshape(-1,3)[:,[0,2,1]].ravel();assert np.array_equal(ind,expected);counts[part]=counts.get(part,0)+len(ind)//3
 for i,source in enumerate(g['materials']):
  pbr=source.get('pbrMetallicRoughness',{});actual=meta['materials'][i];assert actual['metallic']==pbr.get('metallicFactor',1)and actual['roughness']==pbr.get('roughnessFactor',1)and actual['color']==pbr.get('baseColorFactor',[1,1,1,1]);assert actual['doubleSided']==source.get('doubleSided',False)
  for kind,key in [('baseColor','baseColorTexture'),('normal','normalTexture')]:
   tex=pbr.get(key)if kind=='baseColor'else source.get(key)
   if tex:
    im=g['images'][g['textures'][tex['index']]['source']];b=g['bufferViews'][im['bufferView']];data=blob[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']];assert (O/actual[kind]).read_bytes()==data
  packed=pbr.get('metallicRoughnessTexture')
  if packed:
   import io
   im=g['images'][g['textures'][packed['index']]['source']];b=g['bufferViews'][im['bufferView']];data=blob[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']];source=np.asarray(Image.open(io.BytesIO(data)).convert('RGB'));target=np.asarray(Image.open(O/actual['metallicMap']).convert('RGBA'));assert np.array_equal(target[:,:,0],source[:,:,2])and np.array_equal(target[:,:,3],255-source[:,:,1])
assert counts=={'Body':39895,'Cover':9457,'Bolt':470,'Magazine':3774,'Belt':3392},counts
_,arrow=read('m60-arrow');assert len(arrow)==3 and sum(len(r[2])//3 for r in arrow)==144
assert (O/'m60-icon.png').read_bytes()==(S/'assets/ui/catalog094/weapon_8.png').read_bytes()
for a in proof['audio']:
 p=O/a['file'];assert sha(p)==a['sha256']
 with wave.open(str(p),'rb')as w:assert w.getnframes()==a['frames']and w.getframerate()==a['rate']and w.getnchannels()==a['channels']and w.getsampwidth()==2;assert any(w.readframes(w.getnframes()))
rarity=json.loads((S/'assets/data/weapon_rarity_r6.json').read_text())['weapons']['m60'];assert rarity['magazine']==[75,90,105,120,135] and rarity['body']==[27]*5 and rarity['head']==[33]*5 and rarity['recoil']==rarity['spread']==[1]*5
reticle=json.loads((S/'assets/data/reference_062.json').read_text())['reticles']['m60'];assert (reticle['shape'],reticle['rest_px'],reticle['supported_rest_px'],reticle['stroke_px'],reticle['arm_px'],reticle['bloom_impulse_px'],reticle['bloom_tau_s'],reticle['bloom_cap_px'],reticle['unsupported_yellow'])==('bracket',98,58,3,14,67,.063,106,True)
print('PASS every original M60 main/donor vertex/normal/UV/reflected index/material/embedded texture/packed PBR channel;56988real source triangles +144source chevron; real icon,9 nonempty exact PCM clips and frozen pins. No engine/export/device.')
