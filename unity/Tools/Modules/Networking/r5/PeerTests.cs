using System;
using System.Net;
using System.Net.Sockets;
using Ashfall.Core;
using Ashfall.Modules.Networking;
class PeerTests : ICommandSink
{
 const string H="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
 static int checks; int mutations;
 public RuleResult Apply(CommandContext c,string f,byte[] b) { Check(c.IsAuthority,"host authority"); mutations++; return RuleResult.Accept(); }
 static void Check(bool v,string name) { checks++; if(!v)throw new Exception(name); }
 static byte[] P(string actor,long seq,string id,bool hello=false) { return Wire.Encode(new Packet { Kind=hello?PacketKind.Hello:PacketKind.Command,SessionId="s",ContentHash=H,Epoch=1,ActorId=actor,Sequence=seq,CommandId=id,FeatureId="effect",Tick=10 }); }
 static AuthorityHost Host(int active,int history,int queue=2) { return new AuthorityHost("s",H,1,active,queue,4,10,history); }
 static PeerLease Join(AuthorityHost h,string peer,string actor,string canonical=null)
 { PeerReservation r; Check(h.PrepareAdmission(peer,actor,canonical??actor,P(actor,0,"",true),0,100,out r).Accepted,"prepare"); Check(h.Validate(r,0).Accepted,"validate"); return h.CommitValidated(r); }
 static void Drain(AuthorityHost h,PeerTests sink) { RuleResult r;while(h.DispatchOne(sink,out r)) {} }
 static void Main() { try { Run(); Console.WriteLine("PASS "+checks+" assertions; neutral UDP only"); } catch(Exception e) {Console.Error.WriteLine(e);Environment.ExitCode=1;} }
 static void Run()
 {
  var sink=new PeerTests(); var h=Host(2,8); var a=Join(h,"p","a"); var b=Join(h,"q","b"); PeerReservation r;
  Check(!h.PrepareAdmission("third","c",P("c",0,"",true),0,100,out r).Accepted,"active full");
  Check(h.Receive(a,P("a",9,"old"),10).Accepted,"old queued"); Check(h.Release(a).Accepted,"vacancy");
  var c=Join(h,"p","c"); Check(!h.Release(a).Accepted && h.ActiveCount==2,"stale release cannot remove reused peer");
  Check(!h.Receive(a,P("a",long.MaxValue,"late"),10).Accepted,"old connection late high seq denied");
  RuleResult result; Check(h.DispatchOne(sink,out result)&&!result.Accepted&&sink.mutations==0,"queued departed authority invalidated");
  Check(h.Receive(c,P("c",1,"new"),10).Accepted,"new generation command"); Drain(h,sink);Check(sink.mutations==1,"actual effect");
  h.Release(c); var returned=Join(h,"p","returned","a");
  Check(!h.Receive(returned,P("returned",9,"fresh"),10).Accepted,"remapped identity sequence never reset");
  Check(!h.Receive(returned,P("returned",10,"old"),10).Accepted,"remapped identity replay retained");
  Check(h.Receive(returned,P("returned",10,"continuation"),10).Accepted,"authenticated remap continuation");Drain(h,sink);
  Check(h.IdentityCount==3 && h.ActiveCount==2 && sink.mutations==2,"reconnect retains canonical history and mutates");
  var foreign=Host(2,8);Check(!foreign.Release(returned).Accepted&&!foreign.Receive(returned,P("returned",11,"foreign"),10).Accepted,"cross-host lease");
  h.Release(returned);h.Release(b);
  for(int i=0;i<1000;i++) { Check(h.PrepareAdmission("reject","rejected"+i,P("rejected"+i,0,"",true),0,100,out r).Accepted,"rejected domain prepare"); h.Abort(r);h.Abort(r); }
  Check(h.IdentityCount==3 && h.ReservedCount==0,"rejected admission churn consumes no identity history");
  h=Host(1,2);a=Join(h,"p","a");h.Receive(a,P("a",1,"once"),10);Drain(h,sink);h.Release(a);b=Join(h,"p","b");h.Release(b);
  Check(!h.PrepareAdmission("p","c",P("c",0,"",true),0,100,out r).Accepted && h.IdentityCount==2,"history full fails closed without eviction");
  a=Join(h,"p","a");Check(!h.Receive(a,P("a",1,"reset"),10).Accepted&&!h.Receive(a,P("a",2,"once"),10).Accepted,"history saturation preserves sequence and command IDs");h.Release(a);
  Check(h.PrepareAdmission("x","a",P("a",0,"",true),0,2,out r).Accepted,"prepare no active mutation");Check(h.ActiveCount==0&&h.ReservedCount==1,"reservation separate");
  Check(!h.Validate(r,2).Accepted&&h.ReservedCount==0,"expiry equality");
  Check(!h.PrepareAdmission("x","a",P("a",0,"",true),1,3,out r).Accepted,"clock rollback");
  h=Host(2,4);PeerReservation first,second;
  Check(h.PrepareAdmission("a","a",P("a",0,"",true),0,100,out first).Accepted,"first reservation");
  Check(h.PrepareAdmission("b","b",P("b",0,"",true),0,100,out second).Accepted,"second reservation");
  Check(h.Validate(first,0).Accepted&&h.Validate(second,0).Accepted,"validate both");a=h.CommitValidated(first);
  Check(!h.Validate(second,0).Accepted,"intervening membership revision invalidates reservation");h.Abort(second);h.Abort(first);
  Check(h.CommitValidated(first)==a&&h.ActiveCount==1,"abort preserves commit and commit idempotence");
  h.AdvanceEpoch(2);Check(!h.Release(a).Accepted&&h.ActiveCount==0&&!h.Validate(first,0).Accepted,"epoch invalidates leases and reservations");
  h=Host(1,2);a=Join(h,"p","a");Check(h.Receive(a,P("a",1,"pending"),10).Accepted,"history revision setup");h.Release(a);
  Check(h.PrepareAdmission("p","a",P("a",0,"",true),0,100,out r).Accepted,"reservation captures pending history revision");
  Drain(h,sink);Check(!h.Validate(r,0).Accepted,"history mutation invalidates prepared admission");h.Abort(r);
  a=Join(h,"p","a");Check(!h.Receive(a,P("a",2,"pending"),10).Accepted,"reprepare keeps retired pending replay");
  UdpPressure();
 }
 static byte[] Cross(UdpClient from,UdpClient to,byte[] bytes)
 { from.Send(bytes,bytes.Length,(IPEndPoint)to.Client.LocalEndPoint);IPEndPoint remote=null;return to.Receive(ref remote); }
 static void UdpPressure()
 {
  using(var client=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))) using(var server=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
  {
   client.Client.ReceiveTimeout=server.Client.ReceiveTimeout=2000;
   var h=Host(1,260,1);var sink=new PeerTests();
   for(int i=0;i<256;i++)
   {
    string actor="actor"+i;PeerReservation r;
    Check(h.PrepareAdmission("reused",actor,Cross(client,server,P(actor,0,"",true)),0,100,out r).Accepted,"UDP full then vacant admission");
    Check(h.Validate(r,0).Accepted,"UDP validate");var lease=h.CommitValidated(r);
    Check(h.Receive(lease,Cross(client,server,P(actor,1,"effect")),10).Accepted,"UDP command");
    for(int n=0;n<8;n++) Check(!h.Receive(lease,Cross(client,server,P(actor,2,"retry")),10).Accepted,"UDP queue pressure");
    RuleResult result;Check(h.DispatchOne(sink,out result)&&result.Accepted,"UDP actual composed mutation");
    Check(h.Receive(lease,Cross(client,server,P(actor,2,"retry")),10).Accepted,"UDP retry survives pressure");
    Check(h.Release(lease).Accepted,"UDP release");
    Check(h.DispatchOne(sink,out result)&&!result.Accepted,"UDP queued departure rejected");
    Check(!h.Receive(lease,Cross(client,server,P(actor,3,"late")),10).Accepted&&!h.Release(lease).Accepted,"UDP late packet and stale release");
   }
   Check(h.IdentityCount==256&&h.ActiveCount==0&&sink.mutations==256,"256 capacity cycles preserve histories and real mutations");
  }
 }
}
