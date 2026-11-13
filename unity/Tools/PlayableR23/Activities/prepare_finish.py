"""Copy the actual R23 user-authored panel crop byte-identically; no generated art."""
from pathlib import Path
import hashlib,json,shutil,uuid
ROOT=Path(__file__).resolve().parents[4];SOURCE=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Construction'
baseline=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/world/r24-integration/R23-BASELINE.json').read_text());pinned={r['path']:r['sha256'] for r in baseline['actualFiles']}
relative='assets/materials/build_panel_user.png';source=SOURCE/relative;digest=hashlib.sha256(source.read_bytes()).hexdigest()
assert digest==pinned[relative],'Actual source panel crop changed since pinned R23'
OUT.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,OUT/'build_panel_user.png')
inputs={p:hashlib.sha256((SOURCE/p).read_bytes()).hexdigest() for p in [relative,'scripts/construction_piece.gd','scripts/field_builder.gd']}
(OUT/'provenance.json').write_text(json.dumps(dict(baseline='actual original R23 user finish',inputs=inputs,conversion='None: exact source PNG bytes. Unity unit cube full-face UV reproduces Godot BoxMesh3x2 UV multiplied by source(3,2) texture repeat.',material=dict(albedo='white',metallic=.6,roughness=.78,emission=False),health=dict(wall=180,floor=140,ramp=160),collapseStepSeconds=.30),indent=2))
for path in [OUT]+list(OUT.iterdir()):
    if path.suffix=='.meta':continue
    meta=path.with_name(path.name+'.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if path.is_dir() else ''))
print('Source panel PNG:',source.stat().st_size,'bytes, SHA256',digest)
