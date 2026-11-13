from pathlib import Path
import hashlib,json,subprocess
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/NativeRootOutcomeR1';S=R/'campaign-staging/20261104-three-chats/unity/NativeRootOutcomeR1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
records=[];results=[]
old=R/'campaign-staging/20261104-three-chats/unity/CertifiedMotionPublicationR4'
for group in ['run04','preservation02']:
 dst=S/'historical-replay'/group;dst.mkdir(parents=True,exist_ok=False)
 # Entire DLL closure is small, frozen once; no engines/native references are executed.
 for p in list((old/group).glob('*.dll'))+list((old/group).glob('*.exe')):
  t=dst/p.name;t.write_bytes(p.read_bytes());records.append(dict(origin=str(p),snapshot=str(t),sha256=sha(t)))
 names=['PublicConsumer']+[p.stem for p in sorted(dst.glob('Retained*.exe'))] if group=='run04' else ['RootPreservation','PadPreservation','WrapperRetry']
 if group=='preservation02':names=[p.stem for p in sorted(dst.glob('*.exe'))]
 for n in names:
  p=dst/(n+'.exe');c=subprocess.run([str(p)],capture_output=True,text=True,cwd=str(dst));(dst/(n+'.log')).write_text(c.stdout+c.stderr);results.append(dict(argv=[str(p)],exit=c.returncode,output=c.stdout+c.stderr,group=group))
(D/'historical-inputs.json').write_text(json.dumps(records,indent=2));(D/'historical-results.json').write_text(json.dumps(results,indent=2))
print([(p['group'],Path(p['argv'][0]).name,p['exit'],p['output'][-90:]) for p in results])
context=[]
paths=[R/'docs/TWO-CHAT-20-AGENT-OVERRIDE.md',R/'docs/HILL-CLIMB-ORCHESTRATION.md',R/'docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md',R/'docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md',R/'docs/agent-runs/20261104-0020/preflight.json']+list((R/'unity/Documentation').glob('*.md'))+[R/('docs/agent-runs/20261104-three-chats/unity/'+p) for p in ['phase21/ROOT-REPORT.md','phase21/source-index.json','phase24/ROOT-REPORT.md','GAMEPLAY-CHECKPOINT-v1.md']]+list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
for p in paths:
 b=p.read_bytes();e=dict(path=str(p),bytes=len(b),sha256=sha(p))
 if p.suffix=='.json':
  v=json.loads(b.decode('utf-8-sig'));e['shape']=list(v) if isinstance(v,dict) else dict(records=len(v))
 context.append(e)
(D/'coordination-context.json').write_text(json.dumps(context,indent=2))
# Compiler identity separately pinned.
compiler=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
(D/'toolchain.json').write_text(json.dumps(dict(path=str(compiler),sha256=sha(compiler)),indent=2))
