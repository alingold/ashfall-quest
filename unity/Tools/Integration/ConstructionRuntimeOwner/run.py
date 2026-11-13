import json,subprocess,hashlib,sys
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeOwnerR5/run02';O=B/'cpu08';O.mkdir(exist_ok=False)
lookup={r['original']:Path(r['snapshot']) for r in json.loads((B/'inputs.json').read_text())};logs=[];refs=[]
def group(prefix):return [p for k,p in lookup.items() if k.startswith(prefix+'/') and '/Engine/' not in k]
def compile(name,ss,rr,main=None):
 rr=[Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319')/p if not Path(p).is_absolute() else p for p in rr]
 out=O/(name+('.exe' if main else '.dll'));rsp=O/(name+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:'+('exe' if main else 'library')+'\n'+('/main:'+main+'\n' if main else '')+'/out:"'+str(out)+'"\n'+'\n'.join('/r:"'+str(p)+'"' for p in rr+[Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll'),Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll')])+'\n'+'\n'.join('"'+str(p)+'"' for p in ss))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];t=subprocess.run(cmd,capture_output=True,text=True);logs.append(dict(name=name,command=cmd,exit=t.returncode,output=t.stdout+t.stderr));(O/(name+'.log')).write_text(t.stdout+t.stderr);print(name,t.returncode,t.stdout+t.stderr);assert t.returncode==0;return out
for name,prefix in [('Core','unity/Assets/Ashfall/Core'),('Recovery','unity/Assets/Ashfall/RecoveryContracts')]+[(x,'unity/Assets/Ashfall/Modules/'+x) for x in ['HealthStates','Sessions','Player','World','Construction','ConstructionCandidate','InventoryItems','Pickups']]+[('Geometry','unity/Assets/Ashfall/Integration/ConstructionGeometry')]:
 refs.append(compile(name,group(prefix)+(group('unity/Assets/Ashfall/Modules/Combat') if name=='HealthStates' else []),refs+['System.Runtime.Serialization.dll','System.Xml.dll']))
ports=compile('AuthorityPorts',[lookup['unity/Assets/Ashfall/Integration/SessionReplication/Ports.cs'],lookup['unity/Assets/Ashfall/Integration/LifecycleTransactions/Coordinator.cs'],lookup['unity/Assets/Ashfall/Integration/LifecycleTransactions/SessionLifecycleIdentity.cs']],refs);refs.append(ports)
integration=compile('ConstructionIntegration',group('unity/Assets/Ashfall/Integration/ConstructionRuntime')+group('unity/Assets/Ashfall/Integration/ConstructionTransactions'),refs);refs.append(integration)
tests=[]
for p in (R/'unity/Tools/Integration/ConstructionRuntimeOwner').glob('*.cs'):
 q=O/'tests'/p.name;q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());tests.append(q)
for main in ['OwnerTests','RetainedPreparedTests']:
 exe=compile(main,tests,refs+['System.Runtime.Serialization.dll','System.Xml.dll'],main);t=subprocess.run([str(exe)],capture_output=True,text=True);(O/(main+'-tests.log')).write_text(t.stdout+t.stderr);logs.append(dict(name=main+' execute',exit=t.returncode,output=t.stdout+t.stderr));print(t.stdout[-2500:],t.stderr); 
(O/'results.json').write_text(json.dumps(logs,indent=2));assert all(t['exit']==0 for t in logs)
