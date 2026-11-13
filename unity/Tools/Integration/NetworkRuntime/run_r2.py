import pathlib,json,hashlib,subprocess,difflib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/NetworkRuntimeR2';D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2'
sha=lambda b:hashlib.sha256(b).hexdigest().upper()
i=1
while (S/('run%02d'%i)).exists():i+=1
O=S/('run%02d'%i);O.mkdir();L=D/O.name;L.mkdir();inputs=[];commands=[]
baseline=json.loads((D/'baseline.json').read_text());deps=[]
for x in baseline:
 name=x['original'].replace('\\','/')
 if x['kind']!='phase12':continue
 if not (name.endswith('Core/Contracts.cs') or '/Modules/Networking/' in name or '/Integration/Sessions/' in name or '/Integration/SessionReplication/' in name or name.endswith('Modules/Sessions/Session.cs') or name.endswith('Modules/Sessions/Ports.cs')):continue
 b=(R/x['snapshot']).read_bytes();assert sha(b)==x['sha256']
 q=O/(str(len(inputs))+'-'+pathlib.Path(name).name);q.write_bytes(b);deps.append(q);inputs.append(dict(original=x['original'],snapshot=str(q),sha256=sha(b),kind='phase12'))
 if name.endswith('Integration/Sessions/SessionNetworkCoordinator.cs'):coordinator=q
fragment=(R/'unity/Tools/Integration/NetworkRuntime/RetirementApi.cs.fragment').read_text()
old=coordinator.read_text();needle='        public RuleResult RecordPosition('
assert old.count(needle)==1
new=old.replace(needle,fragment+'\n'+needle)
rehello='bool rehello=prior!=null && !prior.Active && session.Get(actor)!=null;'
assert new.count(rehello)==1
# Real PrepareAdmission above has already rejected any active actor/canonical
# lease. Trusted principal + surviving membership permits epoch rehello even
# after obsolete binding retirement, without inventing a new join/token.
new=new.replace(rehello,'bool rehello=session.Get(actor)!=null;')
patched=O/'CoordinatorWithRootPatch.cs';patched.write_text(new);deps.remove(coordinator);deps.append(patched)
(L/'root-api.patch').write_text(''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='a/unity/Assets/Ashfall/Integration/Sessions/SessionNetworkCoordinator.cs',tofile='b/unity/Assets/Ashfall/Integration/Sessions/SessionNetworkCoordinator.cs')))
owned=[]
for folder in ['unity/Assets/Ashfall/Integration/NetworkRuntime','unity/Tools/Integration/NetworkRuntime']:
 for p in sorted((R/folder).rglob('*.cs')):
  b=p.read_bytes();q=O/(str(len(inputs))+'-'+p.name);q.write_bytes(b);owned.append(q);inputs.append(dict(original=str(p.relative_to(R)),snapshot=str(q),sha256=sha(b),kind='candidate'))
def suite(name,main,files,define=''):
 exe=O/(name+'.exe');cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/main:'+main,'/out:'+str(exe)]
 if define:cmd.append('/define:'+define)
 cmd+=list(map(str,files))
 for phase,c in [('compile',cmd),('run',[str(exe)])]:
  p=subprocess.run(c,capture_output=True,text=True,timeout=60);(L/(name+'-'+phase+'.txt')).write_text(p.stdout+p.stderr);commands.append(dict(command=subprocess.list2cmdline(c),exit=p.returncode));print(name,phase,p.returncode,p.stdout+p.stderr)
  if p.returncode:return False
 return True
ok=suite('R2','R2Tests',deps+owned,'ASHFALL_BINDING_RETIREMENT_API')
unpatched=[p for p in deps if p!=patched]+[coordinator]
ok=suite('Compatibility','ChannelTests',unpatched+owned) and ok
(L/'inputs.json').write_text(json.dumps(inputs,indent=2));(L/'commands.json').write_text(json.dumps(commands,indent=2))
(L/'patch-identity.json').write_text(json.dumps(dict(before=sha(coordinator.read_bytes()),patched=sha(patched.read_bytes()),fragment=sha(fragment.encode())),indent=2))
raise SystemExit(0 if ok else 1)
