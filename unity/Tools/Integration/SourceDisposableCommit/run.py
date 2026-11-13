from pathlib import Path
import subprocess,json,shutil,hashlib,sys
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/SourceDisposableCommitR2';D=R/'docs/agent-runs/20261104-three-chats/unity/SourceDisposableCommitR2'
O=S/(sys.argv[1] if len(sys.argv)>1 else 'run01');O.mkdir()
F=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319');C=F/'csc.exe';logs=[]
def run(args,name,expected=0):
 p=subprocess.run([str(x) for x in args],capture_output=True,text=True);logs.append(dict(args=[str(x) for x in args],exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(O/(name+'.log')).write_text(p.stdout+p.stderr);(O/'commands.json').write_text(json.dumps(logs,indent=2));print(name,p.returncode,p.stdout[:3500]);assert (p.returncode==expected if expected>=0 else p.returncode!=0);return p.stdout
def compile(name,src,refs=(),exe=False):
 args=[C,'/nologo','/noconfig','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(O/(name+('.exe' if exe else '.dll'))),'/reference:'+str(F/'System.dll'),'/reference:'+str(F/'System.Core.dll')]+['/reference:'+str(O/(x+'.dll')) for x in refs]+list(src)
 run(args,name)
base=S/'before/unity/Assets/Ashfall'
for name in ['Core','RecoveryContracts','Modules/WeaponHandling','Modules/InventoryItems']:
 dest=O/Path(name).name;dest.mkdir();src=base/name
 for p in src.glob('*.cs'):shutil.copy2(p,dest/p.name)
 if name.startswith('Modules/'):
  # Owned changes overlay the once-pinned dependency graph only.
  owned=['SourceExplosiveMechanics.cs','SourceDispositionReservation.cs','PreparedWeapons.cs'] if name.endswith('WeaponHandling') else ['SourceDisposableDepletion.cs','PreparedConsumables.cs']
  for n in owned:shutil.copy2(R/'unity/Assets/Ashfall'/name/n,dest/n)
compile('Core',(O/'Core').glob('*.cs'))
compile('RecoveryContracts',(O/'RecoveryContracts').glob('*.cs'),['Core'])
compile('WeaponHandling',(O/'WeaponHandling').glob('*.cs'),['Core','RecoveryContracts'])
compile('InventoryItems',(O/'InventoryItems').glob('*.cs'),['Core','RecoveryContracts','WeaponHandling'])
tests=Path(__file__).parent/'Tests.cs'
if tests.exists():
 shutil.copy2(tests,O/'Tests.cs');compile('Tests',[O/'Tests.cs'],['Core','RecoveryContracts','WeaponHandling','InventoryItems'],True);run([O/'Tests.exe'],'tests')
historical=R/'campaign-staging/20261104-three-chats/unity/SourceExplosiveMechanicsR3/run06'
shutil.copy2(historical/'TestSpawn.cs',O/'TestSpawn.cs')
for n in ['Tests','BehaviorTests','ReviewerRegressions','R2Tests','R3Tests','SchemaProbe']:
 p=O/('Legacy'+n+'.cs');shutil.copy2(historical/(n+'.cs'),p);compile('Legacy'+n,[p,O/'TestSpawn.cs'],['Core','RecoveryContracts','WeaponHandling'],True);run([O/('Legacy'+n+'.exe')],'legacy-'+n)
shutil.copy2(R/'unity/Tools/Modules/WeaponHandling/PreparedTests.cs',O/'PreparedTests.cs');compile('PreparedTests',[O/'PreparedTests.cs'],['Core','RecoveryContracts','WeaponHandling'],True);run([O/'PreparedTests.exe'],'obsolete-prepared-tests',-1)
for n in ['InventoryTests','TransactionTests']:
 p=O/(n+'.cs');shutil.copy2(R/'unity/Tools/Modules/InventoryItems'/(n+'.cs'),p);compile(n,[p],['Core','RecoveryContracts','WeaponHandling','InventoryItems'],True);run([O/(n+'.exe')],n+'-run')
# Preserve unchanged 58-observation projection as a separate historical-only owner overlay.
orig=O;O=orig/'projection';O.mkdir()
for n in ['Core','RecoveryContracts','WeaponHandling']:shutil.copy2(orig/(n+'.dll'),O/(n+'.dll'))
proposal=R/'campaign-staging/20261104-three-chats/unity/SourceDisposableInventoryR1/run03/capture'
for n in ['InventoryProjectionProposal.cs','ProjectionTests.cs']:shutil.copy2(proposal/n,O/n)
compile('InventoryItems',list((orig/'InventoryItems').glob('*.cs'))+[O/'InventoryProjectionProposal.cs'],['Core','RecoveryContracts','WeaponHandling'])
compile('ProjectionTests',[O/'ProjectionTests.cs'],['Core','RecoveryContracts','WeaponHandling','InventoryItems'],True);run([O/'ProjectionTests.exe'],'projection-tests')
O=orig
for name,source,refs in [('WeaponFireTransactions',R/'unity/Assets/Ashfall/Integration/WeaponFireTransactions',['Core','WeaponHandling']),('InventoryRecovery',R/'unity/Assets/Ashfall/Integration/InventoryRecovery',['Core','RecoveryContracts','InventoryItems','WeaponHandling'])]:
 dest=O/name;shutil.copytree(source,dest);compile(name,dest.glob('*.cs'),refs)
for name,folder,ref in [('FireTests','WeaponFireTransactions','WeaponFireTransactions'),('ReservationTests','InventoryRecovery','InventoryRecovery')]:
 p=O/(name+'.cs');shutil.copy2(R/'unity/Tools/Integration'/folder/(name+'.cs'),p);compile(name,[p],['Core','RecoveryContracts','InventoryItems','WeaponHandling',ref],True);run([O/(name+'.exe')],name+'-run')
(O/'hashes.json').write_text(json.dumps([dict(path=str(p.relative_to(O)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in O.rglob('*') if p.is_file()],indent=2))
