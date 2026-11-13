"""Publish only the checked DT11 additions against the recorded live21 base."""
from pathlib import Path
import hashlib
import json
import shutil
import uuid

T = Path(__file__).resolve().parent
R = T.parents[3]
P = R / 'unity/Assets/Ashfall/Integration/Playable'
H = P / 'HandsWeapons'
S = R / 'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
base = json.loads((T / 'publication-base.json').read_text())
for name, expected in base.items():
    assert hashlib.sha256((R / name).read_bytes()).hexdigest() == expected, 'Live base changed: ' + name

copies = []
for name in base:
    target = R / name
    copies.append((T / 'Candidate' / target.name, target))
for source in (T / 'Staged').glob('*.cs'):
    folder = P / 'Gallery' if source.name in ('R24Dt11Gallery.cs', 'R24Dt11GallerySession.cs') else H
    target = folder / source.name
    assert not target.exists(), 'New DT11 runtime already exists: ' + str(target)
    copies.append((source, target))
for source in (T / 'Payload').iterdir():
    assert source.is_file() and source.name.startswith('dt11'), source
    target = S / source.name
    assert not target.exists(), 'Unique DT11 payload already exists: ' + str(target)
    copies.append((source, target))
for source, target in copies:
    assert source.is_file(), source
for source, target in copies:
    shutil.copyfile(source, target)
    meta = Path(str(target) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n')
sources = R / 'unity/Tools/PlayableR23/HandsWeapons/sources.txt'
lines = sources.read_text().splitlines()
for name in ('R24Dt11Owner.cs', 'R24Dt11Mechanics.cs', 'R23RoundState.Dt11.cs'):
    path = (H / name).relative_to(R).as_posix()
    if path not in [line.replace('\\', '/') for line in lines]:
        lines.append(path)
sources.write_text('\n'.join(lines) + '\n')
print('Published DT11: 11 scoped existing files, 7 new runtime files, %d unique payloads. Knife/query/fixture untouched.' % len(list((T / 'Payload').iterdir())))
