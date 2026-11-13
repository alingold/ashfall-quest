from pathlib import Path
import hashlib,json,subprocess,wave
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];BASE=ROOT/'unity/Assets/Ashfall/Integration/Playable/JumpPads'
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
CSC='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
r=subprocess.run([CSC,'/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'cases.exe'),str(BASE/'R23HealthStationRecharge.cs'),str(HERE/'Cases.cs')],capture_output=True,text=True);print(r.stdout+r.stderr);assert r.returncode==0
r=subprocess.run([str(HERE/'cases.exe')],capture_output=True,text=True);print(r.stdout+r.stderr);(HERE/'results.txt').write_text(r.stdout+r.stderr);assert r.returncode==0
original=SOURCE/'assets/audio/sfx/r18/station_restore.wav';copy=BASE/'Resources/R23HealthStations/station_restore.wav';assert original.read_bytes()==copy.read_bytes()
with wave.open(str(copy)) as f:audio=dict(channels=f.getnchannels(),sampleBytes=f.getsampwidth(),rate=f.getframerate(),frames=f.getnframes())
runtime=(BASE/'R23HealthStations.cs').read_text();shader=(BASE/'Resources/R23StationRing.shader').read_text()
assert 'contacts.Step(' not in runtime and 'recharge.TryRestore(i,now,restore)' in runtime and 'pollClock<.1f' in runtime
reset=runtime.split('public void ResetContact()',1)[1].split('public bool Step',1)[0];assert 'recharge=' not in reset
for formula in ['.76-aa','.84+aa','atan2(p.x,-p.y)/6.2831853','float available=1-_Recharge','band*.92*(1-smoothstep(55,85,v.distanceToEye))']:assert formula in shader
paths=['scripts/healing_stations_r18.gd','scripts/map_semantics_r14.gd','assets/materials/station_ring_r18.gdshader','assets/audio/sfx/r18/station_restore.wav']
(HERE/'provenance.json').write_text(json.dumps(dict(sourceRoot=str(SOURCE),inputs={p:hashlib.sha256((SOURCE/p).read_bytes()).hexdigest() for p in paths},audio=audio,callback='Func<bool> actual full health+shield restore; false never consumes15s',nativeRun=False),indent=2))
print('PASS original restore WAV, source ring formula bindings, no runtime gradual-rate path, contact reset retains recharge')
