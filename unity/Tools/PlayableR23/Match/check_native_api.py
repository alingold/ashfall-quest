from pathlib import Path
import subprocess
root=Path.cwd();here=root/'unity/Tools/PlayableR23/Match';compiler=root/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler';data=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
engine=[str(data/'NetCoreRuntime/dotnet.exe'),str(data/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll')]
def run(name,lines):
 rsp=here/(name+'.rsp');rsp.write_text('\n'.join(lines)+'\n');r=subprocess.run(engine+['@'+str(rsp)],capture_output=True,text=True);(here/(name+'.log')).write_text(r.stdout+r.stderr)
 print('\n'.join((r.stdout+r.stderr).splitlines()[:25]));print(name,'installed API exit',r.returncode)
 if r.returncode:raise SystemExit(r.returncode)
def base(name):return [x for x in (compiler/name).read_text(encoding='utf-8-sig').splitlines() if not x.strip('"').startswith('/out:')]
editorOut=here/'R24Match.EditorRuntime.dll';lines=base('R23Playable.EditorRuntime.rsp');lines.append('"/out:'+str(editorOut)+'"');seen={x.strip('"').replace('\\','/').lower() for x in lines}
for path in (root/'unity/Assets/Ashfall/Integration/Playable').rglob('*.cs'):
 if str(path).replace('\\','/').lower() not in seen:lines.append('"'+str(path)+'"')
run('match-editor-api',lines)
lines=base('R23Playable.BuildAndSmoke.rsp');lines=[('"/reference:'+str(editorOut)+'"') if 'R23Playable.EditorRuntime.dll' in x else x for x in lines];lines.append('"/out:'+str(here/'R24LocalMatch.NativeApi.dll')+'"')
path=root/'unity/Assets/Ashfall/Editor/Playable/R24LocalMatchSmoke.cs'
if not any(x.strip('"').replace('\\','/').lower()==str(path).replace('\\','/').lower() for x in lines):lines.append('"'+str(path)+'"')
run('native-api',lines)
