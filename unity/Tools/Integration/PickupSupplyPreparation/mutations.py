import json,subprocess,shutil,hashlib
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/PickupSupplyPreparationR1';C=B/'cpu06';O=B/'mutations02';O.mkdir();rows=[]
src=B/'sources/unity/Assets/Ashfall/Modules/Pickups/PreparedSupplyRemoval.cs';body=src.read_text()
changes=[('retain-source','item.Removed=true;','item.Removed=false;'),('omit-replay','owner.seen=commands;','owner.seen=owner.seen;'),('ignore-lifetime','Receipt.Lifetime.Same(life)','true'),('ignore-actual-clock','hostTick!=owner.now','false')]
for name,old,new in changes:
 d=O/name;d.mkdir()
 for p in C.glob('*.dll'):shutil.copyfile(p,d/p.name)
 shutil.copyfile(C/'SupplyTests.exe',d/'SupplyTests.exe');q=d/'PreparedSupplyRemoval.cs';assert old in body;q.write_text(body.replace(old,new))
 # Omit replay mutation must compile without a self-assignment warning.
 if name=='omit-replay':q.write_text(body.replace(old,'owner.seen=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.Dictionary<string,long>>(owner.seen);'))
 rsp=d/'Pickups.rsp';rsp.write_text((C/'Pickups.rsp').read_text().replace(str(C/'Pickups.dll'),str(d/'Pickups.dll')).replace(str(src),str(q)))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(d/'compile.log').write_text(c.stdout+c.stderr)
 t=subprocess.run([str(d/'SupplyTests.exe')],capture_output=True,text=True) if c.returncode==0 else None
 if t:(d/'test.log').write_text(t.stdout+t.stderr)
 rows.append(dict(name=name,command=cmd,compileExit=c.returncode,testExit=t.returncode if t else None,killed=c.returncode==0 and t.returncode!=0));print(rows[-1]);assert rows[-1]['killed']
(O/'results.json').write_text(json.dumps(rows,indent=2))
