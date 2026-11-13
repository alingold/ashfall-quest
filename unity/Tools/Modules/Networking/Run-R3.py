"""Frozen r3 neutral candidate runner. No engine or old wrapper invocation."""
from pathlib import Path
import hashlib,json,subprocess,shutil
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/Networking/r3'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/Networking/r3'
PROD=ROOT/'unity/Assets/Ashfall/Modules/Networking'
TOOLS=Path(__file__).parent
CSC=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def record(p): return dict(path=str(p.relative_to(ROOT)),sha256=sha(p))
def main():
 OUT.mkdir(parents=True,exist_ok=True); DOC.mkdir(parents=True,exist_ok=True)
 snap=OUT/'candidate'; snap.mkdir(exist_ok=True)
 for src,dest in [(PROD,snap/'production'),(TOOLS,snap/'tools')]:
  shutil.copytree(src,dest,dirs_exist_ok=True)
 core=ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'
 shutil.copy2(core,snap/'Contracts.cs')
 inputs=[core,ROOT/'unity/Documentation/CONTRACTS-v1.md',ROOT/'unity/Documentation/CONTRACTS-v2.md',ROOT/'unity/Documentation/COLLISION-LAYERS-v1.md',ROOT/'docs/agent-runs/20261104-three-chats/unity/ownership.json',ROOT/'docs/agent-runs/20261104-three-chats/unity/phase03-ownership.json',ROOT/'docs/agent-runs/20261104-three-chats/unity/reviews/Networking-r2/REPORT.md',ROOT/'docs/agent-runs/20261104-three-chats/unity/Networking/r2/candidate-manifest.json',ROOT/'quest-r22/source/scripts/net_session1000.gd',ROOT/'game/scripts/net_session1000.gd']
 (DOC/'inputs.json').write_text(json.dumps([record(p) for p in inputs],indent=2))
 # Preserve only small source-relevant excerpts, not game trees or map packs.
 for prefix,p,start,end in [('quest',inputs[-2],745,800),('game',inputs[-1],706,760)]:
  lines=p.read_text(encoding='utf-8').splitlines()
  (snap/(prefix+'-resync.txt')).write_text('\n'.join(str(i+1)+': '+lines[i] for i in range(start-1,min(end,len(lines)))))
 before=OUT/'before/production'
 baseline=[]
 for p in before.rglob('*.cs'):
  current=PROD/p.relative_to(before)
  baseline.append(dict(path=str(p.relative_to(before)),beforeSha256=sha(p),currentSha256=sha(current)))
 (DOC/'changed-files.json').write_text(json.dumps(baseline,indent=2))
 results=[]
 for name in ['NetworkingTests','AdversarialR2','NetworkingR2Tests','TransportContractFixture','NetworkingR3Tests']:
  exe=OUT/(name+'.exe'); sources=[snap/'Contracts.cs']+sorted((snap/'production').rglob('*.cs'))+[snap/'tools'/(name+'.cs')]
  args=[str(CSC),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]
  if name=='TransportContractFixture': args+=['/define:UNITY_6000_0_OR_NEWER']
  args+=list(map(str,sources))
  command=' '.join('/out:"'+a[5:]+'"' if a.startswith('/out:') else a if a.startswith('/') else '"'+a+'"' for a in args)
  build=subprocess.run(command,capture_output=True,text=True)
  run=subprocess.run([str(exe)],capture_output=True,text=True) if build.returncode==0 else None
  (DOC/(name+'.txt')).write_text(command+'\nCOMPILE EXIT '+str(build.returncode)+'\n'+build.stdout+build.stderr+ ('\nRUN '+str(exe)+'\nEXIT '+str(run.returncode)+'\n'+run.stdout+run.stderr if run else ''))
  results.append(dict(name=name,command=command,compileExit=build.returncode,runExit=run.returncode if run else None,sources=[record(p) for p in sources],exeSha256=sha(exe) if exe.exists() else None))
  print(name,results[-1]['compileExit'],results[-1]['runExit'])
 (DOC/'test-results.json').write_text(json.dumps(results,indent=2))
 # Input drift is evidence, never hidden by compiling live mutable modules.
 frozen=json.loads((DOC/'inputs.json').read_text())
 drift=[r['path'] for r in frozen if sha(ROOT/r['path'])!=r['sha256']]
 (DOC/'drift.json').write_text(json.dumps(dict(changedInputs=drift),indent=2))
 if drift or any(r['compileExit']!=0 or r['runExit']!=0 for r in results): raise SystemExit(1)
if __name__=='__main__': main()
