"""Direct C#5 compilation; writes only r2 paths. No engine launch or policy changes."""
from pathlib import Path
import subprocess, json, hashlib
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/Networking/r2'
EVIDENCE=ROOT/'docs/agent-runs/20261104-three-chats/unity/Networking/r2'
CSC=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
CORE=ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'
TOOLS=ROOT/'unity/Tools/Modules/Networking'
BASE=ROOT/'campaign-staging/20261104-three-chats/unity/Networking/revisions/r1-before-r2/production'
PROD=ROOT/'unity/Assets/Ashfall/Modules/Networking'
RUNS=[('before-reviewer',BASE,ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Networking-r1/Adversarial.cs',0),
      ('before-regressions',BASE,TOOLS/'NetworkingR2Tests.cs',1),
      ('before-transport-doubles',BASE,TOOLS/'TransportContractFixture.cs',1),
      ('after-original',PROD,TOOLS/'NetworkingTests.cs',0),
      ('after-reviewer-regressions',PROD,TOOLS/'AdversarialR2.cs',0),
      ('after-regressions',PROD,TOOLS/'NetworkingR2Tests.cs',0),
      ('after-transport-doubles',PROD,TOOLS/'TransportContractFixture.cs',0)]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def main():
 OUT.mkdir(parents=True,exist_ok=True); EVIDENCE.mkdir(parents=True,exist_ok=True)
 results=[]
 for name,prod,test,expected in RUNS:
  exe=OUT/(name+'.exe')
  args=[str(CSC),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe),str(CORE)]+[str(prod/x) for x in ['Ports.cs','Protocol.cs','Authority.cs','Engine/UnityTransportAdapter.cs']]+[str(test)]
  if test.name=='TransportContractFixture.cs': args.insert(1,'/define:UNITY_6000_0_OR_NEWER')
  # Explicit absolute quoted Windows compiler, source and output paths.
  command=' '.join(('"'+a+'"') if not a.startswith('/') else ('/out:"'+a[5:]+'"' if a.startswith('/out:') else a) for a in args)
  build=subprocess.run(command,capture_output=True,text=True)
  run=subprocess.run([str(exe)],capture_output=True,text=True) if build.returncode==0 else None
  log=command+'\nCOMPILE EXIT '+str(build.returncode)+'\n'+build.stdout+build.stderr
  if run: log+='RUN '+str(exe)+'\nEXIT '+str(run.returncode)+'\n'+run.stdout+run.stderr
  (EVIDENCE/(name+'.txt')).write_text(log)
  entry=dict(name=name,compileExit=build.returncode,runExit=run.returncode if run else None,expectedRunExit=expected,executableSha256=sha(exe) if run else None,sourceHashes=[dict(path=str(p),sha256=sha(p)) for p in [CORE]+[prod/x for x in ['Ports.cs','Protocol.cs','Authority.cs','Engine/UnityTransportAdapter.cs']]+[test]])
  results.append(entry)
  print(name,'compile',entry['compileExit'],'run',entry['runExit'])
 (EVIDENCE/'test-results.json').write_text(json.dumps(results,indent=2))
 if any(x['compileExit']!=0 or x['runExit']!=x['expectedRunExit'] for x in results): raise SystemExit(1)
if __name__=='__main__': main()
