"""Seal this owned candidate, verifying exact executed sources and pinned dependencies."""
from pathlib import Path
import json,hashlib,shutil
R=Path(__file__).resolve().parents[4]
S=R/'campaign-staging/20261104-three-chats/unity/MapStreamingR4/phase05'
O=R/'docs/agent-runs/20261104-three-chats/unity/MapStreamingR4/phase05'
P=R/'unity/Assets/Ashfall/Modules/MapStreaming';T=R/'unity/Tools/Modules/MapStreaming'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not (O/'seal.json').exists()
for p in P.glob('*.cs'):assert sha(p)==sha(S/'final2/candidate'/p.name)
for row in json.loads((S/'dependencies.json').read_text()):
 assert sha(Path(row['snapshot']))==row['sha256']
current=[]
for row in json.loads((S/'dependencies.json').read_text()):
 current.append(dict(path=row['path'],pinned=row['sha256'],current=sha(Path(row['path'])),drift=sha(Path(row['path']))!=row['sha256']))
(O/'dependencies-at-seal.json').write_text(json.dumps(current,indent=2))
profile=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\NetStandard\ref\2.1.0\netstandard.dll')
(O/'profile-identity.json').write_text(json.dumps(dict(path=str(profile),sha256=sha(profile),bytes=profile.stat().st_size),indent=2))
tools=S/'final2/tools';tools.mkdir(exist_ok=False)
for p in T.iterdir():
 if p.is_file():shutil.copyfile(p,tools/p.name)
handoff=R/'campaign-staging/20261104-three-chats/world/world-accepted-v2/handoff.json'
for row in json.loads(handoff.read_text())['approvedChanges']:
 assert sha(handoff.parent/row['patch'])==row['patchSha256']
originals=[]
for name in ['alfa-aglae-souza','battlefield-paintball-gods-pitdevil']:
 m=json.loads((R/'campaign-staging/20261104-three-chats/unity/MapStreaming/phase03'/(name+'.mapref.json')).read_text());p=Path(m['source']['path']);assert sha(p)==m['source']['sha256'];originals.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
(O/'originals-after.json').write_text(json.dumps(originals,indent=2))
for log in [O/'commands.json',O/'final2/commands.json',O/'extra-final/commands.json']:
 assert all(row['exitCode']==0 for row in json.loads(log.read_text()))
files=[]
for root in [P,T,S,O]:
 for p in root.rglob('*'):
  if p.is_file() and '__pycache__' not in p.parts and p.name not in ['changed-files-sha256.json','seal.json']:
   files.append(dict(path=str(p.relative_to(R)).replace('\\','/'),sha256=sha(p),bytes=p.stat().st_size))
manifest=O/'changed-files-sha256.json';manifest.write_text(json.dumps(dict(format='ASHFALL-CANDIDATE-SHA256/1',module='MapStreamingR4',status='independent-review-required-not-promoted',finalSources=str(S/'final2/candidate'),files=sorted(files,key=lambda x:x['path'])),indent=2))
(O/'seal.json').write_text(json.dumps(dict(manifest=str(manifest),sha256=sha(manifest),reportSha256=sha(O/'REPORT.md'),files=len(files),dependencyDrift=sum(x['drift'] for x in current)),indent=2))
print((O/'seal.json').read_text())
