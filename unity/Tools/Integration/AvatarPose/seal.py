import pathlib,json,hashlib
ROOT=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/AvatarPoseIntegrationR1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
context=[]
for folder in ['AvatarR4','AvatarLifecycleR6','reviews/AvatarLifecycle-r6']:
    base=ROOT/'docs/agent-runs/20261104-three-chats/unity'/folder
    for name in ['REPORT.md','candidate-manifest.json','artifact-sha256.json','changed-file-sha256.json','candidate-inputs.json','consumed-before.json','before-sha256.json']:
        if (base/name).is_file():context.append(base/name)
context+=[ROOT/'campaign-staging/20261104-three-chats/skins/accepted-manifest-v000.json']
for folder in ['unity/Assets/Ashfall/Integration/WorldRuntime','unity/Assets/Ashfall/Modules/Presentation']:
    context+=list((ROOT/folder).rglob('*.cs'))
base=ROOT/'docs/agent-runs/20261104-three-chats/unity/editor-probes/avatar-r6-play-reload-04'
context+=[base/n for n in ['execution.json','inputs.json','fixture-inputs.json','Editor.log'] if (base/n).is_file()]
(DOC/'source-context.json').write_text(json.dumps([{'path':str(p.relative_to(ROOT)),'sha256':sha(p)} for p in context],indent=2))
before=json.loads((DOC/'consumed-before.json').read_text(encoding='utf-8-sig'))
(DOC/'consumed-after.json').write_text(json.dumps([dict(row,current=sha(ROOT/row['path']),match=sha(ROOT/row['path'])==row['sha256']) for row in before],indent=2))
folders=['unity/Assets/Ashfall/Integration/AvatarPose','unity/Tools/Integration/AvatarPose','docs/agent-runs/20261104-three-chats/unity/AvatarPoseIntegrationR1','campaign-staging/20261104-three-chats/unity/AvatarPoseIntegrationR1']
rows=[]
for folder in folders:
    for p in sorted((ROOT/folder).rglob('*')):
        if p.is_file() and p.name not in ['changed-file-sha256.json','changed-file-sha256.json.sha256']:
            rows.append({'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':sha(p)})
manifest=DOC/'changed-file-sha256.json';manifest.write_text(json.dumps(rows,indent=2))
digest=sha(manifest);(DOC/'changed-file-sha256.json.sha256').write_text(digest+'\n')
print(json.dumps({'files':len(rows),'manifestSHA256':digest,'consumedDrift':sum(sha(ROOT/r['path'])!=r['sha256'] for r in before)}))
