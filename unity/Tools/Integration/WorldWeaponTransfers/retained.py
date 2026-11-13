from pathlib import Path
import subprocess,json,shutil,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/WeaponWorldTransfersR2/retained';S.mkdir(exist_ok=True);D=R/'docs/agent-runs/20261104-three-chats/unity/WeaponWorldTransfersR2';old=R/'docs/agent-runs/20261104-three-chats/unity/PreparedWeaponTransfersR1/r5/final-results.json';logs=[]
def pin(p):
 b=p.read_bytes();q=S/'inputs'/hashlib.sha256(b).hexdigest()[:12]/p.name;q.parent.mkdir(parents=True,exist_ok=True)
 if not q.exists():q.write_bytes(b)
 return q
def run(name,sources,target='exe',refs=[]):
 out=S/name;out.mkdir(exist_ok=False);binary=out/('candidate.dll' if target=='library' else 'test.exe');rsp=out/'compile.rsp';args=['/nologo','/langversion:5','/warnaserror+','/target:'+target,'/out:'+str(binary)]+['/reference:'+str(p) for p in refs]+[str(pin(p)) for p in sources];rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);e=dict(name=name,command=cmd,compileExit=c.returncode,diagnostics=c.stdout+c.stderr)
 if c.returncode==0 and target=='exe':
  for p in refs:shutil.copyfile(p,out/p.name)
  t=subprocess.run([str(binary)],capture_output=True,text=True);e.update(runExit=t.returncode,stdout=t.stdout+t.stderr)
 logs.append(e);(D/'retained-results.json').write_text(json.dumps(logs,indent=2));print(name,c.returncode,e.get('runExit'),e.get('stdout',e['diagnostics'])[-250:]);return binary
for n,e in enumerate(json.loads(old.read_text())):
 rsp=Path(e['command'][1][1:]);sources=[Path(a.strip('"')) for a in rsp.read_text().splitlines() if a.strip('"').lower().endswith('.cs')];run('historical-%02d'%n,sources)
prod=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for folder in ['Modules/InventoryItems','Modules/WeaponHandling','Modules/Pickups','Integration/InventoryTransfers','Integration/WorldWeaponTransfers']:prod+=sorted((R/'unity/Assets/Ashfall'/folder).rglob('*.cs'))
dll=run('current-library',prod,'library');run('current-fresh350',[R/'campaign-staging/20261104-three-chats/unity/reviews/PreparedWeaponTransfers-r1/FreshReview.cs'],refs=[dll])
# Current candidate versus unchanged retained composition and mechanics gates.
B=R/'campaign-staging/20261104-three-chats/unity/PreparedWeaponTransfersR1/r5/sealed'
for name in ['Tests','SelectionR4Tests','PreparedCompositionTests']:run('current-'+name,prod+[B/('unity/Tools/Integration/InventoryTransfers/'+name+'.cs')])
run('current-Adversarial',prod+[B/'tests/Adversarial.cs'])
assert all(e['compileExit']==0 and e.get('runExit',0)==0 for e in logs)
