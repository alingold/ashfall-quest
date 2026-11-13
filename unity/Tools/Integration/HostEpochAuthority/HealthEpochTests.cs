using System;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.HostEpochAuthority;
class HealthEpochTests
{
 static int checks;static string H=new string('a',64);
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static bool Throws(Action a){try{a();return false;}catch(InvalidOperationException){return true;}}
 static CommandContext C(string id,string actor="host"){return new CommandContext("s",actor,id,20,0,true);}
 static EpochBinding Binding(){return new EpochBinding("s",0,SourceProfiles.Build,H,H);}
 sealed class World:IHealthWorld,IInteractionQuery
 {public double Time;public double Now{get{return Time;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}}
 static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 static bool Restore(byte[] b,World w,out HealthService h){return HealthService.TryRestoreCheckpoint(C("restore"),H,Hash(b),b,0,true,new Gate(),w,w,out h);}
 sealed class Gate:IAuthorityGate{public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
 static void Main(string[] args)
 {
  if(args.Length==3&&args[0]=="cold") {using(var r=new OwnerRegistry(Binding(),args[1],false))r.Serialize(delegate{Check(!r.OpenFreshSubset().Accepted,"cold tombstone cannot open");Check(Throws(delegate{r.CreateHealth(0,true,new World(),new World());}),"cold cannot register");return true;});HealthService h;Check(Restore(File.ReadAllBytes(args[2]),new World{Time=10},out h),"cold Health reconstructs");Check(h.EpochAccounting.IngressClosed&&!h.Advance(C("raw")).Accepted,"cold Health remains closed with permissive gate");Check(h.PendingTransitionCount==2,"cold committed outbox retained");return;}
  string root=args[0];
  foreach(string family in new[]{"gain","advance","respawn"})foreach(bool disposeOnly in new[]{false,true}) {
   string path=Path.Combine(root,family+disposeOnly+".journal");var world=new World();HealthService h=null;Action commit=null,abort=null;
   var registry=new OwnerRegistry(Binding(),path,true);
   registry.Serialize(delegate {
    h=registry.CreateHealth(0,true,world,world);Check(registry.OpenFreshSubset().Accepted,"open "+family);Check(h.Register(C("reg"),"p",-1,false).Accepted,"register");
    if(family=="gain") {Check(h.ApplyLoss(C("loss"),"p",1,1,0,false).Accepted,"loss");var s=h.Read("p");HealthService.PreparedGain t;Check(h.PrepareGain(C("gain","p"),"logical","p",s.Revision,s.LifeGeneration,s.ConnectionGeneration,1,0,out t)&&t.Validate(),"gain validated");commit=()=>{t.CommitValidated();};abort=t.Abort;}
    if(family=="advance") {HealthService.PreparedAdvance t;Check(h.PrepareAdvance(C("advance"),out t).Accepted&&t.Validate(),"advance validated");commit=t.CommitValidated;abort=t.Abort;}
    if(family=="respawn") {Check(h.ApplyLoss(C("kill"),"p",1,100,0,false).Accepted,"kill");world.Time=5;var s=h.Read("p");HealthService.PreparedRespawn t;Check(h.PrepareRespawn(C("respawn"),"p",s.Revision,s.LifeGeneration,s.ConnectionGeneration,6,out t)&&t.Validate(),"respawn validated");commit=t.CommitValidated;abort=t.Abort;}
    Check(h.EpochAccounting.Publications==1,"actual reservation tracked");if(!disposeOnly)Check(registry.CloseIngress().Accepted,"close flush");return true;
   });
   long before=h.Read("p").Revision;int pending=h.PendingTransitionCount;registry.Dispose();
   using(var cold=new OwnerRegistry(Binding(),path,false))cold.Serialize(delegate{Check(!cold.OpenFreshSubset().Accepted,"cold refusal");return true;});
   Check(h.EpochAccounting.IngressClosed&&h.EpochAccounting.Publications==0,"actual cancellation");Check(Throws(commit),"retained validated commit blocked "+family);abort();Check(Throws(commit),"abort retry still blocked");
   Check(before==h.Read("p").Revision&&pending==h.PendingTransitionCount,"abort preserves committed state/outbox");Check(!h.Advance(C("after")).Accepted,"retained raw ingress blocked");
  }
  // Closure before first registration/open must stay terminal.
  using(var r=new OwnerRegistry(Binding(),Path.Combine(root,"preopen"),true))r.Serialize(delegate{Check(r.CloseIngress().Accepted&&!r.OpenFreshSubset().Accepted,"preopen terminal");Check(Throws(delegate{r.CreateHealth(0,true,new World(),new World());}),"preopen factory terminal");Check(!r.PrepareGlobalRetirement().Accepted,"global retirement refuses");return true;});
  // Closed schema2 preserves state, replay, timers, disposition, and pending transitions.
  var w=new World();HealthService.EpochClosure closure;var service=HealthService.CreateEpochParticipant("s",0,0,true,new Gate(),w,w,out closure);
  service.Register(C("reg"),"p",-1,false);service.ApplyLoss(C("loss"),"p",1,1,0,false);
  byte[] beforeBytes=service.CreateCheckpoint(C("cp"),H);HealthService.PreparedGain gain;var snapshot=service.Read("p");Check(service.PrepareGain(C("gain","p"),"logical","p",snapshot.Revision,1,1,1,0,out gain)&&gain.Validate(),"codec gain validated");
  closure.Close();byte[] bytes=closure.CaptureClosedCheckpoint(C("cp"),H);Check(bytes.Length==beforeBytes.Length,"codec shape stable");bool same=true;for(int i=0;i<bytes.Length-5;i++)same &= bytes[i]==beforeBytes[i];Check(same,"abort preserves all checkpoint state bytes except closure metadata");
  HealthService restored;Check(Restore(bytes,w,out restored),"closed restore");Check(!restored.Advance(C("raw")).Accepted,"closed restore raw rejection");Check(restored.PendingTransitionCount==2,"committed outbox retained");Check(restored.EpochAccounting.AbortedFamily==HealthReservationFamily.Gain,"typed disposition retained");
  var bad=(byte[])bytes.Clone();bad[0]=1;Check(!Restore(bad,w,out restored),"schema1 explicit refusal");bad=(byte[])bytes.Clone();bad[bad.Length-5]=2;Check(!Restore(bad,w,out restored),"invalid closure bool refused");bad=(byte[])bytes.Clone();bad[bad.Length-1]=127;Check(!Restore(bad,w,out restored),"invalid family refused");
  File.WriteAllBytes(Path.Combine(root,"closed.health"),bytes);
  var child=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"cold \""+Path.Combine(root,"gainFalse.journal")+"\" \""+Path.Combine(root,"closed.health")+"\""){UseShellExecute=false});child.WaitForExit();Check(child.ExitCode==0,"physical cold subprocess");
  // Open checkpoints retain replay; aborted identities are available in an independently restored preclosure image.
  Check(Restore(beforeBytes,w,out restored),"open checkpoint restore");Check(!restored.Register(C("reg"),"p",-1,false).Accepted,"committed replay retained");snapshot=restored.Read("p");Check(restored.PrepareGain(C("gain","p"),"logical","p",snapshot.Revision,1,1,1,0,out gain)&&gain.Validate(),"aborted command/logical not consumed");gain.CommitValidated();gain.CommitValidated();gain.Abort();Check(restored.Read("p").Health==100,"committed retry preserved");
  Console.WriteLine("TOTAL "+checks);
 }
}
