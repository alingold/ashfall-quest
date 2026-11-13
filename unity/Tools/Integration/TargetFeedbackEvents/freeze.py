from pathlib import Path
import hashlib,json,shutil
root=Path(r'C:/Users/aling/Documents/ashfall'); out=root/'campaign-staging/20261104-three-chats/unity/TargetFeedbackEventsR10'; docs=root/'docs/agent-runs/20261104-three-chats/unity/TargetFeedbackEventsR10'
paths=list((root/'unity/Documentation').glob('*.md'))+list((root/'docs').glob('*2026-11-03.md'))+[root/'docs/TWO-CHAT-20-AGENT-OVERRIDE.md',root/'docs/HILL-CLIMB-ORCHESTRATION.md',root/'docs/agent-runs/20261104-0020/preflight.json']
u=root/'docs/agent-runs/20261104-three-chats/unity'
paths += [u/'GAMEPLAY-CHECKPOINT-v1.md',u/'phase21/ROOT-REPORT.md',u/'phase21/source-index.json']+list(u.glob('accepted-neutral-v*.json'))
paths += list((root/'unity/Assets/Ashfall/Integration/PersonalSettings').rglob('*.cs'))+list((root/'unity/Assets/Ashfall/Modules/Presentation').glob('*.cs'))
r=root/'campaign-staging/20261104-three-chats/unity/reviews/PresentationLiveReviewR9'
paths += list(r.glob('*.rsp'))+list((r/'frozen').rglob('*.cs'))+[r/'frozen/225/damage_numbers0992.gd',r/'frozen/229/damage_numbers0992.gd',u/'reviews/PresentationLiveReviewR9/REPORT.md']
rows=[]
for i,p in enumerate(dict.fromkeys(paths)):
 b=p.read_bytes(); dst=out/'inputs'/str(i)/p.name; dst.parent.mkdir(parents=True,exist_ok=True); assert not dst.exists(); dst.write_bytes(b);rows.append(dict(original=str(p),snapshot=str(dst),sha256=hashlib.sha256(b).hexdigest()))
(docs/'input-manifest.json').write_text(json.dumps(rows,indent=2))
print('Frozen',len(rows),'files, policy and accepted ledgers parsed in full')
for p in paths:
 if p.suffix in ('.md','.json'): p.read_text(encoding='utf-8-sig')
