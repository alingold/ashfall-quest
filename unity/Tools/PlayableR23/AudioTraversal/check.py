from pathlib import Path
import hashlib,json,subprocess,wave
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];ASSET=ROOT/'unity/Assets/StreamingAssets/PlayableR23/TraversalAudio'
record=json.loads((ASSET/'provenance.json').read_text());source=Path(record['sourceRoot']);manifest=json.loads((ASSET/'audio.json').read_text())
expected=json.loads((source/record['routingSource']).read_text())['generic']
count=0
for cue in manifest['cues']:
    if cue['kind']=='climb':
        climb_path=ASSET/'climb-source.json';assert hashlib.sha256(climb_path.read_bytes()).hexdigest()==record['climbSource']['sha256'];climb=json.loads(climb_path.read_text());assert cue['files']==[x['file']for x in climb['files']]
        for item in climb['files']:assert hashlib.sha256((ASSET/item['file']).read_bytes()).hexdigest()==item['sha256']
        count+=1;continue
    originals=expected.get(cue['kind'],[])or['res://assets/audio/sfx/'+cue['kind']+'_%02d.wav'%i for i in range(3)]
    assert cue['files']==[Path(s).name for s in originals]
    count+=1
for item in record['files']:
    p=ASSET/item['file'];original=source/item['source'].replace('res://','')
    assert hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256']==hashlib.sha256(original.read_bytes()).hexdigest();count+=1
    with wave.open(str(p))as w:assert (w.getnchannels(),w.getsampwidth(),w.getframerate(),w.getnframes())==(item['channels'],item['sampleBytes'],item['rate'],item['frames']);count+=1
assert len(record['files'])==24
print('PASS',count,'source routing, byte identity and PCM-format checks')
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe');exe=HERE/'mechanics.exe'
command=[str(csc),'/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(exe),str(ROOT/'unity/Assets/Ashfall/Integration/Playable/AudioTraversal/R23TraversalState.cs'),str(HERE/'Mechanics.cs')]
compiled=subprocess.run(command,capture_output=True,text=True);(HERE/'compile.log').write_text(compiled.stdout+compiled.stderr);assert compiled.returncode==0,compiled.stdout+compiled.stderr
run=subprocess.run([str(exe)],capture_output=True,text=True);(HERE/'results.txt').write_text(run.stdout+run.stderr);print(run.stdout,run.stderr);assert run.returncode==0
