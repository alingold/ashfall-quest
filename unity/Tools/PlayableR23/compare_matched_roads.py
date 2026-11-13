"""Compare a finite new capture with the already pinned real Godot cameras.

Produces inspection sheets, never a visual PASS or a substitute for raw logs.
"""
import argparse, hashlib, json
from pathlib import Path
from PIL import Image, ImageDraw

p = argparse.ArgumentParser()
p.add_argument('capture', type=Path)
p.add_argument('output', type=Path)
p.add_argument('--recipe', type=Path, required=True)
p.add_argument('--references', type=Path, required=True)
p.add_argument('--expected-views', type=int, default=6)
a = p.parse_args()
assert not a.output.exists(), 'Preserve earlier comparisons'
recipe = json.loads(a.recipe.read_text(encoding='utf-8-sig'))
references = json.loads(a.references.read_text(encoding='utf-8-sig'))
index = {(r['id'], r['view']): r for r in references['pairs']}
assert (recipe['width'], recipe['height'], recipe['verticalFov']) == (1200, 800, 58)
rows = []
for m in recipe['maps']:
    for shot in m['shots']:
        ref = index[(m['id'], shot['name'])]
        for key in ['eye', 'target']:
            assert all(abs(x-y) < 1e-6 for x, y in zip(shot[key], ref[key]))
        source = Path(ref['godot'])
        assert hashlib.sha256(source.read_bytes()).hexdigest() == ref['godotSha256']
        current = a.capture / ('unity-' + m['id'].replace(':', '-') + '-' + shot['name'] + '.png')
        assert source.is_file() and current.is_file(), (source, current)
        for path in [source, current]:
            with Image.open(path) as im:
                assert im.size == (1200, 800), (path, im.size)
        rows.append(dict(id=m['id'], view=shot['name'], godot=str(source), unity=str(current),
                         godotSha256=ref['godotSha256'], unitySha256=hashlib.sha256(current.read_bytes()).hexdigest(),
                         eye=shot['eye'], target=shot['target']))
assert len(rows) == a.expected_views and a.expected_views > 0
a.output.mkdir(parents=True)
for start in range(0, len(rows), 3):
    sheet = Image.new('RGB', (1200, 424*min(3,len(rows)-start)), '#172027')
    draw = ImageDraw.Draw(sheet)
    for n, row in enumerate(rows[start:start+3]):
        for col, kind in enumerate(['godot', 'unity']):
            draw.text((col*600+5, n*424+5), kind.upper()+' '+row['id'], fill='white')
            with Image.open(row[kind]) as im:
                sheet.paste(im.convert('RGB').resize((600, 400), Image.Resampling.LANCZOS), (col*600, n*424+24))
    sheet.save(a.output / ('roads-'+str(start//3)+'.jpg'), quality=94)
(a.output/'pairs.json').write_text(json.dumps(dict(recipe=str(a.recipe), references=str(a.references),
    matchedCameraViews=len(rows), cameraRule='Source eye/target; Unity reflects Z exactly once in native capture.',
    resolution=[1200,800], verticalFov=58, visualAcceptance=False, rawLogClassificationRequired=True,
    pairs=rows), indent=2)+'\n')
print(len(rows), 'actual-source camera pairs ready for personal inspection:', a.output)
