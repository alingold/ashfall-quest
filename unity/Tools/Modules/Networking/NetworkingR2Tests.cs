using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
class NetworkingR2Tests : ICommandSink
{
 const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
 static int checks, failures;
 readonly List<string> ids=new List<string>();
 bool reject, fail;
 public RuleResult Apply(CommandContext c,string f,byte[] b) { ids.Add(c.CommandId); if(fail) throw new InvalidOperationException("sink"); return reject?RuleResult.Reject("domain"):RuleResult.Accept(); }
 static void Check(bool ok,string label) { checks++; if(!ok) failures++; Console.WriteLine((ok?"PASS ":"FAIL ")+label); }
 static bool Throws(Action a) { try { a(); return false; } catch(ArgumentException) { return true; } }
 static Packet P(long seq,string id) { return new Packet { Kind=PacketKind.Command,SessionId="s",ContentHash=Hash,ActorId="a",Epoch=1,Tick=5,Sequence=seq,CommandId=id,FeatureId="f" }; }
 static byte[] B(long seq,string id) { return Wire.Encode(P(seq,id)); }
 static byte[] Hello(string session) { var p=P(0,""); p.Kind=PacketKind.Hello; p.SessionId=session; return Wire.Encode(p); }
 static AuthorityHost Host(int capacity) { var h=new AuthorityHost("s",Hash,1,2,capacity,1,2); if(!h.Admit("p","a",Hello("s")).Accepted) throw new Exception("setup"); return h; }
 static void Main()
 {
  RuleResult result; var h=Host(3); var sink=new NetworkingR2Tests();
  Check(h.Receive("p",B(1,"x"),5).Accepted && h.Receive("p",B(2,"y"),5).Accepted,"queued-x-y");
  Check(!h.Receive("p",B(3,"x"),5).Accepted && h.PendingCount==2,"queued-x-y-x-window-one-rejects-third");
  while(h.DispatchOne(sink,out result)) {} Check(String.Join(",",sink.ids.ToArray())=="x,y","x-dispatched-once-in-fifo");
  // Completed history remains bounded to one; pending protection is separate.
  Check(!h.Receive("p",B(3,"y"),5).Accepted,"latest-completed-retained");
  Check(h.Receive("p",B(3,"x"),5).Accepted,"older-completed-id-outside-window-reusable");
  h=Host(3); sink=new NetworkingR2Tests();
  h.Receive("p",B(1,"x"),5); h.Receive("p",B(2,"y"),5); h.Disconnect("p");
  Check(h.Admit("p","a",Hello("s")).Accepted,"same-peer-key-readmitted");
  Check(!h.Receive("p",B(3,"x"),5).Accepted,"pending-id-survives-disconnect-readmission");
  Check(h.DispatchOne(sink,out result)&&!result.Accepted&&sink.ids.Count==0,"old-generation-discarded");
  Check(!h.Receive("p",B(3,"x"),5).Accepted,"discard-enters-completed-history");
  h.DispatchOne(sink,out result);
  Check(!result.Accepted && sink.ids.Count==0 && h.PendingCount==0,"all-disconnected-work-discarded");
  Check(h.Receive("p",B(3,"x"),5).Accepted,"discarded-older-id-evicted-only-after-consumption");
  h=Host(1); sink=new NetworkingR2Tests();
  h.Receive("p",B(1,"x"),5);
  for(int i=0;i<1000;i++) if(h.Receive("p",B(2,"y"),5).Accepted) throw new Exception("pressure");
  Check(h.PendingCount==1,"queuepressure-thousand-retries-bounded");
  h.DispatchOne(sink,out result);
  Check(h.Receive("p",B(2,"y"),5).Accepted,"queuepressure-id-and-sequence-not-consumed");
  h.DispatchOne(sink,out result);
  Check(sink.ids.Count==2,"queuepressure-exactly-two-dispatches");
  Check(h.Receive("p",B(long.MaxValue,"max"),5).Accepted,"max-sequence-admitted");
  h.DispatchOne(sink,out result); h.Disconnect("p"); h.Admit("new","a",Hello("s"));
  Check(!h.Receive("new",B(long.MinValue,"wrap"),5).Accepted&&!h.Receive("new",B(1,"reset"),5).Accepted,"wrap-and-reconnect-fail-closed");
  h.AdvanceEpoch(2); var hello=P(0,""); hello.Kind=PacketKind.Hello; hello.Epoch=2;
  Check(h.Admit("new","a",Wire.Encode(hello)).Accepted,"new-epoch-readmission");
  var command=P(1,"max"); command.Epoch=2;
  Check(h.Receive("new",Wire.Encode(command),5).Accepted,"new-epoch-resets-bounded-replay-state");
  h=Host(1); sink=new NetworkingR2Tests(); sink.reject=true; h.Receive("p",B(1,"x"),5); h.DispatchOne(sink,out result);
  Check(!result.Accepted&&!h.Receive("p",B(2,"x"),5).Accepted,"domain-rejection-consumed");
  sink.reject=false; sink.fail=true; h.Receive("p",B(2,"y"),5); try { h.DispatchOne(sink,out result); } catch(InvalidOperationException) {}
  Check(h.PendingCount==0&&!h.Receive("p",B(3,"y"),5).Accepted,"throwing-sink-consumed-without-retry");
  foreach(string text in new[]{new string('a',64),new string('\u00e9',32)})
  {
   var host=new AuthorityHost(text,Hash,1,1,1,1,1); var receiver=new ReplicationReceiver("server",text,Hash,1);
   Check(host.Admit("p","a",Hello(text)).Accepted && receiver.NeedsSnapshot,"64-byte-session-config-and-wire");
  }
  foreach(string text in new[]{new string('a',65),new string('\u00e9',32)+"a",new string('\u00e9',64),"\ud800","\udc00",null,""})
  {
   Check(Throws(delegate { new AuthorityHost(text,Hash,1,1,1,1,1); }),"invalid-host-session-rejected");
   Check(Throws(delegate { new ReplicationReceiver("server",text,Hash,1); }),"invalid-receiver-session-rejected");
   if(!String.IsNullOrEmpty(text)) Check(Throws(delegate { Hello(text); }),"same-invalid-session-wire-rejected");
  }
  Check(Throws(delegate { new ReplicationReceiver("","s",Hash,1); }) && Throws(delegate { new ReplicationReceiver("server","s","bad",1); }) && Throws(delegate { new ReplicationReceiver("server","s",Hash,-1); }),"receiver-invalid-authority-hash-epoch");
  Packet decoded; var empty=P(1,""); Check(Wire.TryDecode(Wire.Encode(empty),out decoded)&&decoded.CommandId=="","wire-allows-empty-optional-text");
  Console.WriteLine("RESULT "+checks+" checks, "+failures+" failures"); Environment.ExitCode=failures==0?0:1;
 }
}
