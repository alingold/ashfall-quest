#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Integration.NetworkRuntime;
using Ashfall.Integration.SessionReplication;
using Ashfall.Modules.Sessions;
using Ashfall.Core;
using Ashfall.NetworkRuntimeFixtures;
namespace Ashfall.NetworkRuntimeFixtures
{
    // Coordinator copies Tools fixture + SyntheticSessionFixture + ChannelTests into
    // isolated Editor probe. Call Run under engine_lane.py. Leaf never runs this.
    public static class NativeFixture
    {
        private static void Need(bool b,string message) { if(!b)throw new Exception(message); }
        private static byte[] Transfer(LoopbackEndpoint from,LoopbackEndpoint to,byte[] bytes)
        {
            bool sent=false;
            for(int i=0;i<2000;i++)
            {
                from.Pump(); to.Pump();
                if(!sent)sent=from.TrySend(bytes).Accepted;
                byte[] got; if(to.TryReceive(out got))return got;
                System.Threading.Thread.Sleep(1);
            }
            throw new Exception("bounded-loopback-timeout");
        }
        public static void Run()
        {
            var neverStarted=new LoopbackEndpoint(8);neverStarted.Dispose();neverStarted.Dispose();Need(!neverStarted.ResourcesCreated,"never-started native disposal");
            // No credential service: these records explicitly simulate auth only.
            var f=new SyntheticSessionFixture();var sessions=ChannelTests.Coordinator(f,2);
            var p=new AuthenticatedPeer("a","native-r1",ChannelTests.Hash,0,1,false);
            f.Records["fixture-client"]=p;
            f.Records["fixture-server"]=new AuthenticatedPeer("host","native-r1",ChannelTests.Hash,0,1,true);
            var client=new SessionReplicationClient(f,"a","native-r1",ChannelTests.Hash,0);
            var lane=new SessionHostLane(sessions,f,2);var channel=lane.CreateChannel("fixture-client",p,"host",2);
            LoopbackEndpoint server=null,remote=null;
            try
            {
                server=new LoopbackEndpoint(8);remote=new LoopbackEndpoint(8);
                var endpoint=server.ListenEphemeral();Need(endpoint.Port!=0,"ephemeral port");remote.ConnectLoopback(endpoint.Port);
                for(int i=0;i<2000 && (!server.Connected || !remote.Connected);i++) { server.Pump();remote.Pump();System.Threading.Thread.Sleep(1); }
                Need(server.Connected && remote.Connected,"two native endpoints connected");
                string token;
                ChannelTests.Yes(channel.Admit(Transfer(remote,server,client.Offer()),new JoinRequest(Protocol.Name,1,ChannelTests.Hash,"a","",false),1,out token));
                // Flush the bridge's retained acknowledgement through the real driver.
                ChannelTests.Yes(channel.FlushOne(server));
                byte[] ack=null;
                for(int i=0;i<2000;i++) { server.Pump();remote.Pump();if(remote.TryReceive(out ack))break;System.Threading.Thread.Sleep(1); }
                Need(ack!=null,"native ack");ChannelTests.Yes(client.Bind("fixture-server",ack));
                ChannelTests.Yes(channel.Receive(1,Transfer(remote,server,ChannelTests.Command(1,"native-loaded")),2,0));
                ChannelTests.Yes(lane.DispatchOne(2));Need(sessions.Get("a").Loaded,"real loaded mutation over native transport");
                byte[] request;ChannelTests.Yes(client.Request(0,out request));
                ChannelTests.Yes(channel.Receive(1,Transfer(remote,server,request),2,0));ChannelTests.Yes(channel.FlushOne(server));
                byte[] snapshot=null;
                for(int i=0;i<2000;i++) { server.Pump();remote.Pump();if(remote.TryReceive(out snapshot))break;System.Threading.Thread.Sleep(1); }
                Need(snapshot!=null,"native AFR1 frame");ChannelTests.Yes(client.Receive("fixture-server",snapshot));Need(client.View.Loaded,"real member snapshot");
                bool pressure=false;
                for(int i=0;i<512;i++)if(!server.TrySend(ChannelTests.Command(2,"native-pressure")).Accepted) { pressure=true;break; }
                Need(pressure,"bounded native send queue reports pressure without pumping");
                remote.Disconnect();
                for(int i=0;i<2000 && !server.Lost;i++) { remote.Pump();server.Pump();System.Threading.Thread.Sleep(1); }
                Need(server.Lost,"native disconnect event");Need(!server.TrySend(ChannelTests.Command(2,"closed")).Accepted,"disconnected send rejected");
                f.Records.Remove("fixture-client");ChannelTests.Yes(channel.Close(3));Need(sessions.Get("a")==null,"native departure cleanup");
            }
            finally
            {
                if(remote!=null) { remote.Dispose();remote.Dispose();Need(!remote.ResourcesCreated,"client disposed driver+array"); }
                if(server!=null) { server.Dispose();server.Dispose();Need(!server.ResourcesCreated,"host disposed driver+array"); }
            }
            for(int stage=1;stage<=2;stage++)
            {
                bool failed=false;
                try { using(var never=new LoopbackEndpoint(8,stage)) { throw new Exception("failure injection missed"); } }
                catch(InvalidOperationException e) { failed=e.Message.StartsWith("fixture-after-",StringComparison.Ordinal); }
                Need(failed,"partial allocation failure stage "+stage);
            }
            // Coordinator native leak diagnostics/census are REQUIRED for constructor
            // failure cleanup proof; throwing alone is not a native allocation census.
            UnityEngine.Debug.Log("NetworkRuntimeR1 native behavioral fixture completed; synthetic auth; no WAN/Quest approval");
        }
    }
}
#endif
