from pathlib import Path
import hashlib,json,struct,wave,numpy as np
T=Path(__file__).resolve().parent;O=T/'Payload';S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');F=S.parent/'SOURCE-MANIFEST.json';report=json.loads((O/'knife-provenance.json').read_text());pins={r['path']:r['sha256']for r in json.loads(F.read_text())['files']};checks=0
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def test(value,why):
 global checks;checks+=1;assert value,why
test(sha(F)==report['manifestSha256'],'actual published R6 manifest')
for name,digest in report['inputs'].items():test(sha(S/name)==pins[name]==digest,name)
raw=(O/'knife.meshbin').read_bytes();magic,count=struct.unpack_from('<ii',raw);test(magic==0x5232334d,'native visual header');meta=json.loads((O/'knife.json').read_text());test(len(meta['parts'])==count,'fullsource parts');at=8;triangles=0
for part in range(count):
 nv,ni,mat=struct.unpack_from('<iii',raw,at);at+=12;v=np.frombuffer(raw,'<f4',nv*8,at).reshape(-1,8);at+=nv*32;idx=np.frombuffer(raw,'<i4',ni,at);at+=ni*4;triangles+=ni//3;test(np.isfinite(v).all(),'finite source vertices');test(ni%3==0 and idx.min()>=0 and idx.max()<nv,'complete actual indices');test(0<=mat<len(meta['materials']),'source materials')
test(at==len(raw)and triangles==1142,'entire current source1142 triangles retained')
source=(S/'assets/models/download_knife/model.glb').read_bytes();size=struct.unpack_from('<I',source,12)[0];g=json.loads(source[20:20+size]);blob=source[28+size:];test(all(set(n)<=set(['name','mesh'])for n in g['nodes']),'source downloaded mesh-only copy identity')
for i,m in enumerate(g['materials']):
 for kind,key in [('baseColor','baseColorTexture'),('normal','normalTexture')]:
  tex=m.get('pbrMetallicRoughness',{}).get(key)if kind=='baseColor'else m.get(key)
  if tex:
   im=g['images'][g['textures'][tex['index']]['source']];view=g['bufferViews'][im['bufferView']];original=blob[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']];test((O/meta['materials'][i][kind]).read_bytes()==original,'exact actual embedded '+kind)
test((O/'knife-swing.wav').read_bytes()==(S/report['sourceWhoosh']).read_bytes(),'source exact sword_swing fallback routed to knife')
with wave.open(str(O/'knife-swing.wav'))as w:test(w.getnframes()>0 and w.getframerate()>0,'real original PCM')
test((O/'knife-icon.png').read_bytes()==(S/'assets/ui/catalog094/weapon_29.png').read_bytes(),'source29 original icon')
fit=json.loads((O/'knife-fit.json').read_text());test(fit['modelScale']==1 and fit['bladeBase']==[0,.025,-.025]and fit['bladeTip']==[0,.246,-.025]and fit['renderedHandPhysicalOffset']==[0,0,-.0508],'source marker/grip calibration, no guessed bore')
(T/'asset-results.json').write_text(json.dumps(dict(checks=checks,triangles=triangles,inputs=len(report['inputs']),engine=False,device=False,gpu=False),indent=2)+'\n');print('PASS',checks,'staged exact knife GLB/texture/audio/icon/source cases; no Assets writes')
