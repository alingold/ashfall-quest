"""Apply the exact released seven-map R6 tower partitions; no geometry writes."""
from pathlib import Path
import datetime
import hashlib
import json
import os
import uuid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
STAGE = Path('D:/ashfall-r6-staging/world/tower-family-v001')
MAPS = ROOT / 'unity/Assets/StreamingAssets/PlayableR23/Maps'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
ready = json.loads((STAGE / 'READY-FAMILY7.json').read_text())
receipt = HERE / 'family7-publication.json'
assert not receipt.exists(), 'Preserve existing publication'
assert ready['sourceContentId'] == '23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520'
assert sha(Path('D:/ashfall-r6-staging/frozen-source-r6-v001/SOURCE-MANIFEST.json')) == ready['sourceManifestSha256']
assert len(ready['maps']) == 7
assert not any(x['slug'] in ['alfa-aglae-souza', 'classic-bigbox-vr'] for x in ready['maps'])
pending = []
for row in ready['maps']:
    target = Path(row['mapDestination']).resolve()
    fixture = Path(row['newFixtureDestination']).resolve()
    candidate = Path(row['mapCandidate']).resolve()
    payload = Path(row['fixtureCandidate']).resolve()
    assert target.parent == (MAPS / row['slug']).resolve() and target.name == 'map.json'
    assert fixture.parent == target.parent and fixture.name == 'tower-fixtures-r6.json'
    assert candidate.is_relative_to(STAGE) and payload.is_relative_to(STAGE)
    assert sha(target) == row['mapBeforeSha256'] and sha(candidate) == row['mapAfterSha256']
    assert sha(target.parent / 'geometry.bytes') == row['baseGeometrySha256']
    assert row['fixtureMustNotExist'] and not fixture.exists() and not Path(str(fixture) + '.meta').exists()
    assert sha(payload) == row['fixtureSha256'] and payload.stat().st_size == row['fixtureBytes']
    before = json.loads(target.read_text())
    after = json.loads(candidate.read_text())
    assert not before.get('towerFixturesFile')
    expected = dict(before, towerFixturesFile=fixture.name)
    assert after == expected, 'Only the tower binding may change'
    data = json.loads(payload.read_text())
    assert data['baseGeometrySha256'] == row['baseGeometrySha256'] and len(data['definitions']) == 18
    assert data['sourceContentId'] == ready['sourceContentId'] and data['sourceManifestSha256'] == ready['sourceManifestSha256']
    assert len(data['chunks']) == 9 and data['baseChunks'] == before['chunks']
    assert data['baseMaterialKeys'] == [m['name'] for m in before['materials']]
    assert data['basePanels'] == len(before.get('destructibles', []))
    assert len({d['id'] for d in data['definitions']}) == 18
    assert sum(d['triangles'] for d in data['definitions']) == row['sourceTriangles'] == 2168
    pending.append((row, target, fixture, candidate, payload))

# Validate the entire bounded batch before changing any live file.
for row, target, fixture, candidate, payload in pending:
    backup = HERE / 'BeforeFamily7' / row['slug'] / 'map.json'
    assert not backup.exists()
    backup.parent.mkdir(parents=True, exist_ok=True)
    backup.write_bytes(target.read_bytes())
    fixture.write_bytes(payload.read_bytes())
    Path(str(fixture) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid5(uuid.NAMESPACE_URL, 'ashfall-r6-tower/' + row['slug']).hex + '\n')
    temporary = target.with_name('map.json.tower-publish-tmp')
    assert not temporary.exists()
    temporary.write_bytes(candidate.read_bytes())
    os.replace(temporary, target)
    assert sha(target) == row['mapAfterSha256'] and sha(fixture) == row['fixtureSha256']
receipt.write_text(json.dumps(dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), maps=ready['maps'], owners=126, payloadBytes=ready['fixturePayloadBytes'], geometryWrites=False, materialWrites=False, engineRun=False, nativePending=True), indent=2) + '\n')
print('Published7 exact R6 tower bindings /126 owners; geometry/material bytes unchanged; native pending.')
