from pathlib import Path
import subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
files=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+[HERE/'PracticeClockCases.cs']
files=sorted(set(files+list((ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons').glob('*.cs'))))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(HERE/'PracticeClockCases.exe')]+[str(p) for p in files]
(HERE/'practice.rsp').write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(HERE/'practice.rsp')],capture_output=True,text=True);(HERE/'practice-compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'PracticeClockCases.exe')],capture_output=True,text=True);(HERE/'practice-cases.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
