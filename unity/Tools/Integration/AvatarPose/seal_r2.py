import pathlib,json,hashlib,re,shutil
R=pathlib.Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/AvatarLifecycleBindingR2';S=R/'campaign-staging/20261104-three-chats/unity/AvatarLifecycleBindingR2'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
owned=list((R/'unity/Assets/Ashfall/Integration/AvatarPose').rglob('*.cs'))+[R/'unity/Assets/Ashfall/Modules/Avatar/Engine/AvatarView.cs',R/'unity/Tools/Modules/Avatar/AvatarLifecycleR8NativeTests.cs']+list((R/'unity/Tools/Integration/AvatarPose').glob('*.*'))
rows=[];symbols=[]
for p in owned:
 q=S/'final-source'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);rows.append({'path':str(p.relative_to(R)),'sha256':sha(p),'bytes':p.stat().st_size})
 if p.suffix=='.cs':
  symbols += [{'path':str(p.relative_to(R)),'line':i,'symbol':line.strip()} for i,line in enumerate(p.read_text().splitlines(),1) if re.search(r'public .*\(|public (sealed class|interface|event)',line)]
(D/'changed-files-sha256.json').write_text(json.dumps(rows,indent=2));(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
owned_names={str(p.relative_to(R)) for p in owned};drift=[]
for row in json.loads((D/'before.json').read_text()):
 if row['path'] in owned_names:continue
 p=R/row['path'];current=sha(p);drift.append(dict(row,current=current,match=current==row['sha256']))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
ports=[]
for name,original in [('Session.cs','unity/Assets/Ashfall/Modules/Sessions/Session.cs'),('UnityPlayerDriver.cs','unity/Assets/Ashfall/Modules/Player/Engine/UnityPlayerDriver.cs')]:
 p=R/'campaign-staging/20261104-three-chats/unity/phase15/lifecycle-ports'/name
 ports.append({'rootSnapshot':str(p.relative_to(R)),'sha256':sha(p),'consumedSource':original,'pinnedSha256':sha(S/'before'/original),'match':sha(p)==sha(S/'before'/original)})
(D/'lifecycle-port-bindings.json').write_text(json.dumps(ports,indent=2))
commands={'python':r'C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe','steps':['unity/Tools/Integration/AvatarPose/validate_r2.py','unity/Tools/Integration/AvatarPose/mutations_r2.py','unity/Tools/Integration/AvatarPose/seal_r2.py'],'compilerCommands':json.loads((D/'results.json').read_text()),'scope':'CPU executables and installed-reference compilation only; native fixtures UNRUN'}
(D/'commands.json').write_text(json.dumps(commands,indent=2))
manifest=[{'path':str(p.relative_to(R)),'sha256':sha(p)} for p in sorted(D.rglob('*')) if p.is_file() and p.name not in ['evidence-sha256.json','evidence-sha256.json.sha256']]
(D/'evidence-sha256.json').write_text(json.dumps(manifest,indent=2));(D/'evidence-sha256.json.sha256').write_text(sha(D/'evidence-sha256.json')+'\n')
print('source files',len(rows),'external drift',[x['path'] for x in drift if not x['match']]);print('lifecycle ports',ports);print('manifest',sha(D/'changed-files-sha256.json'))
