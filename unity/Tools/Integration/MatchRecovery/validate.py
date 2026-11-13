import pathlib,json,subprocess,hashlib,datetime
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchStateRecoveryR10';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchStateRecoveryR10'
rows=json.loads((D/'inputs.json').read_text());sources=[]
for row in rows:
 p=R/row['source']; owned=p.parent.name in ['MatchDirector','MatchRounds'] or (p.parent.name=='Sessions' and p.name in ['Session.cs','PreparedLifeProjection.cs','PreparedMatchLifecycle.cs','CheckpointState.cs'])
 sources.append(p if owned else R/row['snapshot'])
for p in [R/'unity/Assets/Ashfall/Modules/Sessions/CheckpointState.cs',R/'unity/Assets/Ashfall/Integration/MatchRounds/CheckpointState.cs']:
 if p not in sources:sources.append(p)
sources+=list((R/"unity/Assets/Ashfall/Integration/MatchRecovery").glob("*.cs"))
sealed=D/'candidate-manifest.json'
if sealed.exists():
 for row in json.loads(sealed.read_text()):
  p=R/row['source'];assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'],'Candidate mismatch: '+str(p)
 for row in rows:
  p=R/row['snapshot'];assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'],'Pinned dependency mismatch: '+str(p)
 sources=[R/row['snapshot'] for row in json.loads(sealed.read_text()) if row['source'].endswith('.cs') and not row['source'].startswith('unity\\Tools')]
 # All original dependency sources plus sealed owned bytes; tests themselves checked above.
 pinned=[]
 for row in rows:
  if not any(x.name==pathlib.Path(row['source']).name and x.parent.name==pathlib.Path(row['source']).parent.name for x in sources):pinned.append(R/row['snapshot'])
 sources+=pinned
commands=[]
run_id=datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
(S/run_id).mkdir()
def run(label,src,test,main):
 exe=S/run_id/(label+'.exe');rsp=S/run_id/(label+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warn:4\n/warnaserror+\n/target:exe\n/main:'+main+'\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in src+[test]))
 for args in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],[str(exe)]]:
  x=subprocess.run(args,capture_output=True,text=True);commands.append(dict(label=label,argv=args,exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/(run_id+"-commands.json")).write_text(json.dumps(commands,indent=2));print(label,x.returncode,x.stdout,x.stderr)
  if x.returncode:return x.returncode
 return 0
assert run('owner-cold',sources,R/'unity/Tools/Integration/MatchRecovery/Tests.cs','RecoveryTests')==0
assert run('r9-regression',sources,R/'unity/Tools/Integration/MatchDirector/r9/Tests.cs','Tests')==0

for label,filename,old,new in [
 ('join-accounting','Session.cs','checkpointReservations.Add(prepared);',''),
 ('death-ledger','CheckpointState.cs','x.deaths.Add(key,life);',''),
 ('session-replay','CheckpointState.cs','Need(s.commands.Add(Text(r)));','Text(r);')]:
 original=next(p for p in sources if p.name==filename and (p.parent.name=='MatchRounds' if label=='death-ledger' else p.parent.name=='Sessions'))
 mutant=S/run_id/(label+'.cs');text=original.read_text();assert old in text;mutant.write_text(text.replace(old,new))
 assert run('mutant-'+label,[mutant if p==original else p for p in sources],R/'unity/Tools/Integration/MatchRecovery/Tests.cs','RecoveryTests')!=0
