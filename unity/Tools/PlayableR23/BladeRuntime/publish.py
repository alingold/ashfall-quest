from pathlib import Path
import shutil,uuid,json,hashlib
T=Path(__file__).resolve().parent;R=T.parents[3];B=R/'unity/Assets/Ashfall/Integration/Playable';P=R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
# Explicit release required by parent; do not use during coordinated Assets HOLD.
H=B/'HandsWeapons';G=B/'Gallery';I=B/'Input'
def meta(p):
 m=p.with_name(p.name+'.meta')
 if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
changed=[]
for folder in ['Candidate','Staged']:
 for p in (T/folder).iterdir():
  if p.suffix not in ['.cs','.shader']:continue
  dest=(H/'Resources'/p.name)if p.suffix=='.shader'else(I/p.name)if p.name=='R23WeaponWheelIcons.cs'else(G/p.name)if p.name in ['R23KnifeGallery.cs','R23LobbyWeaponGallery.cs']else H/p.name
  if not dest.exists()or dest.read_bytes()!=p.read_bytes():shutil.copyfile(p,dest);changed.append(str(dest.relative_to(R)))
  meta(dest)
for p in (T/'Payload').iterdir():
 if not p.is_file():continue
 dest=P/p.name
 if not dest.exists()or dest.read_bytes()!=p.read_bytes():shutil.copyfile(p,dest);changed.append(str(dest.relative_to(R)))
 meta(dest)
p=R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt';s=p.read_text()
for name in ['R24MeleeRuntime.cs','R24MeleeProfiles.cs']:
 line='unity/Assets/Ashfall/Integration/Playable/HandsWeapons/'+name
 if line not in s.splitlines():s=s.rstrip()+'\n'+line+'\n'
p.write_text(s)
(T/'published.json').write_text(json.dumps(dict(files=changed,sourceManifest='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111',engine=False),indent=2)+'\n');print('Published coherent source sword21 candidate:',len(changed),'files; no engine')
