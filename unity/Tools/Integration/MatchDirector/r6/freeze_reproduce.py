import pathlib,json,hashlib,subprocess
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/MatchTransactionsR6'
D=R/'docs/agent-runs/20261104-three-chats/unity/MatchTransactionsR6'
S.mkdir(parents=True,exist_ok=True);D.mkdir(parents=True,exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def copy(p,base):
 q=base/(sha(p)[:12]+'-'+p.name);q.parent.mkdir(parents=True,exist_ok=True)
 if q.exists():assert q.read_bytes()==p.read_bytes(),str(q)
 else:q.write_bytes(p.read_bytes())
 return q
review=R/'docs/agent-runs/20261104-three-chats/unity/reviews/MatchDirector-r5'
for p in [review/'manifest.sha256.json',R/'docs/agent-runs/20261104-three-chats/unity/MatchDirectorR4/r5/manifest.sha256.json']:
 print(p,sha(p))
for p in (R/'unity/Assets/Ashfall/Integration/MatchDirector').glob('*.cs'):copy(p,S/'before')
invent=json.loads((review/'source-inventories.json').read_text())
sources=[copy(pathlib.Path(p),S/'baseline') for p in invent['frozen']]
tests={}
B=R/'campaign-staging/20261104-three-chats/unity/MatchDirectorR4/r5'
V=R/'campaign-staging/20261104-three-chats/unity/reviews/MatchDirector-r5'
for label,p in [('builder',B/'BuilderTests.cs'),('diagnostics',B/'UpdatedDiagnostics.cs'),('additional',R/'unity/Tools/Integration/MatchDirector/r5/AdditionalTests.cs'),('holdouts',V/'Holdouts-final.cs')]:tests[label]=copy(p,S/'baseline')
commands=[]
def suite(label,src,test):
 exe=S/(label+'.exe');rsp=S/(label+'.rsp')
 rsp.write_text('/nologo\n/langversion:5\n/warn:4\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in src+[test]))
 for args in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],[str(exe)]]:
  x=subprocess.run(args,capture_output=True,text=True);commands.append(dict(command=subprocess.list2cmdline(args),exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/'baseline-commands.json').write_text(json.dumps(commands,indent=2));print(x.stdout,x.stderr);assert x.returncode==0
for label,test in tests.items():suite('baseline-'+label,sources,test)
pins=[]
for folder in ['Core','Modules/Sessions','Modules/HealthStates','Modules/Modes','Modules/Persistence']:
 for p in (R/'unity/Assets/Ashfall'/folder).glob('*.cs'):
  q=copy(p,S/'pinned');pins.append(dict(path=str(p.relative_to(R)),sha256=sha(q),bytes=q.stat().st_size))
(D/'pinned-dependencies.json').write_text(json.dumps(pins,indent=2))
for n in range(1,6):
 p=R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%n);data=json.loads(p.read_text(encoding='utf-8-sig'));copy(p,S/'references');print(p.name,json.dumps(data))
