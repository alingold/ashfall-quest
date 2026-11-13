from pathlib import Path
import hashlib,json,struct,wave,base64,re
import numpy as np
T=Path(__file__).resolve().parent;O=T/'Payload';S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');F=S.parent/'SOURCE-MANIFEST.json';report=json.loads((O/'blade-provenance.json').read_text());pins={r['path']:r['sha256']for r in json.loads(F.read_text())['files']};n=0

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def need(x,why):
 global n;n+=1;assert x,why
need(sha(F)==report['manifestSha256']=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111','frozen R6 exact manifest')
for path,digest in report['inputs'].items():need(sha(S/path)==pins[path]==digest,path)
for id,catalog,count in [('katana',14,1388),('excalibur',15,1020),('cyberblade',16,3972)]:
 raw=(O/(id+'.meshbin')).read_bytes();magic,parts=struct.unpack_from('<ii',raw);meta=json.loads((O/(id+'.json')).read_text());at=8;tris=0;need(magic==0x5232334d and len(meta['parts'])==parts,'real source mesh format '+id)
 for _ in range(parts):
  nv,ni,mat=struct.unpack_from('<iii',raw,at);at+=12;v=np.frombuffer(raw,'<f4',nv*8,at).reshape(-1,8);at+=nv*32;idx=np.frombuffer(raw,'<i4',ni,at);at+=ni*4;tris+=ni//3;need(np.isfinite(v).all() and idx.min()>=0 and idx.max()<nv and ni%3==0,'actual full source finite indexed body '+id);need(0<=mat<len(meta['materials']),'actual material binding '+id)
 need(tris==count and at==len(raw),'entire visible source triangle count '+id)
 for material in meta['materials']:
  for key in ['baseColor','normal','metallicMap','occlusion']:
   if material.get(key):need((O/material[key]).is_file(),'complete source texture binding '+id)
 fit=json.loads((O/(id+'-fit.json')).read_text());need(fit['catalog']==catalog and fit['modelScale']==1 and fit['bladeBase']==[0,.125,-.025]and fit['bladeTip'][1]>1.11,'source measured marker identity '+id);need(fit['renderedHandPhysicalOffset']==[0,0,-.0381]and fit['supportPoint']==[0,-.190,-.025],'actual source grip/hilt '+id);need((O/(id+'-icon.png')).read_bytes()==(S/('assets/ui/catalog094/weapon_'+str(catalog)+'.png')).read_bytes(),'original exact icon '+id)
for name,path in report['audio'].items():
 need((O/(name+'.wav')).read_bytes()==(S/path).read_bytes(),'actual exact source routed cue '+name)
 with wave.open(str(O/(name+'.wav')))as w:need(w.getnframes()>0 and w.getframerate()>0,'actual PCM '+name)
need(abs(report['sourceMeasurementFactor']-48/41.5)<1e-12,'source measurement factor not stale comments')
# Confirm newly embedded icon source bytes without emitting their data.
p=T/'Candidate/R23WeaponWheelIcons.cs'
if p.exists():
 code=p.read_text()
 for id in ['katana','excalibur','cyberblade']:
  b=re.search(r'case "'+id+r'":return System.Convert.FromBase64String\("([^"]+)"\);',code);need(b is not None and base64.b64decode(b[1])==(O/(id+'-icon.png')).read_bytes(),'embedded original icon '+id)
(T/'asset-results.json').write_text(json.dumps(dict(checks=n,sourceInputs=len(report['inputs']),triangles=6380,engine=False,gpu=False,device=False),indent=2)+'\n');print('PASS',n,'exact frozen source sword art/audio/icon/calibration cases; no engine')
