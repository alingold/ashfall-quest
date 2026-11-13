from pathlib import Path
import subprocess,json,hashlib,shutil
R=Path('C:/Users/aling/Documents/ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/PingOwnerRecoveryR1';D=R/'docs/agent-runs/20261104-three-chats/unity/PingOwnerRecoveryR1'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
run=S/'checks03';run.mkdir(exist_ok=False)
sources=[]
for folder in ['unity/Assets/Ashfall/Modules/ModesRecovery','unity/Tools/Modules/ModesRecovery']:
 for p in (R/folder).glob('*'):
  if p.is_file():
   q=run/'frozen'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);sources.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=sha(q)))
(D/'candidate-pins.json').write_text(json.dumps(sources,indent=2))
results=[]
def execute(args,label,expect=0):
 p=subprocess.run([str(a) for a in args],capture_output=True,text=True);results.append(dict(label=label,args=[str(a) for a in args],exit=p.returncode,stdout=p.stdout,stderr=p.stderr,expected=expect));(run/'results.json').write_text(json.dumps(results,indent=2));print(label,p.returncode,p.stdout[-1200:]);assert (p.returncode==0)==(expect==0)
def compile(name,src,refs=(),exe=False):
 out=run/(name+('.exe' if exe else '.dll'));args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(out)]+['/reference:'+str(x) for x in refs]+[str(x) for x in src]
 rsp=run/(name+'.rsp');rsp.write_text('\n'.join('"'+x+'"' for x in args));execute([CSC,'@'+str(rsp)],'compile '+name);return out
I=S/'inputs';F=run/'frozen';P=F/'unity/Assets/Ashfall/Modules/ModesRecovery';T=F/'unity/Tools/Modules/ModesRecovery'
core=compile('Ashfall.Core',[I/'Contracts.cs']);wire=compile('Ashfall.RecoveryContracts',[I/'RecoveryWire.cs'],[core]);modes=compile('Ashfall.Modes',[I/x for x in ['CommandLedger.cs','PingService.cs','Ports.cs','ModeRules.cs','RoundController.cs','TacticalProjection.cs']],[core]);recovery=compile('Ashfall.ModesRecovery',list(P.glob('*.cs')),[core,wire,modes]);test=compile('Tests',[T/'Tests.cs'],[core,wire,modes,recovery],True)
execute([test],'semantic differential and malformed')
execute([test,'write',run/'cold.bin'],'cold writer');execute([test,'read',run/'cold.bin'],'independent cold reader')
original=(P/'RecoverablePingService.cs').read_text()
# Compile new private instance state into the actual owner: explicit inventory must reject.
mut=run/'field-mutant';mut.mkdir();
for p in P.glob('*.cs'):shutil.copyfile(p,mut/p.name)
(mut/'RecoverablePingService.cs').write_text(original.replace('private double lastTime;','private double lastTime;\n        private readonly int unknownState=1; public int UnknownState { get { return unknownState; } }'))
compile('Ashfall.ModesRecovery',list(mut.glob('*.cs')),[core,wire,modes]);execute([test,'guard'],'unknown private field rejection')
# Deliberate gameplay drift must be detected by the same differential executable.
(mut/'RecoverablePingService.cs').write_text(original.replace('now-previous<cooldown','now-previous<cooldown+1'))
compile('Ashfall.ModesRecovery',list(mut.glob('*.cs')),[core,wire,modes]);execute([test],'cooldown mutation caught',1)
# Restore exact frozen production candidate for handoff, then verify cold fixture again.
compile('Ashfall.ModesRecovery',list(P.glob('*.cs')),[core,wire,modes]);execute([test,'read',run/'cold.bin'],'final cold reader')
outputs=[dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size) for p in run.rglob('*') if p.is_file()]
(D/'output-pins.json').write_text(json.dumps(outputs,indent=2))
current=[]
for x in json.loads((D/'input-pins.json').read_text())+sources:
 p=R/x['path'];current.append(dict(path=x['path'],frozen=x['sha256'],current=sha(p),match=sha(p)==x['sha256']))
(D/'current-comparison.json').write_text(json.dumps(current,indent=2))
print('Finished:',len(results),'actual process executions')
