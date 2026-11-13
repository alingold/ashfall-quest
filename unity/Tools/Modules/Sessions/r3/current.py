from pathlib import Path
import subprocess,json,hashlib,difflib
ROOT=Path(__file__).resolve().parents[5];S=ROOT/'campaign-staging/20261104-three-chats/unity/Sessions/r3';D=ROOT/'docs/agent-runs/20261104-three-chats/unity/Sessions/r3'
if (D/'current-commands.json').exists(): raise RuntimeError('Frozen compatibility outputs exist; use a new revision path.')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
old=(S/'inputs/Networking/Authority.cs').read_text();new=(S/'inputs-current/Networking/Authority.cs').read_text()
(D/'networking-drift.diff').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='pinned-initial',tofile='pinned-current')))
core=S/'inputs/Core/Contracts.cs';out=S/'attempt-02';prod=list((out/'Sessions').glob('*.cs'))+list((out/'Integration').glob('*.cs'))+list((S/'inputs-current/Networking').glob('*.cs'))
target=out/'CurrentNetworking.exe'
args=[Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(target),core]+prod+[out/'test-R3/R3Tests.cs']
records=[]
for name,argv in [('compile-current',args),('test-current',[target])]:
 r=subprocess.run([str(x) for x in argv],capture_output=True,text=True)
 (D/(name+'.txt')).write_text(r.stdout+r.stderr)
 records.append(dict(name=name,command=subprocess.list2cmdline([str(x) for x in argv]),argv=[str(x) for x in argv],exit=r.returncode))
 print(name,r.returncode,r.stdout,r.stderr)
 if r.returncode:raise RuntimeError(name)
(D/'current-commands.json').write_text(json.dumps(records,indent=2))
inputs=[]
for p in (S/'inputs-current/Networking').glob('*.cs'):
 live=ROOT/'unity/Assets/Ashfall/Modules/Networking'/p.name
 inputs.append(dict(path=str(live),snapshot=str(p),sha256=sha(p),current_sha256=sha(live),matches=sha(p)==sha(live)))
(D/'current-dependencies.json').write_text(json.dumps(inputs,indent=2))
paths=['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/COLLISION-LAYERS-v1.md','docs/agent-runs/20261104-three-chats/unity/ownership.json','docs/agent-runs/20261104-three-chats/unity/phase03-ownership.json','docs/agent-runs/20261104-three-chats/unity/reviews/Sessions-r2/REPORT.md']
for tree in ['quest-r22/source','game']:
 for f in ['net_session1000.gd','net_bridge1000.gd','team_launch_r12.gd']:paths.append(tree+'/scripts/'+f)
(D/'source-inputs.json').write_text(json.dumps(dict(quest_revision='bdba6e30012e1733efd5021fd8ded593f82f26e6',files=[dict(path=p,sha256=sha(ROOT/p)) for p in paths]),indent=2))
print('AuthorityHost byte-equivalent prefix:',old.split('    // Opaque payload')[0]==new.split('    // Opaque payload')[0])
