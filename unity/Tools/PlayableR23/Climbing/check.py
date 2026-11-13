from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent
BASE=ROOT/'unity/Assets/Ashfall';CSC=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[BASE/'Core/Contracts.cs']+list((BASE/'Modules/Climbing').glob('*.cs'))+[BASE/'Integration/Playable/Climbing/R23ClimbState.cs']
def compile(name,files,extras):
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(TOOLS/name)]+extras+[str(p) for p in files]
    rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+p+'"' for p in args))
    r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True)
    (TOOLS/(name+'.log')).write_text(r.stdout+r.stderr);print(name,r.returncode,r.stdout,r.stderr);assert r.returncode==0
compile('mechanics.exe',sources+[TOOLS/'Mechanics.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'mechanics.exe')],capture_output=True,text=True)
(TOOLS/'mechanics-results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
editor=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
compile('Climbing.Native.dll',sources+[BASE/'Integration/Playable/Climbing/R23VRClimbing.cs'],['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
