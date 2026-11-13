import json,subprocess,hashlib
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/ConstructionPreparedR4';O=B/'frame02';O.mkdir(exist_ok=False)
record=json.loads((B/'context/construction-frame-contract-validation.json').read_text(encoding='utf-8-sig'));row=record['sourceHashes'][-1];p=R/row['path'];b=p.read_bytes();assert hashlib.sha256(b).hexdigest().upper()==row['sha256'];q=O/'IndependentV2.cs';q.write_bytes(b)
inputs=json.loads((B/'run09/sources.json').read_text());rsp=O/'frame.rsp';exe=O/'frame.exe';rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/r:System.Runtime.Serialization.dll\n/main:IndependentV2\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+r['snapshot']+'"' for r in inputs)+'\n"'+str(q)+'"');cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(O/'compile.log').write_text(c.stdout+c.stderr);result=dict(command=cmd,compileExit=c.returncode,rootFixture=row,inputs=inputs)
if c.returncode==0:
 t=subprocess.run([str(exe)],capture_output=True,text=True);result.update(testExit=t.returncode,output=t.stdout+t.stderr);(O/'test.log').write_text(t.stdout+t.stderr);print(t.stdout[-1200:]+t.stderr)
else:print(c.stdout+c.stderr)
(O/'results.json').write_text(json.dumps(result,indent=2))
assert c.returncode==0 and result['testExit']==0

