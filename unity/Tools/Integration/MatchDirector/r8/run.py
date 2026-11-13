import pathlib,json,subprocess,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchDeliveryR8';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchDeliveryR8'
O=S/'final07';O.mkdir(exist_ok=False)
before=S/'before/unity/Assets/Ashfall'
deps=[before/'Core/Contracts.cs',before/'Integration/CombatHealthBridge.cs']
for folder in ['Modules/Combat','Modules/HealthStates','Modules/Modes','Modules/Sessions','Modules/Persistence','Integration/CombatPublication']:
    deps+=list((before/folder).glob('*.cs'))
owned=list((R/'unity/Assets/Ashfall/Integration/MatchDirector').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/MatchRounds').glob('*.cs'))
commands=[]
def suite(label,sources,test):
    exe=O/(label+'.exe');rsp=O/(label+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warn:4\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources+[test]))
    for args in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],[str(exe)]]:
        x=subprocess.run(args,capture_output=True,text=True);commands.append(dict(label=label,command=subprocess.list2cmdline(args),exit=x.returncode,stdout=x.stdout,stderr=x.stderr));print(label,x.returncode,x.stdout,x.stderr)
        (D/('commands-'+O.name+'.json')).write_text(json.dumps(commands,indent=2))
        if x.returncode:return False
    return True
old=R/'campaign-staging/20261104-three-chats/unity/reviews/Match-r7/Adversarial.cs'
baseline=[before/'Integration/MatchDirector'/p.name for p in owned if p.parent.name=='MatchDirector' and p.name!='HealthProducerDrain.cs']+list((before/'Integration/MatchRounds').glob('*.cs'))
suite('baseline-r7',deps+baseline,old)
t=old.read_text().replace('public Environment E=new Environment();','public bool Bind=true;public Environment E=new Environment();').replace('foreach(string id in ids){Check(S.Loaded','if(Bind)Check(D.BindProducer(H,C("host"),new string(\'a\',64)).Accepted,"bind real producer");\n            foreach(string id in ids){Check(S.Loaded')
t=t.replace('Check(load.Accepted&&r.S.Epoch==2&&!retry.Accepted&&j.Pending("test",1).Length==1,"counterexample: orphan pending journal stranded by nonfinal rollover");','Check(!load.Accepted&&load.Reason=="pending-score"&&r.S.Epoch==1&&retry.Accepted&&j.Pending("test",1).Length==0,"old epoch retained and genuine event ACKed");Check(Score(r.D,"b")==1&&r.S.Get("a").Deaths==1&&r.D.BeginLoading().Accepted&&r.S.Epoch==2,"credited exactly once before coherent rollover");')
t=t.replace('Check(ack.Accepted&&!retry.Accepted&&Score(r.D,"b")==1&&r.S.Get("c").Deaths==0,"counterexample: no upstream drain barrier");','Check(!ack.Accepted&&ack.Reason=="producer-undelivered"&&retry.Accepted&&Score(r.D,"b")==2&&r.S.Get("c").Deaths==1,"unseen prefix blocks completion then second loss reconciles");Check(r.D.AcknowledgeComplete().Accepted,"all producer events settled");')
t=t.replace('static int Main()', (R/'unity/Tools/Integration/MatchDirector/r8/extra.cs.txt').read_text()+'\n    static int Main()').replace('FinalCuts(false);FinalCuts(true);','PublicationCases();FinalCuts(false);FinalCuts(true);')
test=O/'Success.cs';test.write_text(t.replace('independent assertions including explicit OPEN boundary counterexamples','R8 assertions; baseline counterexamples repaired'))
assert suite('candidate',deps+owned,test)
regression=O/'Regression.cs';regression.write_text(t[:t.index('    static void FinalCuts')]+(R/'unity/Tools/Integration/MatchDirector/r6/SuccessTests.cs.txt').read_text())
assert suite('prior-success-adapted-binding',deps+owned,regression)
# Freeze latest actual upstream closure separately; never overwrite the phase12-derived baseline.
latest=[];latestrows=[]
live=[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
for folder in ['Modules/Combat','Modules/HealthStates','Modules/Modes','Modules/Sessions','Modules/Persistence','Integration/CombatPublication']:
    live+=list((R/'unity/Assets/Ashfall'/folder).glob('*.cs'))
for p in live:
    b=p.read_bytes();q=O/'latest'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);latest.append(q)
    old=S/'before'/p.relative_to(R)
    latestrows.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper(),drift_from_baseline=not old.exists() or old.read_bytes()!=b))
(D/('latest-upstream-'+O.name+'.json')).write_text(json.dumps(latestrows,indent=2))
assert suite('latest-upstream',latest+owned,test)
# Two semantic mutants: each must compile and fail the relevant behavioral assertion.
director=next(p for p in owned if p.name=='Director.cs')
for name,a,b in [('journal-guard','(journal!=null&&journal.Pending(session.Id,Epoch).Length!=0)','false'),('prefix-barrier','producer.Check(session.Id,Epoch,eventSequence)','RuleResult.Accept()')]:
    mutant=O/(name+'.cs');mutant.write_text(director.read_text().replace(a,b));assert mutant.read_text()!=director.read_text()
    assert not suite('mutant-'+name,deps+[mutant if p==director else p for p in owned],test)
rows=[]
for p in deps+owned+[test]:
    b=p.read_bytes();rows.append(dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper(),bytes=len(b)))
(D/('consumed-'+O.name+'.json')).write_text(json.dumps(rows,indent=2))
