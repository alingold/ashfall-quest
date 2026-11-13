"""Update only the user-requested Unity shaker icon; retain every other binding."""
import base64,hashlib,json,sys
from pathlib import Path
from backpack_icons import rasterize

here=Path(__file__).resolve().parent
root=here.parents[3]
svg=here/'UnityIcons/item_shaker.svg'
code=root/'unity/Assets/Ashfall/Integration/Playable/Input/R23WeaponWheelIcons.cs'
source=code.read_text()
old=next(line for line in source.splitlines() if line.startswith('case "shaker":'))
original=base64.b64decode(old.split('Convert.FromBase64String("',1)[1].split('"',1)[0])
pixels=rasterize('item_shaker',svg.parent)
if original==pixels:
    print('Shaker icon already matches current Unity SVG; no files changed.')
    sys.exit(0)
line='case "shaker": return Convert.FromBase64String("'+base64.b64encode(pixels).decode()+'");'
updated=source.replace(old,line)
assert updated.count('case "shaker":')==1
assert updated.replace(line,old)==source
out=Path('D:/ashfall-r6-staging/audio-pod-next')
out.mkdir(parents=True,exist_ok=True)
backup=out/('wheel-icon-preimage-'+hashlib.sha256(code.read_bytes()).hexdigest()[:12]+'.cs')
if backup.exists():assert backup.read_bytes()==code.read_bytes()
else:backup.write_bytes(code.read_bytes())
code.write_text(updated)
(out/'shaker-updated.png').write_bytes(pixels)
rows_file=here/'icons.json'
rows=json.loads(rows_file.read_text())
rows_backup=out/('icons-preimage-'+hashlib.sha256(rows_file.read_bytes()).hexdigest()[:12]+'.json')
if not rows_backup.exists():rows_backup.write_bytes(rows_file.read_bytes())
rows['shaker']=dict(source=str(svg.relative_to(root)),sourceOwner='Working Unity user-requested icon',sourceSha256=hashlib.sha256(svg.read_bytes()).hexdigest(),sha256=hashlib.sha256(pixels).hexdigest(),bytes=len(pixels),conversion='Existing CPU SVG rasterizer;128x160 vector ->256x320 RGBA; no engine/GPU or frozen source edits')
rows_file.write_text(json.dumps(rows,indent=2)+'\n')
result=dict(previousPixelSha256=hashlib.sha256(original).hexdigest(),pixelSha256=hashlib.sha256(pixels).hexdigest(),svg=str(svg),image=str(out/'shaker-updated.png'),oneRuntimeCaseChanged=True,allOtherIconBindingsPreserved=True,sharedBackpackAndWheel=True,nativeRendered=False)
(out/'SHAKER-ICON.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
