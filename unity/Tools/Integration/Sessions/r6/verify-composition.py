from pathlib import Path
import json,hashlib,subprocess,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/SessionAdmissionR6';S=R/'campaign-staging/20261104-three-chats/unity/SessionAdmissionR6'
latest=json.loads((D/'latest.json').read_text());O=Path(latest['staging']);inputs=json.loads((Path(latest['evidence'])/'inputs.json').read_text());deps=json.loads((D/'dependencies.json').read_text())
source=[x['snapshot'] for x in inputs if Path(x['path']).is_relative_to(R/'unity/Assets') and 'before-' not in x['snapshot']]
source=list(dict.fromkeys(source));fixture=R/'unity/Tools/Modules/Networking/r6/SessionAdmissionR6NativeFixture.cs';q=S/'native-fixture-define-off.cs';shutil.copyfile(fixture,q)
args=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(S/'CurrentPinnedComposition.dll')]+source+[x['snapshot'] for x in deps]+[str(q)]
cmd=' '.join('/out:"'+a[5:]+'"' if a.startswith('/out:') else '"'+a+'"' if len(a)>2 and a[1]==':' else subprocess.list2cmdline([a]) for a in args)
r=subprocess.run(cmd,capture_output=True,text=True);(D/'current-composition-compile.txt').write_text(r.stdout+r.stderr);(D/'current-composition-command.json').write_text(json.dumps(dict(argv=args,command=cmd,exit=r.returncode,scope='compile only pinned Health + lifecycle partial + SessionReplication; no behavioral acceptance'),indent=2));print('current pinned composition compile',r.returncode,r.stdout+r.stderr)
checks=[]
for x in deps:
 actual=hashlib.sha256(Path(x['path']).read_bytes()).hexdigest().upper();checks.append(dict(x,current_sha256=actual,match=actual==x['sha256']))
(D/'external-dependency-drift.json').write_text(json.dumps(checks,indent=2));print('external drift',sum(not x['match'] for x in checks))
