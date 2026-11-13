from pathlib import Path
import json,hashlib,subprocess
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR2/historical';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR2/historical';S.mkdir();E.mkdir()
review=R/'campaign-staging/20261104-three-chats/unity/reviews/RuntimeLoop-r1';sources=[]
for row in json.loads((R/'docs/agent-runs/20261104-three-chats/unity/reviews/RuntimeLoop-r1/frozen-source-inventory.json').read_text()):
 p=R/row['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'];sources.append(p)
(S/'reference-map.json').write_bytes((R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5/reference-map.json').read_bytes())
results={}
for label,inputs in [('frozen137',sources),('independent61',[p for p in sources if p.name!='Scenario.cs']+[review/'Adversarial.cs'])]:
 exe=S/(label+'.exe');cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(exe)]+list(map(str,inputs));(E/(label+'-command.txt')).write_text(subprocess.list2cmdline(cmd));p=subprocess.run(cmd,capture_output=True);(E/(label+'-compile.log')).write_bytes(p.stdout+p.stderr);assert p.returncode==0,p.stdout.decode()
 if label=='independent61':
  # Tiny pre-existing review fixture only; preserve manifest pointing at that exact WAV.
  (S/'review-audio.tsv').write_bytes((review/'review-audio.tsv').read_bytes())
 for suffix,args in ([('scenario',[]),('repeat',[]),('strict',['--strict'])] if label=='frozen137' else [('observations',[])]):
  p=subprocess.run([str(exe)]+args,capture_output=True);(E/(label+'-'+suffix+'.log')).write_bytes(p.stdout+p.stderr);results[label+'-'+suffix]=dict(exit=p.returncode,last=p.stdout.decode().splitlines()[-1]);print(label,suffix,p.returncode,results[label+'-'+suffix]['last'])
(E/'results.json').write_text(json.dumps(results,indent=2))
