from pathlib import Path
import json,hashlib
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/CertifiedMotionPublicationR4';O=R/'campaign-staging/20261104-three-chats/unity/CertifiedMotionPublicationR4'
# Complete source-symbol coverage for actual pad implementation.
p=O/'before/unity/Assets/Ashfall/Modules/Flight/FlightMachine.cs';j=json.loads((D/'source-symbols.json').read_text());j.append(dict(source='unity/Assets/Ashfall/Modules/Flight/FlightMachine.cs',sha256=hashlib.sha256(p.read_bytes()).hexdigest(),symbols=[dict(line=i,text=l.strip()) for i,l in enumerate(p.read_text().splitlines(),1) if any(s in l for s in ['class PadContacts','TryLaunch','Launch(','ApplyCollisionResult'])]));(D/'source-symbols.json').write_text(json.dumps(j,indent=2))
r=json.loads((D/'run04.json').read_text());cmd=r['results'];summary=dict(task='CertifiedMotionPublicationR4',status='supported-preparation-only; actual certified physical publication blocked',unresolvedHigh=['MotionPreparedReviewR3 H2 enabled direct root before collision authorization','MotionPreparedReviewR3 H3 direct pad before downstream refusal; no complete capsule/lifecycle composition'],publicConsumerChecks=63,retainedChecks=830,compiledMutantsDetected=3,native='compile-only; never executed',M1='OPEN',G2='OPEN',productionSource='unity/Assets/Ashfall/Integration/MotionAuthority/MotionPublicationPreparation.cs',sourceSha256=hashlib.sha256((R/'unity/Assets/Ashfall/Integration/MotionAuthority/MotionPublicationPreparation.cs').read_bytes()).hexdigest(),results=[dict(argv=x['argv'],exit=x['exit']) for x in cmd])
(D/'handoff.json').write_text(json.dumps(summary,indent=2))
# Seal every produced byte, including unchanged immutable before snapshots and historical attempts.
files=[]
for base in [O,D,R/'unity/Tools/Integration/MotionAuthority/r4']:
 for p in base.rglob('*'):
  if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.sha256']:files.append(p)
files.append(R/'unity/Assets/Ashfall/Integration/MotionAuthority/MotionPublicationPreparation.cs')
manifest=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(set(files))]
m=D/'SHA256-manifest.json';m.write_text(json.dumps(manifest,indent=2));(D/'SHA256-manifest.sha256').write_text(hashlib.sha256(m.read_bytes()).hexdigest()+'  SHA256-manifest.json\n')
print('Sealed',len(manifest),'files',sum(x['bytes'] for x in manifest),'bytes; production SHA256',summary['sourceSha256'])
print('run04 nonzero exits',[(Path(x['argv'][0]).name,x['exit']) for x in cmd if x['exit']])
