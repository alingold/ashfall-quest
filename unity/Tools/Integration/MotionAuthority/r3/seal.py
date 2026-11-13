from pathlib import Path
import hashlib,json,re
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/MotionPreparedOwnersR3';D=R/'docs/agent-runs/20261104-three-chats/unity/MotionPreparedOwnersR3'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest()
ctx=[]
for p in (R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):
 j=json.loads(p.read_text(encoding='utf-8-sig'));q=O/'context'/p.name;q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());ctx.append(dict(source=str(p),snapshot=str(q),sha256=h(q),summary={k:j[k] for k in ['extends','status'] if k in j},entries=len(j.get('accepted',j.get('modules',[])))))
idx=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase16/source-index.json').read_text());drift=[]
for x in idx:
 p=R/x['original'];q=R/x['snapshot'];assert h(q).lower()==x['sha256'].lower();drift.append(dict(source=x['original'],pin=x['sha256'],current=h(p) if p.exists() else None,matches=p.exists() and h(p).lower()==x['sha256'].lower()))
(D/'phase16-comparison.json').write_text(json.dumps(drift,indent=2));(D/'context-read.json').write_text(json.dumps(ctx,indent=2))
inputs=json.loads((D/'inputs.json').read_text());after=[]
for x in inputs:
 p=Path(x['snapshot']);assert h(p)==x['sha256'];live=R/x['source'];after.append(dict(source=x['source'],snapshot=x['snapshot'],sha256=x['sha256'],current=h(live) if live.exists() else None,matches=live.exists() and h(live)==x['sha256']))
(D/'input-verification-after.json').write_text(json.dumps(after,indent=2))
changed=['unity/Assets/Ashfall/Modules/Flight/Capsules.cs','unity/Assets/Ashfall/Modules/Climbing/ClimbController.cs','unity/Assets/Ashfall/Modules/Climbing/PreparedClimbMotion.cs']
changed+= [str(p.relative_to(R)).replace('\\','/') for p in (R/'unity/Tools/Integration/MotionAuthority/r3').glob('*') if p.is_file()]
(D/'changed-files-sha256.json').write_text(json.dumps([dict(path=p,sha256=h(R/p)) for p in changed],indent=2))
symbols=[]
for rel in changed[:3]:
 for i,line in enumerate((R/rel).read_text().splitlines(),1):
  if re.search(r'(public|internal) .*?(Prepare|Commit|CheckpointReady|Abort|ValidateAuthorization|CopyState|SupportedFields|Publish)',line):symbols.append(dict(path=rel,line=i,symbol=line.strip(),sha256=h(R/rel)))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2));print('phase16',len(idx),'current drift',sum(not x['matches'] for x in drift),'input copies verified',len(inputs))
