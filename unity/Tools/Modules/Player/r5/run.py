from pathlib import Path
import subprocess,json,shutil,hashlib
r=Path(r'C:\Users\aling\Documents\ashfall');o=r/'campaign-staging/20261104-three-chats/unity/PlayerIntegrationR5/r5';d=r/'docs/agent-runs/20261104-three-chats/unity/PlayerIntegrationR5/r5';prior=r/'campaign-staging/20261104-three-chats/unity/reviews/Player-r4'
suites={p.stem:p for p in (prior/'tests').glob('*.cs')};suites['FreshR4']=prior/'FreshR4.cs';suites['IntegrationR5']=r/'unity/Tools/Modules/Player/r5/IntegrationR5Tests.cs'
commands=[];results=[]
for lane in ['frozen-r4','candidate']:
 src=list((o/'before/unity/Assets/Ashfall/Modules/Player' if lane=='frozen-r4' else r/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))+[o/'before/unity/Assets/Ashfall/Core/Contracts.cs']
 for name,test in suites.items():
  if lane=='frozen-r4' and name=='IntegrationR5':continue
  folder=o/lane;folder.mkdir(exist_ok=True);dest=folder/test.name;shutil.copyfile(test,dest);exe=folder/(name+'.exe');rsp=folder/(name+'.rsp')
  rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in src+[dest]]))
  argv=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];commands.append(argv);c=subprocess.run(argv,capture_output=True,text=True);(folder/(name+'-compile.log')).write_text(c.stdout+c.stderr)
  code=None;output=c.stdout+c.stderr
  if c.returncode==0:
   commands.append([str(exe)]);t=subprocess.run([str(exe)],capture_output=True,text=True);code=t.returncode;output=t.stdout+t.stderr;(folder/(name+'-tests.log')).write_text(output)
  results.append(dict(lane=lane,name=name,compile=c.returncode,test=code,output=output));print(lane,name,c.returncode,code,output[-180:])
(d/'results.json').write_text(json.dumps(results,indent=2));(d/'commands.json').write_text(json.dumps(commands,indent=2))
raise SystemExit(0 if all(x['compile']==0 and x['test']==0 for x in results) else 1)
