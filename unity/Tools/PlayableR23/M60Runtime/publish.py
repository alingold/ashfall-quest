"""Publish the checked M60 candidate only over its exact current shared baseline."""
from pathlib import Path
import hashlib, json, re, shutil, uuid

here=Path(__file__).resolve().parent
root=here.parents[3]
playable=root/'unity/Assets/Ashfall/Integration/Playable'
streaming=root/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
receipt=here/'publication.json'
assert not receipt.exists(), 'Preserve previous publication'
baseline=json.loads((here/'candidate-inputs.json').read_text())
for relative, expected in baseline.items():
    path=streaming/relative.removeprefix('STREAMING/') if relative.startswith('STREAMING/') else playable/relative
    assert sha(path)==expected, 'Shared source changed; rebase first: '+relative
rows=json.loads((here/'merge-map.json').read_text())
ready=[]
for row in rows:
    source=(root/row['staged']).resolve()
    target=(root/row['destination']).resolve()
    assert source.is_relative_to(here) and source.is_file(), source
    assert target.is_relative_to(root/'unity/Assets'), target
    if row['new']:
        assert not target.exists(), 'New destination already exists: '+str(target)
    else:
        assert target.is_file(), target
    meta=Path(str(source)+'.meta')
    if row['new'] and source.suffix in ['.cs','.shader']:
        assert meta.is_file() and re.search(r'^guid: [a-f0-9]{32}$',meta.read_text(),re.M), meta
    ready.append((source,target,row['new'],meta))
written=[]
for source,target,new,meta in ready:
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source,target)
    if new:
        target_meta=Path(str(target)+'.meta')
        assert not target_meta.exists(), 'Preserve existing metadata: '+str(target_meta)
        if meta.is_file():shutil.copyfile(meta,target_meta)
        else:target_meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-m60/'+str(target.relative_to(root))).hex+'\n')
    assert sha(source)==sha(target)
    written.append(dict(path=str(target.relative_to(root)).replace('\\','/'),sha256=sha(target),new=new))
receipt.write_text(json.dumps(dict(published=True,baseline=baseline,files=written,nativeExecuted=False,headsetVerified=False),indent=2)+'\n')
print('Published exact M60 candidate:',len(written),'files; native rendering/handling pending.')
