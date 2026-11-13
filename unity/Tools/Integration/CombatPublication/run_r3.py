from pathlib import Path
import json,hashlib,shutil,subprocess,sys,os
R=Path.cwd();runid=sys.argv[1];S=R/'campaign-staging/20261104-three-chats/unity/CombatCheckpointR3'/runid;D=R/'docs/agent-runs/20261104-three-chats/unity/CombatCheckpointR3'/runid;S.mkdir(exist_ok=False);D.mkdir(exist_ok=False)
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def save(p,x):p.write_text(json.dumps(x,indent=2))
base=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/CombatCheckpointR3/before.json').read_text());sources=[];inventory=[]
for x in base:
 if not x['path'].endswith('.cs') or 'Tools' in x['path']:continue
 # Consume frozen phase12/current bytes for all dependencies. Only owned adapter changes are live candidate inputs.
 owned='Integration\\CombatPublication\\' in x['path'] or x['path'].endswith('CheckpointState.cs')
 p=R/x['path'] if owned else R/x['snapshot'];q=S/'inputs'/x['path'];q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);sources.append(q);inventory.append(dict(original=x['path'],path=str(q.relative_to(R)),sha256=h(q),baseline=x['sha256']))
for p in (R/'unity/Assets/Ashfall/Integration/CombatPublication').glob('*.cs'):
 if any(x['original']==str(p.relative_to(R)) for x in inventory):continue
 q=S/'inputs'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);sources.append(q);inventory.append(dict(original=str(p.relative_to(R)),path=str(q.relative_to(R)),sha256=h(q),baseline=None))
save(D/'inputs.json',inventory)
os.environ['ASHFALL_CHECKPOINT_BINDING']=hashlib.sha256(json.dumps(inventory,sort_keys=True).encode()).hexdigest();save(D/'binding.json',dict(sourceBinding=os.environ['ASHFALL_CHECKPOINT_BINDING']))
results=[]
for name,main,extra in [('CheckpointTests','unity/Tools/Integration/CombatPublication/CheckpointTests.cs',[]),('R3RegressionTests','unity/Tools/Integration/CombatPublication/R3RegressionTests.cs',[]),('PublicationTests','unity/Tools/Integration/CombatPublication/PublicationTests.cs',[]),('Bridge131','unity/Tools/Integration/CombatHealthBridgeTests.cs',[]),('Health49','unity/Tools/Modules/HealthStates/HealthTests.cs',[])]:
 out=S/name;out.mkdir();m=out/Path(main).name;shutil.copyfile(R/main,m);exe=out/(name+'.exe');rsp=out/'compile.rsp';data=out/'data';data.mkdir()
 rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources+[m]]));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];z=subprocess.run(cmd,capture_output=True,text=True);(out/'compile.log').write_text(z.stdout+z.stderr);v=dict(name=name,compile=z.returncode,command=cmd)
 if z.returncode==0:
  cmd=[str(exe),str(data)];z=subprocess.run(cmd,capture_output=True,text=True);(out/'run.log').write_text(z.stdout+z.stderr);v.update(exit=z.returncode,execution=cmd,output=z.stdout+z.stderr)
 else:v['output']=z.stdout+z.stderr
 results.append(v);save(D/'results.json',results);print(name,'compile',v['compile'],'exit',v.get('exit'),'PASS lines',v['output'].count('PASS'),'tail',v['output'][-140:])
print('inputs',len(sources));sys.exit(any(x.get('exit',1)!=0 for x in results))
