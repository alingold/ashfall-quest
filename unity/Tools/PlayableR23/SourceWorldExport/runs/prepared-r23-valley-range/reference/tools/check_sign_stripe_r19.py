"""Independent native pixel gate for the full-sign head-on fixture, with preserved failure evidence."""
import json,sys
from pathlib import Path
from PIL import Image
path=Path(sys.argv[1]);image=Image.open(path).convert('RGB');w,h=image.size
def cyan_count(left,right):
 count=0
 for y in range(int(h*.30),int(h*.60)):
  for x in range(int(w*left),int(w*right)):
   r,g,b=image.getpixel((x,y))
   if g>70 and b>70 and r<g*.65 and r<b*.65:count+=1
 return count
left=cyan_count(.22,.34);right=cyan_count(.66,.78)
passed=left>1000 and left>right*2
result={'image':str(path),'left_cyan':left,'right_cyan':right,'passed':passed,'method':'fixed near-edge head-on panel ROIs; cyan stripe must be left dominant'}
print(json.dumps(result));Path(str(path)+'.stripe.json').write_text(json.dumps(result,indent=2)+'\n')
sys.exit(0 if passed else 1)
