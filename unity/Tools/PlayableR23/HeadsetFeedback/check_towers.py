from pathlib import Path
import subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];BASE=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/Destructibles'
csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe';exe=HERE/'TowerCases.exe'
args=[csc,'/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:System.Web.Extensions.dll','/out:'+str(exe),str(BASE/'R23ArchiveDestructibles.cs'),str(BASE/'R24TowerFixtures.cs'),str(HERE/'TowerCases.cs')]
result=subprocess.run(args,capture_output=True,text=True);(HERE/'tower-compile.log').write_text(result.stdout+result.stderr);assert result.returncode==0,result.stdout+result.stderr
result=subprocess.run([str(exe)]+[str(ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/slug)for slug in ['alfa-aglae-souza','classic-bigbox-vr']],capture_output=True,text=True);(HERE/'tower-results.txt').write_text(result.stdout+result.stderr);print(result.stdout+result.stderr);assert result.returncode==0
