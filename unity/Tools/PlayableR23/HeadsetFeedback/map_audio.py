"""Bind actual R5 glass/wood/grate sounds; no synthesized replacement audio."""
import hashlib,json,shutil,uuid
from pathlib import Path
from compare_r24 import ROOT,read,frozen
index=frozen();out=ROOT/'unity/Assets/StreamingAssets/PlayableR23/WorldAudio';out.mkdir(exist_ok=True)
def meta(p):
    target=Path(str(p)+'.meta')
    if not target.exists():target.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r24:'+str(p.relative_to(ROOT))).hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
meta(out);rows=[]
for name,relative in [('glass','assets/audio/sfx/glass_break_r8.wav'),('wood','assets/audio/sfx/wood_break_r10.wav'),('metal','assets/audio/sfx/r18/metal_grate.wav')]:
    source=read(relative,index);dest=out/(name+'.wav');shutil.copyfile(source,dest);meta(dest);assert hashlib.sha256(dest.read_bytes()).hexdigest()==index[relative]
    rows.append(dict(file=dest.name,source=relative,sha256=index[relative],bytes=dest.stat().st_size))
(out/'provenance.json').write_text(json.dumps(dict(target='verified R24 R5',files=rows),indent=2)+'\n');meta(out/'provenance.json')
print('Exact R5 map break cues:',len(rows),'bytes:',sum(r['bytes']for r in rows))
