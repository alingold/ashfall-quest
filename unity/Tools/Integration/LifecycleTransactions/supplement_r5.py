import pathlib,json,hashlib,subprocess,sys
R=pathlib.Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/LifecycleBindingsR5';D=R/'docs/agent-runs/20261104-three-chats/unity/LifecycleBindingsR5'
O=S/(sys.argv[1] if len(sys.argv)>1 else 'supplement03');O.mkdir(exist_ok=False);I=S/'inputs/unity/Assets/Ashfall';results=[]
def group(name):return [p for p in (I/name).rglob('*.cs') if 'Engine' not in p.parts]
def run(name,files,main,args=()):
 if len(sys.argv)>2 and name!=sys.argv[2]:return
 out=O/name;out.mkdir();frozen=[]
 for i,p in enumerate(files):
  q=out/('%03d-'%i+p.name);q.write_bytes(p.read_bytes());frozen.append(q)
 exe=out/'Test.exe';rsp=out/'compile.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/r:System.Runtime.Serialization.dll','/target:exe','/main:'+main,'/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in frozen]))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);(out/'compile.log').write_text(p.stdout+p.stderr);row=dict(name=name,command=cmd,compile=p.returncode,inputs=[dict(path=str(x.relative_to(R)),sha256=hashlib.sha256(x.read_bytes()).hexdigest()) for x in frozen])
 if p.returncode==0:
  cmd=[str(exe)]+list(args);p=subprocess.run(cmd,capture_output=True,text=True,cwd=out);(out/'run.log').write_text(p.stdout+p.stderr);row.update(argv=cmd,exit=p.returncode,output=p.stdout[-8000:]+p.stderr)
 results.append(row);print(name,row['compile'],row.get('exit'),row.get('output','')[-400:]);(D/(O.name+'-results.json')).write_text(json.dumps(results,indent=2))
base=group('Core')+group('Modules/HealthStates')+group('Modules/Sessions')
base=[R/'unity/Assets/Ashfall/Modules'/('HealthStates' if p.name=='PreparedLifecycle.cs' else 'Sessions')/p.name if p.name in ['PreparedLifecycle.cs','PreparedLifeProjection.cs'] else p for p in base]
prior=R/'campaign-staging/20261104-three-chats/unity/reviews/LifecycleClockReviewR4'
run('Replay',base+group('Modules/Combat')+[I/'Integration/CombatHealthBridge.cs']+group('Modules/Persistence')+group('Integration/CombatPublication')+[prior/'HealthReplay.cs'],'Independent')
run('Clock',base+[I/'Integration/MatchDirector/Ports.cs']+list((R/'unity/Assets/Ashfall/Integration/LifecycleTransactions').glob('*.cs'))+group('Modules/World')+[prior/'InstrumentedFixture.cs',prior/'ClockTests.cs'],'ClockTests')
recovery=group('Core')+group('Modules/InventoryItems')+group('Modules/WeaponHandling')+group('Integration/InventoryRecovery')+[R/'unity/Tools/Integration/InventoryRecovery/Tests.cs']
run('InventoryRecovery',recovery,'Tests',[str(O/'InventoryRecovery')])
run('SourceBindings',base+[I/'Integration/MatchDirector/Ports.cs',I/'Integration/WorldRuntime/RuntimeBinding.cs']+list((R/'unity/Assets/Ashfall/Integration/LifecycleTransactions').glob('*.cs'))+group('Modules/World')+[R/'unity/Tools/Integration/LifecycleTransactions/Scenario.cs',R/'unity/Tools/Integration/LifecycleTransactions/SourceBindingsTests.cs',R/'unity/Tools/Integration/WorldRuntime/WorldRuntimeFixture.cs'],'SourceBindingsTests')
raise SystemExit(0 if all(x['compile']==0 and x.get('exit')==0 for x in results) else 1)
