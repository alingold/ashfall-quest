from pathlib import Path
import hashlib,json,shutil,uuid,wave,struct,math
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Consumables/Audio'
ASSETS=ROOT/'unity/Assets/Ashfall/Integration/Playable/Consumables'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    OUT.mkdir(parents=True,exist_ok=True)
    manifest_path=SOURCE/'assets/audio/reference0992/manifest.json';m=json.loads(manifest_path.read_text(encoding='utf-8'))
    cues=[];files={}
    for kind in ['soda','shaker','banana']:
        for action in ['item_equip','item_stow','consume_open','consume_drink']+(['consume_shake','consume_ready']if kind=='shaker'else[]):
            if action.startswith('item_'):paths=m['generic'][action]
            else:paths=m['actions'].get(kind,{}).get(action)or m['generic'].get(action)or['res://assets/audio/sfx/'+action+'_%02d.wav'%i for i in range(2 if action in ['consume_open','consume_drink','consume_shake']else 1)]
            names=[]
            for path in paths:
                src=SOURCE/path.removeprefix('res://');name=src.name;names.append(name)
                if name in files:assert files[name]['source']==path;continue
                assert src.is_file(),src
                shutil.copyfile(src,OUT/name)
                with wave.open(str(src),'rb')as w:
                    width=w.getsampwidth();n=w.getnframes();ch=w.getnchannels();rate=w.getframerate();pcm=w.readframes(n)
                assert width==2 and n>0 and ch in [1,2] and rate>0
                vals=struct.unpack('<%dh'%(len(pcm)//2),pcm);peak=max(abs(v)for v in vals)/32768;rms=math.sqrt(sum(v*v for v in vals)/len(vals))/32768
                assert peak>0 and rms>0 and peak<1
                assert sha(src)==sha(OUT/name)
                files[name]=dict(source=path,sha256=sha(src),bytes=src.stat().st_size,pcmSha256=hashlib.sha256(pcm).hexdigest(),channels=ch,sampleRate=rate,frames=n,seconds=n/rate,peak=peak,rms=rms)
            cues.append(dict(kind=kind,action=action,files=names))
    (OUT/'audio.json').write_text(json.dumps(dict(cues=cues),indent=2)+'\n',encoding='utf-8')
    for folder in ['reference0992','reference099','reference097']:
        shutil.copyfile(SOURCE/('assets/audio/'+folder+'/ATTRIBUTION.txt'),OUT/(folder+'-ATTRIBUTION.txt'))
    shutil.copyfile(SOURCE/'assets/audio/sfx/PROVENANCE.txt',OUT/'authored-fallback-PROVENANCE.txt')
    inputs=['scripts/field_consumables.gd','scripts/audio_bank.gd','scripts/reference_audio_099.gd','scripts/sound_cues099.gd','assets/audio/reference0992/manifest.json']
    selected=set(f['source']for f in files.values())
    provenance=dict(originalSourceRoot=str(SOURCE),inputs={p:sha(SOURCE/p)for p in inputs},files=files,bytes=sum(f['bytes']for f in files.values()),sourceManifestScope=m.get('scope'),sourceManifestCredit=m.get('credit'),sourceRouting=[r for r in m['files']if r.get('file')in selected],referenceLicense='Preserves original private user-supplied reference-recording attribution; no new ownership or general redistribution license claimed.',engineRun=False,listeningRun=False,deviceRun=False)
    (OUT/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n',encoding='utf-8')
    for p in [OUT]+list(OUT.iterdir())+[ASSETS/'R23ConsumableAudio.cs',ASSETS/'R23ConsumableAudioPolicy.cs']:
        if p.suffix=='.meta':continue
        meta=Path(str(p)+'.meta')
        if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-consumable-audio:'+str(p.relative_to(ROOT)).replace('\\','/')).hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''),encoding='utf-8')
    print('Prepared',len(files),'byte-identical original WAVs;',provenance['bytes'],'bytes;',len(cues),'source routes')
if __name__=='__main__':main()
