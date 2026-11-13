#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
namespace Ashfall.Integration.NetworkRuntime
{
    // Bounded native fixture transport, loopback only. No GameObject/Start callback.
    // Each endpoint owns exactly one driver and one persistent connection array.
    public sealed class LoopbackEndpoint : IFrameSender,IDisposable
    {
        private NetworkDriver driver;
        private NativeArray<NetworkConnection> peer;
        private NetworkPipeline reliable;
        private readonly BoundedQueue<byte[]> incoming;
        private bool disposed,started,listener;
        private bool ownsDriver,ownsArray;
        private static int liveDrivers,liveArrays;
        // Exact handles owned here; external Unity native leak diagnostics remain
        // a separate required coordinator gate.
        public static int LiveDriverCount { get { return liveDrivers; } }
        public static int LiveConnectionArrayCount { get { return liveArrays; } }
        public const int NativeQueueCapacity=512;
        private readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        public bool Connected { get; private set; }
        public bool Lost { get; private set; }
        public int LastDisconnectReason { get; private set; }
        public bool DriverCreated { get { return driver.IsCreated; } }
        public bool ConnectionArrayCreated { get { return peer.IsCreated; } }
        public bool ConnectionReferenceCreated { get { return peer.IsCreated && peer[0].IsCreated; } }
        public bool Disposed { get { return disposed; } }
        public static bool LastFailedConstructionReleased { get; private set; }
        public bool ResourcesCreated { get { return driver.IsCreated || peer.IsCreated; } }
        public LoopbackEndpoint(int capacity) : this(capacity,0) { }
        // Deterministic partial allocation failure injection ONLY for native fixture.
        public LoopbackEndpoint(int capacity,int fixtureFailureStage)
        {
            if(capacity<1 || capacity>256 || fixtureFailureStage<0 || fixtureFailureStage>3)throw new ArgumentException("configuration");
            incoming=new BoundedQueue<byte[]>(capacity);
            try
            {
                // 6.6 ref-return settings and unmanaged generic setters cannot be
                // consumed by Framework csc/C#5. Explicitly pin the installed
                // bounded512 native defaults; capacity bounds the managed queue.
                ValidateNativeQueueBounds(NetworkParameterConstants.ReceiveQueueCapacity,NetworkParameterConstants.SendQueueCapacity);
                driver=NetworkDriver.Create();ownsDriver=true;liveDrivers++;
                if(fixtureFailureStage==1)throw new InvalidOperationException("fixture-after-driver");
                peer=new NativeArray<NetworkConnection>(1,Allocator.Persistent);
                ownsArray=true;liveArrays++;
                if(fixtureFailureStage==2)throw new InvalidOperationException("fixture-after-array");
                reliable=driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
                if(fixtureFailureStage==3)throw new InvalidOperationException("fixture-after-pipeline");
            }
            catch { Dispose(); LastFailedConstructionReleased=!driver.IsCreated && !peer.IsCreated && !ownsDriver && !ownsArray; throw; }
        }
        private void Check()
        {
            if(disposed)throw new ObjectDisposedException("LoopbackEndpoint");
            if(thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("native-lane");
        }
        private static void ValidateNativeQueueBounds(int receive,int send)
        { if(receive!=NativeQueueCapacity || send!=NativeQueueCapacity)throw new InvalidOperationException("native-queue-policy-drift"); }
        public NetworkEndpoint ListenEphemeral()
        {
            Check(); if(started)throw new InvalidOperationException("already-started"); started=true; listener=true;
            try { if(driver.Bind(NetworkEndpoint.LoopbackIpv4.WithPort(0))!=0 || driver.Listen()!=0)throw new InvalidOperationException("listen"); return driver.GetLocalEndpoint(); }
            catch { Dispose(); throw; }
        }
        public void ConnectLoopback(ushort port)
        {
            Check(); if(started || port==0)throw new InvalidOperationException("connect-state"); started=true;
            try
            {
                if(driver.Bind(NetworkEndpoint.LoopbackIpv4.WithPort(0))!=0)throw new InvalidOperationException("bind");
                peer[0]=driver.Connect(NetworkEndpoint.LoopbackIpv4.WithPort(port));
                if(!peer[0].IsCreated)throw new InvalidOperationException("connect");
            } catch { Dispose(); throw; }
        }
        public void Pump() { PumpCore(null); }
#if UNITY_EDITOR
        // Fixture drains each real frame immediately when saturating the native
        // sender. This avoids conflating managed inbox overflow with native
        // send pressure. The ordinary Pump keeps its fail-closed inbox bound.
        internal void PumpAndDrainFixture(Action<byte[]> drain)
        { if(drain==null)throw new ArgumentNullException("drain");PumpCore(drain); }
#endif
        private void PumpCore(Action<byte[]> drain)
        {
            Check(); if(!started)return;
            driver.ScheduleUpdate().Complete(); // no job survives this call or Dispose
            if(Lost)return; // still flush native disconnect work after local closure
            if(listener)
            {
                NetworkConnection c;
                while((c=driver.Accept()).IsCreated)
                    if(peer[0].IsCreated)driver.Disconnect(c); else { peer[0]=c; Connected=true; }
            }
            if(!peer[0].IsCreated)return;
            DataStreamReader reader;
            NetworkEvent.Type type;
            while((type=driver.PopEventForConnection(peer[0],out reader))!=NetworkEvent.Type.Empty)
            {
                if(type==NetworkEvent.Type.Connect)Connected=true;
                else if(type==NetworkEvent.Type.Disconnect)
                { LastDisconnectReason=reader.Length>0?reader.ReadByte():-1; Lose(); return; }
                else if(type==NetworkEvent.Type.Data)
                {
                    if(reader.Length<1 || reader.Length>Wire.MaxPacketBytes) { Lose(); return; }
                    byte[] bytes=new byte[reader.Length]; for(int i=0;i<bytes.Length;i++)bytes[i]=reader.ReadByte();
                    if(drain!=null)drain(bytes);
                    else if(!incoming.TryAdd(bytes)) { Lose(); return; }
                }
            }
        }
        private void Lose()
        { if(peer.IsCreated && peer[0].IsCreated)driver.Disconnect(peer[0]); peer[0]=default(NetworkConnection); Connected=false; Lost=true; incoming.Clear(); }
        public bool TryReceive(out byte[] bytes) { Check(); return incoming.TryTake(out bytes); }
        public RuleResult TrySend(byte[] bytes)
        {
            Check(); if(!Connected || Lost)return RuleResult.Reject("disconnected");
            if(bytes==null || bytes.Length<1 || bytes.Length>Wire.MaxPacketBytes)return RuleResult.Reject("frame-size");
            return SendCore(bytes);
        }
#if UNITY_EDITOR
        // Native fixture injects a malformed physical datagram over the real driver.
        internal RuleResult SendOversizeFixture()
        { Check();return Connected && !Lost ? SendCore(new byte[Wire.MaxPacketBytes+1]) : RuleResult.Reject("disconnected"); }
#endif
        private RuleResult SendCore(byte[] bytes)
        {
            DataStreamWriter writer;
            int status=driver.BeginSend(reliable,peer[0],out writer,bytes.Length);
            if(status!=0)return RuleResult.Reject("native-send-pressure:"+status);
            try { for(int i=0;i<bytes.Length;i++)writer.WriteByte(bytes[i]); }
            catch { driver.AbortSend(writer); throw; }
            status=driver.EndSend(writer);
            return status<0 ? RuleResult.Reject("native-send-error:"+status) : RuleResult.Accept();
        }
        public void Disconnect() { Check(); Lose(); }
        public void Dispose()
        {
            if(disposed)return;
            if(thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("native-lane");
            disposed=true; Connected=false; Lost=true;
            try { if(driver.IsCreated)driver.Dispose();driver=default(NetworkDriver);reliable=default(NetworkPipeline);if(ownsDriver) { ownsDriver=false;liveDrivers--; } }
            finally { if(peer.IsCreated)peer.Dispose();peer=default(NativeArray<NetworkConnection>);if(ownsArray) { ownsArray=false;liveArrays--; } incoming.Clear(); }
        }
    }
}
#endif
