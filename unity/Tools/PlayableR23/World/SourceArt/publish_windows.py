"""Publish only the checked four-pane SourceArt candidate after Assets release."""
from analyze import *
import uuid

def main():
 candidate=HERE/'staged/window-candidate';live_payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt';live_runtime=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/SourceArt'
 before=json.loads((live_payload/'classic-bigbox-vr.json').read_text());after=json.loads((candidate/'payload/classic-bigbox-vr.json').read_text())
 # Removing the new four pane parts must recover the complete prior plan.
 def without_glass(plan):
  stripped=json.loads(json.dumps(plan));stripped['patches']=[dict(p,parts=[x for x in p['parts']if x['kind']!='clear_glass'])for p in stripped['patches']];stripped['patches']=[p for p in stripped['patches']if p['parts']];return stripped
 assert without_glass(after)==without_glass(before),'Live prior SourceArt changed since candidate staging'
 added=[(p['chunk'],t)for p in after['patches']for x in p['parts']if x['kind']=='clear_glass'for t in x['triangles']];assert added==[(185,351),(185,352),(186,284),(186,285)]
 preserved={p:sha(p)for p in live_payload.rglob('*')if p.is_file()and p.name!='classic-bigbox-vr.json'}
 pairs=[(candidate/'payload/classic-bigbox-vr.json',live_payload/'classic-bigbox-vr.json'),(candidate/'payload/windows-source.json',live_payload/'windows-source.json')]+[(candidate/'runtime'/name,live_runtime/name)for name in ['R24SourceArtPlan.cs','R24SourceArtMaterials.cs']]
 for src,dst in pairs:
  data=src.read_bytes();temp=dst.with_name(dst.name+'.window-new');temp.write_bytes(data);temp.replace(dst)
  staged=HERE/'staged'/('runtime'if src.parent.name=='runtime'else'payload')/src.name;staged.write_bytes(data)
  meta=Path(str(dst)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-source-art:'+str(dst.relative_to(ROOT))).hex+'\n')
 assert all(sha(p)==digest for p,digest in preserved.items())
 report=dict(publishedClearGlassTriangles=added,previousSourceArtPayloadFilesUnchanged=len(preserved),existingArtPartsUnchanged=True,originalCollisionUnchanged=True,rootMapUntouched=True)
 (HERE/'publication-windows.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))

if __name__=='__main__':main()
