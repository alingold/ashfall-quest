"""Apply the explicit source-bound visual review; preserve every prior approved record."""
import json,hashlib,csv,copy,textwrap,collections
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path('/home/al/ashfall/unity');OUT=ROOT.parent/'campaign/scoreboards3'
RESOURCE=ROOT/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapScoreboards.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 before=json.loads((OUT/'R26Baseline-R26MapScoreboards.json').read_text());data=copy.deepcopy(before)
 rows=json.loads((OUT/'R26Candidates.json').read_text())['maps'];notes=json.loads((OUT/'R26ReviewNotes.json').read_text())
 assert notes['originalPagesReviewed']==list(range(52)) and notes['recoveredPagesReviewed']==list(range(8))
 existing={r['id']:r for r in data['maps']};changes=[];decisions=[];counts=[];reject=[];galleries=[]
 font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',15)
 def sheets(items,stem):
  for k in range(0,len(items),12):
   part=items[k:k+12];im=Image.new('RGB',(2400,460*((len(part)+2)//3)),'#101820');d=ImageDraw.Draw(im)
   for j,(path,caption) in enumerate(part):
    x=(j%3)*800;y=(j//3)*460;im.paste(Image.open(path),(x,y))
    d.text((x+6,y+414),'\n'.join(textwrap.wrap(caption,95)[:2]),font=font,fill='white')
   dst=OUT/f'{stem}-{k//12:02}.png';im.save(dst);galleries.append(str(dst))
 for r in rows:
  target=existing[r['id']];old=copy.deepcopy(target['rectangles']);slug=r['id'].replace('archive:','');accepted=[];newids=[]
  assert sha(ROOT/r['geometry'])==r['geometrySha256']
  for q0 in r['candidates']:
   q=copy.deepcopy(q0);path=OUT/'source'/f"R26-{slug}-{q['id']}.png";assert path.is_file()
   was=any(q['id']==a['id'] for a in old)
   if was:ok=True;reason='preserved prior approved placement'
   else:
    note=notes['stock' if q['id'].startswith('R26-stock') else 'maps'][slug];i=int(q['id'].split('-')[-1]);ok=note['accept']=='all' or i in note['accept']
    reason=note.get('reason','reviewed empty stock display') if ok else note['rejectReason']
    if ok:
     if i in note.get('flip',[]):
      q['normal']=[-x for x in q['normal']];q['right']=[-x for x in q['right']]
     q.update(approved=True,confidence=.98,fitAspect=True,reason='SCOREBOARDS3 visual review: '+reason)
     target['rectangles'].append(q);newids.append(q['id'])
     changes.append(dict(map=r['id'],frame=q['id'],action='added',rectangle=q,sourceImage=str(path),imageSha256=sha(path),sourceSha256=r['sourceSha256'],geometrySha256=r['geometrySha256']))
   decisions.append(dict(map=r['id'],frame=q['id'],approved=ok,existing=was,reason=reason,image=str(path)))
   if ok:accepted.append((path,('EXISTING | ' if was else 'ADDED | ')+reason))
   else:reject.append((path,'REJECTED | '+reason))
  assert target['rectangles'][:len(old)]==old
  target['status']='found' if target['rectangles'] else ('ambiguous' if r['candidates'] else 'none')
  counts.append(dict(map=r['id'],beforeStatus=r['status'],afterStatus=target['status'],before=len(old),after=len(target['rectangles']),added=len(newids),rejected=len(r['candidates'])-len(accepted),newFrames=';'.join(newids)))
  if accepted:sheets(accepted,'R26-accepted-'+slug)
 sheets(reject,'R26-rejected')
 data['detectorSha256']=hashlib.sha256(Path(__file__).read_bytes()+(Path(__file__).parent/'R26Scoreboards3Scan.py').read_bytes()+(OUT/'R26ReviewNotes.json').read_bytes()).hexdigest()
 # Preserve the resource's existing newline convention.
 newline='\r\n' if b'\r\n' in RESOURCE.read_bytes() else '\n'
 RESOURCE.write_bytes((json.dumps(data,indent=2)+'\n').replace('\n',newline).encode())
 for name,value in [('R26Changes.json',changes),('R26VisualDecisions.json',decisions),('R26AfterCounts.json',counts),('R26GalleryManifest.json',galleries)]:
  (OUT/name).write_text(json.dumps(value,indent=2)+'\n')
 with (OUT/'R26PerMapCounts.csv').open('w',newline='') as f:
  writer=csv.DictWriter(f,fieldnames=list(counts[0]));writer.writeheader();writer.writerows(counts)
 print(json.dumps(dict(added=len(changes),preserved=sum(len(r['rectangles']) for r in before['maps']),after=sum(len(r['rectangles']) for r in data['maps']),mapsWithAdditions=sum(bool(c['added']) for c in counts),statuses=dict(collections.Counter(c['afterStatus'] for c in counts)),rejected=len(reject)),indent=2))
if __name__=='__main__':main()
