"""Copy exact original R23 construction cues, with no waveform conversion."""
import hashlib,json,shutil,uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Construction/Audio'
def meta(p):
    m=Path(str(p)+'.meta')
    if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r23-construction:'+str(p.relative_to(ROOT))).hex+'\n'+('folderAsset: yes\n' if p.is_dir() else ''))
OUT.mkdir(parents=True,exist_ok=True);meta(OUT)
routing=json.loads((SRC/'assets/audio/reference0992/manifest.json').read_text())['generic'];rows=[]
for kind in ['build_place','build_break','build_lightning']:
    files=routing.get(kind) or ['res://assets/audio/sfx/'+kind+'_%02d.wav'%i for i in range(3)]
    for i,name in enumerate(files):
        source=SRC/name.removeprefix('res://');assert source.is_file(),source
        dest=OUT/(kind+'_'+str(i)+'.wav');shutil.copyfile(source,dest);meta(dest)
        digest=hashlib.sha256(source.read_bytes()).hexdigest();assert digest==hashlib.sha256(dest.read_bytes()).hexdigest()
        rows.append(dict(kind=kind,file=dest.name,source=name,sha256=digest))
(OUT/'audio.json').write_text(json.dumps(dict(cues=rows),indent=2)+'\n');meta(OUT/'audio.json')
for name in ['LICENSES/audio_reference099_ATTRIBUTION.txt','LICENSE']:
    dest=OUT/Path(name).name;shutil.copyfile(SRC/name,dest);meta(dest)
print(json.dumps(dict(exactSourceCues=len(rows),bytes=sum(p.stat().st_size for p in OUT.glob('*.wav')))))
