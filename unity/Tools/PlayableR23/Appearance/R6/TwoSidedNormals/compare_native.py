from pathlib import Path
import hashlib,json
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parents[6]
F=R/'docs/agent-runs/20261104-three-chats/unity/playable-r23'
old=F/'floor-20261105-153908/appearance-images'
new=F/'floor-20261105-160814/appearance-images'
O=Path('D:/ashfall-unity-builds/parity-r6-tower7-neck-next/comparison');O.mkdir(exist_ok=True)
a=json.loads((old/'matched-appearance.json').read_text());b=json.loads((new/'matched-appearance.json').read_text())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for row in b['images']:
 assert sha(new/row['image'])==row['imageSha256']
 if row.get('sourceImage'):assert sha(R/row['sourceImage'])==row['sourceImageSha256']
for row in a['images']:assert sha(old/row['image'])==row['imageSha256']
for style in ['prowler','stacy_throwback']:
 canvas=Image.new('RGB',(1440,1512),'#252525');draw=ImageDraw.Draw(canvas)
 for y,view in enumerate(['head','wrists','front']):
  row=next(r for r in b['images'] if r['style']==style and r['view']==view)
  prev=next(r for r in a['images'] if r['style']==style and r['view']==view)
  for x,(label,path) in enumerate([('Godot source',R/row['sourceImage']),('Unity prior',old/prev['image']),('Unity corrected',new/row['image'])]):
   canvas.paste(Image.open(path).convert('RGB').resize((480,480)),(x*480,y*504+24));draw.text((x*480+8,y*504+5),style+' '+view+' / '+label,fill='white')
 canvas.save(O/(style+'-neck-comparison.jpg'),quality=95)
probes=[r for r in b['images'] if r['classification']!='matched-source-camera']
canvas=Image.new('RGB',(1600,328),'#252525');draw=ImageDraw.Draw(canvas)
for x,row in enumerate(probes):
 canvas.paste(Image.open(new/row['image']).convert('RGB').resize((320,304)),(x*320,24));draw.text((x*320+8,5),row['style']+' / UNMATCHED',fill='white')
canvas.save(O/'unmatched-probes.jpg',quality=95)
print(str(O))
