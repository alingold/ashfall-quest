from pathlib import Path
import json,hashlib
R=Path.cwd();P=R/'campaign-staging/20261104-three-chats/unity/AudioRuntimeR2';E=R/'docs/agent-runs/20261104-three-chats/unity/AudioRuntimeR2'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
for rel in ['phase08-ownership.json','phase10-ownership.json','phase10/audio-small-fix-input.json','phase10/root-repairs.json','phase10/review-inputs.json','phase09/native-summary.json','phase07/status.json','editor-probes/avatar-lifecycle-r6-native/execution.json','editor-probes/avatar-lifecycle-r6-native/result.json','editor-probes/Avatar-r5-native-05/result.json','reviews/AudioRuntime-r1/REPORT.md']:
 src=R/'docs/agent-runs/20261104-three-chats/unity'/rel;dest=P/'root-context'/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(src.read_bytes())
records=json.loads((P/'run05/inputs.json').read_text());drift=[]
for row in records:
 path=Path(row['path']);actual=sha(path);expected=row['sha256'].upper()
 drift.append(dict(path=str(path),expected=expected,actual=actual,match=actual==expected))
(E/'dependency-drift.json').write_text(json.dumps(drift,indent=2));assert all(x['match'] for x in drift)
changed=[];before=[]
for folder,old in [('unity/Assets/Ashfall/Integration/AudioRuntime','before-source'),('unity/Tools/Integration/AudioRuntime','before-tools')]:
 base=R/folder
 for p in sorted(base.rglob('*')):
  if not p.is_file():continue
  previous=P/old/p.relative_to(base);dest=P/'candidate'/p.relative_to(R);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes())
  row=dict(path=str(p.relative_to(R)),sha256=sha(p),snapshot=str(dest.relative_to(R)),before_sha256=sha(previous) if previous.exists() else None)
  if row['sha256']!=row['before_sha256']:changed.append(row)
 for p in sorted((P/old).rglob('*')):
  if p.is_file():before.append(dict(path=str(p.relative_to(P)),sha256=sha(p),bytes=p.stat().st_size))
(E/'before-sha256.json').write_text(json.dumps(before,indent=2));(E/'changed-file-sha256.json').write_text(json.dumps(dict(status='UNREVIEWED; no promotion',files=changed),indent=2))
rows=[]
for root in [P,E]:
 for p in sorted(root.rglob('*')):
  if p.is_file() and p.name not in ['SHA256-MANIFEST.json','SHA256-MANIFEST.sha256']:
   rows.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
manifest=E/'SHA256-MANIFEST.json';manifest.write_text(json.dumps(rows,indent=2));(E/'SHA256-MANIFEST.sha256').write_text(sha(manifest)+'  SHA256-MANIFEST.json\n')
print(json.dumps(dict(changed_files=len(changed),sealed_records=len(rows),drift=sum(not x['match'] for x in drift),manifest_sha256=sha(manifest)),indent=2))
# Verify seal immediately, without mutating artifacts.
assert all(sha(R/x['path'])==x['sha256'] for x in rows)
