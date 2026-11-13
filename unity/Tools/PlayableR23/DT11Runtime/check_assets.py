from pathlib import Path
import json,hashlib,struct,wave,re,base64,math
import numpy as np
from PIL import Image
T=Path(__file__).resolve().parent;R=T.parents[3];O=T/'Payload';S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');F=S.parent/'SOURCE-MANIFEST.json';report=json.loads((O/'dt11-provenance.json').read_text());pins={r['path']:r['sha256']for r in json.loads(F.read_text())['files']};n=0

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def need(x,why):
 global n;n+=1;assert x,why
need(sha(F)==report['manifestSha256']=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111','frozen R6 exact manifest')
for path,digest in report['inputs'].items():need(sha(S/path)==pins[path]==digest,path)
raw=(S/'assets/models/download_dt11/model.glb').read_bytes();size=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+size]);blob=raw[28+size:]
def acc(i):
 a=g['accessors'][i];b=g['bufferViews'][a['bufferView']];width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);return np.ndarray((a['count'],width),dt,blob,offset=b.get('byteOffset',0)+a.get('byteOffset',0),strides=(b.get('byteStride',dt.itemsize*width),dt.itemsize)).copy()
expected={'dt11':4262,'dt11-barrels':1584,'dt11-chambers':800,'dt11-pair':288,'dt11-ejected':120,'dt11-arrow':36};total=0
for id,count in expected.items():
 data=(O/(id+'.meshbin')).read_bytes();magic,parts=struct.unpack_from('<ii',data);meta=json.loads((O/(id+'.json')).read_text());need(magic==0x5232334d and parts==len(meta['parts']),'actual binary format '+id);at=8;tris=0;output=[]
 for i in range(parts):
  nv,ni,mat=struct.unpack_from('<iii',data,at);at+=12;v=np.frombuffer(data,'<f4',nv*8,at).reshape(-1,8);at+=nv*32;indices=np.frombuffer(data,'<i4',ni,at);at+=ni*4;tris+=ni//3;need(np.isfinite(v).all()and indices.min()>=0 and indices.max()<nv and ni%3==0,'real full finite indexed geometry '+id);need(np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-5),'actual transformed unit normals '+id);need(0<=mat<len(meta['materials']),'actual material binding '+id);output.append((v,indices,mat))
 need(at==len(data)and tris==count,'all source triangles retained '+id);total+=tris
 for m in meta['materials']:
  need(m['alphaMode']=='OPAQUE'and m['color'][3]==1,'actual solid_import_085 for DT11')
  for key in ['baseColor','normal','metallicMap','occlusion']:
   if m.get(key):need((O/m[key]).is_file(),'actual source texture exists '+id)
 if id in ['dt11','dt11-barrels']:
  node=next(x for x in g['nodes']if x['name']==('Body'if id=='dt11'else'Barrels'));need(set(node)=={'name','mesh'},'source actual baked identity')
  for p,(v,indices,mat)in zip(g['meshes'][node['mesh']]['primitives'],output):
   original=acc(p['attributes']['POSITION']);original[:,2]*=-1;normals=acc(p['attributes']['NORMAL']);normals[:,2]*=-1;normals/=np.linalg.norm(normals,axis=1)[:,None];need(np.allclose(v[:,:3],original,atol=1e-7)and np.allclose(v[:,3:6],normals,atol=1e-6),'exact original source vertices/relative hinge, no double home '+id);need(np.array_equal(indices,(acc(p['indices']).astype('<i4')if'indices'in p else np.arange(len(v),dtype='<i4')).reshape(-1,3)[:,[0,2,1]].ravel()),'entire source faces once '+id)
   sourceMat=g['materials'][p['material']];m=meta['materials'][mat]
   for kind,key in [('baseColor','baseColorTexture'),('normal','normalTexture')]:
    tex=sourceMat.get('pbrMetallicRoughness',{}).get(key)if kind=='baseColor'else sourceMat.get(key)
    if tex:
     image=g['images'][g['textures'][tex['index']]['source']];b=g['bufferViews'][image['bufferView']];png=blob[b.get('byteOffset',0):b.get('byteOffset',0)+b['byteLength']];need((O/m[kind]).read_bytes()==png,'pixel-identical embedded original '+id+'/'+kind)
fit=json.loads((O/'dt11-fit.json').read_text());need(fit['modelScale']==1.6 and fit['reloadScale']==1.6 and fit['hinge']==[0,-.135,.13]and fit['barrelsVerticesRelativeHinge'],'source1.6 scale once and actualhinge');need(fit['muzzle']==[0,.04,.65]and fit['sourceDefaultModel']=='download_dt11'and fit['sourceVisibleBodyTriangles']==5846,'actual default model not optional sawedoff');need(abs(fit['renderedHandPhysicalOffset'][1]-.127*math.cos(math.radians(35)))<1e-9 and abs(fit['renderedHandPhysicalOffset'][2]-(.127*math.sin(math.radians(35))-.0127))<1e-9 and fit['renderedHandPitchDegreesUnity']==35,'source glove-only calibration exact')
for cue in report['audio']:need((O/cue['file']).read_bytes()==(S/cue['source']).read_bytes(),'exact source routed mechanism WAV '+cue['file'])
with wave.open(str(O/'dt11-authored-blast.wav'))as w:
 need(w.getparams()[:4]==(1,2,32000,20800),'source authored procedural blast format, not source recording');samples=np.frombuffer(w.readframes(w.getnframes()),'<i2').astype(float);need(np.max(np.abs(samples))<=27852 and np.sqrt(np.mean(samples[-3000:]**2))<np.sqrt(np.mean(samples[:3000]**2))*.1,'source original transient formula produces bounded real audible decay')
need(report['blast']['kind']=='source procedural shotgun._bake_cue'and 'no native Godot PCM'in report['blast']['limits'],'procedural provenance does not falsely claim recording/engine proof')
code=(T/'Candidate/R23WeaponWheelIcons.cs').read_text();m=re.search(r'case "dt11":return System.Convert.FromBase64String\("([^"]+)"\);',code);need(m is not None and base64.b64decode(m[1])==(S/'assets/ui/catalog094/weapon_1.png').read_bytes(),'actual source1 icon byteidentity')
live=json.loads((R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons/audio.json').read_text())['cues'];merged=json.loads((T/'Candidate/audio.json').read_text())['cues'];need((merged==live and len([x for x in live if x['weapon']=='dt11'])==4) if any(x['weapon']=='dt11'for x in live) else merged[:len(live)]==live and len(merged)==len(live)+4,'shared cues untouched, bounded additive DT11 mapping');need(next(x for x in merged if x['weapon']=='dt11'and x['action']=='shot')['files']==['dt11-authored-blast.wav'],'actual source procedural fire route intentionally distinct')
published=R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
if (published/'dt11-fit.json').exists():
 for payload in O.iterdir():need((published/payload.name).read_bytes()==payload.read_bytes(),'published exact source-bound payload '+payload.name)
 need((R/'unity/Assets/Ashfall/Integration/Playable/Input/R23WeaponWheelIcons.cs').read_bytes()==(T/'Candidate/R23WeaponWheelIcons.cs').read_bytes(),'published exact source icon binding')
(T/'asset-results.json').write_text(json.dumps(dict(checks=n,sourceInputs=len(report['inputs']),triangles=total,bodyBarrelsTriangles=5846,engine=False,gpu=False,device=False,nativePcmEquality=False),indent=2)+'\n');print('PASS',n,'actual frozen DT11 geometry/PBR/texture/audio/icon/calibration bindings;',total,'triangles, no engine')
