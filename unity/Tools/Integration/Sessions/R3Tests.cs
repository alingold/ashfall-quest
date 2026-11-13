using System;
using System.Collections.Generic;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
sealed class Ports : ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IAuthorityGate,ISessionPrincipal,ISessionPlacement
{
 public double Time; public double Now { get { return Time; } }
 public bool Deny,Block; public int Queries,Published,Serial;
 public Dictionary<string,string> Principals=new Dictionary<string,string>();
 public RuleResult Validate(CommandContext c) { return !Deny && c.IsAuthority ? RuleResult.Accept() : RuleResult.Reject("trusted-denial"); }
 public bool CanManage(CommandContext c) { return c.ActorId=="host"; }
 public bool Resolve(string peer,out string actor) { return Principals.TryGetValue(peer,out actor); }
 public string Issue() { return "fixture-secret-"+(++Serial); }
 public SpawnSite[] Sites(string map,bool warmup) { Queries++;return new[]{new SpawnSite("floor",new Float3(0,0,0),-1,true,false)}; }
 public bool IsSafe(string map,string actor,Float3 feet) { Queries++;return !Block && feet.Y==0; }
 public void Publish(string actor,long epoch,MemberSnapshot m) { if(m.ActorId!=actor || !m.HasPosition)throw new Exception("invalid publication"); Published++; }
}
static class R3Tests
{
 static Ports p; static SessionNetworkCoordinator b; static long tick,life; static int checks;
 static readonly string Hash=new string('a',64);
 static Dictionary<string,long> serial;
 static void Check(bool v,string why) { checks++;if(!v)throw new Exception(why); }
 static void Yes(RuleResult r) { Check(r.Accepted,"accept: "+r.Reason); }
 static void No(RuleResult r,string why) { Check(!r.Accepted && r.Reason==why,"expected "+why+" got "+r.Reason); }
 static SessionRules R(int teams=2) { return new SessionRules("arena","respawn","map",teams,8,true,false,true,true,5); }
 static void New() { tick=life=0;serial=new Dictionary<string,long>();p=new Ports();b=new SessionNetworkCoordinator("r3",Hash,R(),p,p,p,p,p,p,p,4,8,10); }
 static CommandContext L(string actor="host",long epoch=-1)
 { long n=++life;return new CommandContext("r3",actor,"sessions-lifecycle/"+n,n,epoch<0?b.Epoch:epoch,true); }
 static RuleResult Life(LifecycleAction a,SessionRules r=null) { return b.Lifecycle(L(),a,r,"map",true); }
 static byte[] Hello(string actor,string hash=null,long epoch=-1)
 { return Wire.Encode(new Packet {Kind=PacketKind.Hello,ContentHash=hash??Hash,SessionId="r3",ActorId=actor,Epoch=epoch<0?b.Epoch:epoch}); }
 static string Join(string peer,string actor,string token="",string hash=null)
 {
  p.Principals[peer]=actor;string result;
  Yes(b.Admit(peer,Hello(actor,hash),new JoinRequest(Protocol.Name,1,hash??Hash,actor,token,false),++tick,out result));return result;
 }
 static void Rehello(string peer,string actor)
 { string token;Yes(b.Admit(peer,Hello(actor),null,++tick,out token)); }
 static Packet Command(string actor,string feature,byte[] data=null,long epoch=-1)
 {
  long n;serial.TryGetValue(actor,out n);serial[actor]=++n;long t=++tick;
  return new Packet {Kind=PacketKind.Command,SessionId="r3",ContentHash=Hash,ActorId=actor,Epoch=epoch<0?b.Epoch:epoch,
   Tick=t,Sequence=n,CommandId="client/"+n,FeatureId=feature,Payload=data??new byte[0]};
 }
 static RuleResult Send(string peer,string actor,string feature,byte[] data=null)
 {
  Packet c=Command(actor,feature,data);Yes(b.Receive(peer,Wire.Encode(c),tick));RuleResult r;
  Check(b.DispatchOne(out r),"dispatch");return r;
 }
 static void Load(string peer,string actor) { Yes(Send(peer,actor,"sessions.loaded/1",Encoding.UTF8.GetBytes("map"))); }
 static void Integration()
 {
  New();p.Principals["bad"]="bad";string ignored;
  No(b.Admit("bad",Hello("bad",new string('b',64)),new JoinRequest(Protocol.Name,1,Hash,"bad","",false),++tick,out ignored),"compatibility");
  Check(b.Get("bad")==null && p.Queries==0 && p.Published==0,"incompatible cannot join/spawn");
  p.Principals["spoof"]="real";
  No(b.Admit("spoof",Hello("victim"),null,++tick,out ignored),"identity");
  string credential=Join("p","a");Join("q","b");Check(b.Get("a").Team==0 && b.Get("b").Team==1,"balanced join");
  No(Send("p","a","sessions.spawn/1"),"not-ready");Check(p.Queries==0,"preload no world query");
  No(((ICommandSink)b).Apply(new CommandContext("r3","a","fake",++tick,b.Epoch,true),"sessions.spawn/1",new byte[0]),"coordinator-dispatch-required");
  Yes(Life(LifecycleAction.BeginLoading,R(4)));Check(b.Epoch==1 && b.Get("a").Team==0 && b.Get("b").Team==1,"redistribution stable");
  Packet stale=Command("a","sessions.loaded/1",Encoding.UTF8.GetBytes("map"),0);
  No(b.Receive("p",Wire.Encode(stale),tick),"not-admitted");
  Rehello("p","a");Rehello("q","b");No(b.Receive("p",Wire.Encode(stale),tick),"session-epoch");
  No(Send("p","a","sessions.loaded/1",Encoding.UTF8.GetBytes("wrong")),"map-cache-mismatch");
  Load("p","a");Load("q","b");Yes(Life(LifecycleAction.WorldReady));Yes(Life(LifecycleAction.Activate));
  No(Send("p","a","sessions.eliminate/1"),"session-feature");
  p.Block=true;No(Send("p","a","sessions.spawn/1"),"no-safe-spawn");Check(p.Published==0,"failed placement not published");p.Block=false;
  Yes(Send("p","a","sessions.spawn/1"));Check(p.Published==1 && b.Get("a").SpawnRevision==1,"accepted root publication");
  Yes(b.Disconnect("p",++tick,false));Join("p2","returned",credential);Load("p2","returned");Yes(Send("p2","returned","sessions.spawn/1"));
  Check(b.Get("returned").SpawnRevision==1 && b.Get("returned").Life==SessionLife.Alive,"reconnect preserves life/revision");
  Yes(b.Eliminate(new CommandContext("r3","returned","trusted-death",++tick,b.Epoch,true)));
  No(Send("p2","returned","sessions.spawn/1"),"respawn-not-due");p.Time+=5;
  Yes(Send("p2","returned","sessions.spawn/1"));Check(b.Get("returned").SpawnRevision==2 && b.Get("returned").Deaths==1,"respawn after clock deadline");
  Packet duplicate=Command("returned","sessions.spawn/1");Yes(b.Receive("p2",Wire.Encode(duplicate),tick));No(b.Receive("p2",Wire.Encode(duplicate),tick),"replay");
  RuleResult dispatched;Check(b.DispatchOne(out dispatched) && dispatched.Accepted,"single duplicate dispatch");
  Yes(Life(LifecycleAction.BeginLoading,R(2)));Check(b.Get("returned").Team==1 && !b.Get("returned").Loaded,"new layout sorted actors");
  p.Time+=61;long epoch=b.Epoch;CommandContext denied=L("returned");
  No(b.Lifecycle(denied,LifecycleAction.AdvanceTime,null,null,false),"manager-required");Check(b.Epoch==epoch,"denied timeout no mutation");
  p.Deny=true;No(Life(LifecycleAction.AdvanceTime),"trusted-denial");p.Deny=false;
  CommandContext recovery=L();Yes(b.Lifecycle(recovery,LifecycleAction.AdvanceTime,null,null,false));
  Check(b.Epoch==epoch+1 && b.Stage==SessionStage.Lobby && !b.Get("returned").Loaded,"timeout synchronizes epoch");
  No(b.Lifecycle(recovery,LifecycleAction.AdvanceTime,null,null,false),"lifecycle-context");
  Rehello("p2","returned");Packet late=Command("returned","sessions.loaded/1",Encoding.UTF8.GetBytes("map"),epoch);
  No(b.Receive("p2",Wire.Encode(late),tick),"session-epoch");
  No(Send("p2","returned","sessions.spawn/1"),"not-ready");Load("p2","returned");Yes(Life(LifecycleAction.WorldReady));Yes(Send("p2","returned","sessions.spawn/1"));
 }
 static void Saturation()
 {
  New();Join("p","a");Yes(Life(LifecycleAction.BeginLoading,R()));Rehello("p","a");
  // Actual production Wire -> Receive -> queue -> DispatchOne -> Session on every attempt.
  for(int i=0;i<65536;i++)
  {
   Packet c=Command("a","sessions.spawn/1");RuleResult r=b.Receive("p",Wire.Encode(c),tick);
   if(!r.Accepted || !b.DispatchOne(out r) || r.Reason!="not-ready")throw new Exception("saturation "+i+" "+r.Reason);
   p.Time+=0.0001;
  }
  No(Send("p","a","sessions.spawn/1"),"epoch-command-budget");
  No(b.Lifecycle(L("a"),LifecycleAction.AdvanceTime,null,null,false),"manager-required");
  p.Time+=LoadingTimeout();Yes(Life(LifecycleAction.AdvanceTime));Check(b.Stage==SessionStage.Lobby && b.Epoch==2,"saturated network recovery");
  Rehello("p","a");Load("p","a");Yes(Life(LifecycleAction.WorldReady));Yes(Send("p","a","sessions.spawn/1"));
  long epoch=b.Epoch;
  // Sustained lifecycle traffic cannot fill a receipt collection.
  for(int i=0;i<70000;i++) { RuleResult r=Life(LifecycleAction.AdvanceTime);if(!r.Accepted)throw new Exception("heartbeat "+r.Reason); }
  Check(b.Epoch==epoch && b.LifecycleSequence==life,"bounded lifecycle sustained");
  CommandContext replay=new CommandContext("r3","host","sessions-lifecycle/"+life,life,b.Epoch,true);
  No(b.Lifecycle(replay,LifecycleAction.BeginLoading,R(),"map",true),"lifecycle-replay");
  No(b.Lifecycle(new CommandContext("r3","host","rebound",life+1,b.Epoch,true),LifecycleAction.BeginLoading,R(),"map",true),"lifecycle-context");
  CommandContext denied=L();p.Deny=true;
  No(b.Lifecycle(denied,LifecycleAction.BeginLoading,R(),"map",true),"trusted-denial");p.Deny=false;
  Yes(b.Lifecycle(denied,LifecycleAction.BeginLoading,R(),"map",true));
  Check(b.Epoch==epoch+1,"denied lifecycle does not poison receipt");
  RuleResult empty;Check(!b.DispatchOne(out empty) && empty.Reason=="empty","epoch drains old pending work");
  Rehello("p","a");Load("p","a");Yes(Life(LifecycleAction.WorldReady));Yes(Life(LifecycleAction.Activate));
  Packet pending=Command("a","sessions.spawn/1");Yes(b.Receive("p",Wire.Encode(pending),tick));
  Yes(b.Disconnect("p",++tick,false));
  Check(b.DispatchOne(out empty) && empty.Reason=="disconnected","disconnect invalidates queued generation");
  CommandContext client=new CommandContext("r3","host","sessions-lifecycle/"+(life+1),life+1,b.Epoch,false);
  No(b.Lifecycle(client,LifecycleAction.AdvanceTime,null,null,false),"trusted-denial");
  p.Time-=1;No(Life(LifecycleAction.AdvanceTime),"clock");p.Time+=1;
 }
 static double LoadingTimeout() { return Session.LoadingTimeoutSeconds+1; }
 public static int Main() { try { Integration();Saturation();Console.WriteLine("PASS r3 "+checks+" checks + 65536 packet saturation + 70000 lifecycle steps");return 0; }catch(Exception e){Console.WriteLine(e);return 1;} }
}
