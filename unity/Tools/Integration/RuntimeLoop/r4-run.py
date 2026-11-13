from pathlib import Path
import json,hashlib,subprocess,shutil,collections,wave,struct
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopBindingsR4';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopBindingsR4';O=S/'final06';O.mkdir(exist_ok=False);L=E/'final06';L.mkdir(exist_ok=False)
files=[]
# Consume only the concrete prior RuntimeLoop review closure plus its explicit publication dependency.
old=list((R/'campaign-staging/20261104-three-chats/unity/reviews/RuntimeLoopReviewR3/frozen/unity/Assets/Ashfall').rglob('*.cs'))
parents={p.relative_to(R/'campaign-staging/20261104-three-chats/unity/reviews/RuntimeLoopReviewR3/frozen').parent.as_posix() for p in old}
parents.update(['unity/Assets/Ashfall/Integration/CombatPublication','unity/Assets/Ashfall/Modules/Persistence','unity/Assets/Ashfall/Integration/InventoryRecovery'])
for p in (S/'pinned').rglob('*.cs'):
 rel=p.relative_to(S/'pinned')
 if rel.parent.as_posix() in parents and '/RuntimeLoop/' not in p.as_posix():files.append(p)
for p in (R/'unity/Assets/Ashfall/Integration/RuntimeLoop').rglob('*.cs'):
 d=O/'candidate'/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,d);files.append(d)
commands=[];results={}
def compile(name,fs,refs=(),main=None,flags=()):
 out=O/(name+('.exe' if main else '.dll'));args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if main else 'library'),'/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(out)]+['/reference:'+str(x) for x in refs]+(['/main:'+main] if main else [])+list(flags)+[str(p) for p in fs]
 rsp=L/(name+'.rsp');rsp.write_text('\n'.join(subprocess.list2cmdline([a]) for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];commands.append(cmd);p=subprocess.run(cmd,capture_output=True);(L/(name+'-compile.log')).write_bytes(p.stdout+p.stderr);results[name]=p.returncode;print(name,p.returncode,p.stdout.decode()[:2200]);return out,p.returncode
for name in ['Scenario.cs','R2Gates.cs','R3Gates.cs','R4Gates.cs','NativeSchedulerFixture.cs']:shutil.copyfile(R/'unity/Tools/Integration/RuntimeLoop'/name,O/name)
shutil.copyfile(R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5/reference-map.json',O/'reference-map.json')
with wave.open(str(O/'r2-tone.wav'),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(8000);w.writeframes(struct.pack('<8h',0,100,0,-100,0,100,0,-100))
h=hashlib.sha256((O/'r2-tone.wav').read_bytes()).hexdigest();(O/'r2-audio.tsv').write_text('ASHFALL-AUDIO/1\ngeneric/hit\t0\t'+str(O/'r2-tone.wav')+'\t'+h+'\tR4-fixture\taccepted:synthetic-test-only\tgenerated\tSFX\t0\n')
for name,extras,main in [('Scenario',['Scenario.cs'],'Scenario'),('R2',['Scenario.cs','R2Gates.cs'],'R2Tests'),('R3',['Scenario.cs','R2Gates.cs','R3Gates.cs'],'R3Tests'),('R4',['Scenario.cs','R2Gates.cs','R4Gates.cs'],'R4Tests')]:
 exe,rc=compile(name,files+[O/x for x in extras],main=main)
 if not rc:
  p=subprocess.run([str(exe)],cwd=O,capture_output=True);results[name+'Run']=p.returncode;(L/(name+'.log')).write_bytes(p.stdout+p.stderr);print(p.stdout.decode()[-1800:])
# Finer actual DLL graph; preserve known Combat/Health private checkpoint boundary only.
groups=collections.defaultdict(list)
for p in files:
 if '/InventoryRecovery/' in p.as_posix() or (p.name=='CheckpointState.cs' and any(k in p.as_posix() for k in ['/InventoryItems/','/WeaponHandling/'])):continue
 rel=p.as_posix().split('/Assets/Ashfall/')[1].split('/');key='Core' if rel[0]=='Core' else rel[1] if len(rel)>2 else 'CombatHealthBridge'
 if key in ['Combat','HealthStates']:key='CombatHealth'
 if key=='CombatPublication':key='CombatHealthBridge'
 groups[key].append(p)
resolved={};pending=dict(groups);attempt=0
while pending:
 progress=False
 for key,fs in list(pending.items()):
  attempt+=1;out,rc=compile('Edge'+str(attempt)+'-'+key,fs,list(resolved.values()))
  if not rc:resolved[key]=out;del pending[key];progress=True
 if not progress:break
(L/'separate-dll-graph.json').write_text(json.dumps({'resolved':{k:str(v) for k,v in resolved.items()},'unresolved':list(pending)},indent=2))
if 'RuntimeLoop' in resolved:
 u=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine');native=[p for p in files if '/RuntimeLoop/Engine/' in p.as_posix()]+[O/'NativeSchedulerFixture.cs']
 compile('Native',native,list(resolved.values())+[u/'UnityEngine.CoreModule.dll',u/'UnityEngine.PhysicsModule.dll',u.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll'],flags=['/define:UNITY_6000_0_OR_NEWER'])
(L/'results.json').write_text(json.dumps(results,indent=2));(L/'commands.json').write_text(json.dumps(commands,indent=2));(L/'sources.json').write_text(json.dumps([{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files],indent=2))
