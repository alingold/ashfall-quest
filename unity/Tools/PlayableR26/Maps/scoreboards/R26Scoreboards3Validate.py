"""Metamorphic geometry controls and source-bound placement audit."""
import copy,json,sys,hashlib
from pathlib import Path
import numpy as np
from R26Scoreboards3Scan import ROOT,OUT,MapData,reference,extract,TRIM
def main():
 m=MapData(str(ROOT/'Assets/StreamingAssets/PlayableR23/Maps/bi-polar-gods-trashpandakiller'));ref=reference();cases=[]
 a=.63;b=.41
 X=np.array([[1,0,0],[0,np.cos(a),-np.sin(a)],[0,np.sin(a),np.cos(a)]])
 Z=np.array([[np.cos(b),-np.sin(b),0],[np.sin(b),np.cos(b),0],[0,0,1]])
 for scale in [.01,1,100]:
  r=copy.deepcopy(m);rotation=X@Z
  for c in r.chunks:c.positions=(c.positions@rotation.T)*scale
  for key in ['spawn','warmupSpawn','gameSpawn']:
   if key in r.manifest:r.manifest[key]=(np.array(r.manifest[key])@rotation.T*scale).tolist()
  hits=extract(r,ref);assert len(hits)==4,(scale,len(hits));cases.append(dict(scale=scale,rotationDegrees=[36.096,23.491],matched=len(hits)))
 control=copy.deepcopy(m);control.chunks=[c for c in control.chunks if control.materials[c.material]!=TRIM]
 assert len(extract(control,ref))==0
 base=json.loads((OUT/'R26Baseline-R26MapScoreboards.json').read_text());final=json.loads((ROOT/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapScoreboards.json').read_text())
 added=json.loads((OUT/'R26Changes.json').read_text());preserved=0
 for old,new in zip(base['maps'],final['maps']):
  assert old['id']==new['id'];assert new['rectangles'][:len(old['rectangles'])]==old['rectangles'];preserved+=len(old['rectangles'])
  for q in new['rectangles']:
   n=np.array(q['normal']);u=np.array(q['right']);assert abs(n@n-1)<.001 and abs(u@u-1)<.001 and abs(n@u)<.001
   assert q['width']>0 and q['height']>0 and q['frontOffset']>0
 for q in added:assert hashlib.sha256(Path(q['sourceImage']).read_bytes()).hexdigest()==q['imageSha256']
 result=dict(status='PASS',rotatedScaleControls=cases,missingTrimNegativeControl=0,maps=len(final['maps']),preserved=preserved,added=len(added),allAddedHaveReviewedImageHashes=True,positiveDimensionsAndOrthonormalAxes=True)
 (OUT/'R26-validation.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
if __name__=='__main__':main()
