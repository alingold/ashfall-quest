from pathlib import Path
import subprocess,json,hashlib,shutil
ROOT=Path(__file__).resolve().parents[5]
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/Sessions/r3'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/Sessions/r3'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
number=1
while (STAGE/('attempt-%02d'%number)).exists(): number+=1
OUT=STAGE/('attempt-%02d'%number);OUT.mkdir()
LOG=DOC/OUT.name;LOG.mkdir()
records=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def run(name,args,expected=0):
 r=subprocess.run([str(a) for a in args],capture_output=True,text=True)
 (LOG/(name+'.txt')).write_text(r.stdout+r.stderr)
 records.append(dict(name=name,argv=[str(a) for a in args],command=subprocess.list2cmdline([str(a) for a in args]),exit=r.returncode,expected=expected))
 print(name,r.returncode,(r.stdout+r.stderr).strip())
 (LOG/'commands.json').write_text(json.dumps(records,indent=2))
 if r.returncode!=expected:raise RuntimeError(name)
def copy_sources(paths,dest):
 dest.mkdir(parents=True,exist_ok=True)
 result=[]
 for p in paths:
  q=dest/p.name;shutil.copyfile(p,q);result.append(q)
 return result
sources=copy_sources(sorted((ROOT/'unity/Assets/Ashfall/Modules/Sessions').glob('*.cs')),OUT/'Sessions')
bridge=copy_sources(sorted((ROOT/'unity/Assets/Ashfall/Integration/Sessions').glob('*.cs')),OUT/'Integration')
core=STAGE/'inputs/Core/Contracts.cs'
network=sorted((STAGE/'inputs/Networking').glob('*.cs'))
inputs=[]
for folder,original in [('Networking','unity/Assets/Ashfall/Modules/Networking'),('Core','unity/Assets/Ashfall/Core')]:
 for p in (STAGE/'inputs'/folder).glob('*.cs'):
  live=ROOT/original/p.name
  inputs.append(dict(path=str(live),snapshot=str(p),sha256=sha(p),current_sha256=sha(live),matches=sha(p)==sha(live)))
(LOG/'dependencies.json').write_text(json.dumps(inputs,indent=2))
def compile(name,prod,test=None,extra=None):
 target=OUT/(name+('.exe' if test else '.dll'))
 args=[CSC,'/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if test else 'library'),'/out:'+str(target),core]+prod+(extra or [])
 if test:args+=copy_sources([test],OUT/('test-'+name))
 run('compile-'+name,args);return target
before=sorted((STAGE/'before').glob('*.cs'))
fresh=ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Sessions-r2/FreshTests.cs'
run('before-budget',[compile('BeforeBudget',before,fresh),'budget'],1)
compile('Sessions',sources)
for name,test in [('Original',ROOT/'unity/Tools/Modules/Sessions/SessionTests.cs'),('R2',ROOT/'unity/Tools/Modules/Sessions/R2Tests.cs'),('PriorReviewer',ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Sessions-r1/AdversarialTests.cs'),('Fresh',fresh)]:
 exe=compile(name,sources,test);run(name,[exe])
 if name=='Fresh':run('budget',[exe,'budget'])
compile('Composition',sources,extra=network+bridge)
run('R3',[compile('R3',sources,ROOT/'unity/Tools/Integration/Sessions/R3Tests.cs',network+bridge)])
entries=[dict(path=str(p),sha256=sha(p)) for p in OUT.rglob('*') if p.is_file()]
(LOG/'artifact-manifest.json').write_text(json.dumps(entries,indent=2))
print('SUCCESS',OUT)
