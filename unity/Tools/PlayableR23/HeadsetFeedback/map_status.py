"""Small read-only map/source comparison; no engine or geometry rewriting."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4];R23=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
BASE=ROOT/'unity/Assets/StreamingAssets/PlayableR23'
fixture=json.loads((R23/'assets/data/map_fixtures_r18.json').read_text())['maps']
rows=[]
for folder in ['World','Maps/alfa-aglae-souza','Maps/classic-bigbox-vr','Maps/range','Maps/valley']:
    path=BASE/folder/'map.json';m=json.loads(path.read_text());slug=m['mapId'].removeprefix('archive:');catalog=fixture.get(slug,{})
    panels=m.get('destructibles',[]);materials=m.get('materials',[])
    rows.append(dict(id=m['mapId'],payload=str(path.relative_to(ROOT)),manifestSha256=hashlib.sha256(path.read_bytes()).hexdigest(),chunks=m['chunks'],materials=len(materials),texturedMaterials=sum(bool(r.get('color')or r.get('albedo'))for r in materials),destructibles=len(panels),glass=sum(bool(r.get('glass'))for r in panels),doors=sum(not r.get('glass')for r in panels),sourceTowerFixtures=len(catalog.get('tower_gate_grates',[])),fixtureTypes=sorted({str(r.get('fixture_kind'))for r in catalog.get('tower_gate_grates',[])}),runtimeTowerFixturesPorted=False))
print(json.dumps(dict(maps=rows,findings=[
    'Unity ALFA extraction handles r8/r9 panes and doors. Original r18/r21 hash-bound tower fixtures are not part of that extraction.',
    'Unity Classic retains raw exported geometry/material roles; it has no panel ownership payload yet.',
    'A tower fixture omission is proven. The exact user-reported corner/hole locations are not identified, so hole intent/cause is not established.',
    'Source apertures are clipped only around recognized ALFA panes/doors and attached to their owner; no blanket tower cut or hole patch is justified.',
    'WORLD owns new family material treatment; Unity will consume its exact selectors after release.'],nativeRun=False,geometryWritten=False),indent=2))
