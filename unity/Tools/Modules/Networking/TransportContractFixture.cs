// Deliberately small test doubles, NOT Unity packages or package/API compatibility evidence.
using System;
namespace Unity.Collections {
 public struct DataStreamReader { public int Length { get { return 0; } } public byte ReadByte() { return 0; } }
 public struct DataStreamWriter { public void WriteByte(byte b) {} }
}
namespace Unity.Networking.Transport.Utilities { public struct ReliableSequencedPipelineStage {} }
namespace Unity.Networking.Transport {
 public struct NetworkEndpoint { public static NetworkEndpoint AnyIpv4 { get { return new NetworkEndpoint(); } } public NetworkEndpoint WithPort(ushort p) { return this; } }
 public struct NetworkPipeline { public static NetworkPipeline Null { get { return new NetworkPipeline(); } } }
 public struct NetworkConnection {
  public bool IsCreated;
  public static bool operator ==(NetworkConnection a,NetworkConnection b) { return a.IsCreated==b.IsCreated; }
  public static bool operator !=(NetworkConnection a,NetworkConnection b) { return !(a==b); }
  public override bool Equals(object o) { return o is NetworkConnection && this==(NetworkConnection)o; }
  public override int GetHashCode() { return IsCreated?1:0; }
 }
 public static class NetworkEvent { public enum Type { Empty,Connect,Disconnect,Data } }
 public struct Job { public void Complete() {} }
 public struct NetworkDriver {
  public static bool FailPipeline,FailConnect;
  public static int LiveDrivers,Disconnects;
  public bool IsCreated;
  public static NetworkDriver Create() { LiveDrivers++; return new NetworkDriver { IsCreated=true }; }
  public NetworkPipeline CreatePipeline(params Type[] stages) { if(FailPipeline) throw new InvalidOperationException("pipeline"); return new NetworkPipeline(); }
  public void Dispose() { if(IsCreated) { LiveDrivers--; IsCreated=false; } }
  public int Bind(NetworkEndpoint e) { return 0; } public int Listen() { return 0; }
  public NetworkConnection Connect(NetworkEndpoint e) { return new NetworkConnection { IsCreated=!FailConnect }; }
  public NetworkConnection Accept() { return new NetworkConnection(); }
  public void Disconnect(NetworkConnection c) { Disconnects++; }
  public Job ScheduleUpdate() { return new Job(); }
  public NetworkEvent.Type PopEvent(out NetworkConnection c,out Unity.Collections.DataStreamReader r) { c=new NetworkConnection(); r=new Unity.Collections.DataStreamReader(); return NetworkEvent.Type.Empty; }
  public int BeginSend(NetworkPipeline p,NetworkConnection c,out Unity.Collections.DataStreamWriter w,int n) { w=new Unity.Collections.DataStreamWriter(); return 0; }
  public int EndSend(Unity.Collections.DataStreamWriter w) { return 0; }
 }
}
class TransportContractFixture {
 static int checks,failures;
 static void Check(bool b,string label) { checks++; if(!b) failures++; Console.WriteLine((b?"PASS ":"FAIL ")+label); }
 static bool Throws(Action a) { try { a(); return false; } catch(InvalidOperationException) { return true; } }
 static void Main() {
  Unity.Networking.Transport.NetworkDriver.FailPipeline=true;
  Check(Throws(delegate { new Ashfall.Modules.Networking.UnityTransportAdapter(1,1); }),"pipeline-allocation-throws");
  Check(Unity.Networking.Transport.NetworkDriver.LiveDrivers==0,"pipeline-failure-disposes-partially-created-driver");
  Unity.Networking.Transport.NetworkDriver.FailPipeline=false;
  using(var adapter=new Ashfall.Modules.Networking.UnityTransportAdapter(1,1)) {
   Unity.Networking.Transport.NetworkDriver.FailConnect=true;
   bool rejected=true;
   for(int i=0;i<1000;i++) rejected &= Throws(delegate { adapter.Connect(new Unity.Networking.Transport.NetworkEndpoint()); });
   Check(rejected,"uncreated-connect-always-throws");
   Unity.Networking.Transport.NetworkDriver.FailConnect=false;
   string key=null; try { key=adapter.Connect(new Unity.Networking.Transport.NetworkEndpoint()); } catch(InvalidOperationException) {}
   Check(key=="1","failed-connects-do-not-consume-peer-capacity-or-key");
   if(key!=null) {
    Check(Throws(delegate { adapter.Connect(new Unity.Networking.Transport.NetworkEndpoint()); }),"created-peer-consumes-capacity");
    adapter.ClosePeer(key); Check(adapter.Connect(new Unity.Networking.Transport.NetworkEndpoint())=="2","close-releases-capacity");
    adapter.Dispose(); adapter.Dispose();
   }
  }
  // r1 intentionally leaks one driver in the pipeline-failure probe; final candidate must not.
  Check(Unity.Networking.Transport.NetworkDriver.LiveDrivers==0,"all-fixture-drivers-released");
  Console.WriteLine("RESULT "+checks+" checks, "+failures+" failures; DOUBLES ONLY, exact UTP compile UNKNOWN"); Environment.ExitCode=failures==0?0:1;
 }
}
