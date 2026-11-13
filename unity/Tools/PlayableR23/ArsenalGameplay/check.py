from pathlib import Path
import subprocess
T=Path(__file__).resolve().parent;ROOT=T.parents[3];P=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+[P/p for p in ['R23M1014Owner.cs','R23M1014Mechanics.cs','R23M1014Controls.cs','R23M1014Contact.cs','R23MagnumOwner.cs','R23MagnumRuntime.cs','R23WeaponPickupRules.cs']]+[T/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(T/'Cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in sources];rsp=T/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(T/'Cases.exe')],capture_output=True,text=True);(T/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
