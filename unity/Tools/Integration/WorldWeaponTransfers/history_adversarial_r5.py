from pathlib import Path
import json,subprocess,shutil,struct,os,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/WorldTransferRecoveryR5/final02';D=R/'docs/agent-runs/20261104-three-chats/unity/WorldTransferRecoveryR5';O=S/'adversarial';O.mkdir(exist_ok=False);logs=[]
refs=[p for p in S.glob('*.dll') if p.name!='Ashfall.CustodyIntegration.dll']
files=list((S/'inputs/unity/Assets/Ashfall/Integration/InventoryTransfers').glob('*.cs'))+list((S/'inputs/unity/Assets/Ashfall/Integration/WorldWeaponTransfers').glob('*.cs'))
def execute(cmd,ok=True,env=None):
 p=subprocess.run([str(c) for c in cmd],capture_output=True,text=True,env=env);logs.append(dict(command=[str(c) for c in cmd],exit=p.returncode,output=p.stdout+p.stderr));(D/'final02-adversarial-commands.json').write_text(json.dumps(logs,indent=2));assert (p.returncode==0)==ok,logs[-1];return p

def build(folder,owned=None,journal=None):
 folder.mkdir();sources=[]
 for f in files:
  q=folder/(f.parent.name+"-"+f.name);raw=f.read_text()
  if f.name=='WorldTransferCheckpoint.cs' and owned:raw=owned(raw)
  if f.name=='WeaponTransferJournal.cs' and journal:raw=journal(raw)
  q.write_text(raw);sources.append(q)
 def csc(name,src,rr,exe=False):
  out=folder/name;rsp=folder/(name+'.rsp');args=['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(out),'/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll']+['/reference:'+str(p) for p in rr]+[str(p) for p in src];rsp.write_text('\n'.join('"'+a+'"' for a in args));execute([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)]);return out
 dll=csc('Ashfall.CustodyIntegration.dll',sources,refs)
 for p in refs:shutil.copyfile(p,folder/p.name)
 cold=csc('ColdHistoryTests.exe',[S/'ColdHistoryTests.cs'],refs+[dll],True)
 cut=csc('HistoryCutTests.exe',[S/'inputs/unity/Tools/Integration/WorldWeaponTransfers/HistoryCutTests.cs'],refs+[dll],True)
 return cold,cut
# Deliberate compiled faults in candidate-only code. The unchanged external consumer must kill each.
mutants=[('lostack',lambda s:s.replace('h.pending.RemoveAt(index);','/* mutant drops ACK disposition */'), 'cold'),('unboundanchor',lambda s:s.replace('Wire.Require(Wire.Hash(prefix)==Wire.Hash(exactDurablePrefix));','/* mutant ignores durable anchor */'),'cold'),('nodecision',lambda s:s.replace('prepared!=null&&decision&&id==h.Intent','prepared!=null&&id==h.Intent'),'malformed')]
for name,mutate,mode in mutants:
 cold,cut=build(O/name,mutate)
 if mode=='cold':p=execute([cold,'read',S/'world'],False)
 else:p=execute([cut,'malformed',S/'world.journal'],False)
 print('KILLED',name)
# Crash instrumentation is confined to a separate staged DLL, after the genuine Flush(true).
instrument='file.Flush(true);if(Int32.Parse(Environment.GetEnvironmentVariable("ASHFALL_TEST_FLUSH_CUT")??"0")==frames.Count+1)Environment.Exit(0);frames.Add'
cold,cut=build(O/'crashcuts',journal=lambda s:s.replace('file.Flush(true);frames.Add',instrument))
b=(S/'world.journal').read_bytes();o=0;expected=[];seq=0;pending=0;prepared=False;decision=False
while o<len(b):
 n=struct.unpack_from('<i',b,o)[0];stage=b[o+4];o+=n+36
 if stage==1:prepared=True;decision=False
 if stage==2:decision=True
 if stage==3:seq+=1;pending+=1;prepared=False;decision=False
 if stage==4:prepared=False;decision=False
 if stage==5:pending-=1
 expected.append((o,seq,pending,prepared,decision))
for i,(length,seq,pending,prepared,decision) in enumerate(expected,1):
 base=O/'crashcuts'/('cut'+str(i));env=dict(os.environ,ASHFALL_TEST_FLUSH_CUT=str(i));execute([cold,'write',base],env=env)
 actual=base.with_suffix('.journal').read_bytes();assert actual==b[:length],i
 execute([cut,'prefix',base.with_suffix('.journal'),seq,pending,str(prepared),str(decision)])
print('PASS',len(expected),'genuine Flush(true) process exits and fresh-process history reads; no partial-owner adoption claimed')
