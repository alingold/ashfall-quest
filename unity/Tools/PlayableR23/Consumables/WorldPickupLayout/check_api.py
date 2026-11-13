import pathlib,subprocess
root=pathlib.Path.cwd();here=pathlib.Path(__file__).resolve().parent
source=root/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/R23Playable.Runtime.rsp'
lines=[x for x in source.read_text(encoding='utf-8-sig').splitlines() if not x.strip('"').startswith('/out:')]
lines.append('"/out:'+str(here/'WorldPickupLayout.Runtime.dll')+'"')
seen={x.strip('"').replace('\\','/').lower() for x in lines}
for p in (root/'unity/Assets/Ashfall/Integration/Playable').rglob('*.cs'):
 if str(p).replace('\\','/').lower() not in seen:lines.append('"'+str(p)+'"')
rsp=here/'runtime.rsp';rsp.write_text('\n'.join(lines)+'\n')
data=pathlib.Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
r=subprocess.run([str(data/'NetCoreRuntime/dotnet.exe'),str(data/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(rsp)],capture_output=True,text=True)
(here/'runtime-compile.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);print('Installed Runtime API exit',r.returncode);raise SystemExit(r.returncode)
