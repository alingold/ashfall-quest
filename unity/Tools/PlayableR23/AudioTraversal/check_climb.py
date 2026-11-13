from pathlib import Path
import subprocess,json,hashlib,wave
here=Path(__file__).resolve().parent;root=here.parents[3];out=Path('D:/ashfall-r6-staging/traversal-climb-next/cpu');out.mkdir(parents=True,exist_ok=True);asset=root/'unity/Assets/StreamingAssets/PlayableR23/TraversalAudio'
pin=json.loads((asset/'climb-source.json').read_text());manifest=json.loads((asset/'audio.json').read_text());assert next(x['files']for x in manifest['cues']if x['kind']=='climb')==[x['file']for x in pin['files']]
for row in pin['files']:
    p=asset/row['file'];assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256']
    with wave.open(str(p))as w:assert(w.getnframes(),w.getframerate(),w.getsampwidth(),w.getnchannels())==(9600,32000,2,1);assert hashlib.sha256(w.readframes(w.getnframes())).hexdigest()==row['pcmSha256']
for row in json.loads((asset/'provenance.json').read_text())['files']:assert hashlib.sha256((asset/row['file']).read_bytes()).hexdigest()==row['sha256']
for name in ['ClimbCases','Mechanics']:
    command=['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warn:4','/warnaserror+','/target:exe','/out:'+str(out/(name+'.exe')),str(root/'unity/Assets/Ashfall/Integration/Playable/AudioTraversal/R23TraversalState.cs'),str(here/(name+'.cs'))]
    run=subprocess.run(command,capture_output=True,text=True);(out/(name+'-compile.log')).write_text(run.stdout+run.stderr);assert run.returncode==0,run.stdout+run.stderr
    run=subprocess.run([str(out/(name+'.exe'))],capture_output=True,text=True);(out/(name+'.log')).write_text(run.stdout+run.stderr);print(run.stdout+run.stderr);assert run.returncode==0
print('Both actual source PCM hashes and all original18 traversal WAV hashes PASS; runtime/HMD pending.')
