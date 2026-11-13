# Continue against candidate01 frozen sources and DLLs; never repin dependencies.
exec((__import__('pathlib').Path(__file__).parent/'run.py').read_text().split('rows=json.loads')[0].replace("O=S/'candidate01';O.mkdir()","O=S/'supplement01';O.mkdir()"))
base=S/'candidate01';src=base/'source';commands=[];CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
exec((R/'unity/Tools/Integration/HostEpochHealthRegistration/run.py').read_text().split('def run(cmd,required=True):')[1].split("comp('Core'")[0].join(['def run(cmd,required=True):','']))
refs=['Core','Recovery','Health','Combat','Persistence','Publication','Weapon','Inventory','Session','InventoryRecovery','Host']
for n in refs:shutil.copyfile(base/(n+'.dll'),O/(n+'.dll'))
def fixture(n,text,extra=()):
 q=O/(n+'.cs');q.write_text(text);comp(n,[q],refs,'exe');f=O/(n+'-data');f.mkdir();return run([O/(n+'.exe'),f]+list(extra))
fixture('Registration',(R/'unity/Tools/Integration/HostEpochHealthRegistration/RegistrationTests.cs').read_text())
fixture('Registry','using Ashfall.RecoveryContracts;'+(S/'before/unity/Tools/Integration/HostEpochAuthority/Tests.cs').read_text().replace('gain.CommitValidated();Check(health.Read("p").Revision>before,"exposed legacy Health commit after close: global blocker");','Check(Throws(delegate{gain.CommitValidated();}),"Health retained commit revoked");Check(health.Read("p").Revision==before,"revocation preserves Health");'))
def frozen(name):
 hits=[x for x in json.loads((D/'before.json').read_text()) if Path(x['source']).name==name and 'frozen' not in Path(x['source']).parts];assert len(hits)==1,(name,len(hits));return Path(hits[0]['snapshot']).read_text()
a=frozen('Adversarial.cs').replace('gain.CommitValidated();','Check(Throws(delegate{gain.CommitValidated();}),"retained Health gain refuses");').replace('h.Read("p").Revision>hrev','h.Read("p").Revision==hrev').replace('ACTUAL retained validated gain mutates after dispose and cold reopen off lane','Health gain preserves after dispose/cold')
fixture('Adversarial','using Ashfall.RecoveryContracts;'+a)
fixture('AfterSix',frozen('BeforeFamilies.cs'),['after'])
# Actual immutable archive consumer.
archive=S/'before/unity/Tools/Modules/HealthStates/ArchiveTests.cs'
if not archive.exists():
 candidates=list((S/'before/unity/Tools/Modules/HealthStates').glob('*Archive*.cs'));archive=candidates[0]
fixture('Archive',archive.read_text())
