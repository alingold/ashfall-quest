from pathlib import Path
import hashlib,json,struct,subprocess,wave
import numpy as np
from PIL import Image
from source import ROOT,read,INDEX
HERE=Path(__file__).resolve().parent;BASE=ROOT/'unity/Assets/Ashfall';OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Consumables/Supplies'
source=json.loads((OUT/'source-r5.json').read_text());checks=0
for row in source['inputs']:assert hashlib.sha256(read(row['path']).read_bytes()).hexdigest()==row['sha256'];checks+=1
for row in source['models']:
 data=(OUT/(row['family']+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',data);assert magic==0x52323343 and count==row['surfaces'];at=8;triangles=0;points=[]
 for _ in range(count):
  size,=struct.unpack_from('<i',data,at);at+=4+size;nv,ni,mi,cap=struct.unpack_from('<iiii',data,at);at+=16;v=np.frombuffer(data,'<f4',nv*6,at).reshape(nv,6);at+=nv*24;ix=np.frombuffer(data,'<i4',ni,at);at+=ni*4
  assert np.isfinite(v).all() and np.allclose(np.linalg.norm(v[:,3:],axis=1),1,atol=.0001) and ix.min()>=0 and ix.max()<nv and mi<count and cap==0;triangles+=ni//3;points.extend(v[:,:3])
 assert at==len(data) and triangles==row['triangles'];points=np.array(points)
 if row['family']=='backpack':assert np.allclose(points.min(0),[-.15,-.165,-.106],atol=.006)and np.allclose(points.max(0),[.15,.204,.12],atol=.006)
 else:assert np.allclose(points.min(0),[-.112,-.075,-.073],atol=.006) and np.allclose(points.max(0),[.112,.0895,.073],atol=.006)
 checks+=1
 if row['glyph']:
  image=Image.open(OUT/row['glyph']);assert image.mode=='RGBA' and image.size==(240,160) and np.array(image)[:,:,3].max()==255 and np.array(image)[:,:,3].min()==0;checks+=1
for row in source['audio']:
 data=(OUT/row['file']).read_bytes();assert hashlib.sha256(data).hexdigest()==row['sha256'] and data==Path(row['source']).read_bytes()
 with wave.open(str(OUT/row['file']),'rb')as w:assert w.getnframes()==row['frames']and w.getsampwidth()==2 and w.getframerate()==row['rate']and w.getnchannels()==row['channels']and hashlib.sha256(w.readframes(w.getnframes())).hexdigest()==row['pcmSha256']
 checks+=1
files=set(ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines());files.update((BASE/'Integration/Playable/HandsWeapons').glob('*.cs'));files.update((BASE/'RecoveryContracts').glob('*.cs'));files.update((BASE/'Modules/Pickups').glob('*.cs'));files.add(BASE/'Modules/InventoryItems/BuildChargeLedger.cs');files.update([BASE/'Integration/Playable/Consumables/R23ConsumableState.cs',BASE/'Integration/Playable/Consumables/R24SupplyState.cs',HERE/'Cases.cs'])
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in sorted(files)]
args.append(str(BASE/'Integration/Playable/Consumables/LocalPickupRelocation.cs'))
args.append(str(BASE/'Integration/Playable/Explosives/R23GrenadeState.cs'))
rsp=HERE/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
(HERE/'checks.json').write_text(json.dumps(dict(cpu=r.stdout.strip(),sourcePayloadChecks=checks,payloadBytes=sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()),engineRun=False,deviceRun=False),indent=2)+'\n');print('PASS',checks,'pinned R6 geometry/image/WAV checks')
