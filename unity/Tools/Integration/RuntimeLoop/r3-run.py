from pathlib import Path
import json,hashlib,subprocess,shutil,wave,struct,sys
R=Path.cwd(); run=sys.argv[1]; S=R/('campaign-staging/20261104-three-chats/unity/RuntimeLoopR3/'+run);E=R/('docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR3/'+run)
S.mkdir(parents=True,exist_ok=False);E.mkdir(parents=True,exist_ok=False)
base=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR3/run01/baseline'
index=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR3/run01/source-index.json').read_text()); old=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR2/final04/pin.json').read_text())
paths={x['path'].replace('/','\\') for x in old}
# Consume complete namespaces for the updated composed closure, including newly added partials.
parents={str(Path(p).parent) for p in paths}
rows=[]
for x in index:
 p=Path(x['original'])
 if str(p.parent) not in parents and str(p) not in paths:continue
 src=R/p if '/Integration/RuntimeLoop/' in p.as_posix() else base/p
 dst=S/'candidate'/p;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(src.read_bytes());rows.append(dict(path=p.as_posix(),sha256=hashlib.sha256(dst.read_bytes()).hexdigest().upper()))
(E/'candidate.json').write_text(json.dumps(rows,indent=2))
def compile(label,sources,refs=(),exe=False,main=None,extra=()):
 out=S/(label+('.exe' if exe else '.dll'));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library')]+([] if label=='NativeCompileOnly' else ['/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll'])+['/out:'+str(out)]+['/reference:'+str(x) for x in refs]+list(extra)+(['/main:'+main] if main else [])+list(map(str,sources))
 (E/(label+'-command.txt')).write_text(subprocess.list2cmdline(cmd));p=subprocess.run(cmd,capture_output=True);(E/(label+'-compile.log')).write_bytes(p.stdout+p.stderr);print(label,p.returncode,p.stdout.decode()[:5000]);return out,p.returncode
files=[S/'candidate'/x['path'] for x in rows]
for name in ['Scenario.cs','R2Gates.cs','R3Gates.cs','NativeSchedulerFixture.cs']:
 shutil.copyfile(R/'unity/Tools/Integration/RuntimeLoop'/name,S/name)
shutil.copyfile(R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5/reference-map.json',S/'reference-map.json')
with wave.open(str(S/'r2-tone.wav'),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(8000);w.writeframes(struct.pack('<8h',0,100,0,-100,0,100,0,-100))
h=hashlib.sha256((S/'r2-tone.wav').read_bytes()).hexdigest();(S/'r2-audio.tsv').write_text('ASHFALL-AUDIO/1\ngeneric/hit\t0\t'+str(S/'r2-tone.wav')+'\t'+h+'\tR3-fixture\taccepted:synthetic-test-only\tgenerated\tSFX\t0\n')
results={}
for label,names,main in [('Scenario',['Scenario.cs'],None),('R2Gates',['Scenario.cs','R2Gates.cs'],'R2Tests'),('R3Gates',['Scenario.cs','R2Gates.cs','R3Gates.cs'],'R3Tests')]:
 exe,rc=compile(label,files+[S/n for n in names],exe=True,main=main);results[label+'Compile']=rc
 if rc==0:
  for suffix,args in [('',[]),('-repeat',[]),('-strict',['--strict'])] if label=='Scenario' else [('',[])]:
   p=subprocess.run([str(exe)]+args,capture_output=True);(E/(label+suffix+'.log')).write_bytes(p.stdout+p.stderr);results[label+suffix]=p.returncode;print(p.stdout.decode()[-2500:])
groups=[('Core','/Core/'),('Modes','/Modules/Modes/'),('Combat',('/Modules/Combat/','/Modules/HealthStates/')),('InventoryItems','/Modules/InventoryItems/')];used=[];refs=[]
for label,keys in groups:
 keys=(keys,) if isinstance(keys,str) else keys;group=[p for p in files if any(k in p.as_posix() for k in keys)];used+=group;out,rc=compile(label,group,refs[:1]);results[label]=rc;refs.append(out)
loop=[p for p in files if '/Integration/RuntimeLoop/' in p.as_posix()];rest=[p for p in files if p not in used+loop]
dep,rc=compile('Dependencies',rest,refs);results['Dependencies']=rc
out,rc=compile('RuntimeLoop',loop,refs+[dep]);results['RuntimeLoop']=rc
out,rc=compile('DefineOff',[p for p in loop if p.parent.name=='Engine']+[S/'NativeSchedulerFixture.cs']);results['DefineOff']=rc
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine')
if unity.exists():
 out,rc=compile('NativeCompileOnly',[p for p in loop if p.parent.name=='Engine']+[S/'NativeSchedulerFixture.cs'],refs+[dep,S/'RuntimeLoop.dll',unity/'UnityEngine.CoreModule.dll',unity/'UnityEngine.PhysicsModule.dll',unity.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll'],extra=['/define:UNITY_6000_0_OR_NEWER']);results['NativeCompileOnly']=rc
(E/'results.json').write_text(json.dumps(results,indent=2))
