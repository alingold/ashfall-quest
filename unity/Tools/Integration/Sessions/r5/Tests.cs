using System;
using System.Collections.Generic;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
sealed class Ports : ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IAuthorityGate,ISessionPrincipal,ISessionPlacement
{
 public double Time; public double Now {get{return Time;}} public int Issued,Published;
 public Dictionary<string,string> Ids=new Dictionary<string,string>();
 public string Issue(){return "secret-"+(++Issued);}
 public bool CanManage(CommandContext c){return c.ActorId=="host";}
 public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("auth");}
 public bool Resolve(string p,out string a){return Ids.TryGetValue(p,out a);}
 public SpawnSite[] Sites(string m,bool w){return new[]{new SpawnSite("floor",new Float3(0,0,0),-1,true,false)};}
 public bool IsSafe(string m,string a,Float3 f){return true;}
 public void Publish(string a,long e,MemberSnapshot s){Published++;}
}
static class Tests
{
 static readonly string H=new string('a',64); static int n; static long tick,seq;
 static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static void Yes(RuleResult r){Check(r.Accepted,r.Reason);}
 static SessionRules Rules(int cap){return new SessionRules("arena","respawn","map",2,cap,true,false,true,true,5);}
 static byte[] Hello(string a,long e){return Wire.Encode(new Packet{Kind=PacketKind.Hello,ActorId=a,SessionId="test",ContentHash=H,Epoch=e});}
 static JoinRequest Req(string token){return new JoinRequest(Protocol.Name,1,H,"name",token,false);}
 static CommandContext C(string a,string id,long e){return new CommandContext("test",a,id,++tick,e,true);}
 static RuleResult Join(SessionNetworkCoordinator b,Ports p,string peer,string actor,string token,out string issued)
 {p.Ids[peer]=actor;return b.Admit(peer,Hello(actor,b.Epoch),Req(token),++tick,out issued);}
 static RuleResult Send(SessionNetworkCoordinator b,string peer,string actor,string id,string feature,long epoch=-1)
 {
  Packet x=new Packet{Kind=PacketKind.Command,ActorId=actor,SessionId="test",ContentHash=H,Epoch=epoch<0?b.Epoch:epoch,Tick=++tick,Sequence=++seq,CommandId=id,FeatureId=feature,Payload=feature=="sessions.loaded/1"?Encoding.UTF8.GetBytes("map"):new byte[0]};
  RuleResult r=b.Receive(peer,Wire.Encode(x),tick);if(!r.Accepted)return r;
  Check(b.DispatchOne(out r),"dispatch");return r;
 }
 static SessionNetworkCoordinator New(Ports p,int cap){return new SessionNetworkCoordinator("test",H,Rules(cap),p,p,p,p,p,p,p,4,8,10);}
 static void Receipts()
 {
  var p=new Ports(); var b=New(p,2);string token;Yes(Join(b,p,"p","a","",out token));long g=b.ConnectionGeneration("p");
  Yes(Send(b,"p","a","sessions-admit/2","sessions.loaded/1"));
  for(int i=0;i<20;i++)Check(!Send(b,"p","a","sessions-host/"+i,"sessions.spawn/1").Accepted,"no premature spawn");
  Yes(b.Disconnect("p",g,++tick,false));Yes(b.Disconnect("p",g,++tick,false));Check(b.Get("a")==null,"departure");
  Yes(Join(b,p,"p","a",token,out token));Check(b.ConnectionGeneration("p")>g,"generation increases");
  // Exact old retry is idempotent and cannot remove newly admitted membership.
  Yes(b.Disconnect("p",g,++tick,false));Check(b.Get("a")!=null,"late duplicate preserves reconnect");
  Check(!b.Disconnect("p",g,++tick,true).Accepted,"old generation kick denied");
  Yes(b.Disconnect("p",b.ConnectionGeneration("p"),++tick,false));
  Yes(Join(b,p,"q","remapped",token,out token));Check(b.Get("a")==null && b.Get("remapped").CanonicalActorId=="a","canonical reconnect identity");
  Check(Send(b,"q","remapped","sessions-admit/2","sessions.loaded/1").Reason=="replay","canonical replay survives remapping");
  Check(!Send(b,"q","remapped","late","sessions.loaded/1",99).Accepted,"forged epoch");
 }
 static void ReconnectOrdering()
 {
  var p=new Ports();var b=New(p,2);string token;Yes(Join(b,p,"p","a","",out token));
  Yes(b.Lifecycle(new CommandContext("test","host","sessions-lifecycle/1",1,0,true),LifecycleAction.BeginLoading,Rules(2),"map",true));
  string unused;Yes(b.Admit("p",Hello("a",b.Epoch),null,++tick,out unused));
  Yes(Send(b,"p","a","load/first","sessions.loaded/1"));
  Yes(b.Lifecycle(new CommandContext("test","host","sessions-lifecycle/2",2,b.Epoch,true),LifecycleAction.WorldReady,null,"map",true));
  Yes(b.Lifecycle(new CommandContext("test","host","sessions-lifecycle/3",3,b.Epoch,true),LifecycleAction.Activate,null,"map",true));
  Yes(Send(b,"p","a","spawn/first","sessions.spawn/1"));
  Yes(b.RecordPosition(C("a","root",b.Epoch),new Float3(5,0,7)));
  string actor="a",peer="p";
  for(int i=0;i<130;i++)
  {
   // Queued work from a departed generation must never reach the domain.
   Packet old=new Packet{Kind=PacketKind.Command,ActorId=actor,SessionId="test",ContentHash=H,Epoch=b.Epoch,Tick=++tick,Sequence=++seq,CommandId="pending/"+i,FeatureId="sessions.spawn/1",Payload=new byte[0]};
   Yes(b.Receive(peer,Wire.Encode(old),tick));long generation=b.ConnectionGeneration(peer);
   Yes(b.Disconnect(peer,generation,++tick,false));string previous=actor;actor="remap"+i;
   Yes(Join(b,p,peer,actor,token,out token));RuleResult discarded;Check(b.DispatchOne(out discarded) && discarded.Reason=="disconnected","old queued generation discarded");
   Check(b.Receive(peer,generation,Wire.Encode(old),tick).Reason=="connection-generation","late callback cannot acquire new lease");
   Check(b.Get(previous)==null && b.Get(actor).CanonicalActorId=="a","remap cleanup");
   Check(!Send(b,peer,actor,"early/"+i,"sessions.spawn/1").Accepted,"spawn before new load denied");
   Yes(Send(b,peer,actor,"load/"+i,"sessions.loaded/1"));Yes(Send(b,peer,actor,"spawn/"+i,"sessions.spawn/1"));
   Check(b.Get(actor).Position.X==5 && b.Get(actor).SpawnRevision==1,"actual moved root retained");
   Yes(b.Disconnect(peer,generation,++tick,false));Check(b.Get(actor)!=null,"late duplicate no revoke");
  }
  long epoch=b.Epoch;Yes(b.Lifecycle(new CommandContext("test","host","sessions-lifecycle/4",4,epoch,true),LifecycleAction.BeginLoading,Rules(2),"map",true));
  p.Time=61;Yes(b.Lifecycle(new CommandContext("test","host","sessions-lifecycle/5",5,b.Epoch,true),LifecycleAction.AdvanceTime,null,null,false));
  Yes(b.Admit(peer,Hello(actor,b.Epoch),null,++tick,out unused));
  Check(Send(b,peer,actor,"late-ack","sessions.loaded/1",epoch+1).Reason=="session-epoch","late loading ack rejected");
  Check(!b.Get(actor).Loaded && b.Get(actor).SpawnRevision==0,"timeout invalidates round state");
 }
 static void SaturatedDeparture()
 {
  var p=new Ports();var b=New(p,2);string token;Yes(Join(b,p,"p","a","",out token));
  for(int i=0;i<65536;i++)Check(Send(b,"p","a","pressure/"+i,"sessions.spawn/1").Reason=="not-ready","pressure outcome");
  Check(Send(b,"p","a","over","sessions.spawn/1").Reason=="epoch-command-budget","real budget");
  Yes(b.Disconnect("p",++tick,false));Check(b.Get("a")==null,"saturated disconnect succeeds");
  Yes(Join(b,p,"q","b",token,out token));Check(b.Get("b").CanonicalActorId=="a","saturated admission independent");
 }
 static void DomainChurn()
 {
  var p=new Ports();var s=new Session("test",H,Rules(2),p,p,p,p,p);string token;long receipt=0;
  for(int i=0;i<130;i++)
  {
   string actor="actor"+i;var c=new CommandContext("test",actor,"sessions-host/"+(++receipt),receipt,0,true);
   Yes(s.JoinTrusted(c,Req(""),out token));Check(s.Get(actor).CanonicalActorId==actor,"fresh identity");
   c=new CommandContext("test",actor,"sessions-host/"+(++receipt),receipt,0,true);Yes(s.DisconnectTrusted(c,true));Check(s.Count==0,"cleanup");
   Check(s.DisconnectTrusted(c,true).Reason=="host-replay","trusted replay");
  }
  p.Time=0;
 }
 static void Capacity()
 {
  var p=new Ports();var b=New(p,2);string a,z;
  Yes(Join(b,p,"p","a","",out a));Yes(Join(b,p,"q","b","",out z));Yes(b.Disconnect("p",++tick,false));
  RuleResult r=Join(b,p,"r","returned",a,out z);
  Check(r.Accepted,"BLOCKED full roster remapped reconnect: "+r.Reason);
  Check(Join(b,p,"overflow","overflow","",out z).Reason=="peer-capacity" && b.Get("overflow")==null,"full roster rejection preserves domain");
  for(int i=0;i<130;i++){Yes(b.Disconnect(i==0?"r":"new",++tick,true));Yes(Join(b,p,"new","new"+i,"",out z));}
 }
 public static int Main(string[] args){try{if(args.Length>0)Capacity();else{Receipts();ReconnectOrdering();SaturatedDeparture();DomainChurn();}Console.WriteLine("PASS "+n+" assertions");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
