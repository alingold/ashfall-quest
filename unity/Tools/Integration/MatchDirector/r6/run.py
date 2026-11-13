import pathlib,json,subprocess,hashlib,difflib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchTransactionsR6';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchTransactionsR6'
commands=json.loads((D/'candidate-commands.json').read_text()) if (D/'candidate-commands.json').exists() else []
def suite(label,sources,test):
 exe=S/(label+'.exe');rsp=S/(label+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warn:4\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources+[test]))
 for args in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],[str(exe)]]:
  x=subprocess.run(args,capture_output=True,text=True);commands.append(dict(command=subprocess.list2cmdline(args),exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/'candidate-commands.json').write_text(json.dumps(commands,indent=2));print(label,x.stdout,x.stderr); 
  if x.returncode:return False
 return True
sources=list((S/'pinned').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/MatchDirector').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/MatchRounds').glob('*.cs'))+[R/'unity/Assets/Ashfall/Modules/Sessions/PreparedMatchLifecycle.cs']
fixture=next((S/'baseline').glob('*-Holdouts-final.cs')).read_text();fixture=fixture[:fixture.index('    static void PendingAcrossResults()')]
fixture=fixture.replace('ScoreReceiptJournal journal=null)','ScoreReceiptJournal journal=null,bool project=false)').replace('capacity,E,null,journal','capacity,E,project?new SessionHealthProjection(S,E,"host"):null,journal')
new=(R/'unity/Tools/Integration/MatchDirector/r6/SuccessTests.cs.txt').read_text();test=S/'SuccessTests.cs';test.write_text(fixture+new)
suite('candidate-original-builder',sources,next((S/'baseline').glob('*-BuilderTests.cs')))
suite('candidate-success',sources,test)
suite('candidate-unchanged-additional',sources,next((S/'baseline').glob('*-AdditionalTests.cs')))
current=[];drift=[]
for item in json.loads((D/'pinned-dependencies.json').read_text()):
 p=R/item['path'];h=hashlib.sha256(p.read_bytes()).hexdigest().upper();q=S/'current'/(h[:12]+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());current.append(q);drift.append(dict(path=item['path'],pinned=item['sha256'],current=h,changed=h!=item['sha256']))
(D/'current-dependency-drift.json').write_text(json.dumps(drift,indent=2))
owned=[p for p in sources if not p.is_relative_to(S/'pinned')]
suite('current-original-builder',current+owned,next((S/'baseline').glob('*-BuilderTests.cs')))
suite('current-success',current+owned,test)
for name in ['RoundController.cs','CommandLedger.cs']:
 base=R/'unity/Assets/Ashfall/Modules/Modes'/name;candidate=R/'unity/Assets/Ashfall/Integration/MatchRounds'/name
 (D/(name+'.diff')).write_text(''.join(difflib.unified_diff(base.read_text().splitlines(True),candidate.read_text().splitlines(True),fromfile=str(base),tofile=str(candidate))))
