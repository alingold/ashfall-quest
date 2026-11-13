using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.RuntimeLoop;
partial class Scenario
{
 public void RunR3()
 {
  SetupR2();ArmR2();ActorResult[] output;
  health.SetConnected(C("host"),"a",health.Read("a").Revision,true);rejectSink=true;tick++;Now=tick*.1;
  Check(!loop.Tick(C("host"),.1f,new[]{A("a"),A("b")},out output).Accepted&&loop.HasPendingPublication,"R3 sink refusal retains publication");int roots=a.Commits,ammo=weapons.Inspect("rifle").Loaded;
  for(int i=0;i<16400;i++){tick++;if(!health.SetConnected(C("host"),"a",health.Read("a").Revision,true).Accepted)throw new Exception("producer refused");if(i%512==0)combat.Pump(C("host"));}
  Check(!loop.Drain().Accepted&&a.Commits==roots&&weapons.Inspect("rifle").Loaded==ammo,"R3 saturated failed sink preserves roots and ammo");
  rejectSink=false;int calls=0;while(loop.HasPendingPublication&&calls++<100)loop.Drain();
  Check(!loop.HasPendingPublication&&health.PendingTransitionCount==0&&combat.Pump(C("host")).Events.Length==0&&calls>1,"R3 repeated bounded drain empties both genuine queues");
  Check(a.Commits==roots&&weapons.Inspect("rifle").Loaded==ammo&&audio.SetEquals(ui),"R3 ACK recovery never replays actor effects");
  tick++;Now=tick*.1;Check(loop.Tick(C("host"),.1f,new[]{A("a"),A("b")},out output).Accepted,"R3 next tick admitted after saturation recovery");
  SetupR2();var s=NewR3Session();string token;
  Check(s.Join(new CommandContext("s","a","join",0,0,true),new JoinRequest(Protocol.Name,1,"content","a",null,false),out token).Accepted,"R3 genuine Pending join");
  Check(s.BeginLoading(new CommandContext("s","host","load",0,0,true),s.Rules,true).Accepted,"R3 genuine Loading");
  var bound=new HostLoop("s",1,"host",this,health,combat,items,this,s);
  a.Position=s.Get("a").Position;
  Check(!bound.AddActor("a",a).Accepted&&a.Commits==0,"R3 unloaded Pending root cannot bind");
  Check(s.Loaded(C("a"),"hash").Accepted&&s.WorldReady(C("host"),"hash").Accepted&&s.Activate(C("host")).Accepted,"R3 real loaded activation");
  Check(!bound.AddActor("a",a).Accepted,"R3 loaded but unspawned root cannot bind");
  Check(s.Spawn(C("a")).Accepted,"R3 actual spawn");a.Position=s.Get("a").Position;
  Check(bound.AddActor("a",a).Accepted,"R3 actual spawn placement binds");
  tick=1;Now=.1;Check(bound.Tick(C("host"),.1f,new[]{A("a")},out output).Accepted&&a.Commits==1,"R3 genuine spawned root publishes");
  roots=a.Commits;Check(s.EnterResults(C("host")).Accepted,"R3 genuine Results");tick++;
  Check(!bound.Tick(C("host"),.1f,new[]{A("a")},out output).Accepted&&a.Commits==roots,"R3 Results rejects before root");
  SetupR2();s=NewR3Session();s.Join(new CommandContext("s","a","join",0,0,true),new JoinRequest(Protocol.Name,1,"content","a",null,false),out token);s.BeginLoading(new CommandContext("s","host","load",0,0,true),s.Rules,true);s.Loaded(C("a"),"hash");s.WorldReady(C("host"),"hash");s.Activate(C("host"));s.Spawn(C("a"));a.Position=s.Get("a").Position;
  bound=new HostLoop("s",1,"host",this,health,combat,items,this,s);Check(bound.AddActor("a",a).Accepted,"R3 second real spawn bound");
  Check(s.Eliminate(C("a")).Accepted,"R3 actual Session elimination");tick=1;Now=.1;
  Check(!bound.Tick(C("host"),.1f,new[]{A("a")},out output).Accepted&&a.Commits==0,"R3 eliminated Session with Alive Health rejects without root");
  Now=2;Check(s.Spawn(C("a")).Accepted&&s.Get("a").SpawnRevision==2,"R3 actual Session spawn revision replacement");
  Check(!bound.Tick(C("host"),.1f,new[]{A("a")},out output).Accepted&&a.Commits==0,"R3 stale spawn identity rejects despite unchanged root Health accessors");
  SetupR2();s=NewR3Session();s.Join(new CommandContext("s","a","join",0,0,true),new JoinRequest(Protocol.Name,1,"content","a",null,true),out token);s.BeginLoading(new CommandContext("s","host","load",0,0,true),s.Rules,true);s.Loaded(C("a"),"hash");s.WorldReady(C("host"),"hash");s.Activate(C("host"));a.Position=s.Get("a").Position;
  bound=new HostLoop("s",1,"host",this,health,combat,items,this,s);Check(!s.Spawn(C("a")).Accepted&&!bound.AddActor("a",a).Accepted,"R3 real loaded Spectator never binds live root");
  SetupR2();ArmR2();roots=a.Commits;ammo=weapons.Inspect("rifle").Loaded;
  health.SetConnected(C("host"),"a",health.Read("a").Revision,false);health.SetConnected(C("host"),"a",health.Read("a").Revision,true);tick++;
  Check(!loop.Tick(C("host"),.1f,new[]{A("a",RuntimeAction.Fire,"rifle",trigger:true),A("b")},out output).Accepted&&a.Commits==roots&&weapons.Inspect("rifle").Loaded==ammo,"R3 dynamic root accessor cannot follow replacement connection generation");
  SetupR2();var command=C("host");var before=health.CreateCheckpoint(C("host"),new string('a',64));HealthService.PreparedAdvance prepared;
  Check(health.PrepareAdvance(command,out prepared).Accepted,"R3 phase12 prepared advance admission");prepared.Abort();
  Check(Convert.ToBase64String(before)==Convert.ToBase64String(health.CreateCheckpoint(C("host"),new string('a',64))),"R3 abort preserves entire Health checkpoint");
  Check(health.Advance(command).Accepted&&!health.PrepareAdvance(command,out prepared).Accepted,"R3 common replay repair rejects legacy producer command");
  Console.WriteLine("R3 gates "+checks+" PASS; CPU only");
 }
 Session NewR3Session(){return new Session("s","content",new SessionRules("map","respawn","hash",2,4,true,false,true,true,1),this,this,this,this,this);}
}
class R3Tests{static int Main(){try{new Scenario().RunR3();return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}}
