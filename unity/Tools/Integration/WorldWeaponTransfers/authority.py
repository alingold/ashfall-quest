from pathlib import Path
import hashlib,json,shutil,subprocess,sys
r=Path.cwd(); label=sys.argv[1];assert label.isalnum()
root=r/'campaign-staging/20261104-three-chats/unity/InventoryWorldAuthorityR4';s=root/label;s.mkdir(exist_ok=False)
d=r/'docs/agent-runs/20261104-three-chats/unity/InventoryWorldAuthorityR4'
logs=[];manifest=[]
def copy(p,q,original):
 q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);manifest.append(dict(original=original,snapshot=str(q.relative_to(r)),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));return q
before=root/'before';prod=[]
phase13={x['original']:x for x in json.loads((r/'docs/agent-runs/20261104-three-chats/unity/phase13/source-index.json').read_text())} if len(sys.argv)>2 else {}
for p in before.rglob('*.cs'):
 rel=p.relative_to(before)
 if 'Tools' in rel.parts:continue
 live=r/rel
 owned=p.name=='PreparedWeaponHandoff.cs' or 'WorldWeaponTransfers' in rel.parts
 pinned=phase13.get(str(rel));source=r/pinned['snapshot'] if pinned else p
 if pinned:assert hashlib.sha256(source.read_bytes()).hexdigest().upper()==pinned['sha256'].upper()
 prod.append(copy(live if owned else source,s/'inputs'/rel,str(rel)))
def execute(cmd,expected=0):
 c=subprocess.run([str(x) for x in cmd],capture_output=True,text=True);rec=dict(command=[str(x) for x in cmd],exit=c.returncode,output=c.stdout+c.stderr);logs.append(rec);print(c.stdout+c.stderr);assert (c.returncode==0) if expected==0 else (c.returncode!=0);return c
def build(name,files,refs=(),extra=(),expected=0):
 out=s/name;out.mkdir();binary=out/('candidate.dll' if not refs else 'test.exe')
 framework=[] if '/nostdlib+' in extra else ['/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll']
 args=['/nologo','/langversion:5','/warnaserror+','/target:'+('library' if not refs else 'exe'),'/out:'+str(binary)]+framework+list(extra)+['/reference:'+str(p) for p in refs]+[str(p) for p in files]
 rsp=out/'compile.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args));execute([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)],expected)
 for ref in refs:shutil.copyfile(ref,out/ref.name)
 return binary
