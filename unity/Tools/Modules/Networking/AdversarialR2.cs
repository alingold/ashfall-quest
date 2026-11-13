using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
class Adversarial : ICommandSink,IReplicationSink {
 const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
 int calls; readonly Dictionary<string,int> ids=new Dictionary<string,int>(); static int checks;
 public RuleResult Apply(CommandContext c,string f,byte[] p) { calls++; if(!c.IsAuthority) throw new Exception("authority"); if(!ids.ContainsKey(c.CommandId)) ids[c.CommandId]=0; ids[c.CommandId]++; return RuleResult.Accept(); }
 public RuleResult Apply(bool full,long seq,byte[] p) { calls++; return RuleResult.Accept(); }
 static void Check(bool b,string s) { if(!b) throw new Exception(s); checks++; Console.WriteLine("PASS "+s); }
 static Packet P(PacketKind k,long seq,string id) { return new Packet { Kind=k,ContentHash=Hash,SessionId="s",ActorId="a",Epoch=1,Tick=5,Sequence=seq,CommandId=id,FeatureId="f" }; }
 static byte[] B(Packet p) { return Wire.Encode(p); }
 static AuthorityHost H(int cap,int replay) { var h=new AuthorityHost("s",Hash,1,2,cap,replay,2); Check(h.Admit("p","a",B(P(PacketKind.Hello,0,""))).Accepted,"admission"); return h; }
 static void Main() { try { Run(); Console.WriteLine("RESULT "+checks+" checks PASS; r2 corrected defect assertions"); } catch(Exception e) { Console.Error.WriteLine(e); Environment.ExitCode=1; } }
 static void Run() {
 Packet d; var valid=B(P(PacketKind.Command,1,"x")); int at=9;
 for(int field=0;field<6;field++) { int n=valid[at]+256*valid[at+1]; foreach(int size in new int[]{65,513,1000,65535}) { var m=(byte[])valid.Clone(); m[at]=(byte)size; m[at+1]=(byte)(size>>8); Check(!Wire.TryDecode(m,out d),"length-field-"+field+"-"+size); } at+=2+n; }
 foreach(int size in new int[]{513,65535}) { var m=(byte[])valid.Clone(); m[m.Length-2]=(byte)size; m[m.Length-1]=(byte)(size>>8); Check(!Wire.TryDecode(m,out d),"payload-declared-"+size); }
 var rnd=new Random(119); for(int i=0;i<20000;i++) { var m=(byte[])valid.Clone(); for(int j=0;j<1+i%4;j++) m[rnd.Next(m.Length)]=(byte)rnd.Next(256); Wire.TryDecode(m,out d); } Check(true,"20000-structured-valid-prefix-mutations-no-exception");
 var p=P(PacketKind.Command,1,"x"); p.Payload=new byte[512]; p.FeatureId=new string('z',64); Check(Wire.TryDecode(B(p),out d)&&d.Payload.Length==512,"exact-payload-and-text-boundaries");
 var h=H(1,4); var sink=new Adversarial(); RuleResult rr; p=P(PacketKind.Command,1,"x"); p.Tick=long.MinValue; Check(!h.Receive("p",B(p),5).Accepted,"negative-tick"); p.Tick=3; Check(h.Receive("p",B(p),5).Accepted,"inclusive-past-tick-boundary"); h.DispatchOne(sink,out rr); p=P(PacketKind.Command,2,"y"); p.Tick=8; Check(!h.Receive("p",B(p),5).Accepted,"outside-future-tick-boundary"); p.Tick=7; Check(h.Receive("p",B(p),5).Accepted,"inclusive-future-tick-boundary"); h.DispatchOne(sink,out rr);
 p=P(PacketKind.Command,long.MaxValue,"max"); Check(h.Receive("p",B(p),5).Accepted,"max-sequence-accepted"); h.DispatchOne(sink,out rr); p=P(PacketKind.Command,long.MinValue,"wrap"); Check(!h.Receive("p",B(p),5).Accepted,"sequence-wrap-fails-closed"); h.Disconnect("p"); Check(h.Admit("p","a",B(P(PacketKind.Hello,0,""))).Accepted&&!h.Receive("p",B(P(PacketKind.Command,1,"reset")),5).Accepted,"reconnect-cannot-reset-max-watermark");
 var rep=new ReplicationReceiver("server","s",Hash,1); var rs=new Adversarial(); Check(rep.Receive("server",B(P(PacketKind.Snapshot,long.MaxValue,"")),rs).Accepted,"max-snapshot-sequence"); Check(!rep.Receive("server",B(P(PacketKind.Snapshot,0,"")),rs).Accepted&&rs.calls==1,"old-snapshot-after-max-no-mutation");
 // Defect repro: all three admitted before any dispatch; replay window evicts pending x.
 h=H(3,1); sink=new Adversarial(); bool a=h.Receive("p",B(P(PacketKind.Command,1,"x")),5).Accepted; bool b=h.Receive("p",B(P(PacketKind.Command,2,"y")),5).Accepted; bool c=h.Receive("p",B(P(PacketKind.Command,3,"x")),5).Accepted;
 while(h.DispatchOne(sink,out rr)) {} Check(a&&b&&!c&&sink.ids["x"]==1,"REGRESSION-pending-command-id-retained-until-dispatch");
 // Configuration accepted by host but cannot be encoded on its wire.
 string unicode=new string('\u00e9',64); bool throws=false; try { h=new AuthorityHost(unicode,Hash,1,1,1,1,1); } catch(ArgumentException) { throws=true; } Check(throws,"REGRESSION-constructor-rejects-unencodable-session");
 }
}
