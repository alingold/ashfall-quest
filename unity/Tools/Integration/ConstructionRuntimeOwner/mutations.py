import json,subprocess,shutil,hashlib
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeOwnerR5/run02'; C=B/'cpu08'; O=B/'mutations02';O.mkdir(exist_ok=False)
src=B/'sources/unity/Assets/Ashfall/Integration/ConstructionTransactions/CanonicalBuilder.cs';text=src.read_text();results=[]
for name,old,new in [('player-generation','frames.Generation!=playerGeneration','false'),('session-incarnation','!Object.ReferenceEquals(incarnation,session.GetConnectionIdentity(admittedMember))','false'),('host-principal','session.Id,hostActor,"construction-observe"','session.Id,actor,"construction-observe"'),('admitted-member','epoch,true),admittedMember,actor,life.ConnectionGeneration','epoch,true),connection,actor,life.ConnectionGeneration')]:
 d=O/name;d.mkdir();assert old in text
 for p in list(C.glob('*.dll'))+list(C.glob('*.exe')):shutil.copyfile(p,d/p.name)
 q=d/'CanonicalBuilder.cs';q.write_text(text.replace(old,new));rsp=d/'compile.rsp';base=(C/'ConstructionIntegration.rsp').read_text().replace(str(C/'ConstructionIntegration.dll'),str(d/'ConstructionIntegration.dll')).replace(str(src),str(q));rsp.write_text(base)
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(d/'compile.log').write_text(c.stdout+c.stderr)
 t=subprocess.run([str(d/'OwnerTests.exe')],capture_output=True,text=True) if c.returncode==0 else None
 if t:(d/'tests.log').write_text(t.stdout+t.stderr)
 results.append(dict(name=name,command=cmd,old=old,new=new,compileExit=c.returncode,testExit=t.returncode if t else None,killed=c.returncode==0 and t.returncode!=0));print(name,results[-1])
(O/'results.json').write_text(json.dumps(results,indent=2));assert all(r['killed'] for r in results)
