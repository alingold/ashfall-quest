"""Scan replacement captures using both shop frame templates and render front/back source context."""
import sys,json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
root=Path('/home/al/ashfall');out=Path(__file__).resolve().parent
sys.path.insert(0,str(root/'tools/scoreboards'))
import R26ScoreboardsDetect as paint, R26Scoreboards3Scan as stock, R26Scoreboards3Review as review
from r23geom import MapData
p_ref=paint.template();s_ref=stock.reference()
sheets=[];rows={};tmp=root/'campaign/tmp/MAPS2'
for k in (out/'R26ImportIds.txt').read_text().splitlines():
 m=MapData(str(root/'unity/Assets/StreamingAssets/PlayableR23/Maps'/k.split(':')[1]))
 rects=paint.extract(m,p_ref)
 for q in stock.extract(m,s_ref):
  if not any(np.linalg.norm(np.array(a.get('frameCenter',a['center']))-q['center'])<min(q['width'],q['height'])*.005 for a in rects):
   q['id']='R26-stock-'+str(len(rects));rects.append(q)
 rows[k]=dict(source=m.manifest['sourceSha256'],geometry=m.geometry_sha256,rects=rects)
 if rects:
  data=review.prepare(m)
  for i,q in enumerate(rects):
   tile=Image.new('RGB',(300,153),'#101820')
   for b in (False,True):tile.paste(review.render(data,q,b).resize((150,131)),(150*int(b),22))
   ImageDraw.Draw(tile).text((4,2),k.split(':')[1][:28]+' '+str(i),fill='white');sheets.append(tile)
 print(k,len(rects),flush=True)
 (out/'R26ImportBoards.json').write_text(json.dumps(rows,separators=(',',':')))
 for off in range(0,len(sheets),24):
  page=Image.new('RGB',(1200,153*((min(24,len(sheets)-off)+3)//4)),'#101820')
  for j,t in enumerate(sheets[off:off+24]):page.paste(t,(j%4*300,j//4*153))
  page.save(tmp/('R26ImportBoards-%02d.jpg'%(off//24)))
