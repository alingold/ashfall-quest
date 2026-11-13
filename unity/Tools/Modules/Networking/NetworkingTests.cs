using System;
using System.Net;
using System.Net.Sockets;
using Ashfall.Core;
using Ashfall.Modules.Networking;
class NetworkingTests : ICommandSink,IReplicationSink
{
    static int tests;
    const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    int mutations; long lastTick; bool authority; bool reject;
    public RuleResult Apply(CommandContext c,string feature,byte[] payload)
    { mutations++; lastTick=c.Tick; authority=c.IsAuthority; return RuleResult.Accept(); }
    public RuleResult Apply(bool full,long sequence,byte[] payload)
    { if(reject) return RuleResult.Reject("domain"); mutations++; return RuleResult.Accept(); }
    static void Check(bool ok,string label) { if(!ok) throw new Exception("FAIL "+label); tests++; Console.WriteLine("PASS "+label); }
    static Packet P(PacketKind kind,long sequence)
    { return new Packet { Kind=kind,ContentHash=Hash,SessionId="test-session",ActorId="alice",CommandId="command-"+sequence,FeatureId="pickup",Epoch=3,Tick=100,Sequence=sequence,Payload=new byte[] {1,2,3} }; }
    static byte[] Bytes(Packet p) { return Wire.Encode(p); }
    static AuthorityHost Host(int capacity)
    {
        var host=new AuthorityHost("test-session",Hash,3,2,capacity,4,10);
        Check(host.Admit("connection-1","alice",Bytes(P(PacketKind.Hello,0))).Accepted,"compatible-before-admission"); return host;
    }
    static void Main()
    {
        try { Run(); Console.WriteLine("RESULT PASS "+tests+" checks; BCL UDP loopback only, Unity/WAN/device UNKNOWN"); }
        catch(Exception e) { Console.Error.WriteLine(e); Environment.ExitCode=1; }
    }
    static void Run()
    {
        Packet decoded; var p=P(PacketKind.Command,1); p.FeatureId="weapon/\u00e9";
        byte[] encoded=Bytes(p);
        Check(Wire.TryDecode(encoded,out decoded) && decoded.FeatureId==p.FeatureId && decoded.Tick==100 && decoded.Payload[2]==3,"binary-roundtrip-unicode-and-body");
        bool allTruncations=true;
        for(int i=0;i<encoded.Length;i++) { byte[] truncated=new byte[i]; Array.Copy(encoded,truncated,i); if(Wire.TryDecode(truncated,out decoded)) allTruncations=false; }
        Check(allTruncations,"every-truncated-prefix-rejected");
        byte[] tail=new byte[encoded.Length+1]; Array.Copy(encoded,tail,encoded.Length);
        Check(!Wire.TryDecode(tail,out decoded),"trailing-bytes-rejected");
        var corrupt=(byte[])encoded.Clone(); corrupt[0]=0;
        Check(!Wire.TryDecode(corrupt,out decoded),"wrong-magic-rejected");
        // Fixed wire prefix: 4 magic, 1 kind, 4 schema, 2 string length, then protocol UTF8.
        corrupt=(byte[])encoded.Clone(); corrupt[11]=0xff;
        Check(!Wire.TryDecode(corrupt,out decoded),"invalid-utf8-rejected");
        Check(!Wire.TryDecode(new byte[1001],out decoded),"packet-allocation-bound");
        bool payloadBound=false; try { p.Payload=new byte[513]; Bytes(p); } catch(ArgumentException) { payloadBound=true; }
        Check(payloadBound,"send-payload-bound");
        var random=new Random(714);
        for(int i=0;i<10000;i++) { var fuzz=new byte[random.Next(1100)]; random.NextBytes(fuzz); Wire.TryDecode(fuzz,out decoded); }
        Check(true,"deterministic-10000-malformed-datagrams-no-exception");
        var h=Host(2); var sink=new NetworkingTests(); RuleResult r;
        var wrong=P(PacketKind.Hello,0); wrong.Schema++;
        Check(!h.Admit("connection-2","bob",Bytes(wrong)).Accepted,"schema-before-spawn");
        wrong=P(PacketKind.Hello,0); wrong.ContentHash=new string('b',64);
        Check(!h.Admit("connection-2","bob",Bytes(wrong)).Accepted,"content-before-spawn");
        wrong=P(PacketKind.Hello,0); wrong.Protocol="ASHFALL-GODOT/1000";
        Check(!h.Admit("connection-2","bob",Bytes(wrong)).Accepted,"godot-wire-incompatible");
        Check(!h.Receive("unknown",Bytes(P(PacketKind.Command,1)),100).Accepted,"unknown-peer-no-mutation");
        p=P(PacketKind.Command,999); p.Epoch=2;
        Check(!h.Receive("connection-1",Bytes(p),100).Accepted,"stale-epoch-high-sequence-rejected");
        p=P(PacketKind.Command,1); p.ActorId="bob";
        Check(!h.Receive("connection-1",Bytes(p),100).Accepted,"sender-bound-actor-spoof-rejected");
        p=P(PacketKind.Command,1); p.SessionId="old-session";
        Check(!h.Receive("connection-1",Bytes(p),100).Accepted,"cross-session-rejected");
        p=P(PacketKind.Command,1); p.Tick=long.MaxValue;
        Check(!h.Receive("connection-1",Bytes(p),100).Accepted,"future-tick-overflow-safe");
        Check(h.Receive("connection-1",Bytes(P(PacketKind.Command,1)),100).Accepted,"valid-after-stale-rejection");
        Check(!h.Receive("connection-1",Bytes(P(PacketKind.Command,1)),100).Accepted,"duplicate-before-dispatch-rejected");
        p=P(PacketKind.Command,2); p.CommandId="command-1";
        Check(!h.Receive("connection-1",Bytes(p),100).Accepted,"command-id-replay-with-new-sequence-rejected");
        Check(h.Receive("connection-1",Bytes(P(PacketKind.Command,2)),100).Accepted,"second-command-queued");
        Check(!h.Receive("connection-1",Bytes(P(PacketKind.Command,3)),100).Accepted && h.PendingCount==2,"bounded-backpressure");
        Check(h.DispatchOne(sink,out r) && r.Accepted && sink.mutations==1 && sink.authority && sink.lastTick==100,"authority-stamped-exactly-one-mutation");
        Check(h.Receive("connection-1",Bytes(P(PacketKind.Command,3)),100).Accepted,"backpressure-retry-not-burned");
        h.DispatchOne(sink,out r); h.DispatchOne(sink,out r);
        Check(sink.mutations==3 && !h.DispatchOne(sink,out r),"fifo-drain-no-duplicate-mutations");
        h.Disconnect("connection-1");
        Check(h.Admit("connection-2","alice",Bytes(P(PacketKind.Hello,0))).Accepted && !h.Receive("connection-2",Bytes(P(PacketKind.Command,1)),100).Accepted,"reconnect-retains-high-watermark");
        Check(h.Receive("connection-2",Bytes(P(PacketKind.Command,4)),100).Accepted,"reconnected-new-command");
        h.Disconnect("connection-2"); h.Admit("connection-2","alice",Bytes(P(PacketKind.Hello,0)));
        Check(h.DispatchOne(sink,out r) && !r.Accepted && sink.mutations==3,"queued-old-connection-generation-rejected");
        h.Receive("connection-2",Bytes(P(PacketKind.Command,5)),100); h.AdvanceEpoch(4);
        Check(h.PendingCount==0 && !h.Receive("connection-2",Bytes(P(PacketKind.Command,6)),100).Accepted,"epoch-clears-work-and-requires-readmission");
        bool monotonic=false; try { h.AdvanceEpoch(3); } catch(ArgumentException) { monotonic=true; } Check(monotonic,"epoch-cannot-roll-back");
        // Bound identities are retained across disconnect so churn cannot reset replay history.
        var cap=new AuthorityHost("test-session",Hash,3,1,1,1,10); cap.Admit("a","alice",Bytes(P(PacketKind.Hello,0))); cap.Disconnect("a");
        p=P(PacketKind.Hello,0); p.ActorId="bob";
        Check(!cap.Admit("b","bob",Bytes(p)).Accepted,"identity-churn-bounded-without-eviction");
        var replication=new ReplicationReceiver("server","test-session",Hash,3); var state=new NetworkingTests();
        p=P(PacketKind.Delta,1); p.BaseSequence=0;
        Check(!replication.Receive("server",Bytes(p),state).Accepted && replication.NeedsSnapshot,"delta-before-snapshot-rejected");
        p=P(PacketKind.Snapshot,1);
        Check(!replication.Receive("attacker",Bytes(p),state).Accepted && state.mutations==0,"replication-only-from-authority");
        Check(replication.Receive("server",Bytes(p),state).Accepted && state.mutations==1,"initial-full-snapshot");
        p=P(PacketKind.Delta,3); p.BaseSequence=2;
        Check(!replication.Receive("server",Bytes(p),state).Accepted && replication.Sequence==1 && state.mutations==1,"missing-base-requests-snapshot-without-mutation");
        p=P(PacketKind.Snapshot,4);
        Check(replication.Receive("server",Bytes(p),state).Accepted && !replication.NeedsSnapshot,"full-resync-recovers-gap");
        p=P(PacketKind.Delta,5); p.BaseSequence=4;
        Check(replication.Receive("server",Bytes(p),state).Accepted && !replication.Receive("server",Bytes(p),state).Accepted && state.mutations==3,"delta-applied-once");
        p=P(PacketKind.Delta,6); p.BaseSequence=5; state.reject=true;
        Check(!replication.Receive("server",Bytes(p),state).Accepted && replication.Sequence==5 && replication.NeedsSnapshot,"failed-domain-apply-does-not-advance-state");
        SocketLoopback();
    }
    static void SocketLoopback()
    {
        // Test fixture only: BCL UDP is NOT a production alternative transport or UTP test.
        using(var server=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
        using(var client=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
        {
            server.Client.ReceiveTimeout=2000; client.Client.ReceiveTimeout=2000;
            var endpoint=(IPEndPoint)server.Client.LocalEndPoint; var peer=new IPEndPoint(IPAddress.Any,0);
            var host=new AuthorityHost("test-session",Hash,3,1,4,4,10); var sink=new NetworkingTests();
            byte[] bytes=Bytes(P(PacketKind.Hello,0)); client.Send(bytes,bytes.Length,endpoint);
            byte[] received=server.Receive(ref peer); string identity=peer.ToString();
            Check(IPAddress.IsLoopback(peer.Address) && host.Admit(identity,"alice",received).Accepted,"real-udp-loopback-handshake");
            bytes=Bytes(P(PacketKind.Command,1)); client.Send(bytes,bytes.Length,endpoint); received=server.Receive(ref peer);
            Check(host.Receive(peer.ToString(),received,100).Accepted,"real-udp-loopback-command-admission");
            RuleResult r; host.DispatchOne(sink,out r);
            client.Send(bytes,bytes.Length,endpoint); received=server.Receive(ref peer);
            Check(!host.Receive(peer.ToString(),received,100).Accepted && sink.mutations==1,"real-udp-loopback-duplicate-no-second-mutation");
            bytes=Bytes(P(PacketKind.Snapshot,1)); server.Send(bytes,bytes.Length,peer);
            var remote=new IPEndPoint(IPAddress.Any,0); received=client.Receive(ref remote);
            var replication=new ReplicationReceiver(endpoint.ToString(),"test-session",Hash,3);
            Check(replication.Receive(remote.ToString(),received,new NetworkingTests()).Accepted,"real-udp-loopback-authority-snapshot-return");
        }
    }
}

