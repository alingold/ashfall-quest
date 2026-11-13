import pathlib, json, hashlib, shutil, subprocess, sys
R=pathlib.Path.cwd()
S=R/'campaign-staging/20261104-three-chats/unity/WeaponPreparedFireR1'
D=R/'docs/agent-runs/20261104-three-chats/unity/WeaponPreparedFireR1'
name=sys.argv[1]; out=S/name; out.mkdir(); logs=D/name; logs.mkdir()
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
base=S/'before'; sources=[]
for rel in [pathlib.Path('unity/Assets/Ashfall/Core/Contracts.cs')]+[p.relative_to(base) for p in (base/'unity/Assets/Ashfall/Modules/WeaponHandling').glob('*.cs')]:
 p=base/rel; q=out/'sources'/rel; q.parent.mkdir(parents=True,exist_ok=True); shutil.copyfile(p,q); sources.append(q)
for p in [R/'unity/Assets/Ashfall/Modules/WeaponHandling/PreparedFire.cs']+list((R/'unity/Assets/Ashfall/Integration/WeaponFireTransactions').glob('*.cs'))+list((R/'unity/Tools/Integration/WeaponFireTransactions').glob('*.cs')):
 q=out/'sources'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);sources.append(q)
manifest=[{'path':str(p.relative_to(R)),'sha256':sha(p)} for p in sources]
compiler=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
def compile_run(label, items, main=None, library=False, refs=()):
 target=out/(label+('.dll' if library else '.exe')); rsp=out/(label+'.rsp')
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('library' if library else 'exe'),'/out:"'+str(target)+'"']
 if main:args+=['/main:'+main]
 args+=['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in items]
 rsp.write_text('\n'.join(args)); cmd=[compiler,'@'+str(rsp)]
 c=subprocess.run(cmd,capture_output=True,text=True); (logs/(label+'-compile.log')).write_text(c.stdout+c.stderr)
 row={'label':label,'command':subprocess.list2cmdline(cmd),'compile':c.returncode}
 if c.returncode==0:
  row['binary_sha256']=sha(target)
  if not library:
   t=subprocess.run([str(target)],capture_output=True,text=True);(logs/(label+'-tests.log')).write_text(t.stdout+t.stderr);row.update(exit=t.returncode,output=t.stdout+t.stderr)
 print(row);return row,target
results=[]
core=[p for p in sources if p.name=='Contracts.cs'];module=[p for p in sources if 'Modules' in p.parts];integration=[p for p in sources if 'Integration' in p.parts and 'Assets' in p.parts];tests=[p for p in sources if p.name=='FireTests.cs']
row,dll=compile_run('Core',core,library=True);results.append(row)
row,weapon=compile_run('WeaponHandling',module,library=True,refs=[dll]);results.append(row)
row,adapter=compile_run('WeaponFireTransactions',integration,library=True,refs=[dll,weapon]);results.append(row)
row,_=compile_run('FireTests',tests,'FireTests',refs=[dll,weapon,adapter]);results.append(row)
all_sources=core+module+integration+tests
mutations=[('shallow-seen','seen=new HashSet<string>(original.seen,StringComparer.Ordinal);','seen=original.seen;'),('reserve-ABA','reserve.Revision==ammoVersion','true'),('binding-revalidation','r=binding.Validate(result.Command,result.InstanceId,result.EquipGeneration,result.LifeGeneration,\n                    result.ConnectionGeneration,result.FrameSequence);','r=RuleResult.Accept();'),('expiry','authoritativeTick>expires','false')]
original=next(p for p in module if p.name=='PreparedFire.cs');text=original.read_text()
for label,before,after in mutations:
 assert before in text
 p=out/(label+'.cs');p.write_text(text.replace(before,after));row,_=compile_run('mutant-'+label,[p if q==original else q for q in all_sources],'FireTests');row['killed']=row.get('exit',0)!=0 and row['compile']==0;results.append(row)
(logs/'results.json').write_text(json.dumps(results,indent=2));(logs/'consumed-sources.json').write_text(json.dumps(manifest,indent=2))
sys.exit(0 if all(x['compile']==0 and (x.get('killed') if x['label'].startswith('mutant-') else x.get('exit',0)==0) for x in results) else 1)
