from pathlib import Path
import subprocess,json,struct,hashlib,io
import numpy as np
from PIL import Image
T=Path(__file__).resolve().parent;ROOT=T.parents[3];P=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+[P/p for p in ['R23M1014Owner.cs','R23M1014Mechanics.cs','R23M1014Controls.cs','R23M1014Contact.cs','R23MagnumOwner.cs','R23MagnumRuntime.cs','R23FeedbackSourceData.cs','R23WeaponFeedbackState.cs','R23WeaponPickupRules.cs','R23MagazineControls.cs']]+[T/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(T/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in sources];rsp=T/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(T/'Cases.exe')],capture_output=True,text=True);(T/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
records=json.loads((OUT/'ump-p90-mp5-mp9-port.json').read_text());checks=0
for row in records['geometry']:
 name=row['id'];raw=(OUT/(name+'.meshbin')).read_bytes();magic,n=struct.unpack_from('<ii',raw);assert magic==0x5232334d;cursor=8;manifest=json.loads((OUT/(name+'.json')).read_text());assert n==len(manifest['parts']);triangles=0
 for index in range(n):
  nv,ni,mi=struct.unpack_from('<iii',raw,cursor);cursor+=12;v=np.frombuffer(raw,'<f4',nv*8,cursor).reshape(nv,8);cursor+=nv*32;indices=np.frombuffer(raw,'<i4',ni,cursor);cursor+=ni*4;assert np.isfinite(v).all()and indices.min()>=0 and indices.max()<nv and mi<len(manifest['materials']);assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-5);triangles+=ni//3;checks+=1
 assert cursor==len(raw)and triangles==row['triangles'];assert hashlib.sha256((SRC/row['source']).read_bytes()).hexdigest()==row['sha256'];fit=json.loads((OUT/(name+'-fit.json')).read_text());assert fit['sourceGLBTriangles']==row['sourceTriangles']and fit['visibleTriangles']==triangles and fit['ammoFamily']=='smg'and fit['reserve']==1200
 if name=='p90':assert all(m['alphaMode']=='OPAQUE'and m['color'][3]==1 for m in manifest['materials']);checks+=1
 print('PASS',name,'full source geometry',triangles,'triangles / display',fit['modelScale'],'reload',fit['reloadScale'])
for cue in records['audio']:assert (SRC/cue['source'].removeprefix('res://')).read_bytes()==(OUT/cue['file']).read_bytes();checks+=1
for name,digest in json.loads((T/'preserved-existing.json').read_text()).items():assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==digest,name;checks+=1
print('PASS',checks,'bounded source geometry/material/audio/existing-payload checks; no engine/GPU/device')
