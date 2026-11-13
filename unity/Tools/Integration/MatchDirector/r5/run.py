import pathlib,json,hashlib,subprocess,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchDirectorR4/r5';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchDirectorR4/r5'
V=R/'campaign-staging/20261104-three-chats/unity/reviews/MatchDirector-r3'
commands=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def run(args):
 x=subprocess.run(list(map(str,args)),capture_output=True,text=True);commands.append(dict(command=subprocess.list2cmdline(list(map(str,args))),exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/'commands-results.json').write_text(json.dumps(commands,indent=2));print(x.stdout,x.stderr,flush=True);return x.returncode
def suite(label,sources,test):
 exe=S/(label+'.exe');rsp=S/(label+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warn:4\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources+[test]));c=run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]);return c if c else run([exe])
frozen=list((V/'frozen').rglob('*.cs'))+list((V/'source').glob('*.cs'))
old=R/'campaign-staging/20261104-three-chats/unity/MatchDirector/r3/sessions3'
verify=[]
for x in json.loads((old/'inputs.json').read_text(encoding='utf-8-sig')):
 p=old/'inputs'/x['path'];q=V/'frozen'/x['path'];verify.append(dict(path=x['path'],expected=x['sha256'],frozen=sha(p),reviewCopy=sha(q)));assert sha(p)==x['sha256']==sha(q)
(D/'r3-sealed-input-verification.json').write_text(json.dumps(verify,indent=2))
suite('r3-frozen-builder',frozen,V/'BuilderTests.cs');suite('r3-frozen-diagnostics',frozen,V/'IndependentTests.cs')
candidate=S/'candidate';candidate.mkdir(exist_ok=True)
for p in (R/'unity/Assets/Ashfall/Integration/MatchDirector').glob('*.cs'):shutil.copyfile(p,candidate/p.name)
deps=list((S/'before/unity/Assets/Ashfall/Core').glob('*.cs'))+list((S/'before/unity/Assets/Ashfall/Modules').rglob('*.cs'))
sources=deps+list(candidate.glob('*.cs'))
suite('r5-builder',sources,S/'BuilderTests.cs')
test=(S/'IndependentTests.cs').read_text()
test=test.replace('first.Reason=="journal_full"&&timer.Accepted&&retry.Reason=="uncommitted-score"&&load.Reason=="pending-score"','first.Accepted&&timer.Accepted&&retry.Accepted&&load.Accepted').replace('reproduce permanent pending event','reserved score lane recovers round despite exhausted limits')
test=test.replace('Score(r.D,"team:1")==0,"reproduce dropped committed killer credit"','Score(r.D,"team:1")==1,"retained canonical killer credited after disconnect"')
test=test.replace('four counterexamples reproduced (not acceptance PASS)','two fixed counterexamples; remap and downed remain legacy API diagnostics')
(S/'UpdatedDiagnostics.cs').write_text(test)
suite('r5-diagnostics',sources,S/'UpdatedDiagnostics.cs')
suite('r5-additional',sources,R/'unity/Tools/Integration/MatchDirector/r5/AdditionalTests.cs')
current=S/'current-dependencies';current.mkdir(exist_ok=True)
current_sources=[]
for p in deps:
 rel=p.relative_to(S/'before');live=R/rel;dest=current/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(live.read_bytes());current_sources.append(dest)
suite('r5-current-snapshot-builder',current_sources+list(candidate.glob('*.cs')),S/'BuilderTests.cs')
suite('r5-current-snapshot-additional',current_sources+list(candidate.glob('*.cs')),R/'unity/Tools/Integration/MatchDirector/r5/AdditionalTests.cs')
drift=[]
for x in json.loads((D/'before.json').read_text()):
 p=R/x['path'];drift.append(dict(path=x['path'],before=x['sha256'],after=sha(p),changed=sha(p)!=x['sha256']))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
print('Failed commands:',sum(x['exit']!=0 for x in commands))
