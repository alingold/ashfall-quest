"""Check staged source-bound corrections against actual published face controls."""
from pathlib import Path
import base64,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];MAPS=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'
def faces(data):return {(p['chunk'],tri):kind for p in data['patches']for tri,kind in zip(p['triangles'],base64.b64decode(p['triangleKinds']))}
reports=[]
for slug in ['classic-bigbox-vr','alfa-aglae-souza']:
 published=json.loads((MAPS/slug/'architecture.json').read_text());staged=json.loads((HERE/'staged'/(slug+'-architecture.json')).read_text());old=faces(published);new=faces(staged)
 assert published['geometrySha256']==staged['geometrySha256']and published['family']==staged['family']and published['materials']==staged['materials']and published['textures']==staged['textures']
 assert all(new.get(key)==kind for key,kind in old.items()),slug+' lost an accepted exact source face'
 controls={}
 if slug=='classic-bigbox-vr':
  for key,kind in [((270,17925),1),((270,17478),4),((270,17921),1),((266,3617),1),((266,3322),1),((266,3963),1)]:assert new.get(key)==kind,(key,new.get(key));controls[str(key)]=kind
 reports.append(dict(map=slug,retainedPublishedFaces=len(old),addedSourceCoveredFaces=len(set(new)-set(old)),farmRoofSideStairControls=controls,originalGeometrySha256=staged['geometrySha256'],familyAndMaterialsUnchanged=True))
(HERE/'transfer-checks.json').write_text(json.dumps(reports,indent=2)+'\n');print('PASS staged two-map source corrections preserve every accepted face/family/material and actual farm1/roof4/side1/stair1 controls.',reports)
