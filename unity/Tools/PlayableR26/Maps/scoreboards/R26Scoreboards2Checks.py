"""Follow-up regression checks using the captured r12 detector baseline and real geometry."""
import copy,json
from pathlib import Path
import numpy as np
from R26ScoreboardsDetect import ROOT,OUT,extract,template
from r23geom import MapData

def main():
 before=json.loads((OUT/'R26BeforeScoreboardsAudit.json').read_text());after=json.loads((OUT/'R26ScoreboardsAudit.json').read_text());ref=template();byid={r['id']:r for r in after['maps']}
 old_maps=old_frames=0
 for old in before['maps']:
  if old['status']!='found':continue
  old_maps+=1;new=byid[old['id']]
  assert new['status']=='found' and new['sourceSha256']==old['sourceSha256'] and new['geometrySha256']==old['geometrySha256']
  for frame in old['rectangles']:assert frame in new['rectangles'],(old['id'],frame['id']);old_frames+=1
 print('PASS all',old_maps,'previously found maps retain all',old_frames,'exact original rectangle records (IDs, position, orientation, fit, provenance).')
 for slug,count in [('bi-polar-gods-trashpandakiller',4),('chill-gods-pitdevil',16),('flash-gods-pitdevil',2),('badlands-gods-pitdevil',4)]:
  m=MapData(str(ROOT/'Assets/StreamingAssets/PlayableR23/Maps'/slug));actual=extract(m,ref);good=[q for q in actual if q['approved']];assert len(good)==count,(slug,len(good));assert good==byid['archive:'+slug]['rectangles']
  if slug.startswith('bi-polar'):
   assert all(q['normal'][2]>.999 and 6<q['width']<6.01 for q in good)
   missing=copy.copy(m);missing.map_id='test:unreviewed';missing.materials=list(m.materials);missing.materials[missing.materials.index('p8ad59c41')]='missing-trim';assert not extract(missing,ref)
   broken=copy.copy(m);broken.map_id='test:unreviewed';broken.chunks=[copy.copy(c) for c in m.chunks]
   # Remove one real trim triangle and make sure at least its aperture is rejected.
   removed=None
   for c in broken.chunks:
    if m.materials[c.material]=='p8ad59c41':
     centers=c.triangles.mean(1);near=np.flatnonzero(np.linalg.norm(centers-np.array(good[0]['center']),axis=1)<4)
     if len(near):removed=c.triangles[near[0]];break
   assert removed is not None
   for c in broken.chunks:
    if m.materials[c.material]!='p8ad59c41':continue
    same=np.linalg.norm(c.triangles[:,:,None,:]-removed[None,None,:,:],axis=3).min(2).max(1)<.001
    c.indices=c.indices.reshape(-1,3)[~same].reshape(-1)
   assert len(extract(broken,ref))<4
  if slug.startswith('chill'):
   for q in good:
    neighbors=[x for x in good if np.linalg.norm(np.array(q['center'])-x['center'])<22]
    assert len(neighbors)==4
    centroid=np.mean([x['center'] for x in neighbors],axis=0);assert np.dot(np.array(q['center'])-centroid,q['normal'])>6
    assert 1<q['topInset']<1.2
  if slug.startswith('flash'):
   assert np.dot(good[0]['normal'],good[1]['normal'])<-.999
   assert all(1.2<q['topInset']<1.4 for q in good)
   assert len([q for q in actual if not q['approved'] and q['width']>90])==2
  if slug.startswith(('chill','flash')):
   stale=copy.copy(m);stale.geometry_sha256='changed-source';stale_good=[q for q in extract(stale,ref) if q['approved']]
   assert len(stale_good)==(8 if slug.startswith('chill') else 0),'review must not apply to changed geometry'
  print('PASS',slug,count,'complete approved apertures; orientation/header/negative controls')
 print('PASS scoreboard follow-up checks')
if __name__=='__main__':main()
