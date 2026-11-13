from pathlib import Path
import hashlib,json
R=Path(r'C:\Users\aling\Documents\ashfall');O=R/'campaign-staging/20261104-three-chats/unity/DesktopPawnR1';C=O/'compile03'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
package=next((R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/PackageCache').glob('com.unity.inputsystem@*'))
api=[package/'package.json']+[next(package.rglob(name)) for name in ['Keyboard.cs','Mouse.cs','KeyControl.cs','DeltaControl.cs','ButtonControl.cs']]
rows=[]
for i,p in enumerate(api):
    text=p.read_text(encoding='utf-8-sig');q=C/('api-%02d-'%i+p.name);q.write_bytes(p.read_bytes())
    rows.append(dict(path=str(p),snapshot=str(q.relative_to(R)),sha256=sha(q),bytes=q.stat().st_size))
(C/'input-system-source-read.json').write_text(json.dumps(rows,indent=2))
assert json.loads((package/'package.json').read_text())['version']=='1.20.1'
# Tool bytes frozen as part of candidate; shared/current compatibility kept separate.
for p in (R/'unity/Tools/Integration/DesktopPlaytest/DesktopPawnR1').glob('*.py'):
    q=C/('tool-'+p.name);q.write_bytes(p.read_bytes())
inputs=json.loads((C/'inputs.json').read_text())
comparison=[dict(path=r['path'],frozen=r['sha256'],current=sha(R/r['path']),match=r['sha256']==sha(R/r['path'])) for r in inputs]
(O/'current-compatibility.json').write_text(json.dumps(comparison,indent=2))
roots=[R/'unity/Assets/Ashfall/Integration/DesktopPlaytest/Pawn',R/'unity/Tools/Integration/DesktopPlaytest/DesktopPawnR1',O]
out=[]
for root in roots:
    for p in sorted(root.rglob('*')):
        if p.is_file():out.append(dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)))
(R/'docs/agent-runs/20261104-three-chats/unity/DesktopPawnR1/manifest.json').write_text(json.dumps(out,indent=2))
print('Sealed',len(out),'files; source current matches',all(r['match'] for r in comparison))
