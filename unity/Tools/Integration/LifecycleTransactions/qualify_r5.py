import pathlib,subprocess,json,hashlib,sys
R=pathlib.Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/LifecycleBindingsR5';D=R/'docs/agent-runs/20261104-three-chats/unity/LifecycleBindingsR5';I=S/'inputs/unity/Assets/Ashfall';O=S/(sys.argv[1] if len(sys.argv)>1 else 'qualification03');O.mkdir(exist_ok=False)
U=pathlib.Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine')
results=[]
def group(n,engine=False):return [p for p in (I/n).rglob('*.cs') if ('Engine' in p.parts)==engine]
def compile(name,files,refs=(),native=False):
 out=O/name;out.mkdir();sources=[]
 for i,p in enumerate(files):
  q=out/('%03d-'%i+p.name);q.write_bytes(p.read_bytes());sources.append(q)
 dll=O/(name+'.dll');rsp=out/'compile.rsp';args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/r:System.Runtime.Serialization.dll','/out:"'+str(dll)+'"']
 if native:args+=['/define:UNITY_6000_0_OR_NEWER']
 args+=['/r:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in sources];rsp.write_text('\n'.join(args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]
 p=subprocess.run(cmd,capture_output=True,text=True);(out/'compile.log').write_text(p.stdout+p.stderr);results.append(dict(name=name,command=cmd,exit=p.returncode,inputs=[dict(path=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest()) for q in sources],references=[dict(path=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()) for q in refs]));print(name,p.returncode,p.stdout[:1800]);return dll
core=compile('Core',group('Core'))
health=[p for p in group('Modules/HealthStates')+group('Modules/Combat') if p.name!='PreparedLifecycle.cs']+[R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedLifecycle.cs']
health=compile('CombatHealth',health,[core])
sessions=[p for p in group('Modules/Sessions') if p.name!='PreparedLifeProjection.cs']+[R/'unity/Assets/Ashfall/Modules/Sessions/PreparedLifeProjection.cs']
sessions=compile('Sessions',sessions,[core,health])
world=compile('World',group('Modules/World'),[core])
ports=compile('MatchPorts',[I/'Integration/MatchDirector/Ports.cs'],[core,health])
life=compile('Lifecycle',list((R/'unity/Assets/Ashfall/Integration/LifecycleTransactions').glob('*.cs')),[core,health,sessions,world,ports])
nativeRefs=[U/'UnityEngine.CoreModule.dll',U/'UnityEngine.PhysicsModule.dll',U/'UnityEngine.MathematicsModule.dll',U.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll']
stream=compile('MapStreaming',group('Modules/MapStreaming'),[core,world])
native=compile('NativeWorld',group('Modules/World',True)+group('Integration/WorldRuntime')+group('Integration/WorldRuntime',True),[core,world,stream]+nativeRefs,True)
compile('NativeLifecycle',list((R/'unity/Assets/Ashfall/Integration/LifecycleTransactions/Engine').glob('*.cs')),[core,world,health,sessions,ports,life,native]+nativeRefs,True)
(D/'qualification-results.json').write_text(json.dumps(results,indent=2))
raise SystemExit(0 if all(x['exit']==0 for x in results) else 1)
