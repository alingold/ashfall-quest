from pathlib import Path
import hashlib,json
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/SourceDisposableCommitR2';D=R/'docs/agent-runs/20261104-three-chats/unity/SourceDisposableCommitR2'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
before=json.loads((D/'before-manifest.json').read_text());drift=[]
owned=['unity/Assets/Ashfall/Modules/InventoryItems/SourceDisposableDepletion.cs','unity/Assets/Ashfall/Modules/InventoryItems/PreparedConsumables.cs','unity/Assets/Ashfall/Modules/WeaponHandling/SourceExplosiveMechanics.cs','unity/Assets/Ashfall/Modules/WeaponHandling/PreparedWeapons.cs']
for entry in before:
 p=R/entry['path'];h=digest(p) if p.exists() else None
 if h!=entry['sha256']:drift.append(dict(path=entry['path'],before=entry['sha256'],end=h,owned=entry['path'].replace('\\','/') in owned))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
files=list(S.rglob('*'))+list((R/'unity/Tools/Integration/SourceDisposableCommit').rglob('*'))+list(D.glob('*'))
for name in owned+['unity/Assets/Ashfall/Modules/WeaponHandling/SourceDispositionReservation.cs']:files.append(R/name)
F=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319')
files += [F/n for n in ['csc.exe','mscorlib.dll','System.dll','System.Core.dll']]+[Path('C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')]
manifest=[]
for p in sorted(set(files)):
 if p.is_file() and p.name!='final-manifest.json':manifest.append(dict(path=str(p),bytes=p.stat().st_size,sha256=digest(p)))
(D/'final-manifest.json').write_text(json.dumps(manifest,indent=2))
for n in ['WeaponHandling','InventoryItems']:
 for p in (S/'run08'/n).glob('*.cs'):
  active=R/'unity/Assets/Ashfall/Modules'/n/p.name
  assert digest(p)==digest(active),'Post-test source change '+str(active)
print('Sealed',len(manifest),'files. Input changes:',len(drift),'unowned:',sum(not d['owned'] for d in drift))
print('Reservation observations', (S/'run08/ReservationTests-run.log').read_text().count('PASS '))
