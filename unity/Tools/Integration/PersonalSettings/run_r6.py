from pathlib import Path
import json,hashlib,subprocess
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/PresentationR6';E=R/'docs/agent-runs/20261104-three-chats/unity/PresentationR6';B=R/'campaign-staging/20261104-three-chats/unity/PresentationR4/r5'
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
results=[]
def run(name,lines,main=None):
 lines=[l for l in lines if not l.startswith('/out:') and not (main and l.startswith('/main:'))]
 lib='/target:library' in lines;out=S/(name+('.dll' if lib else '.exe'))
 lines=['/out:"'+str(out)+'"']+(['/main:'+main] if main else [])+lines
 rsp=S/(name+'.rsp');rsp.write_text('\n'.join(lines));cmd=[csc,'@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);row=dict(name=name,command=cmd,compileExit=p.returncode,diagnostics=p.stdout+p.stderr)
 if p.returncode==0 and not lib:
  data=S/('data-'+name+'-'+__import__('datetime').datetime.now().strftime('%H%M%S%f'));cmd=[str(out),str(data)];q=subprocess.run(cmd,capture_output=True,text=True);row.update(runCommand=cmd,runExit=q.returncode,output=q.stdout+q.stderr)
 results.append(row);print(name,row['compileExit'],row.get('runExit','library'),row.get('output',row['diagnostics']))
for name in ['Ashfall.Core','Ashfall.Persistence','FrozenR3','R5','ReviewRegressions','Builder','R2','PriorIndependent','R3SourceAudioCorrected','GuardedDefineOff']:
 lines=(B/(name+'.rsp')).read_text().splitlines()
 # Historical15 uses exact original sources. Candidate suites use new presentation,
 # current pinned Player and real Audio (new concrete bridge) without frozen-player substitution.
 if name not in ['Ashfall.Core','Ashfall.Persistence','FrozenR3']:
  new=[];personal=any('PersonalSettings' in l for l in lines)
  for l in lines:
   if l.startswith('"'):
    p=Path(l[1:-1]);text=str(p)
    if '\\Modules\\Player\\' in text:continue
    if 'candidate\\unity\\Assets' in text:
     p=R/'unity/Assets'/text.split('candidate\\unity\\Assets\\')[1]
    l='"'+str(p)+'"'
   new.append(l)
  lines=new
  if name=="R3SourceAudioCorrected":lines=[l.replace(str(B/"R3SourceAudioCorrectedTests.cs"),str(S/"R3AtomicContextTests.cs")) for l in lines]
  if personal:
   have={l for l in lines}
   for p in (S/'dependencies/unity/Assets/Ashfall/Modules/Player').glob('*.cs'):lines.append('"'+str(p)+'"')
   for p in (S/'dependencies/unity/Assets/Ashfall/Modules/Audio').glob('*.cs'):lines.append('"'+str(p)+'"')
   for p in (R/'unity/Assets/Ashfall/Integration/PersonalSettings').glob('*.cs'):
    if '"'+str(p)+'"' not in have:lines.append('"'+str(p)+'"')
 lines=[l.replace('/reference:"'+str(B),'/reference:"'+str(S)) for l in lines]
 run(name,lines)
base=['/nologo','/langversion:5','/warnaserror+','/target:exe']
prod=list((R/'unity/Assets/Ashfall/Modules/Presentation').rglob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/PersonalSettings').rglob('*.cs'))
deps=list((S/'dependencies').rglob('*.cs'))
base+=['"'+str(p)+'"' for p in prod+deps]
base+=['"'+str(R/'unity/Tools/Integration/PersonalSettings/PresentationR5Tests.cs')+'"']
if (R/'unity/Tools/Integration/PersonalSettings/PresentationR6Tests.cs').exists():run('R6',base+['"'+str(R/'unity/Tools/Integration/PersonalSettings/PresentationR6Tests.cs')+'"'],'R6Tests')
run('FreshMigrated',base+['\"'+str(S/'FreshMigrated.cs')+'\"'],'Fresh')
current=[l for l in base if not ('dependencies' in l and '\\Player\\' in l)]
current+=['"'+str(p)+'"' for p in (S/'player-r6-current').rglob('*.cs') if 'Engine' not in p.parts]
run('CurrentPlayerR6',current+['"'+str(R/'unity/Tools/Integration/PersonalSettings/CurrentPlayerR6Test.cs')+'"'],'CurrentPlayerR6Test')
(E/'results.json').write_text(json.dumps(results,indent=2))

