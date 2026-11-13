import pathlib,json,hashlib,re,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/NetworkRuntimeR2';D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2'
sha=lambda b:hashlib.sha256(b).hexdigest().upper()
accepted=[]
def walk(x):
 if isinstance(x,dict):
  if 'path' in x and 'expected' in x:
   p=pathlib.Path(x['path']);p=p if p.is_absolute() else R/p
   accepted.append(dict(path=str(p),expected=x['expected'],actual=sha(p.read_bytes()) if p.is_file() else 'MISSING'))
  for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
for i in range(1,7):walk(json.loads((R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%i)).read_text(encoding='utf-8-sig')))
(D/'accepted-verification.json').write_text(json.dumps(accepted,indent=2))
baseline=json.loads((D/'baseline.json').read_text());drift=[]
for x in baseline:
 p=R/x['original'];b=(R/x['snapshot']).read_bytes();assert sha(b)==x['sha256']
 drift.append(dict(original=x['original'],frozen=x['sha256'],current=sha(p.read_bytes()),kind=x['kind']))
(D/'baseline-current-drift.json').write_text(json.dumps(drift,indent=2))
refs=[R/'docs/TWO-CHAT-20-AGENT-OVERRIDE.md',R/'docs/HILL-CLIMB-ORCHESTRATION.md',R/'docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md',R/'docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md',R/'docs/agent-runs/20261104-0020/preflight.json']
refs+=list((R/'unity/Documentation').glob('*.md'))
U=R/'docs/agent-runs/20261104-three-chats/unity'
refs += [U/('accepted-neutral-v%d.json'%i) for i in range(1,7)]
refs += [U/p for p in ['phase12/source-index.json','phase12/root-validation.json','phase12/player-retry-validation.json','reviews/NetworkRuntime-r1/REPORT.md','reviews/NetworkRuntime-r1/ADDENDUM.md','reviews/SessionAdmission-r6/REPORT.md','SessionReplicationR7/REPORT.md','SessionReplicationR7/API-STUB.txt']]
P=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
refs += [P/'Packages/manifest.json',P/'Packages/packages-lock.json',P/'Library/PackageCache/com.unity.transport@1b9b1e9062a8/Runtime/NetworkParams.cs',P/'Library/PackageCache/com.unity.transport@1b9b1e9062a8/Runtime/NetworkSettings.cs',P/'Library/PackageCache/com.unity.transport@1b9b1e9062a8/Runtime/NetworkDriver.cs',P/'Library/PackageCache/com.unity.transport@1b9b1e9062a8/package.json']
Q=S/'references';Q.mkdir(exist_ok=True);rr=[]
for i,p in enumerate(refs):
 b=p.read_bytes();q=Q/(str(i)+'-'+p.name);q.write_bytes(b);rr.append(dict(original=str(p),snapshot=str(q),sha256=sha(b)))
(D/'references.json').write_text(json.dumps(rr,indent=2))
symbols=[]
for p in sorted((R/'unity/Assets/Ashfall/Integration/NetworkRuntime').rglob('*.cs')):
 symbols.append(dict(path=str(p.relative_to(R)),sha256=sha(p.read_bytes()),declarations=[line.strip() for line in p.read_text().splitlines() if re.match(r'\s*(public|internal) ',line)]))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
run=sorted(S.glob('run*'))[-1]
shutil.copyfile(D/run.name/'root-api.patch',D/'ROOT-REVIEW-ONLY.patch')
print('accepted',len(accepted),'mismatches',sum(x['actual']!=x['expected'].upper() for x in accepted),'baseline snapshots',len(baseline),'phase12-current drift',sum(x['kind']=='phase12' and x['current']!=x['frozen'] for x in drift))
