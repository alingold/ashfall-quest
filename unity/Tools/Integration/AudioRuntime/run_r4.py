from pathlib import Path
import hashlib,json,subprocess,sys
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/AudioNativeBindingsR4';D=R/'docs/agent-runs/20261104-three-chats/unity/AudioNativeBindingsR4';out=S/sys.argv[1];out.mkdir(exist_ok=False)
sources=list((S/'before/unity/Assets/Ashfall/Modules/Audio').glob('*.cs'))+list((S/'before/unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))+list((S/'before/unity/Assets/Ashfall/Integration/AudioSettings').glob('*.cs'))+[S/'before/unity/Assets/Ashfall/Core/Contracts.cs']
records=[]
for p in (R/'unity/Assets/Ashfall/Integration/AudioRuntime').rglob('*.cs'):
 q=out/'snapshot'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());sources.append(q)
refs=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data');csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
for name,test in [('r4','R4Tests.cs'),('runtime','RuntimeTests.cs'),('r2','R2Tests.cs'),('native',None),('oracle-compile','CompareClimbOracle.cs')]:
 folder=out/name;folder.mkdir();native=name=='native';args=['/nologo','/langversion:5','/warnaserror+','/target:'+('library' if native else 'exe'),'/out:"'+str(folder/('gate.dll' if native else 'gate.exe'))+'"'];ss=sources[:]
 if native:
  args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
  for ref in [refs/'NetStandard/ref/2.1.0/netstandard.dll',refs/'Managed/UnityEngine/UnityEngine.CoreModule.dll',refs/'Managed/UnityEngine/UnityEngine.AudioModule.dll']:
   args+=['/reference:"'+str(ref)+'"'];records.append(dict(path=str(ref),sha256=hashlib.sha256(ref.read_bytes()).hexdigest()))
 if test:
  p=R/'unity/Tools/Integration/AudioRuntime'/test;q=folder/test;q.write_bytes(p.read_bytes());ss.append(q)
 if name in ['r2','oracle-compile']:
  p=R/'unity/Tools/Integration/AudioRuntime/ClimbFromOracleNoise.cs';q=folder/p.name;q.write_bytes(p.read_bytes());ss.append(q)
 args+=['"'+str(p)+'"' for p in ss];rsp=folder/'compile.rsp';rsp.write_text('\n'.join(args));cmd=[csc]+(['/noconfig'] if native else [])+['@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(folder/'compile.log').write_text(c.stdout+c.stderr);item=dict(name=name,command=cmd,compile_exit=c.returncode,diagnostics=c.stdout+c.stderr)
 if c.returncode==0 and name not in ['native','oracle-compile']:
  cmd=[str(folder/'gate.exe'),str(folder/'fixtures')];t=subprocess.run(cmd,capture_output=True,text=True);(folder/'test.log').write_text(t.stdout+t.stderr);item.update(test_command=cmd,test_exit=t.returncode,output=t.stdout+t.stderr)
 records.append(item);print(json.dumps(item))
(out/'results.json').write_text(json.dumps(records,indent=2));(D/(sys.argv[1]+'-results.json')).write_text(json.dumps(records,indent=2))
sys.exit(any(x.get('compile_exit',0) or x.get('test_exit',0) for x in records))
