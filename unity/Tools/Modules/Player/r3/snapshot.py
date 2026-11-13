from pathlib import Path
import hashlib,json,subprocess,shutil
r=Path('C:/Users/aling/Documents/ashfall');o=r/'campaign-staging/20261104-three-chats/unity/Player/r3';d=r/'docs/agent-runs/20261104-three-chats/unity/Player/r3'
records=[]
def take(p,relative=None):
 b=p.read_bytes(); dest=o/'inputs'/(relative or p.name);dest.parent.mkdir(parents=True,exist_ok=True)
 if dest.resolve()!=p.resolve():dest.write_bytes(b)
 records.append({'source':str(p),'snapshot':str(dest),'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest().upper()})
for f in ['unity/Assets/Ashfall/Core/Contracts.cs','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/COLLISION-LAYERS-v1.md','docs/agent-runs/20261104-three-chats/unity/ownership.json','docs/agent-runs/20261104-three-chats/unity/phase03-ownership.json','docs/agent-runs/20261104-three-chats/unity/reviews/Player-r2/REPORT.md','unity/Tools/Modules/Player/PlayerTests.cs','unity/Tools/Modules/Player/R2Tests.cs','campaign-staging/20261104-three-chats/unity/Player/r2/Adversarial.cs']:
 take(r/f, 'Player-r2-REPORT.md' if f.endswith('REPORT.md') else None)
for prefix,startEnd in [('quest-r22/source',[(400,420),(778,790),(1087,1125),(1134,1175)]),('game',[(396,416),(728,740),(1037,1074),(1083,1125)])]:
 p=r/prefix/'scripts/player.gd';b=p.read_bytes();lines=b.decode('utf-8-sig').splitlines();dest=o/'inputs'/('quest-player-excerpts.txt' if prefix.startswith('quest') else 'skin-player-excerpts.txt')
 dest.write_text('\n\n'.join('\n'.join(str(i)+': '+lines[i-1] for i in range(a,z+1)) for a,z in startEnd),encoding='utf-8')
 records.append({'source':str(p),'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest().upper(),'excerpts':str(dest),'ranges':startEnd})
 for name in ['measurement_calibration.gd']:
  q=r/prefix/'scripts'/name
  if q.exists():take(q,('quest-' if prefix.startswith('quest') else 'skin-')+name)
(d/'consumed-inputs.json').write_text(json.dumps(records,indent=2))
# Demonstrate preserved r2 failure using unchanged reviewer test, no old wrapper.
rsp=o/'BeforeFresh.rsp';exe=o/'BeforeFresh.exe';sources=[o/'inputs/Contracts.cs']+sorted((o/'before').glob('*.cs'))+[o/'inputs/FreshTests.cs']
rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(f)+'"' for f in sources))
c=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','@'+str(rsp)],capture_output=True,text=True);(o/'BeforeFresh-compile.txt').write_text(c.stdout+c.stderr)
x=subprocess.run([str(exe)],capture_output=True,text=True) if c.returncode==0 else None
if x:(o/'BeforeFresh-results.txt').write_text(x.stdout+x.stderr)
(d/'before-results.json').write_text(json.dumps({'compileExit':c.returncode,'testExit':x.returncode if x else None,'expectedFailure':True},indent=2))
print('Before compile',c.returncode,'test',x.returncode if x else None)
