from pathlib import Path
import subprocess,json,hashlib,struct,wave,io
import numpy as np
from PIL import Image
from prepare import ROOT,HERE,SRC,OUT
P=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';E=ROOT/'unity/Assets/Ashfall/Integration/Playable/Explosives'
sources=set(ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines());sources.update(P.glob('*.cs'));sources.update(E.glob('*.cs'));sources.add(HERE/'Cases.cs')
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(x)for x in sorted(sources)];rsp=HERE/'cases.rsp';rsp.write_text('\n'.join('"'+x+'"'for x in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
port=json.loads((OUT/'provenance.json').read_text());checks=0
for row in port['geometry']:
 raw=(SRC/row['source']).read_bytes();assert hashlib.sha256(raw).hexdigest()==row['sha256'];n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);blob=raw[28+n:];manifest=json.loads((OUT/(row['name']+'.json')).read_text());rawmesh=(OUT/(row['name']+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',rawmesh);assert magic==0x5232334d;cursor=8;triangles=0
 for i in range(count):
  nv,ni,mi=struct.unpack_from('<iii',rawmesh,cursor);cursor+=12;v=np.frombuffer(rawmesh,'<f4',nv*8,cursor).reshape(nv,8);cursor+=nv*32;ix=np.frombuffer(rawmesh,'<i4',ni,cursor);cursor+=ni*4;assert np.isfinite(v).all()and ix.min()>=0 and ix.max()<nv and mi<len(manifest['materials']);triangles+=ni//3;checks+=1
 assert cursor==len(rawmesh)and triangles==row['triangles'];checks+=1
 def image(index):
  im=g['images'][g['textures'][index]['source']];b=g['bufferViews'][im['bufferView']];return blob[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']]
 for i,m in enumerate(g['materials']):
  a=manifest['materials'][i];p=m.get('pbrMetallicRoughness',{})
  for field,tex in [('baseColor',p.get('baseColorTexture')),('normal',m.get('normalTexture')),('emission',m.get('emissiveTexture'))]:
   if tex:assert (OUT/a[field]).read_bytes()==image(tex['index']);checks+=1
  if p.get('metallicRoughnessTexture'):
   pixels=np.array(Image.open(io.BytesIO(image(p['metallicRoughnessTexture']['index']))).convert('RGB'));packed=np.array(Image.open(OUT/a['metallicMap']));assert np.array_equal(packed[:,:,0],np.rint(pixels[:,:,2]*p.get('metallicFactor',1)).astype(np.uint8))and np.array_equal(packed[:,:,3],np.rint(255-pixels[:,:,1]*p.get('roughnessFactor',1)).astype(np.uint8));checks+=1
for row in port['audio']:
 raw=(OUT/row['file']).read_bytes();assert hashlib.sha256(raw).hexdigest()==row['sha256']
 if not row.get('sourceRecipe'):assert raw==(SRC/row['source'].removeprefix('res://')).read_bytes()
 with wave.open(str(OUT/row['file']),'rb')as w:pcm=w.readframes(w.getnframes());assert w.getnframes()==row['frames']and w.getframerate()==row['rate']and w.getsampwidth()==2
 assert hashlib.sha256(pcm).hexdigest()==row['pcmSha256'];checks+=1
(HERE/'checks.json').write_text(json.dumps(dict(cpu=r.stdout.strip(),sourceAssetChecks=checks,payloadBytes=sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()),engineRun=False,deviceRun=False,listeningRun=False),indent=2)+'\n');print('PASS',checks,'source geometry/material/emission/WAV/PCM checks')
