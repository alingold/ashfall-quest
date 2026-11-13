#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
namespace Ashfall.Modules.Networking
{
    // Unity Transport 6.6.0 bundled with Unity 6000.6.8f1. Pump and Send on the Unity main thread. Coordinator owns admission.
    public sealed class UnityTransportAdapter : IPumpedTransport
    {
        private sealed class Received { public string Peer; public byte[] Bytes; }
        private NetworkDriver driver;
        private NetworkPipeline reliable;
        private readonly Dictionary<string,NetworkConnection> peers=new Dictionary<string,NetworkConnection>();
        private readonly BoundedQueue<Received> incoming;
        private readonly int maxPeers, receiveCapacity;
        private long serial;
        private bool disposed;
        private readonly bool relayMode;
        public long MessagesIn { get; private set; }
        public long MessagesOut { get; private set; }
        public event Action<string> Connected;
        public event Action<string> Disconnected;
        public int RejectedPackets { get; private set; }
        public bool DriverCreated {get{return driver.IsCreated;}}
        public UnityTransportAdapter(int maxPeers,int receiveCapacity) : this(maxPeers,receiveCapacity,null) { }
        public UnityTransportAdapter(int maxPeers,int receiveCapacity,RelayServerData? relay)
        {
            if(maxPeers<1) throw new ArgumentOutOfRangeException("maxPeers");
            relayMode=relay.HasValue; this.maxPeers=maxPeers; this.receiveCapacity=receiveCapacity; incoming=new BoundedQueue<Received>(receiveCapacity);
            // 6.6 NetworkParams.cs exposes independent native queue bounds.
            // Defaults are not an application admission/capacity policy.
            var settings=new NetworkSettings(Allocator.Temp);
            try
            {
                settings.WithNetworkConfigParameters(receiveQueueCapacity:receiveCapacity,sendQueueCapacity:receiveCapacity);
                if(relay.HasValue) { var data=relay.Value; settings.WithRelayParameters(ref data); }
                driver=NetworkDriver.Create(settings);
            }
            finally { settings.Dispose(); }
            try { reliable=driver.CreatePipeline(typeof(ReliableSequencedPipelineStage)); }
            catch { if(driver.IsCreated) driver.Dispose(); throw; }
        }
        public void Listen(ushort port)
        {
            Check(); if(driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port))!=0 || driver.Listen()!=0) throw new InvalidOperationException("listen-failed");
        }
        public string Connect(NetworkEndpoint endpoint)
        {
            Check(); if(peers.Count>=maxPeers) throw new InvalidOperationException("peer-capacity");
            var connection=driver.Connect(endpoint);
            if(!connection.IsCreated) throw new InvalidOperationException("connect-failed");
            return Add(connection);
        }
        public string ConnectRelay(NetworkEndpoint endpoint)
        {
            Check(); if(driver.Bind(NetworkEndpoint.AnyIpv4)!=0) throw new InvalidOperationException("relay-bind-failed");
            return Connect(endpoint);
        }
        private string Add(NetworkConnection connection)
        {
            if(!connection.IsCreated) throw new InvalidOperationException("connection-not-created");
            try
            {
                string key=checked(++serial).ToString(System.Globalization.CultureInfo.InvariantCulture);
                peers.Add(key,connection); return key;
            }
            catch { driver.Disconnect(connection); throw; }
        }
        public void Pump()
        {
            Check(); driver.ScheduleUpdate().Complete();
            if(relayMode && driver.GetRelayConnectionStatus()==RelayConnectionStatus.AllocationInvalid) throw new InvalidOperationException("relay-allocation-expired");
            // Drain events before the next ScheduleUpdate (UTP clears unconsumed events).
            // Driver receive queue is bounded; retained application packets have a separate bound.
            for(int i=0;i<maxPeers;i++)
            {
                var connection=driver.Accept(); if(!connection.IsCreated) break;
                if(peers.Count>=maxPeers) { driver.Disconnect(connection); continue; }
                string peer=Add(connection); if(Connected!=null) Connected(peer);
            }
            NetworkConnection c; DataStreamReader reader;
            while(true)
            {
                var type=driver.PopEvent(out c,out reader); if(type==NetworkEvent.Type.Empty) break;
                string key=null; foreach(var entry in peers) if(entry.Value==c) { key=entry.Key; break; }
                if(key==null) continue;
                if(type==NetworkEvent.Type.Disconnect) { peers.Remove(key); if(Disconnected!=null) Disconnected(key); }
                else if(type==NetworkEvent.Type.Connect) { if(Connected!=null) Connected(key); }
                else if(type==NetworkEvent.Type.Data)
                {
                    MessagesIn++;
                    if(reader.Length>Wire.MaxPacketBytes || incoming.Count>=receiveCapacity) { RejectedPackets++; ClosePeer(key); continue; }
                    var bytes=new byte[reader.Length]; for(int b=0;b<bytes.Length;b++) bytes[b]=reader.ReadByte();
                    incoming.TryAdd(new Received { Peer=key,Bytes=bytes });
                }
            }
        }
public void ClosePeer(string peer)
        {
            Check(); NetworkConnection c; if(!peers.TryGetValue(peer,out c)) return;
            driver.Disconnect(c); peers.Remove(peer); if(Disconnected!=null) Disconnected(peer);
        }
        public void Send(string peerId,byte[] payload,bool reliableDelivery)
        {
            Check(); if(payload==null || payload.Length>Wire.MaxPacketBytes) throw new ArgumentException("payload");
            NetworkConnection c; if(!peers.TryGetValue(peerId,out c)) throw new InvalidOperationException("unknown-peer");
            DataStreamWriter writer;
            int status=driver.BeginSend(reliableDelivery ? reliable : NetworkPipeline.Null,c,out writer,payload.Length);
            if(status!=0) throw new InvalidOperationException("send-backpressure:"+status);
            for(int i=0;i<payload.Length;i++) writer.WriteByte(payload[i]);
            status=driver.EndSend(writer); if(status<0) throw new InvalidOperationException("send-failed:"+status);
            MessagesOut++;
        }
        public bool TryReceive(out string peerId,out byte[] payload)
        {
            Check(); Received r;
            while(incoming.TryTake(out r)) if(peers.ContainsKey(r.Peer)) { peerId=r.Peer; payload=r.Bytes; return true; }
            peerId=null; payload=null; return false;
        }
        private void Check() { if(disposed) throw new ObjectDisposedException("UnityTransportAdapter"); }
        public void Dispose() { if(disposed) return; disposed=true; driver.Dispose(); peers.Clear(); incoming.Clear(); }
    }
}
#endif



