"""Compiles staged runtime against installed Unity APIs; runs pure measurement/receipt checks.
Never starts Unity or writes Assets/the engine scratch project.
"""
from pathlib import Path
import json
import subprocess

ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).resolve().parent
STAGE=HERE/'StagedRuntime'
OUT=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/Calibration/check'
EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
OUT.mkdir(parents=True,exist_ok=True)
results=[]
def compile(name,sources,options):
    rsp=OUT/(name+'.rsp')
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(OUT/name)]+options+[str(s) for s in sources]
    rsp.write_text('\n'.join('"'+x+'"' for x in args),encoding='utf-8')
    command=[str(CSC),'/noconfig','@'+str(rsp)]
    run=subprocess.run(command,capture_output=True,text=True)
    (OUT/(name+'.log')).write_text(run.stdout+run.stderr,encoding='utf-8')
    results.append(dict(name=name,command=command,exit=run.returncode,output=run.stdout+run.stderr))
    print(name,run.returncode,run.stdout+run.stderr)
    if run.returncode:raise SystemExit(run.returncode)
refs=[EDITOR/'Managed/UnityEngine'/('UnityEngine.'+name+'Module.dll') for name in ['Core','Physics','JSONSerialize','Mathematics','TextRendering']]
refs+=list((EDITOR/'NetStandard/ref/2.1.0').glob('*.dll'))
compile('R23Calibration.dll',sorted(STAGE.glob('*.cs')),['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
# Apply the UI patch in memory to a non-Assets check copy. Live UI stays untouched.
ui=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI'
text=(ui/'R23VrUI.cs').read_text(encoding='utf-8-sig')
patch=(HERE/'UI-calibration.patch').read_text(encoding='utf-8').splitlines()
hunks=[]
for line in patch:
    if line=='@@':hunks.append([])
    elif hunks and line[:1] in (' ','+','-') and not line.startswith('***'):hunks[-1].append(line)
for hunk in hunks:
    old='\n'.join(line[1:] for line in hunk if line[:1] in (' ','-'))+'\n'
    new='\n'.join(line[1:] for line in hunk if line[:1] in (' ','+'))+'\n'
    assert text.count(old)==1,'UI patch context changed'
    text=text.replace(old,new,1)
candidate=OUT/'R23VrUI.cs';candidate.write_text(text,encoding='utf-8')
compile('R23Calibration.WithPatchedUI.dll',sorted(STAGE.glob('*.cs'))+[candidate,ui/'R23UiState.cs'],['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
compile('PolicyChecks.exe',[STAGE/'R23CalibrationState.cs',HERE/'PolicyChecks.cs'],['/target:exe'])
run=subprocess.run([str(OUT/'PolicyChecks.exe')],capture_output=True,text=True)
print(run.stdout+run.stderr)
results.append(dict(name='PolicyChecks execution',exit=run.returncode,output=run.stdout+run.stderr))
(OUT/'results.json').write_text(json.dumps(dict(results=results,native_physics_and_playerprefs='UNRUN: coordinator engine lane',assets_written=False),indent=2),encoding='utf-8')
raise SystemExit(run.returncode)
