from pathlib import Path
import subprocess
T=Path(__file__).resolve().parent;R=T.parents[3]
src=[R/x for x in (R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
src+=list((R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/Playable/Gallery').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Modules/Flight').glob('*.cs'))+list((T/'Staged').glob('*.cs'))
out=T/'Cases.exe';args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(out),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in sorted(set(src))+[T/'Cases.cs']]
rsp=T/'Cases.rsp';rsp.write_text('\n'.join('"'+x+'"'for x in args));p=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);assert p.returncode==0,p.stdout+p.stderr
p=subprocess.run([str(out)],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
