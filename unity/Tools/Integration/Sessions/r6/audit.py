from pathlib import Path
import json,hashlib,shutil,subprocess
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/SessionAdmissionR6';S=R/'campaign-staging/20261104-three-chats/unity/SessionAdmissionR6'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
required=['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/CONTRACTS-v3.md','unity/Documentation/COLLISION-LAYERS-v1.md','unity/Documentation/NETWORK-LIFECYCLE-v4.md','unity/Documentation/DAMAGE-POLICY-v4.md','unity/Assets/Ashfall/Core/Contracts.cs','docs/agent-runs/20261104-three-chats/unity/phase07-ownership.json','docs/agent-runs/20261104-three-chats/unity/reviews/Sessions-r5/REPORT.md','docs/agent-runs/20261104-three-chats/unity/NetworkingPeerR4/r5/REPORT.md','docs/agent-runs/20261104-three-chats/unity/NetworkingPeerR4/r5/SESSIONS-HANDOFF.md','docs/agent-runs/20261104-three-chats/unity/phase07/Avatar-r5/REPORT.md','docs/agent-runs/20261104-three-chats/unity/editor-probes/Avatar-r5-native-05/result.json','docs/agent-runs/20261104-three-chats/unity/editor-probes/Avatar-r5-native-05/inputs.json','docs/agent-runs/20261104-three-chats/unity/editor-probes/Avatar-r5-native-05/execution.json','docs/agent-runs/20261104-three-chats/unity/editor-probes/PUBLISHED-ADAPTERS-RESULT.md','docs/agent-runs/20261104-three-chats/unity/editor-probes/published-adapter-compile-inputs.json']
records=[]
for rel in required:
 p=R/rel;text=p.read_text(encoding='utf-8-sig');records.append(dict(path=str(p),sha256=sha(p),characters=len(text)))
(D/'read-inputs.json').write_text(json.dumps(records,indent=2))
accepted=[]
def walk(x,rows):
 if isinstance(x,dict):
  path=x.get('path',x.get('Path'));expected=x.get('expected',x.get('sha256',x.get('Hash')))
  if isinstance(path,str) and isinstance(expected,str) and len(expected)==64:
   p=Path(path);p=p if p.is_absolute() else R/p
   rows.append(dict(path=str(p),expected=expected,actual=sha(p) if p.is_file() else None,match=p.is_file() and sha(p)==expected.upper()))
  for v in x.values():walk(v,rows)
 elif isinstance(x,list):
  for v in x:walk(v,rows)
for i in range(1,6):
 p=R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%i);data=json.loads(p.read_text(encoding='utf-8-sig'));rows=[];walk(data,rows);accepted.append(dict(path=str(p),sha256=sha(p),records=rows))
(D/'accepted-verification.json').write_text(json.dumps(accepted,indent=2));print('accepted',[(len(a['records']),sum(not r['match'] for r in a['records'])) for a in accepted])
deps=[]
for p in [R/'unity/Assets/Ashfall/Modules/Sessions/PreparedLifeProjection.cs',*R.glob('unity/Assets/Ashfall/Modules/HealthStates/*.cs'),*R.glob('unity/Assets/Ashfall/Integration/SessionReplication/*.cs')]:
 q=S/'dependencies'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);deps.append(dict(path=str(p),snapshot=str(q),sha256=sha(q)))
(D/'dependencies.json').write_text(json.dumps(deps,indent=2))
refs=[]
for rel in ['quest-r22/source/scripts/net_session1000.gd','game/scripts/net_session1000.gd']:
 p=R/rel;q=S/'references'/('quest-net_session1000.gd' if rel.startswith('quest') else 'game-net_session1000.gd');q.parent.mkdir(exist_ok=True);shutil.copyfile(p,q);refs.append(dict(path=str(p),snapshot=str(q),sha256=sha(q),relevant=[dict(line=i,text=l) for i,l in enumerate(p.read_text().splitlines(),1) if '_seq=0' in l or 'sequence<=int(_seen.get' in l or 'for table in [_seen' in l or 'row=reservation.player' in l]))
git=Path(r'C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe');result=subprocess.run([str(git),'-C',str(R/'quest-r22/source'),'rev-parse','HEAD'],capture_output=True,text=True)
(D/'reference-reconciliation.json').write_text(json.dumps(dict(quest_head=result.stdout.strip(),exit=result.returncode,files=refs,game_git_assumed=False,scope='narrow source bytes only'),indent=2));print('quest',result.stdout.strip())
