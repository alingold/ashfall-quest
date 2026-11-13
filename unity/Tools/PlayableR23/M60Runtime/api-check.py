from pathlib import Path
import subprocess
T=Path(__file__).resolve().parent;R=T.parents[3];E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');COMP=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'
overrides={p.name:p for p in (T/'Candidate').glob('*.cs')};args=[]
for row in (COMP/'R23Playable.EditorRuntime.rsp').read_text(encoding='utf-8-sig').splitlines():
 a=row.strip().strip('"')
 if a.startswith('/out:'):a='/out:'+str(T/'R24M60.EditorRuntime.dll')
 elif a.lower().endswith('.cs')and Path(a).name in overrides:a=str(overrides[Path(a).name])
 args.append(a)
args+= [str(p)for p in (T/'Staged').glob('*.cs')]
rsp=T/'api.rsp';rsp.write_text('\n'.join('"'+s+'"'for s in args),encoding='utf-8')
p=subprocess.run([str(E/'NetCoreRuntime/dotnet.exe'),str(E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'/noconfig','@'+str(rsp)],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
print('Actual M60 candidate carried/realHealth/private-gallery full installed Editor-runtime API compile PASS; shader/native/device UNRUN.')
