using System;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
using Ashfall.Integration.NetworkRuntime;
using Ashfall.NetworkRuntimeFixtures;
public static class ChannelTests
{
    public static readonly string Hash=new string('a',64);
    private static int checks;
    public static void Need(bool b,string m) { checks++; if(!b)throw new Exception(m); }
    public static void Yes(RuleResult r) { Need(r.Accepted,r.Reason); }
    public static SessionNetworkCoordinator Coordinator(SyntheticSessionFixture f,int queue)
    { return new SessionNetworkCoordinator("native-r1",Hash,new SessionRules("source-map","respawn","source-map-hash",2,2,true,false,true,true,5),f,f,f,f,f,f,f,queue,8,100); }
    public static byte[] Command(long seq,string id)
    { return Wire.Encode(new Packet {Kind=PacketKind.Command,ActorId="a",SessionId="native-r1",ContentHash=Hash,Epoch=0,Tick=2,Sequence=seq,CommandId=id,FeatureId="sessions.loaded/1",Payload=Encoding.UTF8.GetBytes("source-map-hash")}); }
    public sealed class Sender : IFrameSender
    {
        public bool Block; public byte[] Bytes;
        public RuleResult TrySend(byte[] bytes) { if(Block)return RuleResult.Reject("synthetic-pressure"); Bytes=(byte[])bytes.Clone(); return RuleResult.Accept(); }
    }
    public static int Main()
    {
        try
        {
            var f=new SyntheticSessionFixture(); var s=Coordinator(f,1);
            var p=new AuthenticatedPeer("a","native-r1",Hash,0,7,false); f.Records["physical/7"]=p;
            f.Records["authority/7"]=new AuthenticatedPeer("host","native-r1",Hash,0,7,true);
            var c=new SessionClientChannel(f,"authority/7","a",f.Records["authority/7"]);
            var lane=new SessionHostLane(s,f,2);var h=lane.CreateChannel("physical/7",p,"host",1); string token;
            byte[] offer=c.Offer(); byte[] bad=(byte[])offer.Clone(); bad[0]=0;
            Need(!h.Admit(bad,new JoinRequest(Protocol.Name,1,Hash,"a","",false),1,out token).Accepted && s.Get("a")==null,"bad negotiation no mutation");
            Yes(h.Admit(offer,new JoinRequest(Protocol.Name,1,Hash,"a","",false),1,out token)); Need(s.Get("a")!=null,"real join");
            var tx=new Sender {Block=true}; Need(!h.FlushOne(tx).Accepted && h.PendingSends==1,"retry retained");
            tx.Block=false; Yes(h.FlushOne(tx)); Need(!c.Bind(6,tx.Bytes).Accepted,"stale hello");Yes(c.Bind(7,tx.Bytes));
            Packet wrong;Need(Wire.TryDecode(Command(1,"wrong"),out wrong),"real command decode");wrong.ContentHash=new string('b',64);
            Need(!h.Receive(7,Wire.Encode(wrong),2,0).Accepted && !s.Get("a").Loaded,"content mismatch before mutation");
            wrong.ContentHash=Hash;wrong.ActorId="forged";Need(!h.Receive(7,Wire.Encode(wrong),2,0).Accepted,"source actor mismatch");
            wrong.ActorId="a";wrong.SessionId="other";Need(!h.Receive(7,Wire.Encode(wrong),2,0).Accepted,"session mismatch");
            Need(!h.Receive(6,Command(1,"loaded"),2,0).Accepted,"captured generation");
            Yes(h.Receive(7,Command(1,"loaded"),2,0)); Need(!s.Get("a").Loaded,"queue prepares no domain mutation");
            Need(!h.Receive(7,Command(2,"second"),2,0).Accepted,"real Networking queue pressure");
            Yes(lane.DispatchOne(2)); Need(s.Get("a").Loaded,"actual Sessions loaded mutation");
            Need(!h.Receive(7,Command(1,"loaded"),2,0).Accepted,"actual replay rejection");
            Yes(h.Receive(7,Command(2,"second"),2,0)); Need(!lane.DispatchOne(2).Accepted,"domain already-loaded is rejected");
            var requests=new Sender {Block=true}; Need(!c.Request(0,requests).Accepted,"client retains pressure request");requests.Block=false;
            Yes(c.Request(0,requests));byte[] request=requests.Bytes; Yes(h.Receive(7,request,2,0));
            Need(!h.Receive(7,request,2,1).Accepted && h.PendingSends==1,"response queue pressure");
            Yes(h.FlushOne(tx)); Need(!c.Receive(6,tx.Bytes).Accepted,"stale client callback");Yes(c.Receive(7,tx.Bytes));
            Need(c.View.Loaded && c.View.Actor=="a" && s.Get("a").CanonicalActorId=="a","AFR1 negotiated real member view");
            var pb=new AuthenticatedPeer("b","native-r1",Hash,0,20,false);f.Records["physical/b"]=pb;
            var hb=lane.CreateChannel("physical/b",pb,"host",1);string bt;
            Yes(hb.Admit(SessionReplicationHello.Encode("b","native-r1",Hash,0),new JoinRequest(Protocol.Name,1,Hash,"b","",false),2,out bt));
            Yes(h.Receive(7,Command(3,"queued-before-revocation"),2,1));
            f.Records.Remove("physical/7");Need(!lane.DispatchOne(-1).Accepted && h.State==ChannelState.Closing,"cleanup error retains receipt and blocks shared dispatch");
            Need(!lane.DispatchOne(2).Accepted && h.State==ChannelState.Closed && s.Get("b")!=null,"other active channel cannot dispatch revoked lease");
            
            Yes(h.Close(3)); Yes(h.Close(3)); Need(s.Get("a")==null,"retry real disconnect");
            Need(!h.Receive(7,Command(4,"late"),3,2).Accepted,"late frame closed");
            var p2=new AuthenticatedPeer("a","native-r1",Hash,0,8,false);f.Records["physical/8"]=p2;
            var h2=lane.CreateChannel("physical/8",p2,"host",1);string unused;
            Yes(h2.Admit(SessionReplicationHello.Encode("a","native-r1",Hash,0),new JoinRequest(Protocol.Name,1,Hash,"a",token,false),4,out unused));
            Need(!lane.DispatchOne(4).Accepted,"no old queued publication");
            Yes(h2.Receive(8,Command(1,"loaded"),2,2));Yes(lane.DispatchOne(4));Need(s.Get("a").Loaded,"reconnect sequence1 real domain mutation");
            Yes(h2.Close(5));
            Yes(hb.Close(5));
            f.Records.Remove("authority/7");Need(!c.SendCommand(Command(9,"revoked"),tx).Accepted && c.State==ChannelState.Closed,"client registry revocation");Yes(c.Close());Yes(c.Close());
            var pe=new AuthenticatedPeer("epoch-actor","native-r1",Hash,0,30,false);f.Records["physical/epoch"]=pe;
            var he=lane.CreateChannel("physical/epoch",pe,"host",1);string et;
            Yes(he.Admit(SessionReplicationHello.Encode("epoch-actor","native-r1",Hash,0),new JoinRequest(Protocol.Name,1,Hash,"epoch-actor","",false),6,out et));
            Yes(s.Lifecycle(new CommandContext("native-r1","host","sessions-lifecycle/10",10,0,true),LifecycleAction.BeginLoading,new SessionRules("source-map","respawn","source-map-hash",2,2,true,false,true,true,5),null,true));
            Need(s.Epoch==1 && !he.Receive(30,Command(1,"in-flight"),10,3).Accepted && he.State==ChannelState.Closing && he.PendingSends==0,"epoch change rejects in-flight and retires response");
            Need(!he.Close(11).Accepted && he.State==ChannelState.Closing,"old epoch departure requires coordinator handoff; stays fail closed");
            Console.WriteLine("PASS NetworkRuntimeR1 "+checks+" behavioral assertions, actual Networking/Sessions/SessionReplication");return 0;
        }
        catch(Exception e) { Console.WriteLine(e);return 1; }
    }
}
