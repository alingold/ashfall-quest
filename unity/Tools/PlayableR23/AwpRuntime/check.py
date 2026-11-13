from pathlib import Path
import subprocess
T=Path(__file__).resolve().parent;R=T.parents[3];H=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
src=sorted(set([R/x for x in (R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+list(H.glob('*.cs'))))
overrides={p.name:p for p in (T/'Candidate').glob('*.cs')}
files=[p for p in src if p.name not in overrides]+list(overrides.values())+list((T/'Staged').glob('*.cs'))+[T/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(T/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in files]
rsp=T/'cases.rsp';rsp.write_text('\n'.join('"'+x+'"'for x in args));q=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);print(q.stdout+q.stderr);assert q.returncode==0
q=subprocess.run([str(T/'Cases.exe')],capture_output=True,text=True);print(q.stdout+q.stderr);assert q.returncode==0
