from pathlib import Path
import subprocess,json,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR2/final04';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR2/final04'
def compile(label,sources,refs=[],exe=False):
 out=S/(label+('.exe' if exe else '.dll'));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(out)]+['/reference:'+str(r) for r in refs]+list(map(str,sources));(E/(label+'-command.txt')).write_text(subprocess.list2cmdline(cmd));p=subprocess.run(cmd,capture_output=True);(E/(label+'-compile.log')).write_bytes(p.stdout+p.stderr);print(label,p.returncode,p.stdout.decode());return out,p.returncode
files=[S/'pin'/x['path'] for x in json.loads((E/'pin.json').read_text())]
p=R/'unity/Tools/Integration/RuntimeLoop/Scenario.cs';d=S/'Scenario.cs';d.write_bytes(p.read_bytes());exe,rc=compile('Scenario',files+[d],exe=True)
(S/'reference-map.json').write_bytes((R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5/reference-map.json').read_bytes())
if rc==0:
 for label,args in [('scenario',[]),('repeat',[]),('strict',['--strict'])]:
  p=subprocess.run([str(exe)]+args,capture_output=True);(E/(label+'.log')).write_bytes(p.stdout+p.stderr);print(label,p.returncode,p.stdout.decode()[-1200:])
