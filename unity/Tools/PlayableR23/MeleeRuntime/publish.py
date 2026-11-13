from pathlib import Path
import shutil,uuid
T=Path(__file__).resolve().parent;R=T.parents[3];H=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';G=R/'unity/Assets/Ashfall/Integration/Playable/Gallery';O=R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
# Publication only after Root's explicit 18:55:52 terminal release.
for p in (T/'Payload').iterdir():
 if p.is_file():shutil.copyfile(p,O/p.name)
for p in (T/'Staged').glob('*.cs'):
 destination=G if p.name=='R23KnifeGallery.cs'else H;shutil.copyfile(p,destination/p.name)
for p in (T/'Candidate').glob('*.cs'):
 destination=G if p.name=='R23LobbyWeaponGallery.cs'else H;assert (destination/p.name).exists(),p.name;shutil.copyfile(p,destination/p.name)
for p in [*H.glob('R24Knife*.cs'),H/'R23HandsWeapons.Knife.cs',H/'R23RoundState.Knife.cs',G/'R23KnifeGallery.cs',*O.glob('knife*')]:
 if p.suffix=='.meta':continue
 meta=p.with_name(p.name+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
p=R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt';s=p.read_text()
for n in ['R24KnifeRuntime.cs','R23RoundState.Knife.cs']:
 line='unity\\Assets\\Ashfall\\Integration\\Playable\\HandsWeapons\\'+n
 if line not in s:s+='\n'+line
p.write_text('\n'.join(x for x in s.splitlines()if x.strip())+'\n')
print('Published coherent genuine knife18 owner, actual contact dispatch, source payload and isolated gallery; no Root/UI/helper edits')
