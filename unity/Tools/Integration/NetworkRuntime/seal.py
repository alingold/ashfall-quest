import hashlib,json,pathlib,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR1'
S=R/'campaign-staging/20261104-three-chats/unity/NetworkRuntimeR1'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
attempt=sorted(S.glob('attempt-*'))[-1]
inputs=json.loads((D/attempt.name/'inputs.json').read_text())
drift=[]
for row in json.loads((D/'initial-inputs.json').read_text(encoding='utf-8-sig')):
    p=R/row['path'];drift.append(dict(path=str(p.relative_to(R)),initial=row['sha256'],current=sha(p),changed=row['sha256']!=sha(p)))
(D/'dependency-drift-final.json').write_text(json.dumps(drift,indent=2))
reads=[]
for p in sorted((D.parent).glob('accepted-neutral-v*.json')):
    j=json.loads(p.read_text(encoding='utf-8-sig'));reads.append(dict(path=str(p.relative_to(R)),sha256=sha(p),top_level_fields=list(j),status=j.get('status'),scope='read-only bounded neutral receipt; no promotion inferred'))
(D/'accepted-receipts-read.json').write_text(json.dumps(reads,indent=2))
package=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/PackageCache/com.unity.transport@1b9b1e9062a8'
refs=[]
for rel in ['package.json','Runtime/NetworkDriver.cs','Runtime/NetworkConnection.cs','Runtime/NetworkEndpoint.cs','Runtime/NetworkParams.cs']:
    p=package/rel;q=S/'native-api-source'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
    refs.append(dict(original=str(p),snapshot=str(q),sha256=sha(q)))
(D/'transport-6.6-api-inputs.json').write_text(json.dumps(refs,indent=2))
native_sources=[x for x in inputs if 'candidate' in x['snapshot'] and x['path'].endswith('.cs')]
deps=[x for x in inputs if 'current-run' in x['snapshot']]
(D/'native-request.json').write_text(json.dumps(dict(status='STAGED_NOT_EXECUTED_UNREVIEWED',entry='Ashfall.NetworkRuntimeFixtures.NativeFixture.Run',editor='6000.6.8f1',transport='6.6.0',lane='Coordinator engine_lane.py only; no leaf engine invocation',defines=['UNITY_EDITOR','UNITY_6000_0_OR_NEWER'],sources=native_sources,neutral_dependencies=deps,assembly='Existing single default runtime assembly; not a separate DLL/asmdef claim',network='Two ephemeral IPv4 loopback endpoints only; synthetic authenticated records',required=['guard-ON actual package API compilation','two-driver member-view/real Session mutation execution','native send-pressure and disconnect assertions','native allocation/leak diagnostics across partial constructor failure stages 1/2 and never-started/normal/idempotent disposal','fresh independent source review'],excluded=['WAN','Quest','physical auth provisioning','whole game state','scene/bootstrap integration']),indent=2))
roots=[R/'unity/Assets/Ashfall/Integration/NetworkRuntime',R/'unity/Tools/Integration/NetworkRuntime',D,S]
rows=[]
for root in roots:
    for p in sorted(root.rglob('*')):
        if p.is_file() and p.name not in ['candidate-manifest.json','manifest.sha256.txt']:
            rows.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
(D/'candidate-manifest.json').write_text(json.dumps(dict(status='UNREVIEWED',final_cpu_attempt=attempt.name,files=rows),indent=2))
(D/'manifest.sha256.txt').write_text(sha(D/'candidate-manifest.json')+'  candidate-manifest.json\n')
print('Sealed',len(rows),'files; final CPU',attempt.name,'initial/current dependency drift',sum(x['changed'] for x in drift))
