import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/NetworkRuntimeR2';D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
assert not (D/'FINAL-VERIFICATION.json').exists()
run=S/'run05';rows=[]
for x in json.loads((D/'run05/inputs.json').read_text()):
 p=pathlib.Path(x['snapshot']);assert sha(p)==x['sha256'];live=R/x['original']
 rows.append(dict(original=x['original'],tested=x['sha256'],current=sha(live),match=sha(live)==x['sha256'],kind=x['kind']))
assert all(x['match'] for x in rows if x['kind']=='candidate')
identity=json.loads((D/'run05/patch-identity.json').read_text());assert sha(run/'CoordinatorWithRootPatch.cs')==identity['patched']
references=json.loads((D/'native-api07/references.json').read_text())
for x in references:assert sha(pathlib.Path(x['snapshot']))==x['sha256']
assert json.loads((D/'native-api07/command.json').read_text())['exit']==0
assert all(x['exit']==0 for x in json.loads((D/'run05/commands.json').read_text()))
before=json.loads((D/'baseline.json').read_text())
assert all(sha(R/x['snapshot'])==x['sha256'] for x in before)
out=dict(status='candidate-root-patch-required',cpuAssertions=1262,compatibilityAssertions=50,nativeApiCompile=0,nativeExecuted=False,independentReview=False,acceptedRecords=1699,testedCurrent=rows,rootPatchIdentity=identity,baselineSnapshots=len(before),initialPhase12Drift=sum(x['kind']=='phase12' and x['current']!=x['sha256'] for x in before),remainingGates=['root dependency API/rehello patch adoption','independent review','production authentication provisioning','native two-endpoint execution and external census','scene/bootstrap integration','WAN/Quest/VR/durable lifecycle'])
(D/'FINAL-VERIFICATION.json').write_text(json.dumps(out,indent=2))
manifest=[]
for root in [R/'unity/Assets/Ashfall/Integration/NetworkRuntime',R/'unity/Tools/Integration/NetworkRuntime',S,D]:
 for p in sorted(root.rglob('*')):
  if p.is_file():manifest.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
p=D/'candidate-manifest.json';p.write_text(json.dumps(manifest,indent=2));(D/'candidate-manifest.sha256').write_text(sha(p)+'\n')
print('sealed',len(manifest),'files manifest SHA256',sha(p),'selected dependency drift',sum(not x['match'] for x in rows if x['kind']=='phase12'))
