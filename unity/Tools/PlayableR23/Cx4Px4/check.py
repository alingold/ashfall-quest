from pathlib import Path
import subprocess,json,struct,hashlib,io
import numpy as np
from PIL import Image
T=Path(__file__).resolve().parent;ROOT=T.parents[3];P=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+[P/p for p in ['R23M1014Owner.cs','R23M1014Mechanics.cs','R23M1014Controls.cs','R23M1014Contact.cs','R23MagnumOwner.cs','R23MagnumRuntime.cs','R23FeedbackSourceData.cs','R23WeaponFeedbackState.cs','R23WeaponPickupRules.cs']]+[T/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(T/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in sources];rsp=T/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(T/'Cases.exe')],capture_output=True,text=True);(T/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
checks=0
for name,total,sourceTotal in [('cx4',6188,6060),('px4',5400,5400)]:
 data=(OUT/(name+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',data);assert magic==0x5232334d;offset=8;vertices=triangles=0;manifest=json.loads((OUT/(name+'.json')).read_text());assert count==len(manifest['parts']);parts=[]
 for index in range(count):
  nv,ni,mi=struct.unpack_from('<iii',data,offset);offset+=12;v=np.frombuffer(data,'<f4',nv*8,offset).reshape(nv,8);offset+=nv*32;indices=np.frombuffer(data,'<i4',ni,offset);offset+=ni*4;assert np.isfinite(v).all()and indices.min()>=0 and indices.max()<nv and mi<len(manifest['materials']);assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-5);vertices+=nv;triangles+=ni//3;parts.append((manifest['parts'][index],ni//3));checks+=1
 assert offset==len(data)and triangles==total
 expected={'Body':5761,'Magazine':299,'SourceBarrelExtension':96,'Bolt':32}if name=='cx4' else {'Body':2143,'Magazine':732,'Slide':2525};assert dict(parts)==expected;checks+=1
 original=SRC/f'assets/models/download_{name}/model.glb';raw=original.read_bytes();length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);blob=raw[28+length:]
 def image(i):
  row=g['images'][g['textures'][i]['source']];view=g['bufferViews'][row['bufferView']];return blob[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
 for index,mat in enumerate(g['materials']):
  pbr=mat.get('pbrMetallicRoughness',{});actual=manifest['materials'][index]
  for field,texture in [('baseColor',pbr.get('baseColorTexture')),('normal',mat.get('normalTexture'))]:
   if texture:assert (OUT/actual[field]).read_bytes()==image(texture['index']);checks+=1
  if pbr.get('metallicRoughnessTexture'):
   source=np.array(Image.open(io.BytesIO(image(pbr['metallicRoughnessTexture']['index']))).convert('RGB'));packed=np.array(Image.open(OUT/actual['metallicMap']));assert np.array_equal(packed[:,:,0],source[:,:,2])and np.array_equal(packed[:,:,3],255-source[:,:,1]);checks+=1
 fit=json.loads((OUT/f'{name}-fit.json').read_text());assert fit['sourceGLBTriangles']==sourceTotal and fit['sourceVisibleTriangles']==total and fit['weaponGripOrigin']==[0,0,0];print('PASS',name,'full original geometry',triangles,'triangles /',vertices,'vertices, partitions/texture bytes/source packing')
manifest=json.loads((OUT/'audio.json').read_text());port=json.loads((OUT/'cx4-px4-port.json').read_text())
for cue in port['audio']:
 assert hashlib.sha256((OUT/cue['file']).read_bytes()).hexdigest()==cue['sha256']==hashlib.sha256((SRC/cue['source'].removeprefix('res://')).read_bytes()).hexdigest();checks+=1
for name,digest in json.loads((T/'preserved-existing.json').read_text()).items():assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==digest;checks+=1
print('PASS',checks,'focused source asset/audio/existing-payload checks')
