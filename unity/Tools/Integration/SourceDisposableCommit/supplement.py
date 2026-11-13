from pathlib import Path
import subprocess,shutil,json,hashlib,sys
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/SourceDisposableCommitR2';B=S/'run08';O=S/sys.argv[1];O.mkdir();F=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319');logs=[]
def run(args,expected=0):
 p=subprocess.run([str(x) for x in args],capture_output=True,text=True);logs.append(dict(command=[str(x) for x in args],exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(O/'commands.json').write_text(json.dumps(logs,indent=2));assert (p.returncode==0 if expected==0 else p.returncode!=0),p.stdout+p.stderr;return p.stdout
def compile(dest,name,src,refs,exe=False,expected=0):
 return run([F/'csc.exe','/nologo','/noconfig','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(dest/(name+('.exe' if exe else '.dll'))),'/reference:'+str(F/'System.dll'),'/reference:'+str(F/'System.Core.dll')]+['/reference:'+str(dest/(n+'.dll')) for n in refs]+list(src),expected)
for version in ['before','after']:
 dest=O/version;dest.mkdir()
 for n in ['Core','RecoveryContracts']:shutil.copy2(B/(n+'.dll'),dest/(n+'.dll'))
 if version=='before':
  for n in ['WeaponHandling','InventoryItems']:
   shutil.copytree(S/'before/unity/Assets/Ashfall/Modules'/n,dest/n);compile(dest,n,(dest/n).glob('*.cs'),['Core','RecoveryContracts'])
 else:
  for n in ['WeaponHandling','InventoryItems']:shutil.copy2(B/(n+'.dll'),dest/(n+'.dll'))
 shutil.copy2(B/'LegacySchemaProbe.cs',dest/'SchemaProbe.cs');compile(dest,'SchemaProbe',[dest/'SchemaProbe.cs'],['Core','RecoveryContracts','WeaponHandling'],True)
 (dest/'weapon-hash.txt').write_text(run([dest/'SchemaProbe.exe']))
 p=dest/'InventoryProbe.cs';p.write_text('''using System;using Ashfall.Core;using Ashfall.RecoveryContracts;using Ashfall.Modules.InventoryItems;
class Probe {sealed class Boundary:IInventoryBoundary {public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] items){return RuleResult.Accept();}}
static void Main(){var b=new CheckpointBinding("build","catalog","s","a",1,1,1,1);Console.WriteLine(RecoveryWire.Hash(InventoryCheckpointCodec.Capture(new Inventory(SourceCatalog.Create(),new Boundary(),"s","a",1,true),b)));}}''')
 compile(dest,'InventoryProbe',[p],['Core','RecoveryContracts','InventoryItems','WeaponHandling'],True);(dest/'inventory-hash.txt').write_text(run([dest/'InventoryProbe.exe']))
for n in ['weapon','inventory']:assert (O/'before'/(n+'-hash.txt')).read_bytes()==(O/'after'/(n+'-hash.txt')).read_bytes();print(n,'schema1 byte hash identical')
dest=O/'after'
for n in ['WeaponFireTransactions']:shutil.copy2(B/(n+'.dll'),dest/(n+'.dll'))
p=dest/'FireTests225.cs';shutil.copy2(R/'campaign-staging/20261104-three-chats/unity/WeaponPreparedFireR1/final03/sources/unity/Tools/Integration/WeaponFireTransactions/FireTests.cs',p)
compile(dest,'FireTests225',[p],['Core','WeaponHandling','WeaponFireTransactions'],True);print('Historical225 after expected failure:',run([dest/'FireTests225.exe'],1))
dest=O/'before';shutil.copy2(B/'WeaponFireTransactions.dll',dest/'WeaponFireTransactions.dll');shutil.copy2(p,dest/'FireTests225.cs');compile(dest,'FireTests225',[dest/'FireTests225.cs'],['Core','WeaponHandling','WeaponFireTransactions'],True);print('Historical225 before expected failure:',run([dest/'FireTests225.exe'],1))
dest=O/'after'
p=dest/'Hostile.cs';p.write_text('class Hostile {object x=new Ashfall.Modules.WeaponHandling.WeaponLifetimes.SourceDispositionReservation();}')
print('private constructor rejects:',compile(dest,'Hostile',[p],['Core','WeaponHandling'],expected=1))
# Pin source semantic groundtruth, separately from current gameplay dependency capture.
dest=O/'source';dest.mkdir()
for name in ['source-game','source-quest']:
 shutil.copytree(R/'campaign-staging/20261104-three-chats/unity/SourceExplosiveMechanicsR3'/name,dest/name)
for name in ['game','quest-r22/source']:
 p=R/name/'scripts/weapon_inventory.gd';d=dest/('game-inventory.gd' if name=='game' else 'quest-inventory.gd');shutil.copy2(p,d)
print('Source inventory/explosive groundtruth pinned')
(O/'hashes.json').write_text(json.dumps([dict(path=str(p.relative_to(O)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in O.rglob('*') if p.is_file()],indent=2))
