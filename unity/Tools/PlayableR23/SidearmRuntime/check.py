from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent
SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
profile=next(p for p in json.loads((SRC/'assets/data/one_star_050.json').read_text())['profiles'] if p['id']=='magnum')
damage=json.loads((SRC/'assets/data/one_star0991.json').read_text())['weapons']['magnum']
assert profile['magazine_per_hand']==7 and profile['rounds_per_second']==3 and profile['family']=='pistol' and damage['body']==63 and damage['head']==81
records={p:hashlib.sha256((SRC/p).read_bytes()).hexdigest() for p in ['scripts/revolver.gd','scripts/pistol.gd','scripts/weapon.gd','scripts/closure_flick.gd','scripts/weapon_rack.gd','scripts/arsenal_data.gd','scripts/one_star0991.gd','scripts/model_scale_084.gd','scripts/main.gd','assets/data/one_star_050.json','assets/data/one_star0991.json']}
(TOOLS/'source-inputs.json').write_text(json.dumps({'baseline':str(SRC),'profile':profile,'damage':damage,'inputs':records},indent=2))
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
production=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
staged=[production/name for name in ['R23M1014Owner.cs','R23M1014Mechanics.cs','R23M1014Controls.cs','R23M1014Contact.cs','R23MagnumOwner.cs','R23MagnumRuntime.cs','R23MagnumView.cs','R23WeaponFeedbackState.cs','R23FeedbackSourceData.cs']];CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
def compile(name,files,extra):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(TOOLS/name)]+extra+[str(p) for p in files];rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/(name+'.log')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
compile('MagnumCases.exe',sources+staged+[TOOLS/'Cases.cs'],['/target:exe','/define:PRODUCTION_ROUND','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'MagnumCases.exe')],capture_output=True,text=True);(TOOLS/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((EDITOR/'Managed/UnityEngine').glob('*.dll'))+list((EDITOR/'NetStandard/ref/2.1.0').glob('*.dll'))
native=sorted(set(sources+staged+list((ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons').glob('*.cs'))))
compile('Magnum.Native.dll',native,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs]);print('PASS installed Unity API compilation; production runtime, no engine/GPU/device')


# Retain the original rifle/health/both-hand cases against the same production dispatch.
compile('RifleRegression.exe',sources+staged+[ROOT/'unity/Tools/PlayableR23/HandsWeapons'/name for name in ['Mechanics.cs','SourceMechanics.cs','BothHandsAndGains.cs','SprMechanics.cs']],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'RifleRegression.exe')],capture_output=True,text=True);(TOOLS/'rifle-results.txt').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr;print(str(sum(v.startswith('PASS ') for v in r.stdout.splitlines()))+' existing rifle/hand/gain/SPR cases passed')
compile('Magnum.NativeEditor.dll',native,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR']+['/reference:'+str(p) for p in refs]);print('PASS installed Unity Editor API compilation')

compile('FeedbackRegression.exe',sources+staged+[production/'R23WeaponFeedbackChecks.cs'],['/target:exe','/define:ASHFALL_FEEDBACK_CHECKS','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'FeedbackRegression.exe')],capture_output=True,text=True);(TOOLS/'feedback-results.txt').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr;print(r.stdout.splitlines()[-1])
# Byte identity and exact current source route; no authored/synthesized replacement.
import wave
manifest=json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons/audio.json').read_text());actual=next(c for c in manifest['cues'] if c['weapon']=='magnum' and c['action']=='shot')
assert actual['files']==['audio/magnum-shot-0.wav']
original=SRC/'assets/audio/reference097/shot_magnum_last_00.wav';copy=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons/audio/magnum-shot-0.wav'
assert copy.read_bytes()==original.read_bytes()
with wave.open(str(copy),'rb') as wav: assert wav.getframerate()==48000 and wav.getnchannels()==1 and wav.getnframes()==29856
print('PASS original Magnum WAV route and byte identity, mono48000Hz/0.622s')
