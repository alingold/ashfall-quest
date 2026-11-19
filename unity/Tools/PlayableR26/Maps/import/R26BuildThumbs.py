from pathlib import Path
import json
import math
from PIL import Image,ImageDraw,ImageFont,ImageOps
p=Path(__file__).resolve().parent
slugs=json.loads((p/'R26AuditedSlugs.json').read_text())
review=p/'review'
cut_maps=[slug for slug in slugs if (review/(slug+'-cut.jpg')).is_file()]
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',13)
sheet=Image.new('RGB',(1140,math.ceil(len(cut_maps)/3)*177),(22,27,34))
draw=ImageDraw.Draw(sheet)
for i,slug in enumerate(cut_maps):
    x,y=(i%3)*380,(i//3)*177
    d=json.loads((p/'stage'/slug/'map.json').read_text())
    draw.text((x+6,y+5),d['title'],font=font,fill='white')
    with Image.open(review/(slug+'-cut.jpg')) as f:
        img=f.crop((0,34,1560,554)).resize((375,125),Image.Resampling.LANCZOS)
        sheet.paste(img,(x,y+25))
sheet.save(review/'capture-review-cutaways.jpg',quality=94)
for slug in slugs:
    suffix='-cut' if slug in cut_maps else ''
    f=p/'review'/(slug+suffix+'.jpg')
    with Image.open(f) as render:
        rgb=render.crop((520,34,1040,554))
        rgb=ImageOps.autocontrast(rgb,cutoff=.4)
        rgb.resize((400,400),Image.Resampling.LANCZOS).save(p/'thumbs'/(slug+'.jpg'),quality=90)
(p/'R26SelectedSlugs.json').write_text(json.dumps(slugs,indent=2)+'\n')
