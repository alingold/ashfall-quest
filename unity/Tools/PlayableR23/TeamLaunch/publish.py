from pathlib import Path
import hashlib,json,shutil,uuid
T=Path(__file__).resolve().parent;R=T.parents[3]
H=R/'unity/Assets/Ashfall/Integration/Playable/TeamLaunch'
O=R/'unity/Assets/StreamingAssets/PlayableR23/TeamLaunch'
copies=[(p,H/p.name)for p in (T/'Staged').glob('*.cs')]+[(p,O/p.name)for p in (T/'Payload').iterdir()if p.is_file()]
assert len(list((T/'Staged').glob('*.cs')))==2
for source,target in copies:assert source.is_file()and not target.exists(),target
for folder in [H,O]:
 folder.mkdir(parents=True,exist_ok=True);meta=Path(str(folder)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\n')
result=[]
for source,target in copies:
 shutil.copyfile(source,target);meta=Path(str(target)+'.meta');assert not meta.exists(),meta
 meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
 result.append(dict(path=target.relative_to(R).as_posix(),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
(T/'published.json').write_text(json.dumps(result,indent=2)+'\n')
p=T/'HANDOFF.txt';s=p.read_text();p.write_text(s.replace('checked component staged pending Root\'s active native freeze terminal.','PUBLISHED coherent disjoint component after Root native terminal release.'))
print('Published',len(result),'new TeamLaunch files only; no existing owner/root changes')
