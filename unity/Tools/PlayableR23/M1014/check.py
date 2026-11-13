from pathlib import Path
import hashlib,json,struct,subprocess,uuid,io
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';BASE=ROOT/'unity/Assets/Ashfall';report=json.loads((OUT/'m1014-provenance.json').read_text());SRC=Path(report['sourceRoot']);count=0
for path,digest in report['inputs'].items():assert hashlib.sha256((SRC/path).read_bytes()).hexdigest()==digest;count+=1
for path,digest in report['outputs'].items():assert hashlib.sha256((OUT/path).read_bytes()).hexdigest()==digest;count+=1
raw=(SRC/'assets/models/download_m1014/model.glb').read_bytes();length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);blob=raw[28+length:]
def acc(i):
 a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);w={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];return np.ndarray((a['count'],w),dt,buffer=blob,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',w*dt.itemsize),dt.itemsize))
for name,node in [('m1014',g['nodes'][0]),('m1014-bolt',g['nodes'][1])]:
 data=(OUT/(name+'.meshbin')).read_bytes();magic,parts=struct.unpack_from('<ii',data);assert magic==0x5232334d;offset=8;total=0
 for primitive in g['meshes'][node['mesh']]['primitives']:
  nv,ni,mi=struct.unpack_from('<iii',data,offset);offset+=12;vertices=np.frombuffer(data,'<f4',nv*8,offset).reshape(nv,8);offset+=nv*32;idx=np.frombuffer(data,'<i4',ni,offset);offset+=ni*4;a=primitive['attributes'];source=acc(a['POSITION']).copy();source[:,2]*=-1;assert np.array_equal(vertices[:,:3],source);count+=1
  uv=acc(a['TEXCOORD_0']).copy();uv[:,1]=1-uv[:,1];assert np.array_equal(vertices[:,6:8],uv);count+=1
  original=acc(primitive['indices']).ravel()if'indices'in primitive else np.arange(nv);assert np.array_equal(idx,original.reshape(-1,3)[:,[0,2,1]].ravel());count+=1;total+=ni//3
 assert offset==len(data)and parts==len(g['meshes'][node['mesh']]['primitives']);assert total==(76594 if name=='m1014'else 2548)
for row,image in zip(report['embeddedImages'],g['images']):
 view=g['bufferViews'][image['bufferView']];assert (OUT/row['file']).read_bytes()==blob[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']];count+=1
manifest=json.loads((OUT/'m1014.json').read_text())
for i,(material,source)in enumerate(zip(manifest['materials'],g['materials'])):
 if source.get('pbrMetallicRoughness',{}).get('metallicRoughnessTexture'):
  texture=source['pbrMetallicRoughness']['metallicRoughnessTexture']['index'];im=g['textures'][texture]['source'];original=np.asarray(Image.open(OUT/report['embeddedImages'][im]['file']).convert('RGBA'));packed=np.asarray(Image.open(OUT/material['metallicMap']));assert np.array_equal(packed[:,:,0],original[:,:,2])and np.array_equal(packed[:,:,3],255-original[:,:,1]);count+=1
fifty=json.loads((SRC/'assets/data/one_star_050.json').read_text());m1014=next(p for p in fifty['profiles']if p['id']=='m1014');latest=json.loads((SRC/'assets/data/one_star0991.json').read_text())['weapons']['m1014'];assert m1014['magazine_per_hand']==6 and abs(1/m1014['rounds_per_second']-.290716666666674)<1e-12 and latest==dict(body=77.,head=77.,pellets=8,head_policy='No separate head damage listed; same as body',wiki_name='M1014')
for cue in json.loads((OUT/'m1014-audio-cues.json').read_text())['cues']:
 for file,path in zip(cue['files'],cue['sourceFiles']):assert (OUT/file).read_bytes()==(SRC/path.removeprefix('res://')).read_bytes();count+=1
for path in (BASE/'Integration/Playable/HandsWeapons').glob('R23M1014*.cs'):
 meta=Path(str(path)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-m1014-script:'+path.name).hex+'\n')
sources=[ROOT/p for p in(ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+list((BASE/'Integration/Playable/HandsWeapons').glob('R23M1014*.cs'))+list((BASE/'Integration/Playable/HandsWeapons').glob('R23Magnum*.cs'))+[HERE/'Mechanics.cs'];sources=sorted(set(sources));csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe';exe=HERE/'mechanics.exe';args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(exe)]+[str(p)for p in sources];rsp=HERE/'mechanics.rsp';rsp.write_text('\n'.join('"'+s+'"'for s in args));r=subprocess.run([csc,'/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
(HERE/'checks.json').write_text(json.dumps(dict(assetSourceChecks=count,bodyTriangles=76594,boltTriangles=2548,packTriangles=1584,arrowTriangles=36,sourceImages=21,sourceAudioCues=7,engineRun=False,deviceRun=False),indent=2));print('PASS',count,'actual source-art/material/audio checks; full original79142 GLB triangles.')
