"""Publish the two actual frozen-R6 AudioBank climbing exports, unchanged."""
from pathlib import Path
import argparse,hashlib,json,shutil,uuid,wave
ROOT=Path(__file__).resolve().parents[4];OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/TraversalAudio'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def meta(p):
    m=Path(str(p)+'.meta')
    if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-climbing:'+str(p.relative_to(ROOT))).hex+'\n')
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--export',type=Path,required=True);args=parser.parse_args()
    completed=args.export.parent/'POST-REAP-VALIDATION.json';verified=json.loads(completed.read_text());assert verified['allSourceFilesVerified']==5084 and not verified['mismatches']
    frozen=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');manifest=(frozen/'SOURCE-MANIFEST.json').read_bytes();assert hashlib.sha256(manifest).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
    pins={x['path']:x['sha256']for x in json.loads(manifest)['files']};inputs={}
    for name in ['scripts/audio_bank.gd','scripts/ambience.gd','scripts/reference_audio_099.gd','assets/audio/reference0992/manifest.json']:
        assert sha(frozen/'source'/name)==pins[name];inputs[name]=pins[name]
    rows=[]
    for i,row in enumerate(verified['clips']):
        source=args.export/row['file'];assert source.name=='climb_%02d.wav'%i and sha(source)==row['fileSha256'];target=OUT/source.name
        with wave.open(str(source))as w:
            assert(w.getnchannels(),w.getsampwidth(),w.getframerate(),w.getnframes())==(1,2,32000,9600);pcm=w.readframes(w.getnframes())
        assert hashlib.sha256(pcm).hexdigest()==row['pcmSha256']
        if target.exists():assert sha(target)==row['fileSha256'],'Refuse existing different climbing bytes'
        else:shutil.copyfile(source,target)
        meta(target);rows.append(dict(file=target.name,sha256=sha(target),pcmSha256=row['pcmSha256'],variant=i))
    assert len(rows)==2
    provenance=dict(sourceRoot=str(frozen/'source'),sourceManifestSha256=hashlib.sha256(manifest).hexdigest(),inputs=inputs,method='Unmodified source AudioBank.sample(climb,index), saved as WAV',files=rows,pcmProcessing=False,exportValidation=str(completed),qualification=verified['qualification'])
    pin=OUT/'climb-source.json';pin.write_text(json.dumps(provenance,indent=2)+'\n');meta(pin)
    p=OUT/'audio.json';data=json.loads(p.read_text());data['cues']=[x for x in data['cues']if x['kind']!='climb']+[dict(kind='climb',files=[x['file']for x in rows])];p.write_text(json.dumps(data,indent=2)+'\n')
    p=OUT/'provenance.json';data=json.loads(p.read_text());data['climbSource']=dict(file=pin.name,sha256=sha(pin));data['limits']=[x for x in data.get('limits',[])if not x.startswith('Source climb waveform')];p.write_text(json.dumps(data,indent=2)+'\n')
    p=OUT/'CLIMB-ATTRIBUTION.txt';p.write_text('ASHFALL R24 R6 climbing contact cues, Apache-2.0.\nActual source procedural AudioBank.sample("climb",0/1) exports; formula/seed/PCM unchanged.\nSee climb-source.json for frozen source and raw-PCM hashes. Original bounded wrapper timed out during post-hash; artifacts were independently verified after reap.\n');meta(p)
    print('Published2 actual source climbing WAVs /38488B; original18 traversal WAVs unchanged. Not runtime/HMD acceptance.')
if __name__=='__main__':main()
