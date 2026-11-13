import pathlib,hashlib,json,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/MatchPingRecoveryR12'; D=R/'docs/agent-runs/20261104-three-chats/unity/MatchPingRecoveryR12';S.mkdir(exist_ok=True);D.mkdir(exist_ok=True)
rows=[]
paths=[]
for folder in ['Core','RecoveryContracts','Modules/Modes','Modules/ModesRecovery','Modules/HealthStates','Modules/Combat','Modules/Sessions','Modules/Persistence','Integration/CombatPublication','Integration/MatchDirector','Integration/MatchRounds','Integration/MatchRecovery']:
 paths+=list((R/'unity/Assets/Ashfall'/folder).glob('*.cs'))
paths+=list((R/'unity/Documentation').glob('*.md'))
paths+=list((R/'unity/Tools/Integration/MatchRecovery').glob('*'))
paths+=list((R/'campaign-staging/20261104-three-chats/unity/reviews/DirectorArchiveReviewR11').glob('IndependentTests.cs'))
for name in ['GAMEPLAY-CHECKPOINT-v1.md']+['accepted-neutral-v%d.json'%n for n in range(1,7)]+['phase21/ROOT-REPORT.md','phase21/source-index.json','reviews/DirectorArchiveReviewR11/REPORT.md','PingOwnerRecoveryR1/REPORT.md']:
 p=R/'docs/agent-runs/20261104-three-chats/unity'/name
 if p.exists():paths.append(p)
for p in paths:
 q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
 if q.exists():raise Exception('already frozen')
 shutil.copyfile(p,q);rows.append(dict(source=p.relative_to(R).as_posix(),snapshot=q.relative_to(R).as_posix(),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
(D/'inputs.json').write_text(json.dumps(rows,indent=2))
print(len(rows),'frozen')
