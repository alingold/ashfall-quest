from pathlib import Path
import hashlib,json,subprocess,sys
root=Path(r'C:\Users\aling\Documents\ashfall');base=root/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5';evbase=root/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR1/r5'
# Reproduce sealed inputs only. No live dependency refresh or source mutation.
manifest=json.loads((base/'sealed-sources.json').read_text());sources=[]
for r in manifest:
 p=base/r['snapshot'];assert hashlib.sha256(p.read_bytes()).hexdigest()==r['sha256'],str(p);sources.append(p)
n=1
while (evbase/('replay-%02d'%n)).exists():n+=1
ev=evbase/('replay-%02d'%n);ev.mkdir();exe=ev/'RuntimeLoop.exe'
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(exe)]+list(map(str,sources))
(ev/'compile-command.txt').write_text(subprocess.list2cmdline(cmd));p=subprocess.run(cmd,capture_output=True,text=True);(ev/'compile.log').write_text(p.stdout+p.stderr)
if p.returncode:print(p.stdout+p.stderr);sys.exit(p.returncode)
# Small immutable pilot JSON, never a copied map pack/cache.
reference=base/'reference-map.json';assert hashlib.sha256(reference.read_bytes()).hexdigest()==json.loads((base/'pilot-manifest.json').read_text())['sha256'];(ev/'reference-map.json').write_bytes(reference.read_bytes())
results=[]
for label,args in [('scenario',[]),('repeat',[]),('strict',['--strict'])]:
 command=[str(exe)]+args;p=subprocess.run(command,capture_output=True,text=True);(ev/(label+'.log')).write_text(p.stdout+p.stderr);results.append({'label':label,'command':subprocess.list2cmdline(command),'exit':p.returncode,'expected':2 if args else 0});print(label,p.returncode,p.stdout.splitlines()[-1] if p.stdout else p.stderr)
drift=[]
for r in json.loads((base/'dependencies.json').read_text()):
 p=root/r['path'];actual=hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
 if actual!=r['sha256']:drift.append(dict(r,currentSha256=actual,classification='current dependency differs; no current integration claim'))
(ev/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
same=(ev/'scenario.log').read_bytes()==(ev/'repeat.log').read_bytes();result={'runs':results,'repeatByteEqual':same,'dependencyDriftCount':len(drift),'complete':False,'unknownGroups':8,'selfApproval':False}
(ev/'validation.json').write_text(json.dumps(result,indent=2));print(json.dumps(result));sys.exit(0 if same and all(r['exit']==r['expected'] for r in results) else 1)
