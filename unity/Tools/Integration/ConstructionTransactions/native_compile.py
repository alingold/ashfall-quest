import json,subprocess,hashlib,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
BASE=ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionPublicationR3'
out=BASE/sys.argv[1];out.mkdir(exist_ok=False)
rows=json.loads((ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeR1/consumed-before.json').read_text(encoding='utf-8-sig'))
index=json.loads((BASE/'baseline/index.json').read_text())
lookup={r['original'].replace('\\','/'):ROOT/r['consumed'] for r in index}
sources=[lookup[r['path'].replace('\\','/')] for r in rows]
sources+=list((ROOT/'unity/Assets/Ashfall/Integration/ConstructionRuntime').rglob('*.cs'))
sources+=[ROOT/'unity/Tools/Integration/ConstructionTransactions/NativeDomainGates.cs',ROOT/'unity/Tools/Integration/ConstructionRuntime/NativeFixture.cs']
inputs=[]
for i,p in enumerate(sources):
    q=out/f'{i}-{p.name}';q.write_bytes(p.read_bytes());inputs.append(q)
managed=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed')
refs=[managed/'UnityEngine/UnityEngine.CoreModule.dll',managed/'UnityEngine/UnityEngine.PhysicsModule.dll',managed/'UnityEngine/UnityEngine.dll']
refs.append(managed.parent/'MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll')
dll=out/'ConstructionNative.dll';rsp=out/'compile.rsp'
rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/define:UNITY_6000_0_OR_NEWER\n/target:library\n/r:System.Runtime.Serialization.dll\n/out:"'+str(dll)+'"\n'+'\n'.join('/r:"'+str(p)+'"' for p in refs)+'\n'+'\n'.join('"'+str(p)+'"' for p in inputs))
cmd=['C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe','@'+str(rsp)]
c=subprocess.run(cmd,capture_output=True,text=True);(out/'compile.log').write_text(c.stdout+c.stderr);print(c.stdout+c.stderr)
(out/'results.json').write_text(json.dumps(dict(command=cmd,compileExit=c.returncode,executed=False,inputs=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None) for p in inputs+refs]),indent=2))
raise SystemExit(c.returncode)
