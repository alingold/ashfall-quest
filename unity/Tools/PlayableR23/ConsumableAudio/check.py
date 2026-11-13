from pathlib import Path
import hashlib,json,subprocess,wave
from prepare import ROOT,HERE,SOURCE,OUT,ASSETS
source=(SOURCE/'scripts/field_consumables.gd').read_text(encoding='utf-8')
for text in ['speed>=.45 and speed<=8 and travel<.3','_shake_motion_left0992=.20','stream_paused=_shake_motion_left0992<=0','if not _shake_voice0992.playing:','for_action("shaker","consume_shake",0)','_play_cue("drink");stow(false)','_play_cue("open");open_item()','audio_events.size()>64','_audio.volume_db=-12;_audio.max_distance=12']:
    assert text in source,text
code=(ASSETS/'R23ConsumableAudio.cs').read_text()
assert 'shake.loop=true'not in code and 'voice.loop=false'in code
assert 'shake.UnPause()'in code and 'shake.Pause()'in code and 'if(shake.isPlaying)return true'in code
assert 'new AudioSource[2]'in code and 'AudioListener'not in code and 'InputDevices'not in code
assert 'IR23ConsumableEffects'not in code and 'R23VRConsumables'not in code
record=json.loads((OUT/'provenance.json').read_text(encoding='utf-8'));checks=0
for name,f in record['files'].items():
    path=OUT/name;src=SOURCE/f['source'].removeprefix('res://');data=path.read_bytes()
    assert data==src.read_bytes() and hashlib.sha256(data).hexdigest()==f['sha256'];checks+=1
    with wave.open(str(path),'rb')as w:pcm=w.readframes(w.getnframes());assert w.getsampwidth()==2 and w.getnchannels()==f['channels'] and w.getframerate()==f['sampleRate']
    assert hashlib.sha256(pcm).hexdigest()==f['pcmSha256'];checks+=1
manifest=json.loads((OUT/'audio.json').read_text());m=json.loads((SOURCE/'assets/audio/reference0992/manifest.json').read_text())
for cue in manifest['cues']:
    kind=cue['kind'];action=cue['action'];paths=m['generic'].get(action)if action.startswith('item_')else m['actions'].get(kind,{}).get(action)or m['generic'].get(action)
    if paths:assert cue['files']==[Path(p).name for p in paths]
    else:assert kind=='banana'and action=='consume_drink'and cue['files']==['consume_drink_00.wav','consume_drink_01.wav']
    checks+=1
assert record['bytes']<2000000
exe=HERE/'mechanics.exe';r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(exe),str(ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'),str(ASSETS/'R23ConsumableAudioPolicy.cs'),str(HERE/'Mechanics.cs')],capture_output=True,text=True)
(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
(HERE/'checks.json').write_text(json.dumps(dict(audioChecks=checks,sourceWavs=len(record['files']),bytes=record['bytes'],policyResult=r.stdout.strip(),engineRun=False,listeningRun=False,deviceRun=False),indent=2)+'\n')
print('PASS',checks,'actual source route/WAV/PCM identity checks; no audio engine/listening run')
