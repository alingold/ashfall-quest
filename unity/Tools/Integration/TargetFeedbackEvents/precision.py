from pathlib import Path
import json,subprocess,hashlib,shutil
root=Path(r'C:/Users/aling/Documents/ashfall');stage=root/'campaign-staging/20261104-three-chats/unity/TargetFeedbackEventsR10';docs=root/'docs/agent-runs/20261104-three-chats/unity/TargetFeedbackEventsR10';run=stage/'precision02';run.mkdir();shutil.copy2(stage/'run05/ActualCPU.dll',run/'ActualCPU.dll');shutil.copy2(stage/'run05/EventPool.dll',run/'EventPool.dll')
s=(root/'unity/Tools/Integration/TargetFeedbackEvents/BindingTests.cs').read_text().replace('i==1?.25f:2.5f','i==1?.1f:2.5f').replace('Check(sink.Pool.Read(0).Text=="1"','Console.WriteLine("Actual captions after 7, .1, 2.5 losses: "+sink.Pool.Read(0).Text+", "+sink.Pool.Read(1).Text);Check(sink.Pool.Read(0).Text=="1"')
p=run/'PrecisionHoldout.cs';p.write_text(s)
rsp=stage/'run05/BindingTests.rsp';old=rsp.read_text();lines=old.splitlines();lines=[x for x in lines if not x.startswith('/out:') and not (x.startswith('"') and x.endswith('BindingTests.cs"'))];lines+=['/out:"'+str(run/'PrecisionHoldout.exe')+'"','"'+str(p)+'"'];response=run/'PrecisionHoldout.rsp';response.write_text('\n'.join(lines));commands=[]
for cmd in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(response)],[str(run/'PrecisionHoldout.exe')]]:
 r=subprocess.run(cmd,capture_output=True,text=True);commands.append(dict(command=cmd,exit=r.returncode,output=r.stdout+r.stderr));print(r.returncode,r.stdout+r.stderr)
(docs/'precision-holdout02.json').write_text(json.dumps(commands,indent=2))

