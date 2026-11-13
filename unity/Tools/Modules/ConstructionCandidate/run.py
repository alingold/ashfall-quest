import json,hashlib,subprocess,sys
from pathlib import Path
R=Path(__file__).resolve().parents[4];B=R/'campaign-staging/20261104-three-chats/unity/ConstructionPreparedR4';O=B/(sys.argv[1] if len(sys.argv)>1 else 'run07');O.mkdir(exist_ok=False)
idx=json.loads((R/'campaign-staging/20261104-three-chats/unity/ConstructionPublicationR3/baseline/index.json').read_text());sources=[]
for row in idx:
 p=R/row['consumed']
 if '/Integration/ConstructionRuntime/' in row['original'].replace('\\','/'):continue
 sources.append((row['original'],p.read_bytes()))
for folder in ['unity/Assets/Ashfall/Integration/ConstructionRuntime','unity/Assets/Ashfall/Integration/ConstructionTransactions','unity/Assets/Ashfall/Modules/ConstructionCandidate','unity/Tools/Integration/ConstructionRuntime','unity/Tools/Integration/ConstructionTransactions','unity/Tools/Modules/ConstructionCandidate']:
 for p in (R/folder).rglob('*.cs'):
  if '/Engine/' not in p.as_posix():sources.append((str(p.relative_to(R)),p.read_bytes()))
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/BuildChargeLedger.cs';sources.append((str(p.relative_to(R)),p.read_bytes()))
inputs=[]
for i,(name,b) in enumerate(sources):
 q=O/'sources'/str(i)/Path(name).name;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);inputs.append(dict(original=name,snapshot=str(q),sha256=hashlib.sha256(b).hexdigest()))
(O/'sources.json').write_text(json.dumps(inputs,indent=2))
results=[]
for main in ['PreparedR4Tests','PublicationTests','CanonicalTests','RuntimeFixtures','R2Holdouts']:
 exe=O/(main+'.exe');rsp=O/(main+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/r:System.Runtime.Serialization.dll\n/main:'+main+'\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+r['snapshot']+'"' for r in inputs));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(O/(main+'-compile.log')).write_text(c.stdout+c.stderr);row=dict(main=main,command=cmd,compileExit=c.returncode)
 if c.returncode==0:
  t=subprocess.run([str(exe)],capture_output=True,text=True);(O/(main+'-tests.log')).write_text(t.stdout+t.stderr);row.update(testExit=t.returncode,output=t.stdout+t.stderr);print(main,t.returncode,(t.stdout+t.stderr)[-1800:])
 else:print(main,c.stdout+c.stderr)
 results.append(row)
(O/'results.json').write_text(json.dumps(results,indent=2))



import sys
sys.exit(int(any(r["compileExit"] or r.get("testExit",1) for r in results)))
