using System;
using System.Net;
using System.Net.Sockets;
using Ashfall.Core;
using Ashfall.Modules.Networking;
class NetworkingR3Tests : ICommandSink,IReplicationSink,IReplicationAdmission
{
    const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    static int checks;
    int commands,states,value; bool deny,reject,throws;
    public RuleResult Authorize(string peer,long generation,string session,string hash,long epoch)
    { return deny ? RuleResult.Reject("unauthenticated") : RuleResult.Accept(); }
    public RuleResult Apply(CommandContext c,string feature,byte[] payload)
    { Check(c.IsAuthority && c.ActorId=="alice","host stamp"); commands++; return RuleResult.Accept(); }
    public RuleResult Apply(bool full,long sequence,byte[] payload)
    { if(throws) throw new InvalidOperationException("sink"); if(reject) return RuleResult.Reject("domain"); states++; value=payload[0]; return RuleResult.Accept(); }
    static void Check(bool ok,string label) { checks++; if(!ok) throw new Exception("FAIL "+label); }
    static Packet P(PacketKind kind,long seq)
    { return new Packet { Kind=kind,SessionId="s",ContentHash=Hash,Epoch=3,ActorId="alice",Sequence=seq,Tick=100,CommandId="cmd"+seq,FeatureId="pickup",Payload=new byte[]{(byte)(seq%256)} }; }
    static byte[] F(long g,long id,Packet p) { return ResyncWire.EncodeState(g,id,p); }
    static ReplicationReceiver R() { return new ReplicationReceiver("server","s",Hash,3); }
    static void Main()
    {
        try { Unit(); Udp(); Console.WriteLine("PASS "+checks+" assertions; actual IPv4 loopback UDP fixture, no UTP/Unity/WAN acceptance"); }
        catch(Exception e) { Console.Error.WriteLine(e); Environment.ExitCode=1; }
    }
    static void Unit()
    {
        var r=R(); var s=new NetworkingR3Tests(); byte[] hello=Wire.Encode(P(PacketKind.Hello,0));
        s.deny=true;
        Check(!r.BeginAuthenticatedReconnect("server",1,hello,s).Accepted && r.Generation==0,"denied binding unchanged");
        s.deny=false; var wrong=P(PacketKind.Hello,0); wrong.ContentHash=new string('b',64);
        Check(!r.BeginAuthenticatedReconnect("server",1,Wire.Encode(wrong),s).Accepted,"incompatible hello");
        Check(r.BeginAuthenticatedReconnect("server",1,hello,s).Accepted,"authenticated bind");
        long id=r.PendingResync.RequestId;
        Check(!r.Receive("server",Wire.Encode(P(PacketKind.Snapshot,5)),s).Accepted,"legacy disabled after binding");
        Check(!r.ReceiveFrame("other",F(1,id,P(PacketKind.Snapshot,5)),s).Accepted,"wrong authenticated peer");
        Check(!r.ReceiveFrame("server",F(2,id,P(PacketKind.Snapshot,5)),s).Accepted,"future generation cannot self bind");
        Check(!r.ReceiveFrame("server",F(1,0,P(PacketKind.Snapshot,5)),s).Accepted,"unsolicited recovery denied");
        Check(r.ReceiveFrame("server",F(1,id,P(PacketKind.Snapshot,5)),s).Accepted && r.Sequence==5,"initial frame");
        var delta=P(PacketKind.Delta,7); delta.BaseSequence=6;
        Check(!r.ReceiveFrame("server",F(1,0,delta),s).Accepted && r.NeedsSnapshot,"missing base");
        var request=r.PendingResync;
        Check(request.RequestId>id && request.MinimumSequence==5,"new recovery request");
        Check(!r.ReceiveFrame("server",F(1,0,delta),s).Accepted && r.PendingResync.RequestId==request.RequestId,"repeated gap retains recovery request");
        Check(!r.ReceiveFrame("server",F(1,id,P(PacketKind.Snapshot,999)),s).Accepted && r.Sequence==5,"late older request cannot advance watermark");
        Check(!r.ReceiveFrame("server",F(1,id,P(PacketKind.Snapshot,5)),s).Accepted,"previous response denied");
        Check(!r.ReceiveFrame("server",F(1,request.RequestId,P(PacketKind.Snapshot,4)),s).Accepted,"matching request cannot roll back");
        foreach(int field in new int[]{0,1,2,3,4})
        {
            wrong=P(PacketKind.Snapshot,99);
            if(field==0) wrong.Schema++; if(field==1) wrong.Protocol="other"; if(field==2) wrong.ContentHash=new string('b',64);
            if(field==3) wrong.SessionId="old"; if(field==4) wrong.Epoch++;
            Check(!r.ReceiveFrame("server",F(1,request.RequestId,wrong),s).Accepted && r.Sequence==5 && r.PendingResync.RequestId==request.RequestId,"incompatible cannot consume request");
        }
        s.reject=true;
        Check(!r.ReceiveFrame("server",F(1,request.RequestId,P(PacketKind.Snapshot,5)),s).Accepted && r.NeedsSnapshot,"domain reject retains request");
        s.reject=false; s.throws=true; bool threw=false;
        try { r.ReceiveFrame("server",F(1,request.RequestId,P(PacketKind.Snapshot,5)),s); } catch(InvalidOperationException) { threw=true; }
        Check(threw && r.NeedsSnapshot && r.Sequence==5,"throw retains recovery watermark"); s.throws=false;
        Check(r.ReceiveFrame("server",F(1,request.RequestId,P(PacketKind.Snapshot,5)),s).Accepted && !r.NeedsSnapshot && s.states==2,"quiescent equal serial resync");
        Check(!r.ReceiveFrame("server",F(1,request.RequestId,P(PacketKind.Snapshot,5)),s).Accepted && s.states==2,"response consumed once");
        r.Disconnect("server",0); Check(r.PendingResync==null,"old disconnect ignored");
        r.Disconnect("server",1);
        Check(!r.ReceiveFrame("server",F(1,0,P(PacketKind.Snapshot,6)),s).Accepted,"disconnected state blocked");
        Check(!r.BeginAuthenticatedReconnect("server",1,hello,s).Accepted,"generation reuse denied");
        Check(r.BeginAuthenticatedReconnect("new",2,hello,s).Accepted && r.Sequence==5,"rebind preserves watermark");
        Check(!r.ReceiveFrame("new",F(1,r.PendingResync.RequestId,P(PacketKind.Snapshot,99)),s).Accepted,"old generation higher serial denied");
        Check(r.ReceiveFrame("new",F(2,r.PendingResync.RequestId,P(PacketKind.Snapshot,5)),s).Accepted,"reconnect same serial restore");
        delta=P(PacketKind.Delta,6); delta.BaseSequence=5; s.throws=true;
        try { r.ReceiveFrame("new",F(2,0,delta),s); } catch(InvalidOperationException) { }
        Check(r.NeedsSnapshot && r.Sequence==5,"delta throw enters recovery"); s.throws=false;
        ResyncRequest decoded; byte[] bytes=ResyncWire.EncodeRequest(r.PendingResync);
        Check(ResyncWire.TryDecodeRequest(bytes,out decoded) && decoded.Generation==2 && decoded.MinimumSequence==5,"request roundtrip");
        for(int n=0;n<bytes.Length;n++) { byte[] shortBytes=new byte[n]; Array.Copy(bytes,shortBytes,n); Check(!ResyncWire.TryDecodeRequest(shortBytes,out decoded),"request truncation"); }
        bytes=F(2,r.PendingResync.RequestId,P(PacketKind.Snapshot,5));
        byte[] tail=new byte[bytes.Length+1]; Array.Copy(bytes,tail,bytes.Length);
        long ignoredGeneration,ignoredRequest; byte[] ignoredState;
        Check(!ResyncWire.TryDecodeState(tail,out ignoredGeneration,out ignoredRequest,out ignoredState),"frame trailing bytes denied");
        var negative=(byte[])bytes.Clone(); for(int i=5;i<13;i++) negative[i]=255;
        Check(!ResyncWire.TryDecodeState(negative,out ignoredGeneration,out ignoredRequest,out ignoredState),"negative generation denied");
        negative=(byte[])bytes.Clone(); for(int i=13;i<21;i++) negative[i]=255;
        Check(!ResyncWire.TryDecodeState(negative,out ignoredGeneration,out ignoredRequest,out ignoredState),"negative response request denied");
        for(int n=0;n<bytes.Length;n++) { byte[] shortBytes=new byte[n]; Array.Copy(bytes,shortBytes,n); long g,k; byte[] b; Check(!ResyncWire.TryDecodeState(shortBytes,out g,out k,out b),"frame truncation"); }
        var random=new Random(3203);
        for(int n=0;n<10000;n++) { bytes=new byte[random.Next(1100)]; random.NextBytes(bytes); long g,k; byte[] b; ResyncWire.TryDecodeState(bytes,out g,out k,out b); ResyncWire.TryDecodeRequest(bytes,out decoded); }
        Check(true,"bounded decoder malformed pressure");
    }
    // Fixture network has exactly two IPv4 loopback sockets. Loss is a deliberately
    // omitted datagram; send order deterministically realizes reorder/duplication.
    // No sleeps, public bind, alternative production transport or game-state claims.
    static byte[] Cross(UdpClient from,UdpClient to,byte[] bytes,out string peer)
    {
        from.Send(bytes,bytes.Length,(IPEndPoint)to.Client.LocalEndPoint);
        var remote=new IPEndPoint(IPAddress.Any,0); byte[] received=to.Receive(ref remote);
        Check(remote.Equals(from.Client.LocalEndPoint) && IPAddress.IsLoopback(remote.Address),"actual datagram endpoint");
        peer=remote.ToString(); return received;
    }
    static void Udp()
    {
        using(var server=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
        using(var client=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
        {
            server.Client.ReceiveTimeout=2000; client.Client.ReceiveTimeout=2000;
            var host=new AuthorityHost("s",Hash,3,1,2,2,10); var sink=new NetworkingR3Tests();
            var receiver=R(); string peer;
            byte[] hello=Cross(client,server,Wire.Encode(P(PacketKind.Hello,0)),out peer);
            string clientPeer=peer;
            Check(host.Admit(peer,"alice",hello).Accepted,"udp admitted");
            string serverPeer=server.Client.LocalEndPoint.ToString();
            Check(receiver.BeginAuthenticatedReconnect(serverPeer,1,hello,sink).Accepted,"fixture admission bound");
            ResyncRequest request;
            byte[] req=Cross(client,server,ResyncWire.EncodeRequest(receiver.PendingResync),out peer);
            Check(peer==clientPeer && ResyncWire.TryDecodeRequest(req,out request),"udp request from admitted endpoint");
            ResyncWire.TryDecodeRequest(req,out request);
            byte[] state=Cross(server,client,F(1,request.RequestId,P(PacketKind.Snapshot,5)),out peer);
            Check(receiver.ReceiveFrame(peer,state,sink).Accepted,"udp initial full");
            // Omit delta6. Deliver7, then late6. Neither may mutate until full resync.
            var d=P(PacketKind.Delta,7); d.BaseSequence=6;
            state=Cross(server,client,F(1,0,d),out peer);
            Check(!receiver.ReceiveFrame(peer,state,sink).Accepted && receiver.Sequence==5,"udp lost base");
            d=P(PacketKind.Delta,6); d.BaseSequence=5;
            state=Cross(server,client,F(1,0,d),out peer);
            Check(!receiver.ReceiveFrame(peer,state,sink).Accepted && receiver.Sequence==5,"udp late delta cannot bypass recovery");
            req=Cross(client,server,ResyncWire.EncodeRequest(receiver.PendingResync),out peer);
            Check(ResyncWire.TryDecodeRequest(req,out request),"udp resync request");
            // First response deliberately lost; resend exact outstanding request.
            req=Cross(client,server,ResyncWire.EncodeRequest(receiver.PendingResync),out peer);
            ResyncRequest retry; Check(ResyncWire.TryDecodeRequest(req,out retry) && retry.RequestId==request.RequestId,"udp timeout retry stable id");
            state=Cross(server,client,F(1,retry.RequestId,P(PacketKind.Snapshot,5)),out peer);
            Check(receiver.ReceiveFrame(peer,state,sink).Accepted && receiver.Sequence==5 && !receiver.NeedsSnapshot,"udp equal serial recovery");
            state=Cross(server,client,state,out peer);
            Check(!receiver.ReceiveFrame(peer,state,sink).Accepted && sink.states==2,"udp duplicate recovery no mutation");
            d=P(PacketKind.Delta,7); d.BaseSequence=5; state=Cross(server,client,F(1,0,d),out peer);
            Check(receiver.ReceiveFrame(peer,state,sink).Accepted && sink.value==7,"udp subsequent sequenced delta");
            long sequence=1; RuleResult result;
            for(int cycle=0;cycle<100;cycle++)
            {
                // Full queue, duplicate pending ID, retry after backpressure, and
                // invalidated work across same-endpoint reconnect in every cycle.
                byte[] first=Cross(client,server,Wire.Encode(P(PacketKind.Command,sequence++)),out peer);
                Check(host.Receive(peer,first,100).Accepted,"udp command first");
                byte[] second=Cross(client,server,Wire.Encode(P(PacketKind.Command,sequence++)),out peer);
                Check(host.Receive(peer,second,100).Accepted,"udp command second");
                var duplicate=P(PacketKind.Command,sequence); duplicate.CommandId="cmd"+(sequence-2);
                byte[] b=Cross(client,server,Wire.Encode(duplicate),out peer);
                Check(!host.Receive(peer,b,100).Accepted,"udp pending ID survives pressure");
                byte[] third=Cross(client,server,Wire.Encode(P(PacketKind.Command,sequence++)),out peer);
                Check(host.Receive(peer,third,100).Reason=="backpressure" && host.PendingCount==2,"udp queue bounded");
                Check(host.DispatchOne(sink,out result) && result.Accepted,"udp dispatch first");
                third=Cross(client,server,third,out peer);
                Check(host.Receive(peer,third,100).Accepted,"udp retry sequence not consumed on overflow");
                host.Disconnect(clientPeer);
                hello=Cross(client,server,hello,out peer);
                Check(host.Admit(peer,"alice",hello).Accepted,"udp same endpoint readmitted");
                int before=sink.commands;
                Check(host.DispatchOne(sink,out result) && result.Reason=="disconnected","udp stale second generation");
                Check(host.DispatchOne(sink,out result) && result.Reason=="disconnected" && sink.commands==before,"udp stale third generation");
                first=Cross(client,server,first,out peer);
                Check(!host.Receive(peer,first,100).Accepted && host.PendingCount==0,"udp replay after disconnect rejected");
            }
            Check(sink.commands==100,"exactly one domain command each pressure cycle");
            receiver.Disconnect(serverPeer,1);
            Check(receiver.BeginAuthenticatedReconnect(serverPeer,2,hello,sink).Accepted,"udp generation2 bind");
            var incompatible=P(PacketKind.Snapshot,999); incompatible.ContentHash=new string('b',64);
            state=Cross(server,client,F(2,receiver.PendingResync.RequestId,incompatible),out peer);
            Check(!receiver.ReceiveFrame(peer,state,sink).Accepted && receiver.Sequence==7 && receiver.NeedsSnapshot,"udp incompatible high serial cannot consume recovery");
            state=Cross(server,client,F(1,0,P(PacketKind.Snapshot,99)),out peer);
            Check(!receiver.ReceiveFrame(peer,state,sink).Accepted && receiver.Sequence==7,"udp old high snapshot denied");
            state=Cross(server,client,F(2,receiver.PendingResync.RequestId,P(PacketKind.Snapshot,7)),out peer);
            Check(receiver.ReceiveFrame(peer,state,sink).Accepted && receiver.Sequence==7,"udp same endpoint generation recovery");
        }
    }
}
