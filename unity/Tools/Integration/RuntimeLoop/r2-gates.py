from pathlib import Path
import subprocess,json,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR2/final04';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR2/final04'
def compile(label,sources,refs=[],exe=False,main=None):
 out=S/(label+('.exe' if exe else '.dll'));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(out)]+['/reference:'+str(r) for r in refs]+list(map(str,sources));cmd+=['/main:'+main] if main else [];(E/(label+'-command.txt')).write_text(subprocess.list2cmdline(cmd));p=subprocess.run(cmd,capture_output=True);(E/(label+'-compile.log')).write_bytes(p.stdout+p.stderr);print(label,p.returncode,p.stdout.decode());return out,p.returncode

import wave,struct
with wave.open(str(S/'r2-tone.wav'),'wb') as wav:
 wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(8000);wav.writeframes(struct.pack('<8h',0,100,0,-100,0,100,0,-100))
h=hashlib.sha256((S/'r2-tone.wav').read_bytes()).hexdigest()
(S/'r2-audio.tsv').write_text('ASHFALL-AUDIO/1\ngeneric/hit\t0\t'+str(S/'r2-tone.wav')+'\t'+h+'\tR2-fixture\taccepted:synthetic-test-only\tgenerated\tSFX\t0\n')
files=[S/'pin'/x['path'] for x in json.loads((E/'pin.json').read_text())]
core=[p for p in files if '/Core/' in p.as_posix()];modes=[p for p in files if '/Modules/Modes/' in p.as_posix()];combat=[p for p in files if '/Modules/Combat/' in p.as_posix() or '/Modules/HealthStates/' in p.as_posix()];inventory=[p for p in files if '/Modules/InventoryItems/' in p.as_posix()];loop=[p for p in files if '/Integration/RuntimeLoop/' in p.as_posix()]
results={};refs=[]
for label,group in [('Core',core),('Modes',modes),('Combat',combat),('InventoryItems',inventory)]:
 out,rc=compile(label,group,refs[:1]);results[label]=rc;refs.append(out)
rest=[p for p in files if p not in core+modes+combat+inventory+loop]
dep,rc=compile('Dependencies',rest,refs);results['Dependencies']=rc
out,rc=compile('RuntimeLoop',loop,refs+[dep]);results['RuntimeLoop']=rc
out,rc=compile('AdapterDefineOff',[p for p in loop if p.parent.name=='Engine']+[R/'unity/Tools/Integration/RuntimeLoop/NativeSchedulerFixture.cs']);results['AdapterDefineOff']=rc
exe,rc=compile('R2Gates',files+[R/'unity/Tools/Integration/RuntimeLoop/Scenario.cs',R/'unity/Tools/Integration/RuntimeLoop/R2Gates.cs'],exe=True,main='R2Tests');results['R2GatesCompile']=rc
if rc==0:
 p=subprocess.run([str(exe)],capture_output=True);(E/'r2-gates.log').write_bytes(p.stdout+p.stderr);results['R2Gates']=p.returncode;print(p.returncode,p.stdout.decode())
(E/'gates.json').write_text(json.dumps(results,indent=2))
