"""Publish the 115 real outfit cards from the immutable, qualified R6 snapshot."""
from pathlib import Path
import hashlib,json,re,shutil,uuid
ROOT=Path(__file__).resolve().parents[4]
SNAPSHOT=Path('D:/ashfall-r6-staging/frozen-source-r6-v001')
manifest=SNAPSHOT/'SOURCE-MANIFEST.json'
assert hashlib.sha256(manifest.read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
index={r['path']:r for r in json.loads(manifest.read_text())['files']}
out=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R24OutfitThumbnails'
template=(out/'000.png.meta').read_text();rows=[]
for number in range(115):
 relative='assets/ui/outfits_r24/%03d.png'%number;source=SNAPSHOT/'source'/relative;row=index[relative]
 assert source.stat().st_size==row['bytes']and hashlib.sha256(source.read_bytes()).hexdigest()==row['sha256']
 dest=out/source.name
 if not dest.exists()or hashlib.sha256(dest.read_bytes()).hexdigest()!=row['sha256']:shutil.copyfile(source,dest)
 meta=Path(str(dest)+'.meta')
 if not meta.exists():meta.write_text(re.sub(r'(?m)^guid: .*$', 'guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r24:'+str(dest.relative_to(ROOT))).hex,template))
 rows.append(row)
report=dict(target='R24 R6',sourceContentId='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520',files=rows,bytes=sum(r['bytes']for r in rows),imageProcessing=False)
(ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/R6-UI-CARDS.json').write_text(json.dumps(report,indent=2)+'\n')
print('Original frozen R6 screenshot cards published:',len(rows),'bytes:',report['bytes'])
