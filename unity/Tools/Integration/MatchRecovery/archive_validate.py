import pathlib,json,subprocess,datetime,hashlib,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/DirectorArchiveR11';S=R/'campaign-staging/20261104-three-chats/unity/DirectorArchiveR11';run=S/('run-'+datetime.datetime.now().strftime('%H%M%S-%f'));run.mkdir()
rows=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/MatchStateRecoveryR10/inputs.json').read_text());src=[]
for row in rows:
 p=R/row['source'];q=S/'before'/row['source'];assert q.exists(),q
 if p.parent.name in ['MatchDirector','MatchRounds']:q=p
 src.append(q)
for folder in ['RecoveryContracts','Modules/HealthStates','Modules/Sessions','Integration/MatchDirector','Integration/MatchRounds']:
 for p in ((R if folder.startswith('Integration/') else S/'before')/'unity/Assets/Ashfall'/folder).glob('*.cs'):
  q=p
  src=[x for x in src if not(x.name==q.name and x.parent.name==q.parent.name)];src.append(q)
# Freeze exact candidate and tools before execution; never use moving source during a run.
frozen=[];manifest=[]
for p in src+[R/'unity/Tools/Integration/MatchRecovery/ArchiveTests.cs',pathlib.Path(__file__)]:
 q=run/'source'/p.parent.name/p.name;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);manifest.append(dict(source=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));frozen.append(q)
(D/(run.name+'-manifest.json')).write_text(json.dumps(manifest,indent=2));log=[];dll={}
def call(name,files,refs=(),exe=False):
 out=run/(name+('.exe' if exe else '.dll'));rsp=run/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']+['/reference:"'+str(dll[k])+'"' for k in refs]+['"'+str(p)+'"' for p in files]));args=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];x=subprocess.run(args,capture_output=True,text=True);log.append(dict(label=name,argv=args,exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/(run.name+'-commands.json')).write_text(json.dumps(log,indent=2));print(name,x.returncode,x.stdout);dll[name]=out;return x.returncode
sources=frozen[:-2];test=frozen[-2]
assert call('ArchiveTests',sources+[test],exe=True)==0
x=subprocess.run([str(dll['ArchiveTests'])],capture_output=True,text=True);log.append(dict(label='actual-cold-physical',exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/(run.name+'-commands.json')).write_text(json.dumps(log,indent=2));print(x.stdout);assert x.returncode==0
regression=S/'before/unity/Tools/Integration/MatchDirector/r9/Tests.cs'
assert call('R9Regression',sources+[regression],exe=True)==0
x=subprocess.run([str(dll['R9Regression'])],capture_output=True,text=True);log.append(dict(label='r9-regression',exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/(run.name+'-commands.json')).write_text(json.dumps(log,indent=2));print(x.stdout);assert x.returncode==0
for label,filename,old,new in [('lost-producer','ProducerArchive.cs','x.pending=pending;','x.pending=null;'),('latest-health','DirectorArchive.cs','State=snapshots[i]','State=quarantinedHealth.Read(key)')]:
 original=next(p for p in sources if p.name==filename);mutant=run/(label+'.cs');text=original.read_text();assert old in text;mutant.write_text(text.replace(old,new));changed=[mutant if p==original else p for p in sources];assert call('Mutant-'+label,changed+[test],exe=True)==0
 x=subprocess.run([str(dll['Mutant-'+label])],capture_output=True,text=True);log.append(dict(label='mutant-'+label,exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/(run.name+'-commands.json')).write_text(json.dumps(log,indent=2));print(label,x.returncode);assert x.returncode!=0
# Independent DLL graph; report the first real boundary failure without hiding it.
for name,folders,refs in [('Core',['Core'],[]),('RecoveryContracts',['RecoveryContracts'],['Core']),('Modes',['Modes'],['Core']),('HealthStates',['HealthStates'],['Core','RecoveryContracts']),('Combat',['Combat'],['Core','HealthStates'])]:
 if call(name,[p for p in sources if p.parent.name in folders],refs):break


