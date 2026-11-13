from pathlib import Path
import hashlib,json,shutil,subprocess,sys
R=Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/AvatarLifecycleR6'
E=R/'docs/agent-runs/20261104-three-chats/unity/AvatarLifecycleR6'
label=sys.argv[1] if len(sys.argv)>1 else 'run02'
if not label.isalnum():raise SystemExit('Use a unique alphanumeric run label')
S=S/label; E=E/label;S.mkdir(exist_ok=False);E.mkdir(exist_ok=False)
F=S/'candidate'; F.mkdir(exist_ok=False)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
inputs=[]
for p in [R/'unity/Assets/Ashfall/Core/Contracts.cs']+list((R/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'))+list((R/'unity/Tools/Modules/Avatar').glob('*.cs')):
 q=F/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
 inputs.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=sha(q)))
(E/'candidate-inputs.json').write_text(json.dumps(inputs,indent=2))
W=S/'work';W.mkdir(exist_ok=False)
prior=R/'campaign-staging/20261104-three-chats/unity/reviews/Avatar-r5'
for p in (prior/'work').iterdir():
 if p.is_file() and p.suffix not in ['.exe','.py']:shutil.copy2(p,W/p.name)
for p in ['Adversarial.cs'] :shutil.copy2(prior/p,W/p)
shutil.copytree(prior/'work/consumed',W/'consumed')
shutil.copy2(R/'campaign-staging/20261104-three-chats/unity/phase09/Avatar-r6/Acceptance.cs',W/'Acceptance.cs')
sources=[F/'unity/Assets/Ashfall/Core/Contracts.cs']+sorted((F/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'))
tests=['AvatarTests','AvatarR2Tests','AvatarR3Tests','IndependentTests','PriorR2Independent','NullPoseAcceptance','AvatarR4Tests','Adversarial','Acceptance','AvatarLifecycleR6Tests']
results=[]
for name in tests:
 t=W/(name+'.cs')
 if not t.exists():t=F/'unity/Tools/Modules/Avatar'/(name+'.cs')
 extra=[F/'unity/Tools/Modules/Avatar/AvatarR4Fixtures.cs'] if name in ['AvatarR4Tests','Adversarial','Acceptance','AvatarLifecycleR6Tests'] else []
 exe=S/(name+'.exe');args=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+list(map(str,sources+[t]+extra))
 c=subprocess.run(args,capture_output=True,text=True)
 log='COMMAND '+' '.join('"'+a+'"' for a in args)+'\n'+c.stdout+c.stderr+'\nCOMPILE EXIT '+str(c.returncode)+'\n'
 if c.returncode:(E/(name+'.log')).write_text(log);raise SystemExit(log)
 r=subprocess.run([str(exe)],cwd=W,capture_output=True,text=True)
 log+='COMMAND "'+str(exe)+'"\n'+r.stdout+r.stderr+'\nRUN EXIT '+str(r.returncode)+'\n'
 (E/(name+'.log')).write_text(log);results.append(dict(test=name,exit=r.returncode,tail=r.stdout.splitlines()[-2:]));print(name,r.returncode,r.stdout.splitlines()[-2:])
 if r.returncode:raise SystemExit(log)
(E/'results.json').write_text(json.dumps(results,indent=2))
(E/'dependency-drift.json').write_text(json.dumps([dict(x,current=sha(R/x['path']),match=sha(R/x['path'])==x['sha256']) for x in inputs],indent=2))
