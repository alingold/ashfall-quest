from pathlib import Path
import json,hashlib,struct,wave,numpy as np
T=Path(__file__).resolve().parent;R=T.parents[3];O=R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';S=R/'quest-r24/source';report=json.loads((O/'missing-items-provenance.json').read_text());checks=0
def test(condition,why):
 global checks;checks+=1;assert condition,why
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
manifest=R/'campaign-staging/20261104-three-chats/world/morning-r5/r5/delivery/SOURCE-CONTENT.json';test(sha(manifest)==report['frozenManifestSha256'],'frozen manifest');pins={x['path']:x['sha256']for x in json.loads(manifest.read_text())['files']}
for name,digest in report['inputs'].items():test(sha(S/name)==pins[name]==digest,name)
def read(name):
 raw=(O/(name+'.meshbin')).read_bytes();offset=0;magic,count=struct.unpack_from('<ii',raw);offset=8;test(magic==0x5232334d,'header '+name);parts=[];meta=json.loads((O/(name+'.json')).read_text());test(len(meta['parts'])==count,'parts '+name)
 for i in range(count):
  nv,ni,mi=struct.unpack_from('<iii',raw,offset);offset+=12;v=np.frombuffer(raw,dtype='<f4',count=nv*8,offset=offset).reshape(-1,8);offset+=nv*32;idx=np.frombuffer(raw,dtype='<i4',count=ni,offset=offset);offset+=ni*4;test(ni%3==0 and len(idx)>0 and idx.min()>=0 and idx.max()<nv,'indices '+name);test(np.isfinite(v).all(),'finite '+name);test(0<=mi<len(meta['materials']),'material '+name);parts.append((v,idx,mi))
 test(offset==len(raw),'no trailing bytes '+name);return meta,parts
counts={}
for name in ['smaw','smaw-round','smaw-arrow','smaw-plume','revive-paddle']:
 meta,parts=read(name);counts[name]=sum(len(i)//3 for v,i,m in parts)
 for m in meta['materials']:
  for key in ['baseColor','normal','metallicMap','occlusion']:
   if m.get(key):test((O/m[key]).is_file(),'actual texture '+name)
test(counts=={'smaw':5216,'smaw-round':1280,'smaw-arrow':288,'smaw-plume':768,'revive-paddle':3384},'full source triangle census')
# GLB texture bytes and transformed primitives are exact, retaining all materials.
raw=(S/'assets/models/download_smaw/model.glb').read_bytes();length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);blob=raw[28+length:];test(all(set(n)<=set(['name','mesh'])for n in g['nodes']),'source source-copy mesh-only node identity')
meta,parts=read('smaw');test(len(parts)==sum(len(m['primitives'])for m in g['meshes']),'all GLB surfaces')
for i,m in enumerate(g['materials']):
 for kind,key in [('baseColor','baseColorTexture'),('normal','normalTexture')]:
  texture=m['pbrMetallicRoughness'].get(key)if kind=='baseColor'else m.get(key)
  if texture:
   image=g['images'][g['textures'][texture['index']]['source']];view=g['bufferViews'][image['bufferView']];original=blob[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']];test((O/meta['materials'][i][kind]).read_bytes()==original,'pixel-identical GLB '+kind)
for name,original in report['audio'].items():
 test((O/(name+'.wav')).read_bytes()==(S/original).read_bytes(),'exact WAV '+name)
 with wave.open(str(O/(name+'.wav')))as w:test(w.getnframes()>0 and w.getframerate()>0,'positive source PCM '+name)
R6=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');m6=R6/'SOURCE-MANIFEST.json';test(sha(m6)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111','published R6 pin');r6pins={x['path']:x['sha256']for x in json.loads(m6.read_text())['files']};differences=[]
for name,digest in report['inputs'].items():
 test(sha(R6/'source'/name)==r6pins[name],'actual R6 frozen input '+name)
 if r6pins[name]!=digest:differences.append(name)
(T/'asset-results.json').write_text(json.dumps(dict(checks=checks,triangles=counts,sourceInputs=len(report['inputs']),r6ChangedInputs=differences,engine=False,device=False,gpu=False),indent=2)+'\n');print('PASS',checks,'geometry/source/exact WAV checks; R6 changed consumed inputs:',differences)
