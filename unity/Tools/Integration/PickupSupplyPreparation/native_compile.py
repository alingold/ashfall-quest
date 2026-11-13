import subprocess,json,hashlib
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/PickupSupplyPreparationR1';C=B/'cpu06';O=B/'native02';O.mkdir()
M=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed');refs=[M/'UnityEngine'/x for x in ['UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll']]+[M.parent/'MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll']+list(C.glob('*.dll'))+[C/'ActualSupplyOwnerTests.exe']
sources=[B/'sources/unity/Assets/Ashfall/Integration/ConstructionRuntime/Engine/ConstructionView.cs',B/'before/unity/Tools/Integration/ConstructionRuntimeOwner/NativeFixture.cs',R/'unity/Tools/Integration/PickupSupplyPreparation/NativeFixture.cs']
for p in sources:
 q=O/p.name
 if q.exists():q=O/('Supply'+p.name)
 q.write_bytes(p.read_bytes())
rsp=O/'compile.rsp';ss=list(O.glob('*.cs'));rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/define:UNITY_6000_0_OR_NEWER\n/target:library\n/out:"'+str(O/'NativeFixture.dll')+'"\n'+'\n'.join('/r:"'+str(p)+'"' for p in refs)+'\n'+'\n'.join('"'+str(p)+'"' for p in ss))
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];t=subprocess.run(cmd,capture_output=True,text=True);(O/'compile.log').write_text(t.stdout+t.stderr);(O/'results.json').write_text(json.dumps(dict(command=cmd,compileExit=t.returncode,executed=False,inputs=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in refs+ss]),indent=2));print(t.returncode,t.stdout+t.stderr);assert t.returncode==0
