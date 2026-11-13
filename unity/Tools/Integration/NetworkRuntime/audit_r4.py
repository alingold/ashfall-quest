"""Read/preserve policy and provenance; no engine or mutable external outputs."""
import difflib, hashlib, json, pathlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/NetworkDispatchR4'
D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkDispatchR4'
sha=lambda b:hashlib.sha256(b).hexdigest().upper()
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
base=R/'docs/agent-runs/20261104-three-chats/unity'
paths=[R/'docs'/n for n in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md']]
paths += [R/'docs/agent-runs/20261104-0020/preflight.json',base/'GAMEPLAY-CHECKPOINT-v1.md']
paths += list((R/'unity/Documentation').glob('*.md'))
paths += list(base.glob('accepted-neutral-v*.json'))
paths += [base/'phase16'/n for n in ['source-index.json','dispatch-plan.json']]
paths += list((base/'NetworkNativeLifecycleR3').glob('*.*'))
paths += list((base/'NetworkNativeLifecycleR3/verification03').glob('*.*'))
paths += list((base/'editor-probes/network-r3-native').glob('*.*'))
paths += [base/'SessionStateReplicationR8/REPORT.md',base/'reviews/NetworkRuntimeReviewR2/REPORT.md']
rows=[]
for p in paths:
    b=p.read_bytes();q=S/'evidence'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
    if q.exists():assert q.read_bytes()==b,str(q)
    else:q.write_bytes(b)
    rows.append(dict(original=str(p),snapshot=str(q),sha256=sha(b),bytes=len(b)))
(D/'evidence-inputs.json').write_text(json.dumps(rows,indent=2))
accepted=[]
def walk(x):
    if isinstance(x,dict):
        if 'path' in x and ('sha256' in x or 'expected' in x):
            p=pathlib.Path(x['path']);p=p if p.is_absolute() else R/p
            if p.is_file():
                expected=x.get('sha256',x.get('expected'));actual=sha(p.read_bytes())
                accepted.append(dict(path=str(p),expected=expected,actual=actual,match=actual.lower()==str(expected).lower()))
        for v in x.values():walk(v)
    elif isinstance(x,list):
        for v in x:walk(v)
for n in range(1,7):
    p=base/('accepted-neutral-v%d.json'%n);data=load(p);walk(data)
    print(p.name, 'status',data.get('status'),'modules',data.get('modules'))
(D/'accepted-audit.json').write_text(json.dumps(accepted,indent=2))
phase=[]
for row in load(base/'phase16/source-index.json'):
    p=R/row['snapshot'];actual=sha(p.read_bytes());assert actual==row['sha256'],str(p)
    live=R/row['original'];phase.append(dict(original=row['original'],snapshot=row['snapshot'],sha256=actual,current=sha(live.read_bytes()) if live.exists() else 'MISSING'))
(D/'phase16-audit.json').write_text(json.dumps(phase,indent=2))
changes=[]
before=load(S/'before-manifest.json')
patch=[]
for row in before:
    p=R/row['path'];q=S/'before'/row['path'];assert sha(q.read_bytes()).lower()==row['sha256'].lower()
    if p.is_file() and p.read_bytes()!=q.read_bytes() and '/Documentation/' not in str(p).replace('\\','/'):
        changes.append(dict(path=row['path'],before=sha(q.read_bytes()),after=sha(p.read_bytes())))
        patch+=list(difflib.unified_diff(q.read_text(encoding='utf-8-sig').splitlines(True),p.read_text(encoding='utf-8-sig').splitlines(True),fromfile='before/'+row['path'],tofile=row['path']))
(D/'changed-files.json').write_text(json.dumps(changes,indent=2));(D/'candidate.patch').write_text(''.join(patch))
# Compare every consumed source to current aliases separately; no whole-tree claim.
compiled=load(D/'verification03/compiled-sources.json');compat=[]
for row in compiled:
    if row['profile']!='exact-verification03':continue
    p=pathlib.Path(row['original']);s=str(p).replace('\\','/')
    if '/inputs/unity/' not in s:continue
    live=R/('unity/'+s.split('/inputs/unity/',1)[1]);candidate=next((x for x in compiled if x['profile']=='candidate' and x['original']==str(live)),None)
    used=candidate or row
    compat.append(dict(current=str(live),compiledSha=used['sha256'],currentSha=sha(live.read_bytes()),matches=used['sha256']==sha(live.read_bytes())))
(D/'current-compatibility.json').write_text(json.dumps(compat,indent=2))
commands=load(D/'verification03/commands.json');native=next(x for x in commands if x['name']=='native-api-compile')
(D/'root-handoff.json').write_text(json.dumps(dict(entry='Ashfall.NetworkRuntimeFixtures.NativeFixtureR3.Run()',throwingEntry='Ashfall.NetworkRuntimeFixtures.NativeFixtureR3.RunForRoot()',nativeCompile=native,defines=['UNITY_EDITOR','UNITY_6000_0_OR_NEWER','ASHFALL_BINDING_RETIREMENT_API'],execution='ROOT ONLY under campaign-staging/20261104-three-chats/world/engine_lane.py',expected='Failures=0; Unknowns=1; generation2 discard-old-owner reason=disconnected followed by actual-dispatch accepted; preserve complete shutdown log and external tracked allocation observation',historical='107 checks / 1 failure / 1 UNKNOWN unchanged',globalCensus='UNKNOWN',sourceState='not in this member-replication closure'),indent=2))
root=load(base/'editor-probes/network-r3-native/inputs.json');root_audit=[]
exact_hashes={x['sha256'] for x in compiled if x['profile']=='exact-verification03'}
for row in root['allCompileInputs']:
    p=R/row['path'];current=sha(p.read_bytes()) if p.exists() else 'MISSING'
    root_audit.append(dict(path=row['path'],rootExpected=row['sha256'],current=current,matchesVerification03=row['sha256'] in exact_hashes))
    # Root's live project may already have been restaged by root. Preserve, label,
    # and never confuse its current wrapper with original execution evidence.
    if p.is_file():
        q=S/'root-tools-current'/(str(len(root_audit))+'-'+p.name);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
        root_audit[-1]['currentSnapshot']=str(q)
(D/'root-input-audit.json').write_text(json.dumps(root_audit,indent=2))
print('phase16 frozen',len(phase),'accepted path checks',len(accepted),'changes',len(changes),'current source drift',sum(not x['matches'] for x in compat))
