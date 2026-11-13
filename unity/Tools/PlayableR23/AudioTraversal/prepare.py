"""Copy the original R23's currently routed traversal WAVs, without processing PCM."""
from pathlib import Path
import hashlib,json,shutil,uuid,wave
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
SOURCE=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
LICENSE_SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/TraversalAudio'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def meta(path):
    target=Path(str(path)+'.meta')
    if not target.exists():
        guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-r23-traversal:'+str(path.relative_to(ROOT)).replace('\\','/')).hex
        target.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+('folderAsset: yes\n' if path.is_dir() else ''))
def main():
    source_manifest=SOURCE.parent/'SOURCE-MANIFEST.json'
    assert sha(source_manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
    pins={row['path']:row['sha256'] for row in json.loads(source_manifest.read_text())['files']}
    before={p:sha(p) for p in OUT.glob('*.wav')}
    manifest_path=SOURCE/'assets/audio/reference0992/manifest.json'
    routing=json.loads(manifest_path.read_text())['generic']
    OUT.mkdir(parents=True,exist_ok=True);meta(OUT)
    result=[];provenance=[]
    for kind in ['glide','metal_step','concrete_step','concrete_run_step','gravel_step','wood_step','impact_metal','impact_concrete']:
        paths=routing.get(kind,[]) or ['res://assets/audio/sfx/'+kind+'_%02d.wav'%i for i in range(3)]
        files=[]
        for source_path in paths:
            source=SOURCE/source_path.removeprefix('res://');assert source.is_file(),source
            assert sha(source)==pins[source.relative_to(SOURCE).as_posix()]
            dest=OUT/source.name;shutil.copyfile(source,dest);meta(dest);assert sha(source)==sha(dest)
            with wave.open(str(dest)) as wav:
                assert wav.getsampwidth()==2 and wav.getnchannels()==1
                provenance.append(dict(file=dest.name,source=source_path,sha256=sha(source),frames=wav.getnframes(),rate=wav.getframerate(),channels=wav.getnchannels(),sampleBytes=wav.getsampwidth()))
            files.append(dest.name)
        result.append(dict(kind=kind,files=files))
    climb_pin=OUT/'climb-source.json';climb=None
    if climb_pin.exists():
        climb=json.loads(climb_pin.read_text())
        for item in climb['files']:assert sha(OUT/item['file'])==item['sha256'],'Preserved source climbing bytes changed'
        result.append(dict(kind='climb',files=[x['file']for x in climb['files']]))
    (OUT/'audio.json').write_text(json.dumps(dict(cues=result),indent=2)+'\n');meta(OUT/'audio.json')
    attribution='Exact frozen ASHFALL R24 R6 traversal/impact WAVs, inherited from Opus R23, unmodified. Apache-2.0.\n\n'
    for name in ['audio_reference096_ATTRIBUTION.txt','audio_reference099_ATTRIBUTION.txt','audio_reference0991_ATTRIBUTION.txt']:
        attribution+=name+'\n'+(LICENSE_SOURCE/'LICENSES'/name).read_text()+'\n'
    attribution+='Gravel/wood cues are original baked authored assets/audio/sfx fallbacks used by audio_bank.gd.\n'
    (OUT/'ATTRIBUTION.txt').write_text(attribution);meta(OUT/'ATTRIBUTION.txt')
    shutil.copyfile(LICENSE_SOURCE/'LICENSE',OUT/'LICENSE.txt');meta(OUT/'LICENSE.txt')
    record=dict(sourceRoot=str(SOURCE),licenseSourceRoot=str(LICENSE_SOURCE),sourceReadOnly=True,sourceManifestSha256=sha(source_manifest),routingSource=str(manifest_path.relative_to(SOURCE)),routingSha256=sha(manifest_path),
        behaviorSource='scripts/ambience.gd',behaviorSha256=sha(SOURCE/'scripts/ambience.gd'),
        files=provenance,pcmProcessing=False,engineRun=False,deviceRun=False,
        limits=['Source climb waveform is generated at runtime by Godot audio_bank.gd; no climb WAV was invented or substituted.',
                'No ambient wind/turbines/creaks included; component covers footsteps, landings, glide and accepted bullet-impact cues.'])
    if climb is not None:
        record['climbSource']=dict(file=climb_pin.name,sha256=sha(climb_pin));record['limits']=[x for x in record['limits']if not x.startswith('Source climb waveform')]
    (OUT/'provenance.json').write_text(json.dumps(record,indent=2)+'\n');meta(OUT/'provenance.json')
    for p in (ROOT/'unity/Assets/Ashfall/Integration/Playable/AudioTraversal').glob('*.cs'):meta(p)
    meta(ROOT/'unity/Assets/Ashfall/Integration/Playable/AudioTraversal')
    assert all(sha(p)==h for p,h in before.items()),'Existing traversal/climb PCM changed'
    print('Copied',len(provenance),'exact source WAVs;',sum((OUT/r['file']).stat().st_size for r in provenance),'bytes')
if __name__=='__main__':main()
