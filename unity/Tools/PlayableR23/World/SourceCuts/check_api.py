from pathlib import Path
import subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];world=ROOT/'unity/Assets/Ashfall/Integration/Playable/World';rsp=ROOT/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/R23Playable.Runtime.rsp'
replacements={str(world/name).replace('\\','/').lower():HERE/'Runtime'/name for name in ['R23PlayableMap.cs','SourceArt/R24SourceArtPlan.cs','SourceArt/R24SourceArtMaterials.cs']}
lines=[];seen=set()
for line in rsp.read_text(encoding='utf-8-sig').splitlines():
 value=line.strip('"')
 if value.startswith('/out:'):continue
 normal=value.replace('\\','/').lower()
 if normal in replacements:line='"'+str(replacements[normal])+'"'
 lines.append(line);seen.add(normal)
for path in (ROOT/'unity/Assets/Ashfall/Integration/Playable').rglob('*.cs'):
 key=str(path).replace('\\','/').lower()
 if key not in seen:lines.append('"'+str(replacements.get(key,path))+'"')
lines.append('"'+str(HERE/'Runtime/R24SourceCuts.cs')+'"');lines.append('"/out:'+str(HERE/'SourceCuts.Runtime.dll')+'"');target=HERE/'runtime-api.rsp';target.write_text('\n'.join(lines)+'\n')
data=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');r=subprocess.run([str(data/'NetCoreRuntime/dotnet.exe'),str(data/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'@'+str(target)],capture_output=True,text=True);(HERE/'runtime-api.log').write_text(r.stdout+r.stderr);print('\n'.join((r.stdout+r.stderr).splitlines()[:30]));print('Staged installed Runtime API exit',r.returncode);raise SystemExit(r.returncode)
