"""Verify Linux frame coverage and assemble uncropped, labeled capture sheets."""
from pathlib import Path
import json,re,hashlib
from PIL import Image,ImageDraw,ImageFont
OUT=Path('/home/al/ashfall/campaign/scoreboards2')
MAPS=[('bi-polar','bi-polar-gods-trashpandakiller',4),('chill','chill-gods-pitdevil',16),('flash','flash-gods-pitdevil',2),('badlands','badlands-gods-pitdevil',4)]
FONT=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',20)
def sheet(name,items):
 cols=2;rows=(len(items)+1)//2;im=Image.new('RGB',(1280,rows*514),'#101820');d=ImageDraw.Draw(im)
 for j,(path,label) in enumerate(items):
  x=(j%cols)*640;y=(j//cols)*514
  with Image.open(path) as src:im.paste(src.resize((640,480)),(x,y+34))
  d.text((x+10,y+6),label,font=FONT,fill='white')
 im.save(OUT/name)
def main():
 audit=json.loads((OUT/'R26ScoreboardsAudit.json').read_text());records=[]
 for short,slug,count in MAPS:
  folder=OUT/('R26-capture-'+short);log=(folder/'R26-runtime-audit.txt').read_text();row=next(r for r in audit['maps'] if r['id']=='archive:'+slug)
  assert re.search(r'faces='+str(count)+r' detected='+str(count)+r' failure= detectorFailure=\n',log)
  assert 'source='+row['sourceSha256'] in log and log.endswith('DONE\n')
  captures=re.findall(r'^capture=(\d+) frame=(\S+)',log,re.M);assert len(captures)==count
  assert {frame for i,frame in captures}=={q['id'] for q in row['rectangles']}
  assert len(list(folder.glob('R26-filled-*.png')))==count
  items=[]
  for index,frame in captures:
   path=folder/('R26-filled-'+index+'.png')
   with Image.open(path) as im:assert im.size==(1280,960);im.verify()
   items.append((path,short+' | '+frame));records.append(dict(map=row['id'],frame=frame,path=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
  if short=='chill':
   for i in range(4):sheet('R26-gallery-chill-tower-'+str(i+1)+'.png',items[i*4:i*4+4])
   sheet('R26-gallery-chill.png',[items[i*4] for i in range(4)])
  else:sheet('R26-gallery-'+short+'.png',items)
  summary=json.loads((OUT/('R26-final-run-'+short)/'summary.json').read_text());assert summary['meta']['map']==row['id']
  print('PASS',short,count,'captured distinct frames, exact source, complete runtime audit, readable images')
 runs=json.loads((OUT/'R26-final-run-results.json').read_text());assert len(runs)==4 and all(r['exit']==0 for r in runs)
 (OUT/'R26CaptureManifest.json').write_text(json.dumps(records,indent=2)+'\n');print('PASS',len(records),'final per-frame Linux captures; all four solo players exited zero')
if __name__=='__main__':main()
