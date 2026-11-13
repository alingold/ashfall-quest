using System;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Player;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
sealed class HealthWorld:IHealthWorld,IInteractionQuery
{
 public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return true;}}
 public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
 public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}
}
static class R8Tests
{
 static int checks;static string Hash=new string('a',64);static Fixture f;static SessionNetworkCoordinator s;static HealthService health;static SessionSourceObservations source;
 static void Need(bool v,string why){checks++;if(!v)throw new Exception(why);}static void Yes(RuleResult r){Need(r.Accepted,"accept "+r.Reason);}static void No(RuleResult r){Need(!r.Accepted,"reject");}
 static CommandContext Host(long tick,long epoch=0){return new CommandContext("r8","host","host-"+tick,tick,epoch,true);}
 static void Init(){f=new Fixture();f.Callback=null;s=new SessionNetworkCoordinator("r8",Hash,new SessionRules("arena","respawn","map",2,64,true,false,true,true,5),f,f,f,f,f,f,f,8,8,100);var w=new HealthWorld();health=new HealthService("r8",0,2,true,f,w,w);source=new SessionSourceObservations(s,health);}
 static SourceStateHost Add(string actor,string peer,long g,string token,out string issued){f.Records[peer]=new AuthenticatedPeer(actor,"r8",Hash,s.Epoch,g,false);var h=new SourceStateHost(s,source,f,actor,"host","r8",Hash,s.Epoch);byte[] ack;Yes(h.Admit(peer,SourceStateHello.Encode(actor,"r8",Hash,s.Epoch),new JoinRequest(Protocol.Name,1,Hash,actor,token,false),1,out issued,out ack));return h;}
 static SourceStateClient Client(long g){f.Records["server"]=new AuthenticatedPeer("host","r8",Hash,s.Epoch,g,true);var c=new SourceStateClient(f,"a","r8",Hash,s.Epoch);c.Offer(s.Epoch);Yes(c.Bind("server",SourceStateHello.Encode("host","r8",Hash,s.Epoch)));return c;}
 static byte[][] Full(SourceStateHost h,SourceStateClient c,double now,long tick){byte[] q;Yes(c.Request(now,out q));byte[][] fs;Yes(h.Respond("pa",q,Host(tick),now,out fs));return fs;}
 static void Deliver(SourceStateClient c,byte[][] fs){foreach(byte[] frame in fs){Need(frame.Length<=1000,"frozen frame cap");Yes(c.Receive("server",frame));}}
 static void Register(string a,long tick){Yes(health.Register(Host(tick),a,s.Get(a).Team,false));}
 static byte[] Rewrite(byte[] frame,Action<Packet> edit,long? generation=null)
 {long g,id;byte[] b;Need(ResyncWire.TryDecodeState(frame,out g,out id,out b),"decode frame");Packet p;Need(Wire.TryDecode(b,out p),"decode packet");edit(p);return ResyncWire.EncodeState(generation??g,id,p);}
 static void Flow()
 {
  Init();string ta,tb;var a=Add("a","pa",10,"",out ta);var b=Add("b","pb",20,"",out tb);Register("a",1);Register("b",2);
  var player=new PlayerFrameOwner(new PlayerInput());player.Observe(new PlayerLifecycleState{Session="r8",Actor="a",Epoch=0,Enabled=true,Focused=true,RigValid=true});
  player.Publish(new InputFrame{Tick=5,Sequence=1,Tracked=true,HeadPosition=new Float3(.1f,1.7f,.2f),LeftHandPosition=new Float3(1,1,1)});Yes(source.BindPlayer(Host(5),"a",player));
  SourceStateView unloaded;Yes(source.Capture(Host(5),out unloaded));Need(!unloaded.Members[0].HasHead,"unloaded head unavailable");
  byte[] loaded=Wire.Encode(new Packet{Kind=PacketKind.Command,ActorId="a",SessionId="r8",ContentHash=Hash,Epoch=0,Sequence=1,Tick=2,CommandId="load-1",FeatureId="sessions.loaded/1",Payload=Encoding.UTF8.GetBytes("map")});
  Yes(a.ReceiveCommand("pa",10,loaded,2));RuleResult dispatched;Need(a.DispatchOne(out dispatched),"actual queued loaded");Yes(dispatched);
  var c=Client(10);byte[][] initial=Full(a,c,0,5);Deliver(c,initial);Need(c.View.Members.Length==2,"real roster");var m=c.View.Members[0];Need(m.Canonical=="a"&&m.HasHead&&m.Head.Y==1.7f&&!m.HasLeftHand&&!m.HasRightHand,"actual head explicit missing hands");Need(m.HasHealth&&m.Health==100&&m.HealthRevision==health.Read("a").Revision,"real health");
  var old=c.View;long seq=c.Sequence;No(c.Receive("server",initial[0]));Need(ReferenceEquals(old,c.View)&&seq==c.Sequence,"replay atomic");
  player.Invalidate();byte[][] delta;Yes(a.Publish("pa",Host(6),out delta));Deliver(c,delta);Need(!c.View.Members[0].HasHead,"actual invalidation clears pose");
  Yes(b.Disconnect("pb",20,7,false));Yes(a.Publish("pa",Host(7),out delta));Deliver(c,delta);Need(c.View.Members.Length==1,"actual departure delta");
  f.Records.Remove("pa");Add("c","pc",30,"",out tb);No(a.Publish("pa",Host(8),out delta));Need(delta==null,"departed recipient blocked");
  f.Records["pa"]=new AuthenticatedPeer("a","r8",Hash,0,11,false);No(a.Publish("pa",Host(8),out delta));
  Yes(a.Disconnect("pa",10,9,false));var remapped=Add("remapped-a","pa",12,ta,out ta);Need(remapped!=null,"canonical remap admitted");SourceStateView remap;
  Yes(source.Capture(Host(9),out remap));SourceMemberState found=null;foreach(var member in remap.Members)if(member.Actor=="remapped-a")found=member;
  Need(found!=null&&found.Canonical=="a"&&found.HasHealth&&found.HealthRevision==health.Read("a").Revision&&!found.HasHead,"real canonical Health remap with unavailable old pose");
 }
 static void FragmentPressure()
 {
  Init();string t;var a=Add("a","pa",10,"",out t);string originalToken=t;Register("a",1);
  for(int i=0;i<30;i++){string id="member"+i;Add(id,"p"+i,100+i,"",out t);Register(id,i+2);}
  var c=Client(10);byte[][] fs=Full(a,c,0,40);Need(fs.Length>3,"multi fragment actual roster");
  for(int i=fs.Length-1;i>0;i--){Yes(c.Receive("server",fs[i]));Need(c.View==null&&c.Sequence==-1,"partial no publication");}
  No(c.Receive("server",fs[fs.Length-1]));Yes(c.Receive("server",fs[0]));Need(c.View.Members.Length==31,"reverse order completed atomically");
  var committed=c.View;long serial=c.Sequence;
  Yes(c.Invalidate());byte[][] retry=Full(a,c,1,40);Yes(c.Receive("server",retry[0]));Need(ReferenceEquals(committed,c.View)&&c.Sequence==serial,"partial recovery retains committed view");
  byte[] repeated;Yes(c.Request(2,out repeated));byte[][] recovered;Yes(a.Respond("pa",repeated,Host(40),2,out recovered));Deliver(c,recovered);Need(c.Sequence==serial,"equal serial partial loss recovery");
  // Reconnect preserves same-epoch sequence and rejects old generation frames.
  Yes(c.Disconnect("server",10));f.Records["server"]=new AuthenticatedPeer("host","r8",Hash,0,11,true);c.Offer(0);Yes(c.Bind("server",SourceStateHello.Encode("host","r8",Hash,0)));
  byte[] req;Yes(c.Request(3,out req));No(c.Receive("server",fs[0]));
  byte[] wrong=Rewrite(fs[0],delegate(Packet p){},10);No(c.Receive("server",wrong));
  // Use the same real recipient reconnect, not a fabricated admission success.
  Yes(a.Disconnect("pa",10,41,false));a=Add("a","pa",11,originalToken,out t);
  byte[][] fresh;Yes(a.Respond("pa",req,Host(42),3,out fresh));Deliver(c,fresh);Need(c.View.Members.Length==31,"real reconnect recovery");
 }
 static void Malformed()
 {
  Init();string t;var a=Add("a","pa",10,"",out t);Register("a",1);var c=Client(10);byte[][] fs=Full(a,c,0,2);
  No(c.Receive("server",Rewrite(fs[0],delegate(Packet p){},11)));Need(c.View==null,"wrong generation current correlated request");
  for(int n=0;n<fs[0].Length;n++){byte[] shortFrame=new byte[n];Buffer.BlockCopy(fs[0],0,shortFrame,0,n);No(c.Receive("server",shortFrame));Need(c.View==null,"truncation atomic");}
  No(c.Receive("server",Rewrite(fs[0],delegate(Packet p){p.Epoch++;})));No(c.Receive("server",Rewrite(fs[0],delegate(Packet p){p.FeatureId="sessions.afr1/1";})));
  No(c.Receive("server",Rewrite(fs[0],delegate(Packet p){p.Payload[5]=255;p.Payload[6]=255;p.Payload[7]=255;p.Payload[8]=127;})));
  No(c.Receive("server",Rewrite(fs[0],delegate(Packet p){p.Payload[9]^=1;})));Need(c.View==null,"digest rejects bytes atomically");
  Deliver(c,fs);Need(c.View.Members.Length==1,"retry after malformed");
  SourceStateView capture;Yes(source.Capture(Host(3),out capture));byte[] image=SourceStateCodec.Encode(capture,null);
  for(int n=0;n<image.Length;n++){byte[] cut=new byte[n];Buffer.BlockCopy(image,0,cut,0,n);SourceStateView decoded;Need(!SourceStateCodec.Decode(cut,null,out decoded)&&decoded==null,"all image truncations");}
  byte[] trailing=new byte[image.Length+1];Buffer.BlockCopy(image,0,trailing,0,image.Length);SourceStateView ignored;Need(!SourceStateCodec.Decode(trailing,null,out ignored),"trailing image bytes");
  // Real encoder/decoder closure under bounded arbitrary bytes, no expected parser clone.
  var random=new Random(813);for(int i=0;i<3000;i++){byte[] garbage=new byte[random.Next(0,600)];random.NextBytes(garbage);SourceStateView decoded;if(SourceStateCodec.Decode(garbage,null,out decoded))Need(SourceStateCodec.Validate(decoded),"fuzz accepted valid only");checks++;}
 }
 static void Recovery()
 {
  Init();string t;var a=Add("a","pa",10,"",out t);Register("a",1);var c=Client(10);Deliver(c,Full(a,c,0,2));
  string tb;var b=Add("b","pb",20,"",out tb);Register("b",3);byte[][] lost;Yes(a.Publish("pa",Host(4),out lost));
  Yes(b.Disconnect("pb",20,5,false));byte[][] next;Yes(a.Publish("pa",Host(6),out next));No(c.Receive("server",next[0]));Need(c.NeedsSnapshot,"loss requires full");
  Deliver(c,Full(a,c,1,7));Need(c.View.Members.Length==1,"loss recovered actual roster");
  var before=health.Read("a");Yes(health.ApplyLoss(Host(8),"a",before.Revision,100,100,true));Yes(a.Publish("pa",Host(8),out next));Deliver(c,next);
  Need(c.View.Members[0].HealthLife==health.Read("a").Life&&c.View.Members[0].HealthRevision==health.Read("a").Revision&&c.View.Members[0].Health==0,"actual Health loss life observation");
  // Equal serial snapshot recovery under an authenticated reconnect.
  Yes(c.Disconnect("server",10));Yes(a.Disconnect("pa",10,9,false));a=Add("a","pa",11,t,out t);f.Records["server"]=new AuthenticatedPeer("host","r8",Hash,0,11,true);c.Offer(0);Yes(c.Bind("server",SourceStateHello.Encode("host","r8",Hash,0)));
  // Replacement adapter shares actual source version ownership.
  byte[] q;Yes(c.Request(2,out q));byte[][] restored;Yes(a.Respond("pa",q,Host(9),2,out restored));Deliver(c,restored);Need(!c.NeedsSnapshot,"producer continuity retained");
 }
 static void Retirement()
 {
  Init();string t;var a=Add("a","pa",10,"",out t);var receipt=s.CaptureBindingRetirement("pa",s.ConnectionGeneration("pa"),10);Need(receipt!=null,"capture real binding");No(s.RetireObsoleteBinding(receipt,0));
  Yes(s.Lifecycle(new CommandContext("r8","host","sessions-lifecycle/10",10,0,true),LifecycleAction.BeginLoading,new SessionRules("next","respawn","map2",2,64,true,false,true,true,5),null,true));Need(s.Epoch==1,"actual epoch");
  byte[][] frames;No(a.Publish("pa",Host(11),out frames));Yes(a.RetireObsolete(1));Yes(a.RetireObsolete(1));Need(s.RetainedBindingCount==0&&s.Get("a")!=null&&s.CaptureMembers().Length==1,"retirement preserves actual membership");
  var newer=Add("a","new-pa",11,"",out t);Need(newer!=null&&s.RetainedBindingCount==1,"new key rehello actual surviving member");Yes(s.RetireObsoleteBinding(receipt,1));Need(s.ConnectionGeneration("new-pa")>0,"old receipt cannot revoke replacement");
  var c=new SourceStateClient(f,"a","r8",Hash,0);f.Records["server"]=new AuthenticatedPeer("host","r8",Hash,1,50,true);c.Offer(1);Yes(c.Bind("server",SourceStateHello.Encode("host","r8",Hash,1)));Need(c.Epoch==1&&c.View==null&&c.NeedsSnapshot,"authenticated epoch transition resync");
  SourceStateView unavailable;No(source.Capture(Host(11,1),out unavailable));Need(unavailable==null,"old Health epoch source fails closed");
  var freshFixture=new Fixture();freshFixture.Callback=null;freshFixture.Records["server"]=new AuthenticatedPeer("host","r8",Hash,0,60,true);
  No(c.Bind("server",SourceStateHello.Encode("host","r8",Hash,0)));
 }
 static void NegotiationAndPressure()
 {
  Init();string token;f.Records["pa"]=new AuthenticatedPeer("a","r8",Hash,0,10,false);var a=new SourceStateHost(s,source,f,"a","host","r8",Hash,0);byte[] ack;
  No(a.Admit("pa",SessionReplicationHello.Encode("a","r8",Hash,0),new JoinRequest(Protocol.Name,1,Hash,"a","",false),1,out token,out ack));Need(s.Get("a")==null,"old extension never joins source schema");
  Yes(a.Admit("pa",SourceStateHello.Encode("a","r8",Hash,0),new JoinRequest(Protocol.Name,1,Hash,"a","",false),1,out token,out ack));
  for(int i=0;i<63;i++){string id="roster-"+i;Add(id,"p-"+i,100+i,"",out token);Register(id,i+1);}
  Register("a",64);var c=Client(10);byte[][] full=Full(a,c,0,65);Need(full.Length<SourceStateCodec.MaxChunks,"bounded 64 member image");Deliver(c,full);Need(c.View.Members.Length==64,"actual maximum roster");
  Yes(c.Invalidate());byte[] request;Yes(c.Request(1,out request));byte[][] frames;Yes(a.Respond("pa",request,Host(65),1,out frames));Yes(c.Receive("server",frames[0]));var view=c.View;long seq=c.Sequence;
  for(int i=0;i<200;i++){byte[] malformed=Rewrite(frames[1],delegate(Packet p){p.Payload[3]=255;p.Payload[4]=255;});No(c.Receive("server",malformed));Need(ReferenceEquals(view,c.View)&&seq==c.Sequence,"pressure preserves committed state");}
  No(c.Request(1.5,out request));Yes(c.Request(2,out request));Yes(a.Respond("pa",request,Host(65),2,out frames));Deliver(c,frames);Need(c.View.Members.Length==64&&!c.NeedsSnapshot,"pressure recovery");
 }
 static int Main(){try{Flow();Malformed();FragmentPressure();Recovery();Retirement();NegotiationAndPressure();Console.WriteLine("PASS R8 "+checks+" checks");return 0;}catch(Exception e){Console.WriteLine("FAIL after "+checks+" "+e);return 1;}}
}


