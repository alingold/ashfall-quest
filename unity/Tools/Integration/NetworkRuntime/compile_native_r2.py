import pathlib,json,hashlib,subprocess
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/NetworkRuntimeR2';D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2'
run=sorted(S.glob('run*'))[-1];i=1
while (S/('native-api%02d'%i)).exists():i+=1
O=S/('native-api%02d'%i);O.mkdir();L=D/O.name;L.mkdir()
E=pathlib.Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
A=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/ScriptAssemblies'
refs=[E/'Managed/UnityEngine/UnityEngine.CoreModule.dll', E/'NetStandard/ref/2.1.0/netstandard.dll']
refs += [A/n for n in ['Unity.Networking.Transport.dll','Unity.Collections.dll','Unity.Mathematics.dll','Unity.Burst.dll']]
referenceInputs=[];frozenRefs=[]
for p in refs:
 if not p.is_file():continue
 q=O/p.name;b=p.read_bytes();q.write_bytes(b);frozenRefs.append(q)
 referenceInputs.append(dict(original=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest().upper()))
refs=frozenRefs
sources=[p for p in run.glob('*.cs') if not p.name.endswith('-SessionNetworkCoordinator.cs') and not p.name.endswith('-UnityTransportAdapter.cs')]
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/noconfig','/nostdlib+','/langversion:5','/warnaserror+','/target:library','/define:UNITY_EDITOR,UNITY_6000_0_OR_NEWER,ASHFALL_BINDING_RETIREMENT_API','/out:'+str(O/'NativeApi.dll')]
cmd+=['/reference:'+str(p) for p in refs]+list(map(str,sources))
p=subprocess.run(cmd,capture_output=True,text=True,timeout=60);print(p.returncode,p.stdout+p.stderr)
(L/'compile.txt').write_text(p.stdout+p.stderr);(L/'command.json').write_text(json.dumps(dict(command=subprocess.list2cmdline(cmd),exit=p.returncode),indent=2))
(L/'references.json').write_text(json.dumps(referenceInputs,indent=2))
raise SystemExit(p.returncode)
