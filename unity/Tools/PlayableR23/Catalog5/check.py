from pathlib import Path
import subprocess,json,struct,hashlib,io,wave,re
import numpy as np
from PIL import Image
from prepare import ROOT,HERE,SRC,OUT,IDS,FOLDERS
P=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
sources=set(ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines());sources.update(P.glob('*.cs'));sources.add(HERE/'Cases.cs')
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in sorted(sources)]
rsp=HERE/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
port=json.loads((HERE/'source-port.json').read_text());checks=0;triangles=0
for record in port['geometry']:
 name=record['id'];original=SRC/record['source'];raw=original.read_bytes();assert hashlib.sha256(raw).hexdigest()==record['sha256'];length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);blob=raw[28+length:];data=json.loads((original.parent/'asset-provenance.json').read_text())
 def acc(index):
  a=g['accessors'][index];v=g['bufferViews'][a['bufferView']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
  return np.ndarray((a['count'],width),dt,blob,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dt.itemsize*width),dt.itemsize)).copy()
 expected=[]
 for n in g['nodes']:
  if 'mesh'not in n:continue
  assert all(k not in n for k in ['translation','rotation','matrix','scale']),n
  label=n['name'];offset=np.zeros(3)
  if label in ['Magazine','Tactical1911Magazine']:offset=np.array(data['magazine_center'])
  elif label in ['Bolt','Slide','Tactical1911Slide']and name!='s85':offset=np.array(data['slide_home'if label in ['Slide','Tactical1911Slide']else'bolt_home'])
  label={'Tactical1911Body':'Body','Tactical1911Slide':'Slide','Tactical1911Magazine':'Magazine'}.get(label,label)
  for prim in g['meshes'][n['mesh']]['primitives']:
   a=prim['attributes'];pos=(acc(a['POSITION'])+offset)*[1,1,-1];normal=acc(a['NORMAL'])*[1,1,-1];normal/=np.linalg.norm(normal,axis=1)[:,None];uv=acc(a['TEXCOORD_0']);uv[:,1]=1-uv[:,1];ix=(acc(prim['indices'])if 'indices'in prim else np.arange(len(pos))).reshape(-1,3)[:,[0,2,1]].ravel();expected.append((label,np.c_[pos,normal,uv],ix,prim.get('material',0)))
 manifest=json.loads((OUT/(name+'.json')).read_text());raw=(OUT/(name+'.meshbin')).read_bytes();magic,n=struct.unpack_from('<ii',raw);assert magic==0x5232334d and n==len(expected)==len(manifest['parts']);cursor=8;total=0
 for index,(label,vertex,indexes,material)in enumerate(expected):
  nv,ni,mi=struct.unpack_from('<iii',raw,cursor);cursor+=12;v=np.frombuffer(raw,'<f4',nv*8,cursor).reshape(nv,8);cursor+=nv*32;ix=np.frombuffer(raw,'<i4',ni,cursor);cursor+=ni*4
  assert label==manifest['parts'][index]and mi==material and np.array_equal(ix,indexes)and np.allclose(v,vertex,atol=1e-7);checks+=1
  assert np.isfinite(v).all()and ix.min()>=0 and ix.max()<nv and np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-5);checks+=1;total+=ni//3
 assert cursor==len(raw)and total==record['triangles'];triangles+=total
 def image(index):
  im=g['images'][g['textures'][index]['source']];b=g['bufferViews'][im['bufferView']];return blob[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']]
 for i,m in enumerate(g['materials']):
  a=manifest['materials'][i];pbr=m.get('pbrMetallicRoughness',{});assert a['color']==pbr.get('baseColorFactor',[1,1,1,1])and a['metallic']==pbr.get('metallicFactor',1)and a['roughness']==pbr.get('roughnessFactor',1);checks+=1
  for field,texture in [('baseColor',pbr.get('baseColorTexture')),('normal',m.get('normalTexture'))]:
   if texture:assert (OUT/a[field]).read_bytes()==image(texture['index']);checks+=1
  if pbr.get('metallicRoughnessTexture'):
   src=np.array(Image.open(io.BytesIO(image(pbr['metallicRoughnessTexture']['index']))).convert('RGB'));packed=np.array(Image.open(OUT/a['metallicMap']));assert np.array_equal(packed[:,:,0],src[:,:,2])and np.array_equal(packed[:,:,3],255-src[:,:,1]);checks+=1
 fit=json.loads((OUT/(name+'-fit.json')).read_text());assert fit['sourceGLBTriangles']==total and fit['sourceVisibleTriangles']==total and fit['weaponGripOrigin']==[0,0,0];checks+=1
 assert (OUT/(name+'-ATTRIBUTION.txt')).read_bytes()==(original.parent/'ATTRIBUTION.txt').read_bytes();checks+=1
 print('PASS',name,total,'full original triangles; exact positions/homes/normals/UV/winding/materials')
for cue in port['audio']:
 raw=(OUT/cue['file']).read_bytes();assert raw==(SRC/cue['source'].removeprefix('res://')).read_bytes()and hashlib.sha256(raw).hexdigest()==cue['sha256']
 with wave.open(str(OUT/cue['file']),'rb')as w:pcm=w.readframes(w.getnframes());assert w.getsampwidth()==2 and w.getnframes()==cue['frames']and w.getframerate()==cue['sampleRate']
 assert hashlib.sha256(pcm).hexdigest()==cue['pcmSha256'];checks+=1
data=(P/'SourceCatalog5FeedbackData.cs').read_text();obs=json.loads((SRC/'assets/data/recoil_observed_067.json').read_text());endpoint=json.loads((SRC/'assets/data/recoil_endpoint_experiment_071.json').read_text())
for name,rows in [('RfbOne',endpoint['profiles']['rfb']['one']['samples_pitch_yaw_deg']),('Tec9One',obs['profiles']['tec9']['one']['samples']),('Tec9Two',obs['profiles']['tec9']['two']['samples'])]:
 numbers=[float(x.strip().removesuffix('f'))for x in re.search(name+r'=\{([^}]+)',data)[1].split(',')];assert np.allclose(numbers,np.array(rows).ravel(),atol=1e-8);checks+=1
files=[p for p in OUT.iterdir()if p.is_file()and p.name.startswith(tuple(IDS))];size=sum(p.stat().st_size for p in files);assert size<100000000
(HERE/'checks.json').write_text(json.dumps(dict(ownerResult=r.stdout.strip(),artAudioChecks=checks,currentOriginalTriangles=triangles,payloadBytes=size,sourceGlbSha256={r['id']:r['sha256']for r in port['geometry']},engineRun=False,renderRun=False,deviceRun=False),indent=2)+'\n')
print('PASS',checks,'source art/audio/trace checks; total',triangles,'triangles,',size,'bytes; no engine/GPU/device')

