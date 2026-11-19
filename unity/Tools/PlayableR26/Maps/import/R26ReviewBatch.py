from collections import Counter
from pathlib import Path
import json
import math
import sys

import R26ArchiveCandidate as c
import R26ArchiveVisual as visual
from R26AuditArchive import validate
from PIL import Image,ImageDraw,ImageFont
import numpy as np

root=c.TMP
records=[json.loads(line) for line in (root/'R26ArchiveBatchResults.jsonl').read_text().splitlines()]
assert len(records)==57
records=[dict(r,result='converted') if r['slug']=='havoc-gods-pitdevil' else r for r in records]
fixed=['banana-land-gods-pitdevil','g0dtham-gods-poplightning','cold-hearted-gods-pitdevil']
selected=[]
screens=root/'review'
thumbs=root/'thumbs'
screens.mkdir(exist_ok=True)
thumbs.mkdir(exist_ok=True)
outcomes=[]
for slug in fixed+[r['slug'] for r in records if r['result']=='converted']:
    if slug=='gyurune-gods-delicious-b-mma':
        outcomes.append(dict(slug=slug,result='skipped',reason='Recorded/default floor at 40 m lies under the floating maze, whose walls start at 56 m; no playable floor between walls was verified.'))
        continue
    try:
        audit=validate(slug)
    except (AssertionError,ValueError) as err:
        outcomes.append(dict(slug=slug,result='skipped',reason=f'Independent audit rejected: {err}'))
        continue
    folder=c.STAGE/slug
    src=c.read(c.input_files(slug)[0])
    launch=c.read(c.STAGE/(slug+'-launch.json'))
    width=min(max(100,src['extent']*.63),.62*max(launch['catalogMaxX']-launch['catalogMinX'],launch['catalogMaxZ']-launch['catalogMinZ']))
    window=[src['center'][0],-src['center'][2],width]
    image=screens/(slug+'.jpg')
    visual.render(folder,image,window)
    if c.read(folder/'provenance.json')['sourceRoles'].get('roof',0):
        mp=c.read(folder/'map.json')
        cut=screens/(slug+'-cut.jpg')
        visual.render(folder,cut,window,mp['gameSpawn'][1]+25)
    selected.append(slug)
    outcomes.append(dict(slug=slug,result='audited',**audit))
    print(slug,audit['matchSites'],audit['warmupSites'],flush=True)
c.write(root/'R26MapsiteAudit.json',outcomes)
c.write(root/'R26AuditedSlugs.json',selected)
for start in range(0,len(selected),9):
    rows=selected[start:start+9]
    width,height=378,173
    sheet=Image.new('RGB',(3*width,math.ceil(len(rows)/3)*height),(22,27,34))
    font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',12)
    draw=ImageDraw.Draw(sheet)
    for i,s in enumerate(rows):
        x,y=i%3*width,i//3*height
        im=Image.open(screens/(s+'.jpg')).crop((0,34,1560,554)).resize((375,125),Image.Resampling.LANCZOS)
        sheet.paste(im,(x,y+25))
        display=c.read(c.STAGE/s/'map.json')['title']
        draw.text((x+5,y+6),display,fill='white',font=font)
        if (screens/(s+'-cut.jpg')).exists():
            draw.text((x+5,y+150),'Cutaway available',fill='#bbd3dc',font=font)
    sheet.save(screens/f'capture-review-{start//9+1}.jpg',quality=94)
print('Audited:',len(selected),'report:',dict(Counter(r['result'] for r in outcomes)),flush=True)
