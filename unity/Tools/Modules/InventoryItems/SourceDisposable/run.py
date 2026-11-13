from pathlib import Path
import subprocess,hashlib,json,sys
r=Path(__file__).resolve().parents[5]
base=r/'campaign-staging/20261104-three-chats/unity/SourceDisposableInventoryR1'
d=r/'docs/agent-runs/20261104-three-chats/unity/SourceDisposableInventoryR1'
run=base/sys.argv[1];run.mkdir(exist_ok=False)
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
before=base/'before'
def frozen(rel):return before/rel
core=frozen('unity/Assets/Ashfall/Core/Contracts.cs')
recovery=list(frozen('unity/Assets/Ashfall/RecoveryContracts').glob('*.cs'))
owner=list(frozen('unity/Assets/Ashfall/Modules/InventoryItems').glob('*.cs'))
live=r/'unity/Assets/Ashfall/Modules/InventoryItems/SourceDisposableDepletion.cs'
proposal=base/'InventoryProjectionProposal.cs'
test=Path(__file__).with_name('ProjectionTests.cs')
capture=run/'capture';capture.mkdir()
for p in [live,proposal,test,Path(__file__)]:
 q=capture/p.name;q.write_bytes(p.read_bytes())
live=capture/live.name;proposal=capture/proposal.name;test=capture/test.name
results=[]
def command(label,args):
 p=subprocess.run([str(x) for x in args],capture_output=True,text=True,cwd=r)
 (run/(label+'.log')).write_text(p.stdout+p.stderr)
 results.append(dict(label=label,command=[str(x) for x in args],exit=p.returncode))
 print(label,p.returncode,(p.stdout+p.stderr)[-500:]);return p.returncode
def compile(label,out,sources,refs=(),exe=False):
 rsp=run/(label+'.rsp')
 refs=list(refs)+[csc.parent/'System.dll',csc.parent/'System.Core.dll']
 rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in sources]))
 if command(label,[csc,'/noconfig','@'+str(rsp)]):raise RuntimeError(label+' compilation failed')
coreDll=run/'Core.dll';recDll=run/'RecoveryContracts.dll'
compile('core',coreDll,[core]);compile('recovery',recDll,recovery,[coreDll])
def suite(name,files,expected_failure=False):
 folder=run/name;folder.mkdir(); dll=folder/'InventoryItems.dll';exe=folder/'Tests.exe'
 compile(name+'-owner',dll,files,[coreDll,recDll])
 compile(name+'-consumer',exe,[test],[coreDll,recDll,dll],True)
 for dep in [coreDll,recDll]:(folder/dep.name).write_bytes(dep.read_bytes())
 code=command(name+'-tests',[exe]);return code!=0 if expected_failure else code==0
ok=suite('candidate',owner+[live,proposal])
mutants={
 'holster':('if(afterItems.Count==0)holstered=true;','holstered=true;'),
 'survivor':('selected=member.InstanceId;break;','selected="";break;'),
 'expiry':('clock.Tick<expiry','true'),
 'exclude':('preparedBusy=true;','preparedBusy=false;'),
 'whole-stack':('afterItems.Remove(item);','afterItems.RemoveAll(delegate(ItemInstance x){return o.Group(item).Contains(x);});'),
 'revocation':('if(!owner.boundary.Validate(now,InventoryOperation.Remove,null,new ItemInstance[]{removed}).Accepted)return false;','// removed eligibility revalidation'),
}
for name,(old,new) in mutants.items():
 text=proposal.read_text();assert old in text
 m=capture/(name+'.cs');m.write_text(text.replace(old,new))
 ok=suite(name,owner+[live,m],True) and ok
# Guard evidence: a deliberately added owner field must reject schema1 capture.
m=capture/'field-drift.cs';m.write_text('namespace Ashfall.Modules.InventoryItems { public sealed partial class Inventory { public int UnaccountedState; } }')
ok=suite('field-drift',owner+[live,proposal,m],True) and ok
# Preserve unchanged public regression fixtures, separately compiled against live owner.
liveDll=run/'InventoryItems.dll';compile('live-owner',liveDll,owner+[live],[coreDll,recDll])
for name in ['InventoryTests','TransactionTests']:
 src=frozen('unity/Tools/Modules/InventoryItems/'+name+'.cs');exe=run/(name+'.exe')
 compile(name+'-compile',exe,[src],[coreDll,recDll,liveDll],True)
 ok=(command(name+'-tests',[exe])==0) and ok
(d/(sys.argv[1]+'-results.json')).write_text(json.dumps(dict(passed=ok,scope='inventory-only staged proposal; live refusal; no mechanics authority',results=results),indent=2))
manifest=[]
for p in sorted(run.rglob('*')):
 if p.is_file():manifest.append(dict(path=p.relative_to(r).as_posix(),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(d/(sys.argv[1]+'-outputs.json')).write_text(json.dumps(manifest,indent=2))
sys.exit(0 if ok else 1)
