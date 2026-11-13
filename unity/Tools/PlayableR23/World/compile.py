"""Installed Unity API compile only. Does not launch the editor/import/build/player."""
from pathlib import Path
import subprocess,json
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'unity/Tools/PlayableR23/World/compiler'
OUT.mkdir(exist_ok=True)
DATA=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
refs=sorted((DATA/'Managed/UnityEngine').glob('*.dll'))+[DATA/'NetStandard/ref/2.1.0/netstandard.dll']
args=['/nologo','/nostdlib+','/langversion:5','/warnaserror+','/target:library','/out:'+str(OUT/'R23PlayableWorld.dll')]+['/reference:'+str(p) for p in refs]+[str(p) for p in (ROOT/'unity/Assets/Ashfall/Integration/Playable/World').glob('*.cs')]
rsp=OUT/'compile.rsp';rsp.write_text('\n'.join('"'+x+'"' for x in args))
r=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True)
report=dict(exitCode=r.returncode,diagnostics=r.stdout+r.stderr,scope='C#5 warnings-as-errors against installed Unity6000.6.8f1 assemblies. No engine execution, shader import, physics cook or render.')
(ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/World/compile-check.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));raise SystemExit(r.returncode)
