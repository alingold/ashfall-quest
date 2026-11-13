using System;
using Ashfall.Core;
using Ashfall.Modules.Projectiles;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;
class RetryTests
{
 static int n;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);n++;Console.WriteLine("PASS "+name);}
 static Fixture Source(){var f=new Fixture();var provenance=new SourceRadialProvenance("bdba6e30012e1733efd5021fd8ded593f82f26e6","one_star_050",CatalogHash.Value);f.A=new CombatProjectileAdapter(f.H,f.W,f.W,f.W,"s",3,"host",f.W.MapRevision,false,1,provenance);f.S=new ProjectileSimulation("s",3,"host",f.W.MapRevision,f.W,f.W,f.A,RadialPolicy.SourceRadialV1);return f;}
 static void Fill(Fixture f,int count){for(int i=0;i<count;i++)CheckSetup(f.H.Advance(f.C("pressure/"+i)).Accepted);}
 static void CheckSetup(bool ok){if(!ok)throw new Exception("pressure setup failed");}
 static void Main(){
  var f=Source();f.W.Block=true;f.W.Contact=true;f.W.Targets=new[]{f.T("b")};Check(f.Launch("smaw").Accepted,"typed launch actual provenance");f.Step();Check(f.H.Read("b").Shield==40,"actual source radial bypasses structural fixture only");
  long rev=f.H.Read("b").Revision;Check(f.A.Radial(f.C(),"shot",0,new Float3(),4,60,true,new[]{f.T("b")},RadialPolicy.SourceRadialV1).Accepted&&f.H.Read("b").Revision==rev,"typed retry same event once");
  Check(!f.A.Radial(new CommandContext("s","host","client",1,3,false),"shot",1,new Float3(),4,60,true,new[]{f.T("b")},RadialPolicy.SourceRadialV1).Accepted,"client cannot mint source policy");
  Check(!f.Blast(new[]{f.T("b")}).Accepted,"typed launch cannot select generic policy");
  f=new Fixture();f.Launch();Check(!f.A.Radial(f.C(),"shot",0,new Float3(),4,60,true,new[]{f.T("b")},RadialPolicy.SourceRadialV1).Accepted,"generic launch cannot select typed source exception");
  f=Source();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("smaw");Fill(f,4093);Check(!f.Step().Accepted&&f.S.PendingDeliveries("shot")==1,"typed actual Health pressure retains one event");f.W.Targets=new[]{f.T("c")};f.Steps(300);Check(f.H.Read("b").Shield==40&&f.H.Read("c").Shield==100&&f.S.PendingDeliveries("shot")==0,"typed frozen overlap drains without retargeting");
  f=new Fixture(true);f.W.Contact=true;f.W.Targets=new[]{f.T("b"),f.T("c")};f.Launch("smaw");Fill(f,4092);f.Step();Check(f.H.Read("b").Shield==40&&f.H.Read("c").Shield==100&&f.S.PendingDeliveries("shot")==1,"partial physical batch actual first success second pressure");f.Steps(300);Check(f.H.Read("b").Shield==40&&f.H.Read("c").Shield==40,"partial retry exactly once for each target");
  rev=f.H.Read("b").Revision;HealthService.PreparedGain gain;var b=f.H.Read("b");Check(f.H.PrepareGain(new CommandContext("s","b","gain",f.Tick,3,true),"post-retry","b",b.Revision,b.LifeGeneration,b.ConnectionGeneration,0,10,out gain),"real gain prepares after retry");Check(f.H.Serialize(()=>gain.Validate()&&gain.CommitValidated().Outcome==GainOutcome.Applied),"real gain commits in shared lane");f.Steps(5);Check(f.H.Read("b").Shield==50&&f.H.Read("b").Revision==rev+1,"late simulation preserves healed pools");
  f=Source();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("smaw");Fill(f,4093);f.Step();f.Steps(255);f.Tick=257;f.W.Now=12.85;Check(f.H.SetConnected(f.C("disconnect"),"b",f.H.Read("b").Revision,false).Accepted,"actual disconnect under drained admission");Check(f.H.SetConnected(f.C("reconnect"),"b",f.H.Read("b").Revision,true).Accepted,"actual reconnect new generation");f.Tick=256;f.Steps(45);Check(f.H.Read("b").Shield==100&&f.S.PendingDeliveries("shot")==0,"pending target capability cannot hit replacement connection");
  f=Source();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("zone");f.Steps(64);Fill(f,4093);f.Step();Check(f.S.PendingDeliveries("shot")==1,"zone first half-second event retained");f.Steps(200);Float3 pos;bool ended;f.S.Read("shot",out pos,out ended);Check(!ended&&f.S.PendingDeliveries("shot")>0&&f.S.PendingDeliveries("shot")*5+100-f.H.Read("b").Shield==95,"zone retirement accounts for all nineteen applied or pending ticks");f.Steps(100);Check(f.H.Read("b").Shield==5&&f.S.PendingDeliveries("shot")==0,"zone drains nineteen original subhits once");
  f=Source();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("smaw");f.H.SetConnected(f.C("owner-leave"),"a",f.H.Read("a").Revision,false);f.Step();Check(f.H.Read("b").Shield==40,"typed owner disconnect preserves launched attribution");
  f=Source();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("smaw");f.H.ApplyLoss(f.C("owner-death"),"a",f.H.Read("a").Revision,100,100,false);f.Step();Check(f.H.Read("b").Shield==40,"typed owner death preserves launched attribution");
  f=new Fixture();for(int tick=0;tick<1400;tick++){f.Step();for(int j=0;j<12;j++)CheckSetup(f.H.SetConnected(f.C("outbox/"+tick+"/"+j),"c",f.H.Read("c").Revision,true).Accepted);}
  var offer=f.A.Pump(f.C("offer"));Check(!offer.Result.Accepted&&offer.Events.Length==16384&&offer.PendingHealthEvents>0,"actual outbox pressure established");f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.Launch("smaw");f.Step();Check(f.H.Read("b").Shield==100&&f.S.PendingDeliveries("shot")==1,"outbox pressure retains physical event");
  Check(!f.A.Acknowledge(f.C("future"),offer.Events[offer.Events.Length-1].Sequence+1).Accepted,"future ACK rejected");Check(!f.A.Acknowledge(new CommandContext("s","intruder","ack",f.Tick,3,true),1).Accepted,"foreign ACK rejected");Check(f.A.Acknowledge(f.C("ack"),offer.Events[offer.Events.Length-1].Sequence).Accepted,"offered prefix ACK forwards to actual bridge");var next=f.A.Pump(f.C("next"));Check(next.Result.Accepted&&next.PendingHealthEvents==0,"producer backlog transfers after ACK");f.Step();Check(f.H.Read("b").Shield==40&&f.S.PendingDeliveries("shot")==0,"outbox drained physical retry applied exactly once");
  f=Source();f.W.Block=true;Check(f.Launch("smaw").Accepted,"typed falloff registration");Check(f.A.Radial(f.C(),"shot",0,new Float3(),4,60,true,new[]{f.T("b",2),f.T("c")},RadialPolicy.SourceRadialV1).Reason=="friendly-fire"&&f.H.Read("b").Shield==70&&f.H.Read("c").Shield==100,"typed actual falloff and independent ally protection");
  f=Source();f.Launch("smaw");f.W.Match=false;Check(f.H.Spawn(f.C("protect"),"b",f.H.Read("b").Revision,100,100).Accepted,"typed protection setup actual Health");f.W.Match=true;Check(!f.A.Radial(f.C(),"shot",0,new Float3(),4,60,true,new[]{f.T("b")},RadialPolicy.SourceRadialV1).Accepted&&f.H.Read("b").Shield==100,"typed spawn protection remains authoritative");
  Check(!f.A.Radial(f.C(),"shot",1,new Float3(),4,60,true,new[]{new Target("b",new Float3(),"unknown-map")},RadialPolicy.SourceRadialV1).Accepted,"typed unknown map fails closed");
  Check(ProjectileDelivery.Classify(RuleResult.Reject("future-unknown-failure"))==DeliveryDisposition.Retryable&&ProjectileDelivery.Classify(RuleResult.Reject("event-expired"))==DeliveryDisposition.Expired,"unknown failure retained and explicit expiry classified");
  f=new Fixture();f.S=new ProjectileSimulation("s",3,"host",f.W.MapRevision,f.W,f.W,f.A,RadialPolicy.SourceRadialV1);Check(!f.Launch("smaw").Accepted,"missing source provenance cannot launch typed event");
  Console.WriteLine("RESULT "+n+" PASS 0 FAIL; external geometry/auth remain fixtures");
 }
}


