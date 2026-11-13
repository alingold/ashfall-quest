using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
sealed class Fixture : ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IAuthorityGate,ISessionPrincipal,ISessionPlacement,IAuthenticatedPeerPort
{
 public double Time=0; public double Now { get { return Time; } }
 public int Tokens,Published; public Action Callback;
 public readonly Dictionary<string,AuthenticatedPeer> Records=new Dictionary<string,AuthenticatedPeer>();
 public bool TryResolve(string c,out AuthenticatedPeer p) { if(Callback!=null)Callback(); return c!=null && Records.TryGetValue(c,out p) ? true : Missing(out p); }
 private static bool Missing(out AuthenticatedPeer p) {p=null;return false;}
 public bool Resolve(string peer,out string actor) { AuthenticatedPeer p;bool ok=TryResolve(peer,out p);actor=ok?p.Actor:null;return ok; }
 public string Issue() {return "test-only-token-"+(++Tokens);}
 public RuleResult Validate(CommandContext c) {return c.IsAuthority && c.ActorId=="host"?RuleResult.Accept():RuleResult.Reject("authority");}
 public bool CanManage(CommandContext c) {return c.ActorId=="host";}
 public SpawnSite[] Sites(string map,bool warmup) {return new[]{new SpawnSite("floor",new Float3(1,0,2),-1,true,false)};}
 public bool IsSafe(string map,string actor,Float3 p) {return p.Y==0;}
 public void Publish(string actor,long epoch,MemberSnapshot m) {Published++;}
}
static class Tests
{
 static int checks; static long tick,serial; static Fixture f; static SessionNetworkCoordinator s; static SessionReplicationHost h; static SessionReplicationClient c;
 static readonly string Hash=new string('a',64); static string token;
 static void Check(bool b,string reason) {checks++;if(!b)throw new Exception(reason);}
 static void Yes(RuleResult r) {Check(r.Accepted,"accept "+r.Reason);}
 static void No(RuleResult r,string reason=null) {Check(!r.Accepted && (reason==null || r.Reason==reason),"reject "+reason+" got "+r.Reason);}
 static byte[] Transfer(UdpClient from,UdpClient to,byte[] data)
 {from.Send(data,data.Length,(IPEndPoint)to.Client.LocalEndPoint); IPEndPoint source=new IPEndPoint(IPAddress.Any,0); byte[] got=to.Receive(ref source); Check(source.Equals(from.Client.LocalEndPoint),"actual loopback endpoint");return got;}
 static void New()
 {
  tick=serial=0; f=new Fixture(); s=new SessionNetworkCoordinator("sr4",Hash,new SessionRules("arena","respawn","map",2,8,true,false,true,true,5),f,f,f,f,f,f,f,2,4,100);
  h=new SessionReplicationHost(s,f,"a","host","sr4",Hash,0); c=new SessionReplicationClient(f,"a","sr4",Hash,0); Records(1);
 }
 static void Records(long g)
 { f.Records["client"]=new AuthenticatedPeer("a","sr4",Hash,0,g,false);f.Records["server"]=new AuthenticatedPeer("host","sr4",Hash,0,g,true); }
 static void Bind(UdpClient client,UdpClient host,string credential="")
 {
  byte[] response;string issued; byte[] offer=Transfer(client,host,c.Offer());
  Yes(h.Admit("client",offer,new JoinRequest(Protocol.Name,1,Hash,"a",credential,false),++tick,out issued,out response));
  if(issued!=null)token=issued;
  Yes(c.Bind("server",Transfer(host,client,response)));
 }
 static byte[] Recover(UdpClient client,UdpClient host,double now,bool apply=true)
 {
  byte[] req,res; Yes(c.Request(now,out req)); Yes(h.Respond("client",Transfer(client,host,req),now,out res));
  byte[] got=Transfer(host,client,res);if(apply)Yes(c.Receive("server",got));return got;
 }
 static Packet Command(string feature)
 {long t=++tick;return new Packet {Kind=PacketKind.Command,SessionId="sr4",ContentHash=Hash,Epoch=s.Epoch,ActorId="a",Tick=t,Sequence=++serial,CommandId="cmd/"+serial,FeatureId=feature};}
 static RuleResult Send(UdpClient client,UdpClient host,Packet p)
 { Yes(h.ReceiveCommand("client",Transfer(client,host,Wire.Encode(p)),p.Tick));RuleResult r;Check(h.DispatchOne(out r),"dispatch actual Sessions");return r; }
 static byte[] State(long generation,long request,long sequence,long baseSeq,PacketKind kind,byte[] payload,long epoch=0)
 {return ResyncWire.EncodeState(generation,request,new Packet {Kind=kind,ActorId="host",SessionId="sr4",ContentHash=Hash,Epoch=epoch,Sequence=sequence,BaseSequence=baseSeq,Payload=payload});}
 static void Run(UdpClient client,UdpClient host)
 {
  New();byte[] response,unused;string issued;
  No(c.Receive("server",new byte[0]));No(c.Bind("server",SessionReplicationHello.Encode("host","sr4",Hash,0)),"negotiation");
  byte[] offer=c.Offer();Packet legacy;Check(Wire.TryDecode(offer,out legacy),"hello decode");legacy.FeatureId="";
  No(h.Admit("client",Wire.Encode(legacy),null,++tick,out issued,out response),"negotiation");Check(s.Get("a")==null && f.Tokens==0,"no admission before negotiation");
  f.Records["client"]=new AuthenticatedPeer("forged","sr4",Hash,0,1,false);
  No(h.Admit("client",offer,null,++tick,out issued,out response),"negotiation");Records(1);
  Bind(client,host);No(c.Receive("server",State(1,2,0,0,PacketKind.Snapshot,new byte[0])),"resync-request");No(h.Publish("attacker",out response));No(c.Receive("attacker",new byte[0]));
  byte[] first=Recover(client,host,0);Check(c.Sequence==0 && c.View.Actor=="a" && !c.View.Loaded,"actual joined member roundtrip");
  No(c.Receive("server",first));
  long fg,fi;byte[] fb;Packet forged;Check(ResyncWire.TryDecodeState(first,out fg,out fi,out fb) && Wire.TryDecode(fb,out forged),"state decode");
  Wire.TryDecode(fb,out forged);forged.ActorId="attacker";No(c.Receive("server",ResyncWire.EncodeState(fg,0,forged)),"authority-actor");
  No(((IReplicationSink)c).Apply(true,999,new byte[0]),"member-snapshot");
  // Lost/missing base: omit6; deliver7/base6, then late6 and duplicate7.
  No(c.Receive("server",Transfer(host,client,State(1,0,7,6,PacketKind.Delta,new byte[0]))),"snapshot-required");
  byte[] req;Yes(c.Request(0.1,out req));ResyncRequest pending;Check(ResyncWire.TryDecodeRequest(req,out pending),"request decode");
  No(c.Receive("server",State(1,0,6,0,PacketKind.Delta,new byte[0])),"snapshot-required");
  Yes(h.Respond("client",Transfer(client,host,req),0.3,out response)); // deliberately lose response
  No(c.Request(0.9,out unused),"retry-time");Yes(c.Request(1.1,out unused));Check(Convert.ToBase64String(req)==Convert.ToBase64String(unused),"retry keeps exact request");
  No(h.Respond("client",req,0.4,out unused),"rate");
  Yes(h.Respond("client",Transfer(client,host,req),1.1,out response));
  No(c.Receive("server",State(1,pending.RequestId+1,90,0,PacketKind.Snapshot,new byte[0])),"resync-request");
  No(c.Receive("server",State(1,pending.RequestId,90,0,PacketKind.Snapshot,new byte[0],1)),"compatibility");
  Yes(c.Receive("server",Transfer(host,client,response)));Check(c.Sequence==0 && !c.NeedsSnapshot,"equal serial recovery");No(c.Receive("server",response));
  // Real loaded/spawn mutation through production queues, then member publication.
  Packet loaded=Command("sessions.loaded/1");loaded.Payload=Encoding.UTF8.GetBytes("map");Yes(Send(client,host,loaded));
  Yes(s.Lifecycle(new CommandContext("sr4","host","sessions-lifecycle/1",1,0,true),LifecycleAction.WorldReady,null,"map",false));
  Packet spawn=Command("sessions.spawn/1");Yes(Send(client,host,spawn));Check(f.Published==1,"actual placement callback");
  No(h.ReceiveCommand("client",Wire.Encode(spawn),spawn.Tick),"replay");Yes(h.Publish("client",out response));Yes(c.Receive("server",Transfer(host,client,response)));
  Check(c.View.Loaded && c.View.HasPosition && c.View.Position.X==1 && c.View.Revision==1 && c.Sequence==1,"actual Sessions source state");
  // Invalid domain full must leave immutable view and receiver watermark untouched.
  SessionReplica old=c.View; No(c.Receive("server",State(1,0,2,0,PacketKind.Snapshot,new byte[]{255})),"member-snapshot");Check(Object.ReferenceEquals(old,c.View) && c.Sequence==1 && c.NeedsSnapshot,"atomic rejection");
  byte[] recovery; Yes(c.Request(2.2,out recovery));ResyncRequest rrq;Check(ResyncWire.TryDecodeRequest(recovery,out rrq),"domain recovery request");
  No(c.Receive("server",State(1,rrq.RequestId,0,0,PacketKind.Snapshot,new byte[0])),"stale-state");
  Yes(h.Respond("client",Transfer(client,host,recovery),2.2,out response));Yes(c.Receive("server",Transfer(host,client,response)));Check(c.Sequence==1,"equal recovery after domain rejection");
  No(c.Receive("server",State(1,rrq.RequestId,900,0,PacketKind.Snapshot,new byte[0])),"resync-request");
  No(h.Respond("client",ResyncWire.EncodeRequest(new ResyncRequest(1,rrq.RequestId,100)),2.5,out unused),"request-replay");
  No(h.Respond("client",ResyncWire.EncodeRequest(new ResyncRequest(1,rrq.RequestId+1,100)),2.5,out unused),"minimum-sequence");
  No(h.Respond("client",recovery,2.4,out unused),"clock");
  // Guard malicious trusted-port callback attempts (does not establish authentication).
  f.Callback=delegate {No(c.Disconnect("server",1),"reentry");No(c.Receive("server",first),"reentry");No(((IReplicationSink)c).Apply(true,500,new byte[0]),"member-snapshot");};
  No(c.Receive("server",first)); f.Callback=null;Check(!c.NeedsSnapshot && c.Sequence==1,"reentry cannot regress binding");
  byte[] oldFrame=response;
  // Reconnect, stale generation at arbitrarily high serial, and old request replay.
  for(int g=2;g<=66;g++)
  {
   Packet queued=Command("sessions.spawn/1"); Yes(h.ReceiveCommand("client",Transfer(client,host,Wire.Encode(queued)),queued.Tick));
   Yes(c.Disconnect("server",g-1));Yes(h.Disconnect("client",g-1,++tick,false));RuleResult drop;Check(!h.DispatchOne(out drop) && drop.Reason=="principal","revoked connection cannot dispatch queued work");
   Records(g);
   if(g==2) {c.Offer();f.Records["server"]=new AuthenticatedPeer("replacement","sr4",Hash,0,g,true);No(c.Bind("server",SessionReplicationHello.Encode("replacement","sr4",Hash,0)),"negotiation");Records(g);}
   Bind(client,host,token);Check(h.DispatchOne(out drop) && !drop.Accepted && drop.Reason=="disconnected","old queued generation discarded after readmission");No(c.Receive("server",Transfer(host,client,oldFrame)));No(h.Respond("client",req,g+3,out unused),"request-generation");
   No(c.Receive("server",State(g-1,0,Int64.MaxValue,0,PacketKind.Snapshot,new byte[0])));
   oldFrame=Recover(client,host,g+3);Check(c.View.Revision==1 && !c.View.Loaded && c.Sequence==2,"reconnect retains domain state and watermark");
  }
  // Fill actual command queue; no successful fixture sink substitutes for Sessions.
  Packet a=Command("sessions.spawn/1"),b=Command("sessions.spawn/1"),d=Command("sessions.spawn/1");
  Yes(h.ReceiveCommand("client",Transfer(client,host,Wire.Encode(a)),a.Tick));Yes(h.ReceiveCommand("client",Transfer(client,host,Wire.Encode(b)),b.Tick));
  No(h.ReceiveCommand("client",Transfer(client,host,Wire.Encode(d)),d.Tick),"backpressure");RuleResult result;Check(h.DispatchOne(out result),"drain1");Check(h.DispatchOne(out result),"drain2");
  No(c.Request(Double.NaN,out unused),"clock");No(h.Respond("client",req,Double.PositiveInfinity,out unused),"clock");
  // Saturate actual Session receipts through actual UDP, codecs and command queue.
  int flood=0;
  for(;flood<65537;flood++)
  {
   Packet q=Command("sessions.spawn/1");RuleResult rr=Send(client,host,q);
   if(rr.Reason=="epoch-command-budget")break;
   Check(!rr.Accepted && rr.Reason=="not-ready","saturation invokes actual Session.Spawn");
  }
  Check(flood<65537,"receipt budget reached");
  No(h.Disconnect("client",66,++tick,false),"epoch-command-budget");
  No(h.Publish("client",out unused),"principal");
  No(h.Admit("client",c.Offer(),null,++tick,out issued,out unused),"generation");
  // Registry revocation and epoch transition fail closed before any payload.
  f.Records.Remove("server");No(c.Receive("server",oldFrame),"principal");Records(66);
  Yes(s.Lifecycle(new CommandContext("sr4","host","sessions-lifecycle/2",2,0,true),LifecycleAction.BeginLoading,new SessionRules("arena","respawn","map",2,8,true,false,true,true,5),"map",true));
  No(h.Publish("client",out unused),"principal");Check(c.Sequence==2,"no state regression");
 }
 public static int Main()
 {
  try { using(var client=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))) using(var host=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
  {client.Client.ReceiveTimeout=host.Client.ReceiveTimeout=2000;Run(client,host);}Console.WriteLine("PASS SessionReplication r4 "+checks+" assertions; 65 reconnects; two ephemeral IPv4 loopback endpoints");return 0; }
  catch(Exception e) {Console.WriteLine(e);return 1;}
 }
}






