"""Report finite native image completion separately from raw Editor faults."""
from pathlib import Path
import argparse,json,re
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);a=p.parse_args();folder=a.directory.resolve()
result=json.loads((folder/'parity.json').read_text());recipe=json.loads(Path(result['recipe']).read_text())
expected={'unity-'+m['id'].replace(':','-')+'-'+s['name']+'.png'for m in recipe['maps']for s in m['shots']}
actual={Path(s).name for s in result['images']};assert actual==expected and len(result['images'])==len(expected),'Finite camera/image membership mismatch'
for name in expected:
 with Image.open(folder/name)as image:assert image.size==(recipe['width'],recipe['height'])
log=(folder/'editor.log').read_text(encoding='utf-8-sig',errors='replace');lines=log.splitlines();exceptions=[]
for i,line in enumerate(lines):
 if not re.match(r'^[A-Za-z.]*Exception:',line):continue
 stack=[]
 for following in lines[i+1:i+35]:
  if not following.strip()and stack:break
  stack.append(following)
 block='\n'.join([line]+stack);search='UnityEditor.Search.'in block and 'Ashfall.'not in block
 exceptions.append(dict(kind='qualified Editor.Search infrastructure'if search else'production or unclassified',stack=block))
shader=[line for line in lines if 'Shader error'in line or re.search(r'error CS\d+',line)]
report=dict(scope='Exact camera image capture; no gameplay/device/full-map parity acceptance',images=len(expected),cameraImagesComplete=not result['error'],reportedCaptureError=result['error'],shaderOrCompilerErrors=shader,exceptions=exceptions,productionOrUnclassifiedExceptions=sum(e['kind']=='production or unclassified'for e in exceptions),cleanEditorLog=not shader and not exceptions,visuallyInspected=False)
(folder/'raw-log-qualification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2));assert not result['error'],'Native capture reported failure'
