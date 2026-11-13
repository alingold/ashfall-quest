from pathlib import Path
import json,hashlib,shutil,subprocess,datetime
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/HealthArchiveRecordsR1';S=R/'campaign-staging/20261104-three-chats/unity/HealthArchiveRecordsR1';out=S/datetime.datetime.now().strftime('checks-%Y%m%d-%H%M%S-%f');out.mkdir();rows=json.loads((D/'inputs.json').read_text());log=[];dll={}
# Freeze new owned files and actual tools before any test.
for p in [R/'unity/Assets/Ashfall/Modules/HealthStates/MatchCheckpointRecords.cs',R/'unity/Assets/Ashfall/Integration/MatchDirector/HealthProducerDrain.cs']+list((R/'unity/Tools/Integration/HealthArchiveRecords').glob('*.*')):
 q=out/'inputs'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
(out/'tool-inputs.json').write_text(json.dumps([dict(path=str(p.relative_to(R)),sha256=hashlib.sha256((out/'inputs'/p.relative_to(R)).read_bytes()).hexdigest()) for p in [R/'unity/Assets/Ashfall/Modules/HealthStates/MatchCheckpointRecords.cs',R/'unity/Assets/Ashfall/Integration/MatchDirector/HealthProducerDrain.cs']+list((R/'unity/Tools/Integration/HealthArchiveRecords').glob('*.*'))],indent=2))
def sources(folder):return [R/x['snapshot'] for x in rows if Path(x['source']).parent.name==folder and x['source'].endswith('.cs') and not x['source'].endswith('MatchCheckpointRecords.cs')]
def run(argv):
 z=subprocess.run([str(x) for x in argv],cwd=out,capture_output=True,text=True);log.append(dict(argv=[str(x) for x in argv],exit=z.returncode,stdout=z.stdout,stderr=z.stderr));(D/(out.name+'.json')).write_text(json.dumps(log,indent=2));print(z.returncode,z.stdout[-2500:]);assert z.returncode==0
csc=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def build(name,files,refs,exe=False):
 target=out/(name+('.exe' if exe else '.dll'));rsp=out/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library')]+(['/define:UNITY_6000_0_OR_NEWER','/nostdlib+'] if name=='NativeFixture' else [])+['/out:"'+str(target)+'"']+['/reference:"'+str(dll[x])+'"' for x in refs]+['"'+str(p)+'"' for p in files]));run([csc]+(['/noconfig'] if name=='NativeFixture' else [])+['@'+str(rsp)]);dll[name]=target
owner=out/'inputs/unity/Assets/Ashfall/Modules/HealthStates/MatchCheckpointRecords.cs';test=out/'inputs/unity/Tools/Integration/HealthArchiveRecords/Tests.cs'
build('Core',sources('Core'),[]);build('RecoveryContracts',sources('RecoveryContracts'),['Core']);build('HealthStates',sources('HealthStates')+[owner],['Core','RecoveryContracts']);build('Consumer',[test],['Core','RecoveryContracts','HealthStates'],True);run([dll['Consumer']])
build('RetainedHealth',[out/'inputs/unity/Tools/Integration/HealthArchiveRecords/RetainedHealthTests.cs'],['Core','RecoveryContracts','HealthStates'],True);run([dll['RetainedHealth']])
# Actual production record class field drift must fail closed.
ports=next(p for p in sources('HealthStates') if p.name=='Ports.cs');mutant=out/'Ports-field-drift.cs';mutant.write_text(ports.read_text().replace('public sealed class HealthSnapshot\n    {','public sealed class HealthSnapshot\n    {\n        public readonly int FutureArchiveField=1;'))
assert mutant.read_text()!=ports.read_text()
original=dll['HealthStates'];build('HealthStatesMutant',[p if p!=ports else mutant for p in sources('HealthStates')]+[owner],['Core','RecoveryContracts']);dll['HealthStates']=dll['HealthStatesMutant'];build('Guard',[test],['Core','RecoveryContracts','HealthStates'],True);run([dll['Guard'],'guard']);dll['HealthStates']=original
# Independent transition inventory drift probe.
mutant2=out/'Ports-transition-drift.cs';mutant2.write_text(ports.read_text().replace('public sealed class HealthTransition\n    {','public sealed class HealthTransition\n    {\n        public readonly int FutureTransitionField=1;'))
assert mutant2.read_text()!=ports.read_text()
build('HealthTransitionMutant',[p if p!=ports else mutant2 for p in sources('HealthStates')]+[owner],['Core','RecoveryContracts']);dll['HealthStates']=dll['HealthTransitionMutant'];build('TransitionGuard',[test],['Core','RecoveryContracts','HealthStates'],True);run([dll['TransitionGuard'],'guard']);dll['HealthStates']=original
# Neutral root compatibility is a separate merged compile, never separate-assembly proof.
allcs=[R/x['snapshot'] for x in rows if x['source'].startswith('unity\\Assets') or x['source'].startswith('unity/Assets')];allcs=[p for p in allcs if p.name not in ['MatchCheckpointRecords.cs','HealthProducerDrain.cs']]+[owner,out/'inputs/unity/Assets/Ashfall/Integration/MatchDirector/HealthProducerDrain.cs'];build('NeutralCompatibility',allcs,[])
rootrows=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase18/source-index.json').read_text(encoding='utf-8-sig'))
for x in rootrows:assert hashlib.sha256((R/x['snapshot']).read_bytes()).hexdigest().upper()==x['sha256'].upper()
rootcs=[R/x['snapshot'] for x in rootrows if not x['path'].endswith('HealthProducerDrain.cs')]+[owner,out/'inputs/unity/Assets/Ashfall/Integration/MatchDirector/HealthProducerDrain.cs'];build('Root302Compatibility',rootcs,[])
(out/'root302-inputs.json').write_text(json.dumps(rootrows,indent=2))
# Compiler-only installed Unity API probe. No Unity process is started.
engine=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll')
dll['UnityCore']=engine
dll['NetStandard']=engine.parents[2]/'NetStandard/ref/2.1.0/netstandard.dll'
dll['MscorlibShim']=engine.parents[2]/'NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll'
build('NativeFixture',[out/'inputs/unity/Tools/Integration/HealthArchiveRecords/HealthArchiveNativeFixture.cs'],['Core','RecoveryContracts','HealthStates','UnityCore','NetStandard','MscorlibShim'])
(D/'current-comparison.json').write_text(json.dumps([dict(path=x['source'],frozen=x['sha256'],current=hashlib.sha256((R/x['source']).read_bytes()).hexdigest()) for x in rows],indent=2))
print(out)
