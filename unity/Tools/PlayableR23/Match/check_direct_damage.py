from pathlib import Path
import subprocess
T=Path(__file__).resolve().parent
R=T.parents[3]
H=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
files=sorted(set([R/x for x in (R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+list(H.glob('*.cs'))))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(T/'DirectDamageCases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in files+[T/'DirectDamageCases.cs']]
rsp=T/'direct-damage.rsp';rsp.write_text('\n'.join('"'+x+'"' for x in args))
compile=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True)
assert compile.returncode==0,compile.stdout+compile.stderr
run=subprocess.run([str(T/'DirectDamageCases.exe')],capture_output=True,text=True)
(T/'direct-damage.log').write_text(run.stdout+run.stderr)
print(run.stdout+run.stderr)
assert run.returncode==0
