from pathlib import Path
import hashlib,json,shutil
r=Path(r'C:\Users\aling\Documents\ashfall');o=r/'campaign-staging/20261104-three-chats/unity/PlayerIntegrationR5/r5';d=r/'docs/agent-runs/20261104-three-chats/unity/PlayerIntegrationR5/r5'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
before=json.loads((o/'before.json').read_text());drift=[]
for row in before:
 p=r/row['path'];drift.append(dict(path=row['path'],before=row['sha256'].upper(),after=h(p),changed=h(p)!=row['sha256'].upper(),owned='/Modules/Player/' in row['path'].replace('\\','/')))
(d/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
m=r/'docs/agent-runs/20261104-three-chats/unity/phase05/candidates/Player-r4/candidate-manifest.json';verification=[]
for x in json.loads(m.read_text())['files']:
 p=r/x['path'];verification.append(dict(path=x['path'],match=h(p)==x['sha256'].upper() and p.stat().st_size==x['bytes']))
(d/'frozen-r4-verification.json').write_text(json.dumps(dict(manifest=h(m),expectedManifest='A72F2FE94199FD40C1508E3C4479E1F5FACA9EF38D1CA00BDB983208C13701CE',files=verification),indent=2))
package=r/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/PackageCache/com.unity.xr.openxr@b5b77b4027be'
for rel in ['Runtime/Features/OpenXRFeature.cs','Runtime/NativeTypes/Core/XrSessionState.cs','package.json']:
 dest=o/'openxr-inputs'/Path(rel).name;dest.parent.mkdir(exist_ok=True);shutil.copyfile(package/rel,dest)
for p in (r/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'):
 q=o/'candidate-source'/p.relative_to(r);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
print('frozen manifest',h(m),'files matching',sum(x['match'] for x in verification),'/',len(verification))
print('external drift',[x['path'] for x in drift if x['changed'] and not x['owned']])
