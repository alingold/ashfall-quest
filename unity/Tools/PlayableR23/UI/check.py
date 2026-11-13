"""Focused UI API, source defaults, owned-loadout and personal-preference checks. No Unity process."""
from pathlib import Path
import json
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).resolve().parent
SOURCE=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI'
OUT=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/UI/check'
staged='--staged' in sys.argv
if staged:OUT=OUT.parent/'source-fidelity-check'
EDITOR=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
CSC=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
OUT.mkdir(parents=True,exist_ok=True)

def compile(name,sources,options):
    rsp=OUT/(name+'.rsp')
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(OUT/name)]+options+[str(s) for s in sources]
    rsp.write_text('\n'.join('"'+x+'"' for x in args),encoding='utf-8')
    command=[str(CSC),'/noconfig','@'+str(rsp)]
    run=subprocess.run(command,capture_output=True,text=True)
    (OUT/(name+'.log')).write_text(run.stdout+run.stderr,encoding='utf-8')
    print(name,run.returncode,run.stdout+run.stderr)
    if run.returncode:raise SystemExit(run.returncode)

refs=[p for p in (EDITOR/'Managed/UnityEngine').glob('*.dll') if not p.name.startswith('UnityEditor')]
refs+=list((EDITOR/'NetStandard/ref/2.1.0').glob('*.dll'))
extra=[SOURCE.parent/'Appearance/R23PlayerAppearance.cs',SOURCE.parent/'HandsWeapons/R23Visuals.cs']
extra += [SOURCE.parent/'Input/R23WeaponWheelState.cs',SOURCE.parent/'Input/R23WeaponWheelIcons.cs']
ui_sources=sorted(SOURCE.glob('*.cs'))
if staged:ui_sources=[p for p in ui_sources if p.name not in ('R23VrUI.cs','R23AppearancePreview.cs')]+sorted((HERE/'SourceFidelity/StagedRuntime').glob('*.cs'))
compile('R23VrUI.dll',ui_sources+extra,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
pure=['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']
compile('SourceDefaults.exe',[SOURCE/'R23UiState.cs',HERE/'SourceDefaults.cs'],pure)
run=subprocess.run([str(OUT/'SourceDefaults.exe')],capture_output=True,text=True)
print(run.stdout+run.stderr)
assert run.returncode==0
compile('LoadoutChecks.exe',[SOURCE/'R23UiState.cs',SOURCE/'R23LoadoutGesture.cs',HERE/'LoadoutChecks.cs'],pure)
loadout=subprocess.run([str(OUT/'LoadoutChecks.exe')],capture_output=True,text=True);print(loadout.stdout+loadout.stderr)
assert loadout.returncode==0
compile('PreferencesChecks.exe',[SOURCE/'R23UiState.cs',SOURCE/'R23UiPreferences.cs',HERE/'PreferencesChecks.cs'],pure)
preferences=subprocess.run([str(OUT/'PreferencesChecks.exe')],capture_output=True,text=True);print(preferences.stdout+preferences.stderr)
assert preferences.returncode==0
compile('MapFlowChecks.exe',[SOURCE/'R23UiState.cs',SOURCE/'R23MapSelection.cs',HERE/'MapFlowChecks.cs'],pure)
mapflow=subprocess.run([str(OUT/'MapFlowChecks.exe')],capture_output=True,text=True);print(mapflow.stdout+mapflow.stderr)
assert mapflow.returncode==0
compile('MenuAudioChecks.exe',[SOURCE/'R23MenuAudioEdges.cs',HERE/'MenuAudioChecks.cs'],pure)
menuaudio=subprocess.run([str(OUT/'MenuAudioChecks.exe')],capture_output=True,text=True);print(menuaudio.stdout+menuaudio.stderr)
assert menuaudio.returncode==0
(OUT/'results.json').write_text(json.dumps({'installed_api_compile':'PASS','source_defaults_exit':run.returncode,'source_defaults':run.stdout+run.stderr,'loadout_exit':loadout.returncode,'loadout':loadout.stdout+loadout.stderr,'preferences_exit':preferences.returncode,'preferences':preferences.stdout+preferences.stderr,'mapflow_exit':mapflow.returncode,'mapflow':mapflow.stdout+mapflow.stderr,'menuaudio_exit':menuaudio.returncode,'menuaudio':menuaudio.stdout+menuaudio.stderr,'engine_shader_headset_playerprefs':'UNRUN: coordinator engine/device lane'},indent=2),encoding='utf-8')
raise SystemExit(menuaudio.returncode)
