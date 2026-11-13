from pathlib import Path
import subprocess
t=Path(__file__).resolve().parent;r=t.parents[3]
sources=[r/'unity/Assets/Ashfall/Integration/Playable/Input/R23ObjectiveGripArbiter.cs',t/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(t/'cases.exe')]+[str(p)for p in sources]
rsp=t/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args))
x=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);assert x.returncode==0,x.stdout+x.stderr
x=subprocess.run([str(t/'cases.exe')],capture_output=True,text=True);(t/'results.txt').write_text(x.stdout+x.stderr);print(x.stdout+x.stderr);assert x.returncode==0
