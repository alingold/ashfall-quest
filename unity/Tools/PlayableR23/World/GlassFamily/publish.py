"""For Root's later explicit Assets release: publish only staged pane plans.

Preparation/checking do not call this script. No runtime/geometry/material,
collision, shader, architecture, gameplay-owner or existing source image writes.
"""
from prepare import *
import uuid

def main():
 checks=json.loads((HERE/'checks.json').read_text());assert checks['passed']and checks['assetsWritten']is False;pending=[]
 for slug in source.PLANS:
  report=json.loads((OUT/(slug+'-checks.json')).read_text());live=LIVE/'SourceArt'/(slug+'.json');staged=OUT/live.name
  assert sha(live)==report['basePlanSha256'],'Regenerate after another SourceArt owner change; do not overwrite it'
  assert sha(staged)==report['stagedPlanSha256']and sha(LIVE/'Maps'/slug/'geometry.bytes')==report['geometrySha256']and sha(LIVE/'Maps'/slug/'map.json')==report['mapManifestSha256']
  if live.read_bytes()!=staged.read_bytes():pending.append((live,staged))
 assert len(pending)==8 and all(live.stem!='alfa-aglae-souza'for live,staged in pending)
 for live,staged in pending:
  temporary=live.with_name(live.name+'.family-glass-new');temporary.write_bytes(staged.read_bytes());temporary.replace(live)
 # Publication receipt is Tools-owned, rather than adding runtime data that
 # the existing loader does not need. Original pane/frame ownership stays put.
 (HERE/'publication.json').write_text(json.dumps(dict(publishedPlans=[p.name for p,s in pending],newGlassTriangles=11700,alreadyCorrectAlphaUnchanged=True,sourceClearMaterialOnly=True,runtimeOrGeometryWrites=False),indent=2)+'\n')
 print('Published eight exact pane treatment plans; ALFA/native glass and all original owners/physics unchanged.')

if __name__=='__main__':main()
