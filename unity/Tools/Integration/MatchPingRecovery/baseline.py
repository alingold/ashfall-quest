import pathlib,json,hashlib,subprocess,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchPingRecoveryR12';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchPingRecoveryR12';O=S/'baseline';O.mkdir(exist_ok=True);logs=[];csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
def call(label,args):
 x=subprocess.run(list(map(str,args)),capture_output=True,text=True);logs.append(dict(label=label,argv=list(map(str,args)),exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/'baseline-commands.json').write_text(json.dumps(logs,indent=2));print(label,x.returncode,x.stdout[-2000:]);return x.returncode

def build(name,files,refs=(),exe=False):
 out=O/(name+('.exe' if exe else '.dll'));rsp=O/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in files]));return call(name,[csc,'@'+str(rsp)]),out
rows=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/DirectorArchiveR11/run-142821-369227-manifest.json').read_text());src=[]
for row in rows:
 p=R/row['snapshot'];assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'];q=O/'historical'/p.parent.name/p.name;q.parent.mkdir(parents=True,exist_ok=True)
 if not q.exists():shutil.copyfile(p,q)
 if p.suffix=='.cs' and p.name!='ArchiveTests.cs':src.append(q)
t=O/'historical/MatchRecovery/ArchiveTests.cs'
for name,test in [('Archive',t),('R9',R/'campaign-staging/20261104-three-chats/unity/DirectorArchiveR11/before/unity/Tools/Integration/MatchDirector/r9/Tests.cs')]:
 code,out=build(name,src+[test],exe=True);assert code==0;assert call(name+'-run',[out])==0
code,out=build('HistoricalCombined',src);assert code==0
p=S/'before/campaign-staging/20261104-three-chats/unity/reviews/DirectorArchiveReviewR11/IndependentTests.cs';code,exe=build('External120',[p],[out],True);assert code==0;assert call('External120-run',[exe])==0
# Current once-frozen real owners. No combined owner graph approval.
B=S/'before/unity/Assets/Ashfall'; dll={}
for name,folders,refs in [('Core',['Core'],[]),('RecoveryContracts',['RecoveryContracts'],['Core']),('Modes',['Modules/Modes'],['Core']),('ModesRecovery',['Modules/ModesRecovery'],['Core','RecoveryContracts','Modes']),('HealthStates',['Modules/HealthStates'],['Core','RecoveryContracts']),('Combat',['Modules/Combat'],['Core','RecoveryContracts','HealthStates']),('Persistence',['Modules/Persistence'],['Core']),('Sessions',['Modules/Sessions'],['Core','RecoveryContracts','HealthStates']),('MatchRounds',['Integration/MatchRounds'],['Core','RecoveryContracts','Modes','HealthStates'])]:
 code,out=build(name,[p for f in folders for p in (B/f).glob('*.cs')],[dll[n] for n in refs]);dll[name]=out
 if code:break
