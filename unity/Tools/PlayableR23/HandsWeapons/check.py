from pathlib import Path
import subprocess,json
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent
BASE=ROOT/'unity/Assets/Ashfall';CSC=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[BASE/'Core/Contracts.cs']
for folder in ['RecoveryContracts','Modules/WeaponHandling','Modules/HealthStates','Modules/Combat']:
 sources+=sorted((BASE/folder).glob('*.cs'))
sources+=[BASE/'Integration/DesktopPlaytest/Combat/LocalRifleSandbox.cs',BASE/'Integration/Playable/HandsWeapons/R23RoundState.cs',BASE/'Integration/Playable/HandsWeapons/R23HandsInputFrame.cs',BASE/'Integration/Playable/HandsWeapons/R23SourceRifleOwner.cs',BASE/'Integration/Playable/HandsWeapons/R23SprScopeMath.cs']
(TOOLS/'sources.txt').write_text('\n'.join(str(p.relative_to(ROOT)) for p in sources))
def run(name,files,extra):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+']+extra+['/out:'+str(TOOLS/name)]+[str(p) for p in files]
 rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+p+'"' for p in args));r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);print(name,r.returncode,r.stdout,r.stderr);(TOOLS/(name+'.log')).write_text(r.stdout+r.stderr);assert r.returncode==0
run('mechanics.exe',sources+[TOOLS/'Mechanics.cs',TOOLS/'SourceMechanics.cs',TOOLS/'BothHandsAndGains.cs',TOOLS/'SprMechanics.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'mechanics.exe')],capture_output=True,text=True);print(r.stdout,r.stderr);(TOOLS/'mechanics-results.txt').write_text(r.stdout+r.stderr);assert r.returncode==0
editor=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
native=sources+[p for p in (BASE/'Integration/Playable/HandsWeapons').glob('*.cs') if p not in sources]
run('native.dll',native,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
run('native-editor.dll',native,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR']+['/reference:'+str(p) for p in refs])

