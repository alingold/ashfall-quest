import pathlib,json,hashlib,subprocess
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchCompletionR9';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchCompletionR9'
rows=json.loads((D/'inputs.json').read_text());deps=[R/x['snapshot'] for x in rows if pathlib.Path(x['source']).parent.name not in ['MatchDirector','MatchRounds','CombatPublication']]
# Candidate freeze is a one-time explicit reviewable checkpoint, separate from before bytes.
owned=[]
for folder in ['MatchDirector','MatchRounds','CombatPublication']:
 for p in (R/'unity/Assets/Ashfall/Integration'/folder).glob('*.cs'):
  q=S/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
  if not q.exists():q.write_bytes(p.read_bytes())
  if q.read_bytes()!=p.read_bytes():raise RuntimeError('Candidate changed: create a new revision instead of overwriting '+str(p))
  owned.append(q)
test=R/'unity/Tools/Integration/MatchDirector/r9/Tests.cs'
commands=[]
def suite(label,sources):
 exe=S/(label+'.exe');rsp=S/(label+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warn:4\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources+[test]))
 result=None
 for args in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],[str(exe)]]:
  x=subprocess.run(args,capture_output=True,text=True);commands.append(dict(label=label,argv=args,exit=x.returncode,stdout=x.stdout,stderr=x.stderr));print(label,x.returncode,x.stdout,x.stderr);(D/'final-commands.json').write_text(json.dumps(commands,indent=2));result=x.returncode
  if result:break
 return result
assert suite('candidate-r9',deps+owned)==0
for label,filename,old,new in [('completion-guard','Director.cs','r=producer.Retirement(session.Id,Epoch,eventSequence);if(!r.Accepted)return r;',''),('admission-guard','HealthPublicationLane.cs','if(admissionClosed)return RuleResult.Reject("producer-admission-closed");',''),('closure-persistence','HealthPublicationLane.cs','w.Write(admissionClosed);','w.Write(false);')]:
 original=next(p for p in owned if p.name==filename);mutant=S/(label+'.cs');content=original.read_text();assert old in content;mutant.write_text(content.replace(old,new));assert suite('mutant-'+label,deps+[mutant if p==original else p for p in owned])!=0
manifest=[]
for p in deps+owned+[test]:
 b=p.read_bytes();manifest.append(dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),bytes=len(b)))
(D/'candidate-manifest.json').write_text(json.dumps(manifest,indent=2))
drift=[]
for row in rows:
 p=R/row['source'];h=hashlib.sha256(p.read_bytes()).hexdigest()
 if h!=row['sha256']:drift.append(dict(source=row['source'],before=row['sha256'],current=h,owned=pathlib.Path(row['source']).parent.name in ['MatchDirector','MatchRounds','CombatPublication']))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
