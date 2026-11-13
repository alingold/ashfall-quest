from source import *
import struct,io,wave,numpy as np
from PIL import Image
P=HERE/'Payload';report=json.loads((HERE/'source-port.json').read_text());checks=0
def need(v,why):
 global checks
 assert v,why;checks+=1
for relative,digest in report['inputs'].items():need(sha(source(relative))==digest,relative)
def glb(path):
 b=path.read_bytes();need(struct.unpack_from('<II',b)==(0x46546c67,2),'actual glTF2');length=struct.unpack_from('<I',b,12)[0];g=json.loads(b[20:20+length]);return g,b[28+length:]
def array(g,b,index):
 a=g['accessors'][index];v=g['bufferViews'][a['bufferView']];dtype=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);size={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];return np.ndarray((a['count'],size),dtype=dtype,buffer=b,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dtype.itemsize*size),dtype.itemsize)).copy()
def texture(g,b,index):
 image=g['images'][g['textures'][index]['source']];v=g['bufferViews'][image['bufferView']];return b[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]
for record in report['geometry']:
 profile=record['id'];name='dual-'+profile;g,b=glb(source(record['source']));m=json.loads((P/(name+'.json')).read_text());fit=json.loads((P/(name+'-fit.json')).read_text());provenance=json.loads(source(str(Path(record['source']).parent/'asset-provenance.json').replace('\\','/')).read_text());mesh=(P/(name+'.meshbin')).read_bytes();magic,nparts=struct.unpack_from('<II',mesh);need(magic==0x5232334d and nparts==len(m['parts']),'R23Visuals exact header/part names');offset=8;total=0;part=0
 for node in g['nodes']:
  need(not any(k in node for k in ['matrix','translation','rotation','scale','skin']),'source runtime copies mesh with identity instance basis')
  if 'mesh' not in node:continue
  for primitive in g['meshes'][node['mesh']]['primitives']:
   nv,ni,mi=struct.unpack_from('<III',mesh,offset);offset+=12;actual=np.frombuffer(mesh,dtype='<f4',count=nv*8,offset=offset).reshape(nv,8);offset+=nv*32;indices=np.frombuffer(mesh,dtype='<i4',count=ni,offset=offset).reshape(-1,3);offset+=ni*4;a=primitive['attributes'];v=array(g,b,a['POSITION']);norm=array(g,b,a['NORMAL']);uv=array(g,b,a['TEXCOORD_0']);sourceindices=(array(g,b,primitive['indices']) if 'indices' in primitive else np.arange(len(v))).astype('i4').reshape(-1,3)
   if node['name']=='Magazine':v+=np.array(provenance['magazine_center'])
   if node['name']=='Bolt':v+=np.array(provenance['bolt_home'])
   v[:,2]*=-1;norm[:,2]*=-1;norm/=np.linalg.norm(norm,axis=1)[:,None];uv[:,1]=1-uv[:,1]
   need(m['parts'][part]==node['name'] and mi==primitive.get('material',0),'actual independent body/magazine material ownership')
   need(nv==len(v) and ni==sourceindices.size and np.array_equal(indices,sourceindices[:,[0,2,1]]),'complete original triangles and once-reflected winding')
   need(np.all(np.isfinite(actual)) and np.max(np.abs(actual[:,:3]-v))<1e-6,'actual source positions + moving-part local homes; Z reflect once')
   need(np.max(np.abs(actual[:,3:6]-norm))<1e-5 and np.max(np.abs(actual[:,6:8]-uv))<1e-6,'actual normalized source normals/UVs')
   total+=ni//3;part+=1
 need(offset==len(mesh) and total==record['geometry']['triangles'] and part==nparts,'full GLB census, no decimation/trailing bytes')
 for i,material in enumerate(g['materials']):
  meta=m['materials'][i];pbr=material.get('pbrMetallicRoughness',{});need(meta['color']==pbr.get('baseColorFactor',[1,1,1,1]) and meta['metallic']==pbr.get('metallicFactor',1) and meta['roughness']==pbr.get('roughnessFactor',1),'actual PBR source material factors')
  for field,tex in [('baseColor',pbr.get('baseColorTexture')),('normal',material.get('normalTexture'))]:
   if tex:need((P/meta[field]).read_bytes()==texture(g,b,tex['index']),'exact embedded '+field+' image bytes')
  if pbr.get('metallicRoughnessTexture'):
   rgb=np.array(Image.open(io.BytesIO(texture(g,b,pbr['metallicRoughnessTexture']['index']))).convert('RGB'));rgba=np.array(Image.open(P/meta['metallicMap']).convert('RGBA'));need(np.array_equal(rgba[:,:,0],rgb[:,:,2]) and np.array_equal(rgba[:,:,3],255-rgb[:,:,1]),'exact source metallic/roughness channel conversion')
  if material.get('occlusionTexture'):
   rgb=np.array(Image.open(io.BytesIO(texture(g,b,material['occlusionTexture']['index']))).convert('RGB'));occlusion=np.array(Image.open(P/meta['occlusion']).convert('RGB'));need(np.array_equal(occlusion[:,:,0],rgb[:,:,0]) and np.array_equal(occlusion[:,:,1],rgb[:,:,0]),'source actual occlusion R channel')
 need(fit['weaponGripOrigin']==[0,0,0] and fit['modelScale']==(.65436 if profile=='uzis'else.9),'source corrected-grip origin and active084 scale')
 need((P/(name+'-icon.png')).read_bytes()==source('assets/ui/catalog094/weapon_'+str(fit['catalogIndex'])+'.png').read_bytes(),'actual094 icon fallback used by frozen source')
for cue in report['audio']:
 p=P/cue['file'];need(sha(p)==cue['sha256']==sha(source(cue['source'].removeprefix('res://'))),'exact original licensed WAV bytes')
 with wave.open(str(p),'rb')as w:need(w.getsampwidth()==2 and w.getnframes()==cue['frames'] and w.getframerate()==cue['rate'] and w.getnchannels()==cue['channels'] and hashlib.sha256(w.readframes(w.getnframes())).hexdigest()==cue['pcmSha256'],'actual PCM/rate/channels/length identity')
need(sum(p.stat().st_size for p in P.iterdir())<10*1024*1024,'bounded6.3MB source payload')
result=dict(checks=checks,sourceManifestSha256=sha(FROZEN/'SOURCE-MANIFEST.json'),geometry=[dict(profile=x['id'],triangles=x['geometry']['triangles'],vertices=x['geometry']['vertices'])for x in report['geometry']],wavFiles=len(report['audio']),bytes=sum(p.stat().st_size for p in P.iterdir()),nativeRun=False,limits=['Unity/native/Quest rendering, audio listening, physical flick calibration and simultaneous source aim remain untested.'])
(HERE/'asset-checks.json').write_text(json.dumps(result,indent=2)+'\n');print(checks,'exact source pins/fullGLB triangles/part homes/material images/PCM/icons/fit checks PASS')

