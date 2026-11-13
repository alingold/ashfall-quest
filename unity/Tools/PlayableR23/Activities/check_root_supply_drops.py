from pathlib import Path
import subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];BASE=ROOT/'unity/Assets/Ashfall'
files=[BASE/'Core/Contracts.cs',BASE/'Modules/InventoryItems/BuildChargeLedger.cs',BASE/'Integration/Playable/Activities/R23ActivityState.cs',HERE/'RootSupplyDrops.cs']
for folder in ['Construction','ConstructionCandidate','Flight','Player']:files+=list((BASE/'Modules'/folder).glob('*.cs'))
output=HERE/'RootSupplyDrops.exe';args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(output)]+['/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'for n in ['System','System.Core']]+[str(p)for p in files]
rsp=HERE/'root-supply-drops.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(output)],capture_output=True,text=True);(HERE/'root-supply-drops.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
