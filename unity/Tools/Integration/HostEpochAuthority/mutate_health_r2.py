from health_epoch_r2 import R,S,D
from pathlib import Path
import shutil,subprocess,json
base=S/'final02';dest=S/'mutations';dest.mkdir();results=[]
cases=[('gain','PreparedGains.cs','if(!lease.CanCommit||aborted||player.Revision','if(aborted||player.Revision'),('advance','PreparedAdvance.cs','if(!lease.CanCommit||!validated||closed)','if(!validated||closed)'),('fields','HealthService.cs','private long eventSequence;','private long eventSequence; public readonly int UnexpectedCodecField=1;'),('raw','HealthService.cs','if(epochIngressClosed)return RuleResult.Reject("health-epoch-closed");','')]
for name,file,a,b in cases:
 out=dest/name;out.mkdir();shutil.copytree(base/'source',out/'source')
 for p in base.glob('*.dll'):shutil.copyfile(p,out/p.name)
 shutil.copyfile(base/'Tests.exe',out/'Tests.exe')
 p=out/'source/Modules/HealthStates'/file;s=p.read_text();assert a in s;p.write_text(s.replace(a,b))
 rsp=out/'Health.rsp';rsp.write_text((base/'Health.rsp').read_text().replace(str(base),str(out)))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True)
 row=dict(name=name,compileCommand=cmd,compileExit=c.returncode,compileOutput=c.stdout+c.stderr)
 if c.returncode==0:
  test=[str(out/'Tests.exe'),str(out)];t=subprocess.run(test,capture_output=True,text=True);row.update(testCommand=test,testExit=t.returncode,testOutput=t.stdout+t.stderr,killed=t.returncode!=0)
 results.append(row)
 (D/'mutations.json').write_text(json.dumps(results,indent=2))
 print(name,'compile',c.returncode,'killed',row.get('killed'))
assert all(x.get('killed') for x in results)
