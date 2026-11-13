from pathlib import Path
import hashlib,json,shutil,subprocess,os,sys
R=Path(r'C:\Users\aling\Documents\ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/CombatCheckpointBoundaryR4'; D=R/'docs/agent-runs/20261104-three-chats/unity/CombatCheckpointBoundaryR4'
S.mkdir(parents=True,exist_ok=True);D.mkdir(parents=True,exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def freeze():
 paths=list((R/'unity/Assets/Ashfall/Core').glob('*.cs'))+list((R/'unity/Assets/Ashfall/RecoveryContracts').glob('*.cs'))
 for f in ['Combat','HealthStates','Persistence','Modes','Sessions']:paths+=list((R/'unity/Assets/Ashfall/Modules'/f).glob('*.cs'))
 for f in ['CombatPublication','MatchDirector','MatchRounds']:paths+=list((R/'unity/Assets/Ashfall/Integration'/f).glob('*.cs'))
 paths+=[R/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
 for f in ['CombatPublication','HealthArchiveRecords','MatchRecovery','CombatCheckpointBoundary']:paths+=list((R/'unity/Tools/Integration'/f).glob('*.*'))
 paths+=[R/'unity/Tools/Integration/CombatHealthBridgeTests.cs',R/'unity/Tools/Modules/HealthStates/HealthTests.cs']
 rows=[]
 for p in paths:
  q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);rows.append(dict(path=str(p.relative_to(R)),sha256=sha(q)))
 (D/'before.json').write_text(json.dumps(rows,indent=2))
if not (D/'before.json').exists():freeze()
mode=sys.argv[1];out=S/mode;out.mkdir(exist_ok=False);rows=json.loads((D/'before.json').read_text());sources=[];inputs=[]
if mode!='baseline':
 for f in ['unity/Assets/Ashfall/Integration/CombatPublication','unity/Tools/Integration/CombatCheckpointBoundary']:
  for p in (R/f).glob('*.*'):
   if not any(x['path']==str(p.relative_to(R)) for x in rows):rows.append(dict(path=str(p.relative_to(R))))
for x in rows:
 p=R/x['path']; owned=p.name in ['CheckpointState.cs','MatchCheckpointRecords.cs'] and p.parent.name in ['Combat','HealthStates'] or p.parent.name in ['CombatPublication','CombatCheckpointBoundary']
 p=p if mode!='baseline' and owned else S/'before'/x['path']
 q=out/'inputs'/x['path'];q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);inputs.append(dict(path=x['path'],sha256=sha(q)));sources.append(q)
(out/'inputs.json').write_text(json.dumps(inputs,indent=2));log=[];dll={};csc=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def run(args,label):
 z=subprocess.run([str(a) for a in args],cwd=out,capture_output=True,text=True);log.append(dict(label=label,argv=[str(a) for a in args],exit=z.returncode,stdout=z.stdout,stderr=z.stderr));(out/'commands.json').write_text(json.dumps(log,indent=2));print(label,z.returncode,(z.stdout+z.stderr)[-3500:]);return z
def build(name,files,refs=(),exe=False):
 target=out/(name+('.exe' if exe else '.dll'));rsp=out/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(target)+'"']+['/reference:"'+str(dll[k])+'"' for k in refs]+['"'+str(p)+'"' for p in files]));z=run([csc,'@'+str(rsp)],name);dll[name]=target;return z.returncode
def folder(f):return [p for p in sources if p.parent.name==f and 'Assets' in p.parts and p.suffix=='.cs']
for name,refs in [('Core',[]),('RecoveryContracts',['Core']),('HealthStates',['Core','RecoveryContracts']),('Combat',['Core','HealthStates','RecoveryContracts']),('Persistence',['Core']),('Modes',['Core']),('Sessions',['Core','RecoveryContracts','HealthStates'])]:
 if build(name,folder(name),refs):sys.exit(1)
if build('CombatPublication',folder('CombatPublication')+[p for p in sources if p.name=='CombatHealthBridge.cs'],['Core','RecoveryContracts','HealthStates','Combat','Persistence']):sys.exit(1)
allrefs=list(dll)
if build('Director',folder('MatchDirector')+folder('MatchRounds'),allrefs):sys.exit(1)
allrefs=list(dll)
os.environ['ASHFALL_CHECKPOINT_BINDING']=sha(out/'inputs.json')
for name,file in [('HealthArchive','Tests.cs'),('RetainedHealth','RetainedHealthTests.cs'),('Health49','HealthTests.cs'),('Bridge','CombatHealthBridgeTests.cs'),('Checkpoint','CheckpointTests.cs'),('R3','R3RegressionTests.cs'),('Publication','PublicationTests.cs'),('DirectorCold','ArchiveTests.cs')]:
 parent='HealthArchiveRecords' if name in ['HealthArchive','RetainedHealth'] else 'MatchRecovery' if name=='DirectorCold' else None
 p=next(p for p in sources if p.name==file and (parent is None or p.parent.name==parent))
 # Retain original white-box tests as inputs; the external consumer uses equivalent
 # real PUBLIC reservation and archive cursor APIs, with no friend assembly.
 text=p.read_text();adapted=text.replace('HealthService.PreparedPublicationLease lease;Check(health.TryReservePublication(1,out lease)', 'HealthService.PreparedAdvance lease;Check(health.PrepareAdvance(C(),out lease).Accepted')
 for expr in ['r.H','h']:adapted=adapted.replace(expr+'.CheckpointSequence','MatchCheckpointRecords.Sequence('+expr+')')
 if adapted!=text:
  q=out/('Public-'+file);q.write_text(adapted);p=q
 if build(name,[p],allrefs,True):sys.exit(1)
 data=out/(name+'-data');data.mkdir()
 run([dll[name]]+([] if name=='DirectorCold' else [str(data)]),name+'-run')
(D/(mode+'-results.json')).write_text(json.dumps(log,indent=2))


sys.exit(1 if any(x["exit"]!=0 for x in log) else 0)
