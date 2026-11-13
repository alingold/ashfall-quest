"""Publish the source-bound complete Classic passages without changing geometry."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
STAGE = Path('D:/ashfall-r6-staging/world/classic-passages-v001')
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
ready = json.loads((STAGE / 'READY-SUBSET319.json').read_text())
target = Path(ready['baseMapManifest']).resolve()
payload = Path(ready['candidate']).resolve()
candidate = Path(ready['mapCandidate']).resolve()
fixture = target.parent / payload.name
receipt = HERE / 'classic319-publication.json'
assert target == (ROOT / 'unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr/map.json').resolve()
assert payload.is_relative_to(STAGE) and candidate.is_relative_to(STAGE)
assert not receipt.exists() and not fixture.exists() and not Path(str(fixture)+'.meta').exists()
for path, key in [(target,'baseMapManifestSha256'), (Path(ready['baseTowerFile']),'baseTowerSha256'),
                  (payload,'candidateSha256'), (candidate,'mapCandidateSha256'), (Path(ready['binding']),'bindingSha256')]:
    assert sha(path) == ready[key], str(path)
assert sha(target.parent/'geometry.bytes') == ready['baseGeometrySha256']
before = json.loads(target.read_text())
after = json.loads(candidate.read_text())
assert after == dict(before, towerFixturesFile=fixture.name), 'Only owner sidecar binding may change'
old = json.loads(Path(ready['baseTowerFile']).read_text())
new = json.loads(payload.read_text())
assert new['definitions'][:18] == old['definitions']
assert len(new['definitions']) == 337 and len(new['chunks']) == 29
assert new['baseGeometrySha256'] == ready['baseGeometrySha256']
assert new['baseMaterialKeys'] == [m['name'] for m in before['materials']]
assert new['baseChunks'] == before['chunks'] and new['basePanels'] == len(before.get('destructibles', []))
assert payload.stat().st_size == ready['candidateBytes']
backup = HERE/'BeforeClassic319'/'map.json'
temporary = target.with_name('map.json.classic319-publish-tmp')
assert not backup.exists() and not temporary.exists()
# Every input is checked before the first live write. Keep the old sidecar and map.
backup.parent.mkdir(parents=True, exist_ok=True)
backup.write_bytes(target.read_bytes())
fixture.write_bytes(payload.read_bytes())
Path(str(fixture)+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-classic/passages319').hex+'\n')
temporary.write_bytes(candidate.read_bytes())
os.replace(temporary,target)
assert sha(target) == ready['mapCandidateSha256'] and sha(fixture) == ready['candidateSha256']
receipt.write_text(json.dumps(dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    map=str(target), mapSha256=sha(target), fixture=str(fixture), fixtureSha256=sha(fixture),
    retainedTowerOwners=18, glass=165, doors=154, totalOwners=337,
    remainingStaticSourcePassages=873, geometryWrites=False, materialWrites=False,
    sourceArtOwnerPartitionRetained=True, nativePending=True),indent=2)+'\n')
print('Published Classic 165 glass +154 doors; retained tower18, source art and original collision geometry. Native pending.')