try:
 dll=build('library',prod)
 old=copy(before/'unity/Tools/Integration/WorldWeaponTransfers/WorldTests.cs',s/'WorldTests.cs','R3 retained WorldTests.cs')
 exe=build('retained',[old],[dll]);execute([exe])
 raw=old.read_text().replace('public class WorldTests','public class AuthorityTests')
 raw=raw.replace('public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] outgoing){return Validate(c);}', 'public bool RemoveAllowed=true,AdmitAllowed=true;public int RemoveChecks,AdmitChecks;public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] outgoing){if(o==InventoryOperation.Remove){RemoveChecks++;if(!RemoveAllowed)return RuleResult.Reject("remove revoked");}if(o==InventoryOperation.Admit){AdmitChecks++;if(!AdmitAllowed)return RuleResult.Reject("admit revoked");}return Validate(c);}')
 at='  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"drop prepare");'
 added='''
  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"boundary revoke prepared pair");
  int removals=host.RemoveChecks;host.RemoveAllowed=false;
  A(!op.TryCommit(),"final Inventory removal revocation denied");
  A(host.RemoveChecks==removals+1&&source.Items.Length==2&&coordinator.Records.Length==0&&geometry.Commits==0&&weapons.IsActive(items[0],"a",1)&&weapons.IsActive(items[1],"a",1),"exact real owners survive boundary denial");host.RemoveAllowed=true;
'''
 assert at in raw;raw=raw.replace(at,added+at)
 at='A(op.TryCommit(),"collect publish");'
 added='''
  var same=records[0];int admissions=host.AdmitChecks;host.AdmitAllowed=false;
  A(!op.TryCommit(),"final Inventory admission revocation denied");
  A(host.AdmitChecks==admissions+1&&target.Items.Length==0&&Object.ReferenceEquals(same,coordinator.Records[0])&&coordinator.Pending.Length==1,"collect denial retains exact world receipt and identity");host.AdmitAllowed=true;
  A(coordinator.PrepareCollect(C(clock,"b",2),principal,60,target,records,out op),"collect retry after boundary denial");
  var stale=op;clock.Now=60;
  A(coordinator.Ack(1),"ACK reaps abandoned expired collect");
  A(!stale.TryCommit()&&target.Items.Length==0&&coordinator.Records.Length==2&&coordinator.Pending.Length==0,"stale collect cannot resurrect ownership or erased ACK");
  clock.Now=61;A(coordinator.PrepareCollect(C(clock,"b",2),principal,90,target,records,out op),"transfer retry after expiry");
'''
 # Existing world records expire at60; use a longer drop deadline for this expiry scenario.
 raw=raw.replace('principal,60,source,new[]{"one","two"},out op),"drop prepare"','principal,120,source,new[]{"one","two"},out op),"drop prepare"')
 assert at in raw;raw=raw.replace(at,added+at)
 # ACK1 was deliberately consumed during abandoned-token cleanup above.
 raw=raw.replace('coordinator.Pending.Length==2&&coordinator.Ack(1)&&!coordinator.Ack(1)&&coordinator.Pending[0].Sequence==2','coordinator.Pending.Length==1&&!coordinator.Ack(1)&&coordinator.Pending[0].Sequence==2')
 test=s/'AuthorityTests.cs';test.write_text(raw);exe=build('authority',[test],[dll]);execute([exe])
 recovery=copy(before/'unity/Tools/Integration/WorldWeaponTransfers/RecoveryTests.cs',s/'RecoveryTests.cs','R3 retained RecoveryTests.cs')
 exe=build('recovery',[recovery],[dll])
 for mode in ['pressure','capacity','decisionfail','ackfail']:execute([exe,mode,s/(mode+'.journal')])
 for writer,reader in [('write','inspect'),('writeack','inspectack'),('prepare','inspectprepare')]:
  path=s/(writer+'.journal');execute([exe,writer,path]);execute([exe,reader,path])
 path=s/'torn.journal';path.write_bytes((s/'write.journal').read_bytes()[:-1]);execute([exe,'corrupt',path])
 # Installed real Unity/netstandard references, compile only. No engine execution.
 refRsp=r/'campaign-staging/20261104-three-chats/unity/reviews/WeaponWorldTransfersReviewR2/real-unity-api04-neutral-only/compile.rsp'
 refargs=[line.strip().strip('"') for line in refRsp.read_text().splitlines() if line.strip().strip('"').startswith('/reference:')]
 build('installed-api-full',prod,extra=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+refargs,expected=1)
 build('installed-api-neutral',[p for p in prod if 'Engine' not in p.parts],extra=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+refargs)
 for name,filename,oldtext,newtext in [
  ('remove','PreparedWeaponHandoff.cs','if(removed.Length>0&&!owner.boundary.Validate','if(removed.Length<0&&!owner.boundary.Validate'),
  ('admit','PreparedWeaponHandoff.cs','foreach(var item in admitted)if(!owner.boundary.Validate','foreach(var item in new ItemInstance[0])if(!owner.boundary.Validate'),
  ('expiry','WorldWeaponCoordinator.cs','void Expire(){if(outstanding!=null&&clock.Tick>=outstanding.Expiry)outstanding.Abort();}','void Expire(){}'),
  ('connection','WorldWeaponCoordinator.cs','principal.ConnectionLife!=connection||','principal.ConnectionLife<connection||')]:
  original=next(p for p in prod if p.name==filename);text=original.read_text();assert oldtext in text
  mutant=s/(name+'.cs');mutant.write_text(text.replace(oldtext,newtext));files=[mutant if p==original else p for p in prod]
  mdll=build('mutant-'+name,files);mexe=build('mutant-test-'+name,[test],[mdll]);execute([mexe],expected=1)
finally:
 (d/(label+'-commands.json')).write_text(json.dumps(logs,indent=2));(d/(label+'-inputs.json')).write_text(json.dumps(manifest,indent=2))
