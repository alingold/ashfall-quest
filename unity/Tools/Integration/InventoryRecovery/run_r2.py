from pathlib import Path
import subprocess,hashlib,json,sys,shutil
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/InventoryCheckpointR2';D=R/'docs/agent-runs/20261104-three-chats/unity/InventoryCheckpointR2'
out=B/sys.argv[1];out.mkdir();log=D/sys.argv[1];log.mkdir();results=[];inputs=[]
def pin(p):
 q=out/'src'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());inputs.append({'source':str(p),'snapshot':str(q),'sha256':hashlib.sha256(q.read_bytes()).hexdigest()});return q
groups={}
owned={'CheckpointState.cs','FiniteAmmoReserve.cs','PreparedWeapons.cs','PreparedLifetimes.cs','WeaponLifetimes.cs','PreparedFire.cs'}
for label,folder in [('Core','Core'),('RecoveryContracts','RecoveryContracts'),('Inventory','Modules/InventoryItems'),('Weapon','Modules/WeaponHandling'),('Recovery','Integration/InventoryRecovery'),('FireAdapter','Integration/WeaponFireTransactions')]:
 # Other owners' newly appearing registry code is excluded: single before pin only.
 before=list((B/'before/unity/Assets/Ashfall'/folder).glob('*.cs'))
 names={p.name for p in before}
 paths=list((R/'unity/Assets/Ashfall'/folder).glob('*.cs'))
 groups[label]=[]
 for p in paths:
  if p.name not in names and label!='RecoveryContracts':continue
  if label in ['Core','Inventory','Weapon','FireAdapter'] and p.name not in owned:
   source=B/'before'/p.relative_to(R);q=out/'src'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(source.read_bytes());inputs.append({'source':str(source),'snapshot':str(q),'sha256':hashlib.sha256(q.read_bytes()).hexdigest()});groups[label].append(q)
  else:groups[label].append(pin(p))
for folder in ['InventoryRecovery','WeaponFireTransactions']:
 for p in (R/'unity/Tools/Integration'/folder).glob('*.py'):pin(p)
