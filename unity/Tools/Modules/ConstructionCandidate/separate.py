import json,subprocess,hashlib
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/ConstructionPreparedR4';O=B/'separate03';O.mkdir(exist_ok=False)
idx=json.loads((B/'run09/sources.json').read_text());lookup={r['original'].replace('\\','/'):r['snapshot'] for r in idx}
results=[]
def compile(name,sources,refs):
 out=O/(name+'.dll');rsp=O/(name+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:library\n/out:"'+str(out)+'"\n'+ '\n'.join('/r:"'+str(p)+'"' for p in refs)+'\n'+'\n'.join('"'+lookup[s]+'"' for s in sources));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(O/(name+'.log')).write_text(c.stdout+c.stderr);results.append(dict(name=name,command=cmd,exit=c.returncode,output=c.stdout+c.stderr,sources=sources,sha256=hashlib.sha256(out.read_bytes()).hexdigest() if out.exists() else None));print(name,c.returncode,c.stdout+c.stderr);return out
core=compile('Core',['unity/Assets/Ashfall/Core/Contracts.cs'],[])
con=compile('Construction',[s for s in lookup if s.startswith('unity/Assets/Ashfall/Modules/Construction/')],[core])
candidate=compile('ConstructionCandidate',[s for s in lookup if s.startswith('unity/Assets/Ashfall/Modules/ConstructionCandidate/')],[core,con])
ledger=compile('BuildChargeLedger',['unity/Assets/Ashfall/Modules/InventoryItems/BuildChargeLedger.cs'],[core])
world=compile('World',[s for s in lookup if s.startswith('unity/Assets/Ashfall/Modules/World/') and '/Engine/' not in s],[core,'System.Runtime.Serialization.dll'])
geo=compile('ConstructionGeometry',[s for s in lookup if s.startswith('unity/Assets/Ashfall/Integration/ConstructionGeometry/')],[core,con])
integration=compile('PreparedConstruction',[s for s in lookup if s.startswith('unity/Assets/Ashfall/Integration/ConstructionRuntime/') and '/Engine/' not in s]+['unity/Assets/Ashfall/Integration/ConstructionTransactions/PreparedPublication.cs','unity/Assets/Ashfall/Integration/ConstructionTransactions/ConcreteParticipants.cs'],[core,con,candidate,ledger,world,geo])
(O/'results.json').write_text(json.dumps(results,indent=2));assert all(r['exit']==0 for r in results)

