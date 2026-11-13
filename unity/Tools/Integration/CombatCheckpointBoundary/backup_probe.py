from pathlib import Path
import shutil,subprocess,json,os,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/CombatCheckpointBoundaryR4';base=S/'candidate07';out=S/'backup-probe01';out.mkdir();log=[]
for p in base.glob('*.dll'):shutil.copyfile(p,out/p.name)
os.environ['ASHFALL_CHECKPOINT_BINDING']=hashlib.sha256((base/'inputs.json').read_bytes()).hexdigest()
source=base/'Public-R3RegressionTests.cs';s=source.read_text();needle='l.Offer(C());string current=l.CheckpointDigest;';assert needle in s
s=s.replace(needle,needle+'var backup=new AtomicSettingsStore(path+".backup");Check(backup.Load(false).Accepted&&backup.TryRead("combat-checkpoint-sha/2",out prior),"trusted local fixture retains actual physical backup receipt");')
p=out/'PhysicalBackupReceiptR3.cs';p.write_text(s);exe=out/'BackupProbe.exe';rsp=out/'compile.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['/reference:"'+str(p)+'"' for p in out.glob('*.dll')]+['"'+str(p)+'"']))
for args in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],[str(exe),str(out/'data')]]:
 z=subprocess.run(args,cwd=out,capture_output=True,text=True);log.append(dict(argv=args,exit=z.returncode,stdout=z.stdout,stderr=z.stderr));(out/'commands.json').write_text(json.dumps(log,indent=2));print(z.returncode,(z.stdout+z.stderr)[-2200:]);assert z.returncode==0
