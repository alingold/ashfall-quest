from pathlib import Path
import hashlib,json,subprocess,sys
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCustodyRecoveryR5';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCustodyRecoveryR5';F=O/(sys.argv[1] if len(sys.argv)>1 else 'final06');label=sys.argv[2] if len(sys.argv)>2 else 'supplement07';D=O/label;D.mkdir();logs=E/label;logs.mkdir()
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';results=[]
def run(name,args,execute=None):
 rsp=D/(name+'.rsp');rsp.write_text('\n'.join('"'+str(x)+'"' for x in args));cmd=[csc,'@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);row=dict(name=name,command=cmd,compile=p.returncode,output=p.stdout+p.stderr)
 if not p.returncode and execute:
  t=subprocess.run(execute,capture_output=True,text=True);row.update(execution=execute,exit=t.returncode,testOutput=t.stdout+t.stderr)
 results.append(row);print(name,row)
 return row
flags=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library'];refs=['/reference:'+str(p) for p in F.glob('*.dll')]
core=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll')
run('NativeFixture',flags+refs+['/reference:'+str(core),'/define:UNITY_6000_0_OR_NEWER','/out:'+str(D/'NativeFixture.dll'),str(F/'TestsR5Base.cs'),str(F/'src/unity/Tools/Integration/ConsumableTransactions/TestsR5.cs'),str(F/'src/unity/Tools/Integration/ConsumableTransactions/NativeFixtureR5.cs')])
# Mutants use the identical separately built owners, replacing only one Integration DLL.
for name,old,new in [('semantic','source.SemanticOwnerId!=placements[0].SemanticOwnerId','false'),('lifetime','!CurrentLifetime(request.ActorId)||!pendingDrop.Commit()','false||!pendingDrop.Commit()')]:
 folder=D/name;folder.mkdir()
 for p in F.glob('*.dll'):(folder/p.name).write_bytes(p.read_bytes())
 (folder/'R5.exe').write_bytes((F/'R5.exe').read_bytes())
 sources=[]
 for p in (F/'src/unity/Assets/Ashfall/Integration/ConsumableTransactions').glob('*.cs'):
  q=folder/p.name;s=p.read_text();
  if p.name=='ConsumableBridge.cs':assert old in s;s=s.replace(old,new)
  q.write_text(s);sources.append(str(q))
 run(name,flags+['/reference:'+str(p) for p in folder.glob('*.dll') if p.name!='Consumables.dll']+['/out:'+str(folder/'Consumables.dll')]+sources,[str(folder/'R5.exe'),'capture',str(folder/'custody.bin')])
# Guard against an unaccounted compiled owner field; no private state is reflected/mutated by tests.
folder=D/'added-field';folder.mkdir()
for p in F.glob('*.dll'):(folder/p.name).write_bytes(p.read_bytes())
(folder/'R5.exe').write_bytes((F/'R5.exe').read_bytes());sources=[]
for p in (F/'src/unity/Assets/Ashfall/Modules/Pickups').glob('*.cs'):
 q=folder/p.name;s=p.read_text()
 if p.name=='CheckpointState.cs':s=s.replace('private bool quarantined;','public int FutureSerializationState;\n        private bool quarantined;')
 q.write_text(s);sources.append(str(q))
run('added-field',flags+['/reference:'+str(folder/'Core.dll'),'/reference:'+str(folder/'RecoveryContracts.dll'),'/out:'+str(folder/'Pickups.dll')]+sources,[str(folder/'R5.exe'),'capture',str(folder/'custody.bin')])
(logs/'results.json').write_text(json.dumps(results,indent=2));(logs/'installed-api.json').write_text(json.dumps(dict(path=str(core),sha256=hashlib.sha256(core.read_bytes()).hexdigest().upper()),indent=2))
