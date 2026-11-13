using System;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
static class R7Tests
{
 static int checks; static string Hash=new string('a',64); static Fixture f; static SessionNetworkCoordinator s;
 static void Need(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static void Yes(RuleResult r){Need(r.Accepted,"accept "+r.Reason);}
 static void No(RuleResult r){Need(!r.Accepted,"reject");}
 static void Init(){f=new Fixture();f.Callback=null;s=new SessionNetworkCoordinator("r5",Hash,new SessionRules("arena","respawn","map",2,2,true,false,true,true,5),f,f,f,f,f,f,f,8,8,100);}
 static SessionReplicationHost Bind(string actor,string peer,long g,string credential,out string token){f.Records[peer]=new AuthenticatedPeer(actor,"r5",Hash,0,g,false);var h=new SessionReplicationHost(s,f,actor,"host","r5",Hash,0);byte[] ack;Yes(h.Admit(peer,SessionReplicationHello.Encode(actor,"r5",Hash,0),new JoinRequest(Protocol.Name,1,Hash,actor,credential,false),1,out token,out ack));return h;}
 static byte[] Cmd(string a,int n){return Wire.Encode(new Packet{Kind=PacketKind.Command,ActorId=a,SessionId="r5",ContentHash=Hash,Epoch=0,Sequence=n,Tick=2,CommandId="network-generation/2/9:spoof/"+n,FeatureId="sessions.loaded/1",Payload=Encoding.UTF8.GetBytes("map")});}
 static void Drain(SessionReplicationHost h,bool accepted){RuleResult r;Need(h.DispatchOne(out r),"real queued item");Need(r.Accepted==accepted,"item authorization "+r.Reason);}
 // A is the revoked owner; B is healthy and dispatches both A and B work.
 static void Mixed(int reason){Init();string ta,tb;var a=Bind("a","pa",10,"",out ta);var b=Bind("b","pb",20,"",out tb);
  Yes(a.ReceiveCommand("pa",10,Cmd("a",1),2));Yes(b.ReceiveCommand("pb",20,Cmd("b",1),2));
  if(reason==0)f.Records.Remove("pa");
  if(reason==1){No(a.Disconnect("pa",10,-1,false));Need(a.DeparturePending,"pending receipt");}
  if(reason==2)f.Records["pa"]=new AuthenticatedPeer("a","r5",Hash,0,11,false);
  if(reason==3)f.Records["pa"]=new AuthenticatedPeer("other","r5",Hash,0,10,false);
  if(reason==4)f.Records["pa"]=new AuthenticatedPeer("a","other",Hash,0,10,false);
  if(reason==5)f.Records["pa"]=new AuthenticatedPeer("a","r5",new string('b',64),0,10,false);
  if(reason==6)f.Records["pa"]=new AuthenticatedPeer("a","r5",Hash,1,10,false);
  if(reason==7)f.Records["pa"]=new AuthenticatedPeer("a","r5",Hash,0,10,true);
  Drain(b,false);Need(!s.Get("a").Loaded&&!s.Get("b").Loaded,"no unauthorized or premature publication");
  Drain(b,true);Need(s.Get("b").Loaded&&!s.Get("a").Loaded,"healthy B actual Loaded publication");
  f.Time=31;Yes(a.Disconnect("pa",10,3,false));Need(s.Get("a")==null&&s.Get("b").Loaded,"cleanup retains B");
  var remapped=Bind("remap","pa",12,ta,out ta);Need(s.Get("remap").CanonicalActorId=="a","real canonical remap");
  No(a.ReceiveCommand("pa",10,Cmd("a",2),2));Yes(remapped.ReceiveCommand("pa",12,Cmd("remap",1),2));Drain(b,true);Need(s.Get("remap").Loaded,"healthy B dispatches legitimate other actor");
 }
 static void Duplicate(SessionReplicationHost h){RuleResult r;Need(h.DispatchOne(out r),"queued duplicate");Need(!r.Accepted&&r.Reason=="already-loaded","authorized actual domain duplicate "+r.Reason);}
 static void Pressure(){Init();string t;var a=Bind("a","pa",10,"",out t);var b=Bind("b","pb",20,"",out t);
  for(int n=1;n<=4;n++){Yes(a.ReceiveCommand("pa",10,Cmd("a",n),2));Yes(b.ReceiveCommand("pb",20,Cmd("b",n),2));}
  No(b.ReceiveCommand("pb",20,Cmd("b",5),2));No(a.Disconnect("pa",10,-1,false));
  for(int n=1;n<=4;n++){Drain(b,false);if(n==1)Drain(b,true);else Duplicate(b);}
  Need(!s.Get("a").Loaded&&s.Get("b").Loaded,"pressure preserves exact queue ownership");
  Yes(b.ReceiveCommand("pb",20,Cmd("b",5),2));Duplicate(b);RuleResult r;Need(!b.DispatchOne(out r),"empty metadata and network queues");
  No(s.BindDispatchGeneration("pa",s.ConnectionGeneration("pa"),99));No(s.RevokeDispatch("pb",s.ConnectionGeneration("pb")+1));
  bool refused=false;try{new SessionReplicationHost(s,new Fixture(),"x","host","r5",Hash,0);}catch(ArgumentException){refused=true;}Need(refused,"private registry replacement rejected");
  f.Time=31;Yes(a.Disconnect("pa",10,4,false));Need(s.Get("a")==null,"cleanup retry after queue saturation");
 }
 static void QueuedReplacement(){Init();string ta,tb;var a=Bind("a","pa",10,"",out ta);var b=Bind("b","pb",20,"",out tb);Yes(a.ReceiveCommand("pa",10,Cmd("a",1),2));Yes(a.Disconnect("pa",10,3,false));var next=Bind("a","pa",11,ta,out ta);Yes(next.ReceiveCommand("pa",11,Cmd("a",1),2));Drain(b,false);Need(!s.Get("a").Loaded,"retired item cannot borrow replacement");Drain(b,true);Need(s.Get("a").Loaded,"replacement work remains usable");}
 static void Reentry(){Init();string ta,tb;var a=Bind("a","pa",10,"",out ta);var b=Bind("b","pb",20,"",out tb);Yes(a.ReceiveCommand("pa",10,Cmd("a",1),2));Yes(b.ReceiveCommand("pb",20,Cmd("b",1),2));
  int calls=0;RuleResult dispatch=RuleResult.Accept(),receive=RuleResult.Accept(),revoke=RuleResult.Accept();bool drained=true;
  f.Callback=delegate{calls++;if(calls==2){drained=s.DispatchOne(out dispatch);receive=b.ReceiveCommand("pb",20,Cmd("b",2),2);revoke=s.RevokeDispatch("pa",s.ConnectionGeneration("pa"));}};
  Drain(b,true);f.Callback=null;Need(calls>=2&&!drained&&!dispatch.Accepted&&dispatch.Reason=="reentry","registry callback cannot drain nested item");Need(!receive.Accepted&&!revoke.Accepted,"registry callback cannot change queue/revocation");Drain(b,true);Need(s.Get("a").Loaded&&s.Get("b").Loaded,"exact queue survives registry callback reentry");
 }
 public static int Main(){try{for(int i=0;i<8;i++)Mixed(i);Pressure();QueuedReplacement();Reentry();Console.WriteLine("PASS R7 "+checks+" actual composed assertions");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
