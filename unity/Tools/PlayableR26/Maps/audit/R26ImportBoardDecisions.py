"""Apply the front/back source review of the new captures, preserving the shared score canvas."""
import json
from pathlib import Path
root=Path('/home/al/ashfall/unity');out=Path(__file__).resolve().parent
p=root/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapScoreboards.json'
captures=json.loads((out/'R26ImportBoards.json').read_bytes());content=json.loads(p.read_bytes());rows={r['id']:r for r in content['maps']}
# Display faces, independent physical artwork, reverse-facing lettering. Omitted rings belong to buildings, floors, or windows.
choices={'banana-land':([0],[],[]),'g0dtham':(list(range(8)),[],[7]),'cold-hearted':([0,1],[],[]),'garden-of-eden':([0],[],[]),'doom':(list(range(6)),[],list(range(6))),'rush':([0],[],[0]),'meliworld':(list(range(16)),[],[1,3,8,13]),'havoc':([3],[],[]),'dark-void':([0,1,2,3,5],[4],[0,2]),'meli-cafe':([0,1],[],[]),'eternal':([0],[],[])}
for k,v in captures.items():
 slug=k.split(':')[1].split('-gods')[0];values=choices.get(slug,([],[],[]))
 if not any(values) and k not in rows:continue
 disp,signs,flip=values
 row=dict(id=k,sourceSha256=v['source'],geometrySha256=v['geometry'],status='found' if disp else 'none',rectangles=[],retainedFaces=[])
 for i in disp+signs:
  q=v['rects'][i].copy();q.update(approved=True,confidence=.98,fitAspect=True,reason='r18 front/back source review')
  if i in flip:
   for field in ['normal','right']:q[field]=[-x for x in q[field]]
  row.setdefault('solidPanels' if i in signs else 'rectangles',[]).append(q)
 if k in rows:
  i=content['maps'].index(rows[k]);content['maps'][i]=row
 else:content['maps'].append(row)
newline='\r\n' if b'\r\n' in p.read_bytes() else '\n'
p.write_bytes((json.dumps(content,indent=2)+'\n').replace('\n',newline).encode())
