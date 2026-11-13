from pathlib import Path
import json,subprocess,hashlib,shutil
R=Path(r'C:\Users\aling\Documents\ashfall'); O=R/'campaign-staging/20261104-three-chats/unity/ConsumableTransactionsR1'; E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableTransactionsR1'
def snapshot(p):
 rel=p.relative_to(R); parts=rel.parts; rel=Path('root-frozen')/Path(*parts[parts.index('inputs')+1:]) if 'inputs' in parts and 'phase05' in parts else rel; q=O/'inputs'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q);return q
sources=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for m in ['HealthStates','InventoryItems','Pickups','Player']:sources+=list((R/'unity/Assets/Ashfall/Modules'/m).glob('*.cs'))
sources+=list((R/'unity/Assets/Ashfall/Integration/ConsumableTransactions').glob('*.cs'))
prod=[snapshot(p) for p in sources]
records=[]
def run(label,src):
 out=O/(label+'.exe');rsp=O/(label+'.rsp');args=['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(out)]+list(map(str,src));rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);(E/(label+'-compile.log')).write_text(p.stdout+p.stderr);record={'label':label,'command':cmd,'compileExit':p.returncode};print(label,'compile',p.returncode,p.stdout[-1500:]);
 if p.returncode==0:
  t=subprocess.run([str(out)],capture_output=True,text=True);(E/(label+'-tests.log')).write_text(t.stdout+t.stderr);record.update(testExit=t.returncode,output=t.stdout+t.stderr);print(t.returncode,t.stdout[-800:],t.stderr[-800:])
 records.append(record)
run('Health49',prod+[snapshot(R/'unity/Tools/Modules/HealthStates/HealthTests.cs')])
run('Consumption',prod+[snapshot(R/'unity/Tools/Integration/ConsumableTransactions/Tests.cs')])
for suite in ['InventoryTests','TransactionTests']:
 run(suite,prod+[snapshot(R/('unity/Tools/Modules/InventoryItems/'+suite+'.cs'))])
for suite in ['Tests','BatchTests','r4/TestsR4']:
 run('Pickups-'+suite.replace('/','-'),prod+[snapshot(R/('unity/Tools/Modules/Pickups/'+suite+'.cs'))])
# Original root tests, frozen source closure, then only replace Health inputs with candidate.
root=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase05/small-fix-results.json').read_text(encoding='utf-8-sig'))
for entry in root:
 if entry.get('group')=='CombatHealth':
  originals=[Path(x) for x in entry['compiler'] if x.endswith('.cs')]
  pinned=[snapshot(p) for p in originals]
  run('RootFrozen-'+entry['label'],pinned)
  replaced=[p for p in pinned if 'Assets\\Ashfall\\Modules\\HealthStates\\' not in str(p)]+[p for p in prod if 'Modules\\HealthStates\\' in str(p)]
  run('RootCandidate-'+entry['label'],replaced)
(E/'results.json').write_text(json.dumps(records,indent=2))
inputs=[{'original':str(p.relative_to(O/'inputs')),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in (O/'inputs').rglob('*') if p.is_file()];(E/'inputs.json').write_text(json.dumps(inputs,indent=2))



raise SystemExit(1 if any(x["compileExit"] or x.get("testExit",1) for x in records) else 0)
