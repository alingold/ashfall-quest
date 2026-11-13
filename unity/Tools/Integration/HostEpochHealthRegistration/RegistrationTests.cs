using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.HostEpochAuthority;
class RegistrationTests {
 static int count;static string H=new string('a',64);
 static void Check(bool v,string s){if(!v)throw new Exception(s);count++;}
 static bool Throws(Action a){try{a();return false;}catch(InvalidOperationException){return true;}}
 static CommandContext C(string s,string a="host"){return new CommandContext("s",a,s,20,0,true);}
 static EpochBinding B(){return new EpochBinding("s",0,SourceProfiles.Build,H,H);}
 sealed class W:IHealthWorld,IInteractionQuery {public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}public bool TryGetPose(string a,out RescuePose p){p=new RescuePose();return false;}public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}}
 static void Main(string[] args){
  string root=args[0];HealthParticipantKey foreign=null;var w=new W();
  using(var other=new OwnerRegistry(B(),Path.Combine(root,"foreign"),true))other.Serialize(delegate{other.CreateHealth(0,true,w,w,out foreign);return true;});
  using(var r=new OwnerRegistry(B(),Path.Combine(root,"main"),true))r.Serialize(delegate{
   HealthParticipantKey key;var h=r.CreateHealth(0,true,w,w,out key);var initial=r.InspectHealthParticipant(key);
   Check(initial.Key==key&&initial.Binding==r.Binding&&initial.RegistryRevision==1,"exact factory binding");Check(initial.BeforeClose==null&&initial.AfterClose==null,"initial quarantine is not closure disposition");
   Check(Throws(()=>r.InspectHealthParticipant(null))&&Throws(()=>r.InspectHealthParticipant(foreign)),"foreign/null key refusal");Check(!h.Register(C("early"),"p",-1,false).Accepted,"initial raw quarantine");
   Check(r.OpenFreshSubset().Accepted,"open");Check(h.Register(C("reg"),"p",-1,false).Accepted,"register");Check(h.ApplyLoss(C("loss"),"p",1,1,0,false).Accepted,"loss");
   var state=h.Read("p");HealthService.PreparedGain gain;Check(h.PrepareGain(C("gain","p"),"logical","p",state.Revision,1,1,1,0,out gain)&&gain.Validate(),"prepare actual gain");
   var active=r.InspectHealthParticipant(key);Check(active.Current.Gains==1&&active.Current.Publications==1,"actual token inventory");int pending=h.PendingTransitionCount;
   Check(r.CloseIngress().Accepted,"close");var d=r.InspectHealthParticipant(key);Check(d.BeforeClose.Gains==1&&d.AfterClose.Gains==0&&d.AfterClose.Publications==0&&d.AfterClose.IngressClosed,"actual before/after disposition");
   Check(d.AfterClose.AbortedFamily==HealthReservationFamily.Gain,"typed aborted family");Check(d.BeforeClose.PendingTransitions==pending&&d.AfterClose.PendingTransitions==pending,"committed prefix preserved");
   Check(active.Current.Gains==1&&initial.AfterClose==null,"old observations immutable");Check(Throws(()=>gain.CommitValidated()),"retained token rejected");Check(!h.Advance(C("raw")).Accepted,"retained raw rejected");Check(h.Read("p").Revision==state.Revision,"no partial health mutation");
   Check(r.CloseIngress().Accepted,"close retry");var retry=r.InspectHealthParticipant(key);Check(Object.ReferenceEquals(d.BeforeClose,retry.BeforeClose)&&Object.ReferenceEquals(d.AfterClose,retry.AfterClose),"retry retains first disposition");
   Check(!r.OpenFreshSubset().Accepted&&!r.PrepareGlobalRetirement().Accepted,"terminal and incomplete global refusal");Check(Throws(()=>r.CreateHealth(0,true,w,w)),"late factory refusal");Check(r.CaptureClosedHealthCheckpoint(C("checkpoint")).Length>0,"closed actual checkpoint");return true;
  });
  for(int cut=0;cut<=112;cut++){var image=File.ReadAllBytes(Path.Combine(root,"main"));string p=Path.Combine(root,"cut"+cut);var bytes=new byte[cut];Array.Copy(image,bytes,cut);File.WriteAllBytes(p,bytes);bool safe=false;try{using(var r=new OwnerRegistry(B(),p,false))r.Serialize(delegate{safe=r.IngressClosed&&!r.OpenFreshSubset().Accepted&&Throws(()=>r.InspectHealthParticipant(foreign))&&!r.PrepareGlobalRetirement().Accepted;return true;});}catch(IOException){safe=true;}catch(InvalidDataException){safe=true;}Check(safe,"journal cut "+cut);}
  foreach(bool withHealth in new[]{false,true})using(var r=new OwnerRegistry(B(),Path.Combine(root,"pre"+withHealth),true))r.Serialize(delegate{if(withHealth)r.CreateHealth(0,true,w,w);Check(r.CloseIngress().Accepted&&!r.OpenFreshSubset().Accepted,"preopen terminal");Check(Throws(()=>r.CreateHealth(0,true,w,w)),"preopen registration refused");return true;});
  Check(typeof(HealthParticipantKey).GetConstructors().Length==0,"no public key constructor");Console.WriteLine("Registration assertions "+count);
 }
}

