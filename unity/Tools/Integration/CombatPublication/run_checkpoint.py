from pathlib import Path
import json,hashlib,shutil,subprocess,sys,datetime,os
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/CombatCheckpointR2';runid=sys.argv[1] if len(sys.argv)>1 else datetime.datetime.now().strftime('replay-%Y%m%d-%H%M%S');assert runid and all(c.isalnum() or c in '-_' for c in runid);D=R/'docs/agent-runs/20261104-three-chats/unity/CombatCheckpointR2'/runid;S=S/runid;S.mkdir(parents=True,exist_ok=False);D.mkdir(parents=True,exist_ok=False)
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def save(p,x):p.write_text(json.dumps(x,indent=2))
sources=list((R/'unity/Assets/Ashfall/Modules/Combat').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Modules/HealthStates').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))+[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']+list((R/'unity/Assets/Ashfall/Integration/CombatPublication').glob('*.cs'))
inputs=[];pinned=[]
for p in sources:
 q=S/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);pinned.append(q);inputs.append(dict(path=str(p.relative_to(R)),sha256=h(q)))
shutil.copyfile(Path(__file__),S/'Run.py')
save(D/'inputs.json',inputs)
os.environ['ASHFALL_CHECKPOINT_BINDING']=hashlib.sha256(json.dumps(inputs,sort_keys=True).encode()).hexdigest()
save(D/'binding.json',dict(sourceBinding=os.environ['ASHFALL_CHECKPOINT_BINDING'],scope='exact pinned sources inventory'))
results=[]
def run(name,src,main,args=[]):
 out=S/'gates'/name;out.mkdir(parents=True,exist_ok=True);exe=out/(name+'.exe');rsp=out/'compile.rsp'
 frozen=[]
 for index,p in enumerate(src+[main]):
  q=out/'inputs'/(str(index)+'-'+p.name);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);frozen.append(q)
 save(out/'inputs.json',[dict(source=str(p),sha256=h(p),snapshot=str(q)) for p,q in zip(src+[main],frozen)])
 rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in frozen]))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];z=subprocess.run(cmd,capture_output=True,text=True);(out/'compile.log').write_text(z.stdout+z.stderr);v=dict(name=name,compile=z.returncode,command=subprocess.list2cmdline(cmd))
 if z.returncode==0:
  z=subprocess.run([str(exe)]+args,capture_output=True,text=True);(out/'run.log').write_text(z.stdout+z.stderr);v.update(run=z.returncode,tail=(z.stdout+z.stderr)[-800:],execution=subprocess.list2cmdline([str(exe)]+args))
 else:v['tail']=z.stdout+z.stderr
 print(name,v.get('run'),v['tail']);results.append(v);save(D/'results.json',results)
# Reproduce original sealed expected sources; no old wrappers/binaries or artifacts mutated.
old=R/'campaign-staging/20261104-three-chats/unity/reviews/CombatHealth-v3'
manifest=R/'docs/agent-runs/20261104-three-chats/unity/phase05/candidates/CombatHealth-v3/candidate-manifest.json'
assert h(manifest)=='39C595BCB00027683706AE8F1002BD22FCE033468BE2BD3B6765AD9C015010D9'
records=json.loads(manifest.read_text(encoding='utf-8-sig'))['files'];verification=[]
for f in records:
 if 'validation\\inputs\\' in f['path']:
  rel=f['path'].split('validation\\inputs\\',1)[1];p=old/'sealed'/rel;verification.append(dict(path=str(p),expected=f['sha256'],actual=h(p)));assert h(p)==f['sha256']
save(D/'sealed-verification.json',verification)
sealed=list((old/'sealed/unity/Assets').rglob('*.cs'))
for name,main in [('FrozenBridge131',old/'sealed/unity/Tools/Integration/CombatHealthBridgeTests.cs'),('FrozenHealth49',old/'sealed/unity/Tools/Modules/HealthStates/HealthTests.cs'),('FrozenPrior46',old/'Prior46Acceptance.cs'),('FrozenAdversarial42',old/'Adversarial.cs')]:run(name,sealed,main)
run('CurrentBridge131',pinned,R/'unity/Tools/Integration/CombatHealthBridgeTests.cs')
run('CurrentHealth49',pinned,R/'unity/Tools/Modules/HealthStates/HealthTests.cs')
run('CurrentPrior46',pinned,old/'Prior46Acceptance.cs');run('CurrentAdversarial42',pinned,old/'Adversarial.cs')
testdir=S/'gates/PublicationTests/data';testdir.mkdir(parents=True,exist_ok=True)
run('PublicationTests',pinned,R/'unity/Tools/Integration/CombatPublication/PublicationTests.cs',[str(testdir)])
(S/'independent-data').mkdir()
run('Independent45',pinned,R/'campaign-staging/20261104-three-chats/unity/reviews/CombatPublication-r1/Independent.cs',[str(S/'independent-data')])
run('CheckpointTests',pinned,R/'unity/Tools/Integration/CombatPublication/CheckpointTests.cs',[str(S/'checkpoint-data')])
for name in ['CombatTests','ReviewerRegressions','R2Tests']:
 run(name,pinned+[R/'unity/Tools/Modules/Combat/CombatFixture.cs'],R/('unity/Tools/Modules/Combat/'+name+'.cs'))
save(D/'dependency-after.json',[dict(path=x['path'],before=x['sha256'],after=h(R/x['path']),stable=x['sha256']==h(R/x['path'])) for x in inputs])





sys.exit(1 if any(x.get('run',1)!=0 or x['compile']!=0 for x in results) else 0)


