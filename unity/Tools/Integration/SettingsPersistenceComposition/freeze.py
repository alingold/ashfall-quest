from pathlib import Path
import json,hashlib,shutil
R=Path(__file__).resolve().parents[4]
B=R/'campaign-staging/20261104-three-chats/unity/SettingsPersistenceCompositionR7'
F=B/'before'; F.mkdir(exist_ok=False)
paths=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for d in ['unity/Assets/Ashfall/Modules/Persistence','unity/Assets/Ashfall/Modules/SaveMigration','unity/Assets/Ashfall/Integration/MigrationRuntime','unity/Tools/Integration/MigrationRuntime','unity/Documentation']:
 paths+= [p for p in (R/d).glob('*') if p.is_file()]
C=R/'docs/agent-runs/20261104-three-chats/unity'
paths += [C/f'accepted-neutral-v{i}.json' for i in range(1,7)]
paths += [C/x for x in ['phase21/source-index.json','phase21/ROOT-REPORT.md','phase25/ROOT-REPORT.md','GAMEPLAY-CHECKPOINT-v1.md','reviews/SettingsMigrationRuntimeReviewR6/REPORT.md','phase06/SaveMigration-r5/candidate-manifest.json']]
V=R/'campaign-staging/20261104-three-chats/unity/reviews/SettingsMigrationRuntimeReviewR6'
paths += [V/x for x in ['IndependentTests.cs','PartialWriteTests.cs']]
records=[]
for p in paths:
 d=F/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,d)
 records.append(dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(d.read_bytes()).hexdigest()))
(B/'before-inputs.json').write_text(json.dumps(records,indent=2))
print('Frozen',len(records),'inputs')
