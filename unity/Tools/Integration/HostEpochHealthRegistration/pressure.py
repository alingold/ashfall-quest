from pathlib import Path
import shutil,json,subprocess
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/HostEpochHealthRegistrationR2';O=S/'pressure01';O.mkdir();commands=[];CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
exec((R/'unity/Tools/Integration/HostEpochHealthRegistration/run.py').read_text().split('def run(cmd,required=True):')[1].split("comp('Core'")[0].join(['def run(cmd,required=True):','']))
refs=['Core','Recovery','Health','Combat','Persistence','Publication','Weapon','Inventory','Session','InventoryRecovery','Host']
for n in refs:shutil.copyfile(S/'candidate01'/(n+'.dll'),O/(n+'.dll'))
t=(S/'supplement01/Registration.cs').read_text();at='  Check(typeof(HealthParticipantKey)'
block='''  using(var r=new OwnerRegistry(B(),Path.Combine(root,"pressure"),true))r.Serialize(delegate{
   HealthParticipantKey key;var h=r.CreateHealth(0,true,w,w,out key);Check(r.OpenFreshSubset().Accepted,"pressure open");Check(h.Register(C("reg"),"p",-1,false).Accepted,"pressure registration");
   for(int i=0;i<511;i++){Check(h.ApplyLoss(C("loss"+i),"p",h.Read("p").Revision,1,0,false).Accepted,"pressure loss");var s=h.Read("p");HealthService.PreparedGain g;Check(h.PrepareGain(C("gain"+i,"p"),"logical"+i,"p",s.Revision,1,1,1,0,out g)&&g.Validate(),"pressure gain");g.CommitValidated();}
   Check(h.ApplyLoss(C("final"),"p",h.Read("p").Revision,1,0,false).Accepted,"fill outbox");Check(h.PendingTransitionCount==1024,"actual 1024 committed transitions");var before=h.Read("p");HealthService.PreparedGain rejected;Check(!h.PrepareGain(C("overflow","p"),"overflow","p",before.Revision,1,1,1,0,out rejected),"full outbox refuses token");Check(h.Read("p").Revision==before.Revision,"capacity refusal preserves state");Check(r.CloseIngress().Accepted,"saturated close");var d=r.InspectHealthParticipant(key);Check(d.AfterClose.PendingTransitions==1024&&d.AfterClose.Publications==0,"saturated committed archive retained");Check(r.CaptureClosedHealthCheckpoint(C("cp")).Length>0,"saturated closed checkpoint");return true;
  });
'''
assert at in t;t=t.replace(at,block+at);q=O/'Pressure.cs';q.write_text(t);comp('Pressure',[q],refs,'exe');f=O/'data';f.mkdir();run([O/'Pressure.exe',f])
