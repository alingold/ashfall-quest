"""Focused world-ping/HUD CPU checks; no engine or render jobs."""
from pathlib import Path
import hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent
source=ROOT/'quest-r24/source/scripts/world_ping.gd'
runtime=ROOT/'unity/Assets/Ashfall/Integration/Playable/Input/R23WorldPing.cs'
ui=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/R23VrUI.cs'
original=source.read_text(encoding='utf-8');code=runtime.read_text(encoding='utf-8');hud=ui.read_text(encoding='utf-8')
for token in ('marker.text="◇"','marker.font_size=36;marker.pixel_size=0.001','marker.outline_size=8','remaining=20.0','hit.position+hit.normal*0.035','distance_to(marker.global_position)*0.12,0.7,6'):
    assert token in original,token
assert 'hit.normal' not in code or 'nearest.normal' in code
assert 'Physics.RaycastAll' in code and 'QueryTriggerInteraction.Ignore' in code
assert 'collider.transform.IsChildOf(actor)' in code and 'finally{Physics.queriesHitBackfaces=backfaces;}' in code
assert 'OnApplicationFocus' in code and 'OnApplicationPause' in code and 'OnDestroy' in code
assert 'CompareFunction.LessEqual' in code and 'renderQueue=2010' in code and 'R23VrUi' not in code
for stale in ('ammoGroup','ammoText','weaponText','hintText','A RELOAD','A JUMP','B SETTINGS'):
    assert stale not in hud,stale
for kept in ('Health remaining','Shield remaining','Stamina remaining','Build resources','Build resource face'):
    assert kept in hud,kept
assert 'Resources.Load<Font>("R23GodotUI")' in hud
csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'PingCases.exe'),str(runtime),str(HERE/'Cases.cs')]
compile=subprocess.run([csc]+args,capture_output=True,text=True);assert compile.returncode==0,compile.stdout+compile.stderr
run=subprocess.run([str(HERE/'PingCases.exe')],capture_output=True,text=True);print(run.stdout+run.stderr);assert run.returncode==0
integration=subprocess.run([sys.executable,str(ROOT/'unity/Tools/PlayableR23/check_integration.py')],capture_output=True,text=True)
(HERE/'integration-api.log').write_text(integration.stdout+integration.stderr,encoding='utf-8');print(integration.stdout+integration.stderr);assert integration.returncode==0
(HERE/'checks.json').write_text(json.dumps(dict(sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),sourceCases=run.stdout.strip(),
    hudAmmoRemoved=True,sourceResourceMetersPreserved=True,installedApiCompile='PASS',
    limits='No engine/GPU/native physics, marker occlusion/billboard render or device execution. Root support-hand input and map lifetime integration are separate; team marks/network unported.'),indent=2)+'\n',encoding='utf-8')
print('PASS source constants, depth-tested render contract, actor/source layer exclusions, HUD ammo removal/resource preservation and full installed API compile')
