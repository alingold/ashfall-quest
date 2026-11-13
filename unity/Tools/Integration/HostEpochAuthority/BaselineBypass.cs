using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.HostEpochAuthority;
class BaselineBypass
{
 static CommandContext C(string id,string actor="host"){return new CommandContext("s",actor,id,1,0,true);}
 sealed class World:IHealthWorld,IInteractionQuery {public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}}
 static EpochBinding B(){return new EpochBinding("s",0,SourceProfiles.Build,new string('a',64),new string('b',64));}
 static void Main(string[] args)
 {
  var w=new World();HealthService h=null;HealthService.PreparedGain token=null;string path=Path.Combine(args[0],"baseline.journal");
  using(var r=new OwnerRegistry(B(),path,true))r.Serialize(delegate{h=r.CreateHealth(0,true,w,w);if(!r.OpenFreshSubset().Accepted||!h.Register(C("reg"),"p",-1,false).Accepted||!h.ApplyLoss(C("loss"),"p",1,1,0,false).Accepted)throw new Exception("setup");var s=h.Read("p");if(!h.PrepareGain(C("gain","p"),"logical","p",s.Revision,1,1,1,0,out token)||!token.Validate()||!r.CloseIngress().Accepted)throw new Exception("setup gain");return true;});
  using(var cold=new OwnerRegistry(B(),path,false))if(!cold.IngressClosed)throw new Exception("cold open");
  long before=h.Read("p").Revision;token.CommitValidated();if(h.Read("p").Revision!=before+1)throw new Exception("historical bypass not reproduced");Console.WriteLine("REPRODUCED actual validated gain commits after CloseIngress, Dispose and physical reopen");
 }
}
