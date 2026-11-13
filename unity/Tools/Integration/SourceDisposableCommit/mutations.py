from pathlib import Path
import subprocess,shutil,json,hashlib,sys
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/SourceDisposableCommitR2';B=S/sys.argv[1];O=S/sys.argv[2];O.mkdir()
F=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319');logs=[]
def run(args,expected=0):
 p=subprocess.run([str(x) for x in args],capture_output=True,text=True);logs.append(dict(command=[str(x) for x in args],exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(O/'commands.json').write_text(json.dumps(logs,indent=2));assert (p.returncode==0 if expected==0 else p.returncode!=0),p.stdout+p.stderr;return p.stdout+p.stderr
def compile(dest,name,src,refs,exe=False):
 return run([F/'csc.exe','/nologo','/noconfig','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(dest/(name+('.exe' if exe else '.dll'))),'/reference:'+str(F/'System.dll'),'/reference:'+str(F/'System.Core.dll')]+['/reference:'+str(dest/(n+'.dll')) for n in refs]+list(src))
mutants=[
 ('first-throw','WeaponHandling','SourceDispositionReservation.cs','p.Descriptor.Before.Loaded!=1||p.Descriptor.After.Loaded!=0||',''),
 ('raw-split','WeaponHandling','SourceExplosiveMechanics.cs','disposition!=null||!validated','!validated'),
 ('no-rearm','WeaponHandling','PreparedWeapons.cs','n.Suspend();n.overrun=0;','n.overrun=0;'),
 ('whole-stack','InventoryItems','SourceDisposableDepletion.cs','afterItems.Remove(member);','afterItems.RemoveAll(delegate(ItemInstance i){return i.DefinitionId==member.DefinitionId;});'),
 ('wrong-fallback','InventoryItems','SourceDisposableDepletion.cs','if(selected==""&&afterItems.Count>0)','selected="";if(selected==""&&afterItems.Count>0)'),
 ('schema-field','InventoryItems','SourceDisposableDepletion.cs','public sealed partial class Inventory','public sealed partial class Inventory')]
for name,owner,file,old,new in mutants:
 dest=O/name;dest.mkdir()
 for n in ['Core','RecoveryContracts','WeaponHandling','InventoryItems']:shutil.copy2(B/(n+'.dll'),dest/(n+'.dll'))
 shutil.copytree(B/owner,dest/owner);p=dest/owner/file;t=p.read_text();assert old in t
 if name=='schema-field':t=t.replace('    {','    {\n        public int UnknownDispositionField;',1)
 else:t=t.replace(old,new)
 p.write_text(t)
 refs=['Core','RecoveryContracts']+(['WeaponHandling'] if owner=='InventoryItems' else [])
 compile(dest,owner,(dest/owner).glob('*.cs'),refs)
 shutil.copy2(B/'Tests.cs',dest/'Tests.cs');compile(dest,'Tests',[dest/'Tests.cs'],['Core','RecoveryContracts','WeaponHandling','InventoryItems'],True)
 result=run([dest/'Tests.exe'],1);(dest/'result.txt').write_text(result);print(name,'DETECTED',result.split('System.Exception:')[-1].splitlines()[0])
(O/'hashes.json').write_text(json.dumps([dict(path=str(p.relative_to(O)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in O.rglob('*') if p.is_file()],indent=2))
