using System;
using System.Collections.Generic;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
sealed class Fixture : ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IAuthorityGate,ISessionPrincipal,ISessionPlacement,ICommandSink
{
 public double Time; public double Now { get { return Time; } } public int Issued,Applied; public bool BadToken;
 public Dictionary<string,string> Principals=new Dictionary<string,string>();
 public string Issue(){return BadToken ? "" : "secret-"+(++Issued);}
 public bool Resolve(string p,out string a){return Principals.TryGetValue(p,out a);}
 public bool CanManage(CommandContext c){return true;}
 public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("auth");}
 public SpawnSite[] Sites(string m,bool w){return new[]{new SpawnSite("floor",new Float3(0,0,0),-1,true,false)};}
 public bool IsSafe(string m,string a,Float3 p){return true;}
 public void Publish(string a,long e,MemberSnapshot m){Applied++;}
 public RuleResult Apply(CommandContext c,string f,byte[] p){Applied++;return RuleResult.Accept();}
}
static class R6
{
 static readonly string H=new string('a',64); static int checks; static long tick;
 static void Check(bool v,string m){checks++;if(!v)throw new Exception(m);}
 static void Yes(RuleResult r){Check(r.Accepted,r.Reason);}
 static void No(RuleResult r){Check(!r.Accepted,"unexpected success");}
 static SessionRules Rules(bool locked=false,bool spectators=true){return new SessionRules("arena","respawn","map",2,2,true,locked,true,spectators,5);}
 static JoinRequest Req(string token="",bool spectator=false){return new JoinRequest(Protocol.Name,1,H,"name",token,spectator);}
 static byte[] Hello(string a,long epoch=0){return Wire.Encode(new Packet{Kind=PacketKind.Hello,SessionId="r6",ActorId=a,ContentHash=H,Epoch=epoch});}
 static byte[] Packet(string a,long seq,string id,long epoch=0){return Wire.Encode(new Packet{Kind=PacketKind.Command,SessionId="r6",ActorId=a,ContentHash=H,Epoch=epoch,Sequence=seq,Tick=++tick,CommandId=id,FeatureId="sessions.loaded/1",Payload=Encoding.UTF8.GetBytes("map")});}
 static SessionNetworkCoordinator New(Fixture f,SessionRules rules){return new SessionNetworkCoordinator("r6",H,rules,f,f,f,f,f,f,f,8,4,100);}
 static void Remaps()
 {
  var f=new Fixture();var b=New(f,Rules());string token;f.Principals["p"]="original";Yes(b.Admit("p",Hello("original"),Req(),++tick,out token));
  string actor="original";
  for(int i=0;i<130;i++)
  {
   long generation=b.ConnectionGeneration("p");byte[] first=Packet(actor,1,"client/1");Yes(b.Receive("p",generation,first,tick));No(b.Receive("p",generation,first,tick));
   RuleResult r;Check(b.DispatchOne(out r),"dispatch");Yes(r);Check(b.Get(actor).Loaded,"new generation seq1 reaches real Session");
   byte[] high=Packet(actor,Int64.MaxValue,"queued-high");Yes(b.Receive("p",generation,high,tick));
   Yes(b.Disconnect("p",generation,++tick,false));actor="remap"+i;f.Principals["p"]=actor;
   Yes(b.Admit("p",Hello(actor),Req(token),++tick,out token));No(b.Receive("p",generation,high,tick));
   Check(b.DispatchOne(out r) && r.Reason=="disconnected","queued old high callback discarded");
   Check(b.Get(actor).CanonicalActorId=="original" && !b.Get(actor).Loaded,"canonical life retained and load reset");
   Check(b.IdentityHistoryCount==1,"no remap history expansion");
   Yes(b.Disconnect("p",generation,++tick,false));Check(b.Get(actor)!=null,"old receipt cannot revoke replacement");
  }
 }
 static void RejectPressure()
 {
  foreach(int kind in new[]{0,1,2})
  {
   var f=new Fixture();var b=New(f,Rules(kind==0,kind!=1));f.BadToken=kind==2;
   for(int i=0;i<1000;i++)
   {
    string actor="denied"+i,token;f.Principals["p"]=actor;
    No(b.Admit("p",Hello(actor),Req("",kind==1),++tick,out token));
    Check(b.IdentityHistoryCount==0 && b.Get(actor)==null && b.ConnectionGeneration("p")==-1,"domain rejection spends no history or active slot");
   }
   if(kind!=0){f.BadToken=false;string token;f.Principals["p"]="good";Yes(b.Admit("p",Hello("good"),Req(),++tick,out token));Check(b.IdentityHistoryCount==1,"genuine fresh identity after rejection pressure");}
  }
 }
 static CommandContext Host(long n){return new CommandContext("r6","a","sessions-host/"+n,n,0,true);}
 static void Prepared()
 {
  var f=new Fixture();var s=new Session("r6",H,Rules(),f,f,f,f,f);Session.PreparedJoin p;
  Yes(s.PrepareJoinTrusted(Host(1),Req(),out p));Check(s.Count==0,"prepare has no domain publication");Yes(s.ValidateJoin(p));s.AbortJoin(p);s.AbortJoin(p);No(s.ValidateJoin(p));Check(s.Count==0,"abort unchanged");
  Yes(s.PrepareJoinTrusted(Host(2),Req(),out p));f.Time=1;No(s.ValidateJoin(p));s.AbortJoin(p);
  Yes(s.PrepareJoinTrusted(Host(3),Req(),out p));Yes(s.ValidateJoin(p));s.CommitJoinValidated(p);s.AbortJoin(p);s.CommitJoinValidated(p);Check(s.Count==1,"commit retry and abort preserve member");
 }
 static PeerLease Admit(AuthorityHost n,string actor,string identity)
 {PeerReservation r;Yes(n.PrepareAdmission("p",actor,identity,Hello(actor),tick,Int64.MaxValue,out r));Yes(n.Validate(r,tick));return n.CommitValidated(r);}
 static void Leases()
 {
  var f=new Fixture();var n=new AuthorityHost("r6",H,0,1,8,4,100,1);var lease=Admit(n,"a","canonical");byte[] p=Packet("a",Int64.MaxValue,"old-high");Yes(n.Receive(lease,p,tick));Yes(n.Release(lease));
  PeerReservation r;Yes(n.PrepareAdmission("p","b","canonical",Hello("b"),tick,Int64.MaxValue,out r));Yes(n.Validate(r,tick));RuleResult result;
  Check(n.DispatchOne(f,out result) && result.Reason=="disconnected" && f.Applied==0,"real stale dispatch");No(n.Validate(r,tick));n.Abort(r);n.Abort(r);
  var replacement=Admit(n,"b","canonical");No(n.Receive(lease,p,tick));No(n.Release(lease));Yes(n.Receive(replacement,Packet("b",1,"old-high"),tick));Check(n.DispatchOne(f,out result),"dispatch replacement");Yes(result);
  No(n.Receive(replacement,Packet("b",2,"old-high"),tick));
  var other=new AuthorityHost("r6",H,0,1,8,4,100);No(other.Receive(replacement,Packet("b",2,"x"),tick));
  n.AdvanceEpoch(1);No(n.Release(replacement));No(n.Receive(replacement,Packet("b",2,"x",1),tick));
 }
 static void PendingAndCapacity()
 {
  var f=new Fixture();var n=new AuthorityHost("r6",H,0,1,2,1,100,2);var lease=Admit(n,"a","a");RuleResult result;
  Yes(n.Receive(lease,Packet("a",1,"x"),tick));Yes(n.Receive(lease,Packet("a",2,"y"),tick));No(n.Receive(lease,Packet("a",3,"x"),tick));
  Check(n.DispatchOne(f,out result),"first mutation");Yes(result);
  // A pending new command cannot evict the most recently completed receipt.
  Yes(n.Receive(lease,Packet("a",3,"z"),tick));No(n.Receive(lease,Packet("a",4,"x"),tick));
  Check(n.DispatchOne(f,out result),"second mutation");Yes(result);Check(n.DispatchOne(f,out result),"third mutation");Yes(result);
  Yes(n.Receive(lease,Packet("a",4,"x"),tick));Check(n.DispatchOne(f,out result),"old bounded receipt reusable");Yes(result);
  Yes(n.Release(lease));lease=Admit(n,"b","b");Yes(n.Release(lease));
  PeerReservation denied;No(n.PrepareAdmission("p","c",Hello("c"),tick,Int64.MaxValue,out denied));Check(n.IdentityCount==2,"history saturation never evicts");
  lease=Admit(n,"a","a");Yes(n.Receive(lease,Packet("a",1,"x"),tick));Check(n.DispatchOne(f,out result),"seq1 at history bound");Yes(result);
  Yes(n.Release(lease));PeerReservation expired;Yes(n.PrepareAdmission("p","a",Hello("a"),tick,tick+1,out expired));No(n.Validate(expired,tick+1));Check(n.ReservedCount==0,"expiry frees only reservation");
 }
 public static int Main(){try{Remaps();RejectPressure();Prepared();Leases();PendingAndCapacity();Console.WriteLine("PASS R6 "+checks+" assertions; 130 sequence1 remaps, 3000 rejected domains, real lease/history mutation");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
