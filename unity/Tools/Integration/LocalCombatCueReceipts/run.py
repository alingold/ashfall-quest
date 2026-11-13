from pathlib import Path
import json,subprocess,hashlib
R=Path(__file__).resolve().parents[4]
O=R/'campaign-staging/20261104-three-chats/unity/LocalCombatCueReceiptsR2'
M=json.loads((O/'inputs.json').read_text()); mapping={x['path']:x['frozen'] for x in M}
CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
S=R/'campaign-staging/20261104-three-chats/unity/reviews/DesktopHardeningReviewR3'
out=O/'run06';out.mkdir();results=[]
sources={}
for name in ['LocalRifleSandbox','LocalCombatFeedback','CommittedDesktopAudio']:
 p=next((R/'unity/Assets/Ashfall/Integration/DesktopPlaytest').rglob(name+'.cs'));q=out/p.name;q.write_bytes(p.read_bytes());sources[name]=q
for p in Path(__file__).parent.glob('*'):
 if p.is_file():(out/p.name).write_bytes(p.read_bytes())
def mapped(p):return mapping.get(str(p),str(p))
def rsp_lines(name):
 text=Path(mapped(S/(name+'.rsp'))).read_text(encoding='utf-8-sig');lines=[]
 for line in text.splitlines():
  if line.startswith('/out:'):line='/out:"'+str(out/(name+('.dll' if name in ['DesktopCombat','ExactPlayer','NativeHolds'] else '.exe')))+'"'
  elif line.startswith('/reference:'):
   p=Path(line[len('/reference:'):].strip('"'))
   if p.parent==S:
    q=out/p.name
    if p.name!='DesktopCombat.dll' and not q.exists():q.write_bytes(Path(mapped(p)).read_bytes())
    line='/reference:"'+str(q)+'"'
   else:line='/reference:"'+mapped(p)+'"'
  elif line.startswith('"'):
   p=Path(line.strip('"'));line='"'+str(next((v for k,v in sources.items() if p.name.endswith(k+'.cs')),Path(mapped(p))))+'"'
  lines.append(line)
 return lines
def run(label,cmd,execute=True):
 p=subprocess.run(cmd,cwd=out,capture_output=True,text=True,errors='replace');results.append(dict(label=label,command=cmd,exit=p.returncode,output=p.stdout+p.stderr));(out/'results.json').write_text(json.dumps(results,indent=2));print(label,p.returncode,(p.stdout+p.stderr)[-500:]);return p.returncode
def compile(name,lines):
 p=out/(name+'.rsp');p.write_text('\n'.join(lines));return run(name+' compile',[CSC,'/noconfig','@'+str(p)])
prod=rsp_lines('DesktopCombat')+['"'+str(sources['LocalCombatFeedback'])+'"']
assert compile('DesktopCombat',prod)==0
for name in ['behavior','adversarial','independent']:
 assert compile(name,rsp_lines(name))==0
 assert run(name,[str(out/(name+'.exe'))]+(['held-acceptance'] if name=='adversarial' else []))==0
check=[l for l in rsp_lines('independent') if not l.startswith('"') and not l.startswith('/out:')]+['/out:"'+str(out/'checks.exe')+'"','"'+str(out/'Checks.cs')+'"']
assert compile('checks',check)==0
assert run('checks',[str(out/'checks.exe')])==0
# Actual installed Unity APIs; native fixture libraries compile but are never executed.
native=rsp_lines('ExactPlayer')+['"'+str(sources['LocalCombatFeedback'])+'"','"'+str(sources['CommittedDesktopAudio'])+'"','"'+mapped(R/'unity/Assets/Ashfall/Integration/DesktopPlaytest/Audio/DesktopCue.cs')+'"']
assert compile('ExactPlayer',native)==0
fixture=[l for l in native if not l.startswith('"') and not l.startswith('/out:')]+['/out:"'+str(out/'NativeReceiptFixture.dll')+'"','/reference:"'+str(out/'ExactPlayer.dll')+'"','"'+str(out/'NativeReceiptFixture.cs')+'"']
assert compile('NativeReceiptFixture',fixture)==0
# Compiled mutations must fail behavior, never count compiler errors as kills.
base=sources['LocalCombatFeedback'].read_text();sandbox=sources['LocalRifleSandbox'].read_text()
mutants=[('replay','feedback','cursor=receipt.Sequence;','cursor=cursor+0;'),('mute','feedback','disposed||suppressed','disposed'),('late','feedback','cursor=producer.FeedbackSequence','cursor=0'),('gap','feedback','DiscardPending();receipt=null;return false;','cursor=owner.FeedbackSequence-1;receipt=null;return false;'),('loss','sandbox','(double)Before.Health-After.Health','(double)Before.Health-After.Health+1')]
for name,kind,a,b in mutants:
 p=sources['LocalCombatFeedback' if kind=='feedback' else 'LocalRifleSandbox'];original=base if kind=='feedback' else sandbox;assert a in original;p.write_text(original.replace(a,b))
 assert compile('mutant-'+name,prod)==0
 (out/('mutant-'+name+'.dll')).write_bytes((out/'DesktopCombat.dll').read_bytes())
 (out/('mutant-'+name+'.cs')).write_bytes(p.read_bytes())
 assert run('mutant-'+name,[str(out/'checks.exe')])!=0
 p.write_text(original)
assert compile('DesktopCombat',prod)==0
(out/'manifest.json').write_text(json.dumps([dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in out.iterdir() if p.is_file()],indent=2))


