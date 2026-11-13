using System;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
static class R5Tests
{
 static int checks; static readonly string Hash=new string('a',64); static Fixture f; static SessionNetworkCoordinator s;
 static void Need(bool b,string m) {checks++;if(!b)throw new Exception(m);}
 static void Yes(RuleResult r) {Need(r.Accepted,"accept: "+r.Reason);}
 static void No(RuleResult r) {Need(!r.Accepted,"expected rejection");}
 static void Init(int capacity=2) {f=new Fixture();f.Time=0;f.Callback=null;s=new SessionNetworkCoordinator("r5",Hash,new SessionRules("arena","respawn","map",2,capacity,true,false,true,true,5),f,f,f,f,f,f,f,8,8,100);}
 static SessionReplicationHost Bind(string actor,string peer,long gen,string credential,out string token)
 {f.Records[peer]=new AuthenticatedPeer(actor,"r5",Hash,0,gen,false);var h=new SessionReplicationHost(s,f,actor,"host","r5",Hash,0);byte[] ack;Yes(h.Admit(peer,SessionReplicationHello.Encode(actor,"r5",Hash,0),new JoinRequest(Protocol.Name,1,Hash,actor,credential,false),1,out token,out ack));return h;}
 static Packet Cmd(string actor,long sequence,string id)
 {return new Packet {Kind=PacketKind.Command,SessionId="r5",ContentHash=Hash,Epoch=0,ActorId=actor,Tick=2,Sequence=sequence,CommandId=id,FeatureId="sessions.loaded/1",Payload=Encoding.UTF8.GetBytes("map")};}
 static void Send(SessionReplicationHost h,string peer,long gen,Packet p) {Yes(h.ReceiveCommand(peer,gen,Wire.Encode(p),2));RuleResult r;Need(h.DispatchOne(out r),"dispatch");Yes(r);}
 static void Departure()
 {
  Init();string token;var h=Bind("a","p",10,"",out token);Send(h,"p",10,Cmd("a",1,"sessions-host/2"));
  byte[] data;No(h.Disconnect("p",10,-1,false));Need(h.DeparturePending && s.Get("a")!=null,"pending retains member on denial");
  No(h.Publish("p",out data));No(h.ReceiveCommand("p",10,Wire.Encode(Cmd("a",2,"later")),2));RuleResult rr;Need(!h.DispatchOne(out rr),"pending blocks dispatch");
  No(h.Disconnect("p",11,2,false));No(h.Disconnect("p",10,2,true));Need(h.DeparturePending,"policy cannot change");
  f.Time=Double.NaN;No(h.Disconnect("p",10,3,false));Need(h.DeparturePending,"clock denial retains receipt");
  f.FailClock=true;bool threw=false;try {h.Disconnect("p",10,3,false);}catch(InvalidOperationException){threw=true;}Need(threw&&h.DeparturePending,"exception retains exact receipt");f.FailClock=false;
  f.Time=31;f.Records.Remove("p");Yes(h.Disconnect("p",10,4,false));Need(!h.DeparturePending && s.Get("a")==null,"cleanup without registry after delay");
  Yes(h.Disconnect("p",10,4,false));
  f.Records["p"]=new AuthenticatedPeer("a","r5",Hash,0,11,false);byte[] ack;string issued;Yes(h.Admit("p",SessionReplicationHello.Encode("a","r5",Hash,0),new JoinRequest(Protocol.Name,1,Hash,"a",token,false),5,out issued,out ack));
  Yes(h.Disconnect("p",10,5,false));Need(s.Get("a")!=null,"old completion cannot revoke replacement");No(h.ReceiveCommand("p",10,Wire.Encode(Cmd("a",2,"old")),2));
  Send(h,"p",11,Cmd("a",2,"new"));Yes(h.Disconnect("p",11,6,false));
 }
 static void Capacity()
 {Init();string token,btoken;var a=Bind("a","a",1,"",out token);var b=Bind("b","b",1,"",out btoken);byte[] state;Yes(b.Publish("b",out state));
  Yes(a.Disconnect("a",1,2,false));for(int i=0;i<130;i++){string id="fresh"+i;var h=Bind(id,id,1,"",out token);Need(s.Get(id)!=null,"fresh vacancy");Yes(h.Disconnect(id,1,3,false));}Need(s.Get("b")!=null,"other member retained");}
 static void Malformed()
 {
  Init();string token;var h=Bind("a","p",1,"",out token);f.Records["server"]=new AuthenticatedPeer("host","r5",Hash,0,1,true);var c=new SessionReplicationClient(f,"a","r5",Hash,0);c.Offer();Yes(c.Bind("server",SessionReplicationHello.Encode("host","r5",Hash,0)));
  byte[] req,res;Yes(c.Request(0,out req));Yes(h.Respond("p",req,0,out res));Yes(c.Receive("server",res));var old=c.View;long serial=c.Sequence;
  byte[][] bad={new byte[]{1,255,255,255,255,255},new byte[]{1,128,0},new byte[]{1,65},new byte[]{1,255,255,255,255,15}};
  var cases=new System.Collections.Generic.List<byte[]>(bad);byte[] valid=MemberCodec.Encode(s.Get("a"));for(int n=0;n<valid.Length;n++){byte[] prefix=new byte[n];Array.Copy(valid,prefix,n);cases.Add(prefix);}
  double now=1;
  foreach(var payload in cases){var frame=ResyncWire.EncodeState(1,0,new Packet{Kind=PacketKind.Snapshot,SessionId="r5",ContentHash=Hash,ActorId="host",Epoch=0,Sequence=serial+1,Payload=payload});No(c.Receive("server",frame));Need(Object.ReferenceEquals(old,c.View)&&c.Sequence==serial&&c.NeedsSnapshot,"unchanged malformed public Receive");Yes(c.Request(now,out req));Yes(h.Respond("p",req,now,out res));Yes(c.Receive("server",res));Need(!c.NeedsSnapshot&&c.Sequence==serial,"equal serial recovery");old=c.View;now++;}
 }
 static void Budget()
 {Init();string token;var h=Bind("a","p",1,"",out token);for(int i=1;i<=65537;i++){var p=Cmd("a",i,"flood/"+i);p.FeatureId="sessions.spawn/1";p.Payload=new byte[0];Yes(h.ReceiveCommand("p",1,Wire.Encode(p),2));RuleResult r;Need(h.DispatchOne(out r),"budget dispatch");Need(!r.Accepted,"spawn not ready");}Yes(h.Disconnect("p",1,3,false));Need(s.Get("a")==null,"trusted departure survives gameplay saturation");}
 static int Restart()
 {
  Init();string token;var h=Bind("a","p",1,"",out token);Send(h,"p",1,Cmd("a",1,"first"));
  for(int i=2;i<=67;i++)
  {
   string old=i==2?"p":"p"+(i-1);long oldGen=i-1;
   string oldActor=i==2?"a":"remapped"+(i-1);Yes(h.ReceiveCommand(old,oldGen,Wire.Encode(Cmd(oldActor,2,"queued")),2));
   Yes(h.Disconnect(old,oldGen,3,false));string actor="remapped"+i,peer="p"+i;
   h=Bind(actor,peer,i,token,out token);
   RuleResult discarded;Need(h.DispatchOne(out discarded)&&!discarded.Accepted&&discarded.Reason=="disconnected","queued retired generation discarded");
   Need(s.Get(actor).CanonicalActorId=="a","canonical identity survives remap");
   No(h.ReceiveCommand(peer,oldGen,Wire.Encode(Cmd(actor,999,"stale")),2));
   var packet=Cmd(actor,1,"first");var r=h.ReceiveCommand(peer,i,Wire.Encode(packet),2);
   if(!r.Accepted){Console.WriteLine("Sequence1 reconnect required gate: OPEN "+r.Reason);return 1;}
   No(h.ReceiveCommand(peer,i,Wire.Encode(packet),2));RuleResult domain;Need(h.DispatchOne(out domain),"real remap dispatch");Yes(domain);Need(s.Get(actor).Loaded,"real loaded mutation after sequence1");
   f.Records["server"]=new AuthenticatedPeer("host","r5",Hash,0,i,true);
   var client=new SessionReplicationClient(f,actor,"r5",Hash,0);client.Offer();Yes(client.Bind("server",SessionReplicationHello.Encode("host","r5",Hash,0)));
   byte[] req,res;Yes(client.Request(0,out req));Yes(h.Respond(peer,req,0,out res));Yes(client.Receive("server",res));Need(client.View.Actor==actor&&client.View.Loaded,"new owner bound ready member snapshot");
  }
  Console.WriteLine("PASS sequence1 remap/replay/member snapshot "+checks+" assertions; 66 reconnects");return 0;
 }
 public static int Main(string[] args) {try {if(args.Length>0)return Restart();Departure();Capacity();Malformed();Budget();Console.WriteLine("PASS R5 "+checks+" assertions; real Sessions/Networking mutations");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
