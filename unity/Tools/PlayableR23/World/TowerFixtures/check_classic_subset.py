from pathlib import Path
import subprocess
T=Path(__file__).resolve().parent
R=T.parents[4]
files=[R/'unity/Assets/Ashfall/Integration/Playable/World/Destructibles/R24TowerFixtures.cs',R/'unity/Assets/Ashfall/Integration/Playable/World/Destructibles/R23ArchiveDestructibles.cs',T/'ClassicSubsetCases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(T/'ClassicSubsetCases.exe'),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll']+[str(p) for p in files]
rsp=T/'classic-subset.rsp';rsp.write_text('\n'.join('"'+x+'"' for x in args))
q=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True)
assert q.returncode==0,q.stdout+q.stderr
q=subprocess.run([str(T/'ClassicSubsetCases.exe'),'D:/ashfall-r6-staging/world/classic-passages-v001/classic-tower18-passages319.json',str(R/'unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr/map.json'),str(R/'unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr/geometry.bytes')],capture_output=True,text=True)
(T/'classic-subset.log').write_text(q.stdout+q.stderr);print(q.stdout+q.stderr);assert q.returncode==0
