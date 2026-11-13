"""Copy the verified R5 screenshot cards, without recapture or image processing."""
import hashlib,json,shutil,uuid
from pathlib import Path
from compare_r24 import ROOT,read,frozen
index=frozen();out=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R24OutfitThumbnails';out.mkdir(exist_ok=True)
def meta(path,template=None):
    m=Path(str(path)+'.meta')
    if m.exists():return
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r24:'+str(path.relative_to(ROOT))).hex
    if template:
        import re
        text=re.sub(r'(?m)^guid: .*$', 'guid: '+guid,template.read_text());m.write_text(text)
    else:m.write_text('fileFormatVersion: 2\nguid: '+guid+'\nfolderAsset: yes\n')
meta(out);template=out.parent/'R23MapThumbnails/range.png.meta';rows=[]
for i in range(85):
    relative='assets/ui/outfits_r24/%03d.png'%i;source=read(relative,index);dest=out/source.name
    shutil.copyfile(source,dest);meta(dest,template);assert hashlib.sha256(dest.read_bytes()).hexdigest()==index[relative]
    rows.append(dict(source=relative,sha256=index[relative],bytes=dest.stat().st_size))
report=dict(target='R24 R5',files=rows,bytes=sum(r['bytes']for r in rows),imageProcessing=False)
(ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/R24-UI-CARDS.json').write_text(json.dumps(report,indent=2)+'\n')
print('Verified original R5 outfit screenshots:',len(rows),'bytes:',report['bytes'])