tests=pin(R/'unity/Tools/Integration/InventoryRecovery/Tests.cs');extra=pin(R/'unity/Tools/Integration/InventoryRecovery/ReservationTests.cs');fire=pin(R/'unity/Tools/Integration/WeaponFireTransactions/FireTests.cs')
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
def compile(label,src,refs=(),exe=False):
 target=out/(label+('.exe' if exe else '.dll'));rsp=out/(label+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(target)+'"']+['/r:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in src]));run([csc,'@'+str(rsp)],label+'-compile');return target
def run(cmd,label,expected=0):
 p=subprocess.run(cmd,capture_output=True,text=True);(log/(label+'.log')).write_text(p.stdout+p.stderr);results.append(dict(label=label,command=cmd,exit=p.returncode,passes=p.stdout.count('PASS ')));(log/'results.json').write_text(json.dumps(results,indent=2));print(label,p.returncode,p.stdout[-300:] if p.returncode else 'PASS lines '+str(p.stdout.count('PASS ')));assert (p.returncode==0)==(expected==0)
core=compile('Core',groups['Core']);contract=compile('RecoveryContracts',groups['RecoveryContracts'],[core]);inv=compile('Inventory',groups['Inventory'],[core,contract]);weapon=compile('Weapon',groups['Weapon'],[core,contract]);recovery=compile('Recovery',groups['Recovery'],[core,contract,inv,weapon]);adapter=compile('FireAdapter',groups['FireAdapter'],[core,contract,weapon]);refs=[core,contract,inv,weapon,recovery]
for name,src,rr,args in [('Original1152',tests,refs,[str(out)]),('Reservations',extra,refs,[]),('OriginalFire225',fire,[core,contract,weapon,adapter],[])]:
 exe=compile(name,[src],rr,True)
 for i in range(2):run([str(exe)]+args,name+'-cold'+str(i))
# Mutation probes compile each actual module independently, retaining graph boundaries.
original=next(p for p in groups['Weapon'] if p.name=='PreparedWeapons.cs')
for label,needle,replacement in [('untracked-suspension','if(!ReserveOperation(token,long.MaxValue,0))','if(token==null)'),('no-exclusion','if(FireReservation!=null && (!(FireReservation is IPreparedWeaponFire)||now<=FireReservationExpiry))return false;','if(token==null)return false;')]:
 folder=out/label;folder.mkdir();mutant=folder/original.name;mutant.write_text(original.read_text().replace(needle,replacement));assert mutant.read_text()!=original.read_text()
 dll=compile(label,[mutant if p==original else p for p in groups['Weapon']],[core,contract]);shutil.copyfile(dll,folder/'Weapon.dll')
 # Use same assembly identity so downstream Recovery binds the mutated implementation.
 rsp=out/(label+'.rsp');s=rsp.read_text().replace(str(dll),str(folder/'Weapon.dll'));rsp.write_text(s);run([csc,'@'+str(rsp)],label+'-identity')
 exe=compile(label+'Tests',[extra],refs,True);shutil.copyfile(exe,folder/exe.name)
 for p in [core,contract,inv,recovery]:shutil.copyfile(p,folder/p.name)
 run([str(folder/exe.name)],label+'-killed',1)
(log/'inputs.json').write_text(json.dumps(inputs,indent=2))
# Unknown serialization fields fail closed in each separately compiled actual owner.
for name,owner,marker in [('Inventory.cs','Inventory','public sealed partial class Inventory'),('WeaponHandling.cs','Weapon','public sealed partial class WeaponHandling'),('FiniteAmmoReserve.cs','Weapon','public sealed class FiniteAmmoReserve : IRevisionedAmmoReserve'),('ClosureFlick.cs','Weapon','internal sealed class ClosureFlick')]:
 folder=out/('field-'+name);folder.mkdir();original=next(p for p in groups[owner] if p.name==name);text=original.read_text();pos=text.index('{',text.index(marker))+1
 mutant=folder/name;mutant.write_text(text[:pos]+'\nprivate int unaccounted=7;public int Probe{get{return unaccounted;}}\n'+text[pos:]);dll=compile('field-'+name,[mutant if p==original else p for p in groups[owner]],[core,contract]);rsp=out/('field-'+name+'.rsp');rsp.write_text(rsp.read_text().replace(str(dll),str(folder/(owner+'.dll'))));run([csc,'@'+str(rsp)],'field-'+name+'-identity')
 for p in refs:
  if p.name!=owner+'.dll':shutil.copyfile(p,folder/p.name)
 probe=folder/'Probe.cs';probe.write_text('''using System;using System.IO;using Ashfall.Core;using Ashfall.RecoveryContracts;using Ashfall.Integration.InventoryRecovery;using Ashfall.Modules.InventoryItems;using Ashfall.Modules.WeaponHandling;class Probe{sealed class G:IAuthorityGate{public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}sealed class B:IInventoryBoundary{public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] a){return RuleResult.Accept();}}static void Main(){var cat=SourceCatalog.Create();try{ColdInventoryCheckpoint.Capture(new CheckpointBinding("b",ColdInventoryCheckpoint.CatalogFingerprint(cat),"s","a",0,0,0,1),cat,new Inventory(cat,new B(),"s","a",0,false),new[]{new WeaponCheckpointMember("w","p",new WeaponHandling("s","a",0,WeaponMechanism.ServiceRifle,new FiniteAmmoReserve(10,5),new G()))});}catch(InvalidDataException){Console.WriteLine("PASS unknown owner field rejected");return;}throw new Exception("unaccounted field accepted");}}''')
 exe=compile('field-'+name+'Probe',[probe],refs,True);shutil.copyfile(exe,folder/exe.name);run([str(folder/exe.name)],'field-'+name+'-refusal')
