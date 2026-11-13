"""Copy index-0 current R23 menu cues byte-for-byte; no synthesis or native audio job."""
from pathlib import Path
import hashlib,json,shutil,uuid,wave

ROOT=Path(__file__).resolve().parents[4]
SOURCE=ROOT/'quest-r24/source'
OUTPUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/MenuAudio'
manifest=SOURCE/'assets/audio/reference0992/manifest.json'
routing=json.loads(manifest.read_text(encoding='utf-8'))['generic']
OUTPUT.mkdir(parents=True,exist_ok=True)
rows=[]
for cue in ('menu_hover','menu_click','vr_initialize'):
    source=SOURCE/routing[cue][0].removeprefix('res://')
    target=OUTPUT/source.name
    assert source.exists(),f'Missing actual source cue: {source}'
    with wave.open(str(source)) as audio:
        assert audio.getsampwidth()==2 and audio.getframerate()==48000 and audio.getcomptype()=='NONE'
        channels=audio.getnchannels();frames=audio.getnframes()
        assert channels==(2 if cue=='vr_initialize' else 1)
        assert 0<frames<=48000*3,'Unexpected unbounded menu payload'
    shutil.copyfile(source,target)
    assert source.read_bytes()==target.read_bytes()
    rows.append(dict(cue=cue,source=str(source.relative_to(ROOT)),file=target.name,bytes=target.stat().st_size,
                     sha256=hashlib.sha256(target.read_bytes()).hexdigest(),sample_rate=48000,channels=channels,
                     frames=frames,seconds=frames/48000))
(OUTPUT/'source.json').write_text(json.dumps(dict(routing=str(manifest.relative_to(ROOT)),variant_index=0,
    source_gain_db=-8,bus='SFX',cues=rows),indent=2)+'\n',encoding='utf-8')
for path in (ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/R23MenuAudio.cs',ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/R23MenuAudioEdges.cs',OUTPUT):
    meta=path.with_name(path.name+'.meta')
    if not meta.exists():
        importer='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if path.is_dir() else 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+importer,encoding='utf-8')
print('PASS 3 exact source WAV payloads,',sum(row['bytes'] for row in rows),'bytes; 48 kHz PCM16; original mono/stereo retained')
