from pathlib import Path
import subprocess,json
R=Path(__file__).resolve().parents[5]
O=Path(__file__).resolve().parent
shared=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'
rsp=(shared/'R23Playable.BuildAndSmoke.rsp').read_text(encoding='utf-8-sig').splitlines()
args=[]
for line in rsp:
    item=line.strip().strip('"')
    if item.startswith('/out:'):continue
    if not item.startswith('/'):continue
    args.append(item)
# Compile the new fixture against the real installed API/runtime and existing
# editor smoke classes, without writing shared compiler outputs.
args+=['/reference:'+str(shared/'R23Playable.BuildAndSmoke.dll'),'/out:'+str(O/'R24BaselineGroundSmoke.dll'),str(R/'unity/Assets/Ashfall/Editor/Playable/R24BaselineGroundSmoke.cs')]
(O/'fixture.rsp').write_text('\n'.join('"'+x+'"' for x in args),encoding='utf-8')
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
p=subprocess.run([str(E/'NetCoreRuntime/dotnet.exe'),str(E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'/noconfig','@'+str(O/'fixture.rsp')],capture_output=True,text=True,errors='replace')
(O/'fixture-api.log').write_text(p.stdout+p.stderr,encoding='utf-8')
print(p.stdout+p.stderr);print('Private installed fixture API exit',p.returncode)
raise SystemExit(p.returncode)
