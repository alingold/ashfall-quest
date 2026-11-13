using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
sealed class Fixture : ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IAuthorityGate,ISessionPrincipal,ISessionPlacement,IAuthenticatedPeerPort
{
 public bool FailClock=false; public double Time=0; public double Now { get { if(FailClock)throw new InvalidOperationException("fixture-clock");return Time; } }
 public int Tokens,Published; public Action Callback;
 public readonly Dictionary<string,AuthenticatedPeer> Records=new Dictionary<string,AuthenticatedPeer>();
 public bool TryResolve(string c,out AuthenticatedPeer p) { if(Callback!=null)Callback(); return c!=null && Records.TryGetValue(c,out p) ? true : Missing(out p); }
 private static bool Missing(out AuthenticatedPeer p) {p=null;return false;}
 public bool Resolve(string peer,out string actor) { AuthenticatedPeer p;bool ok=TryResolve(peer,out p);actor=ok?p.Actor:null;return ok; }
 public string Issue() {return "test-only-token-"+(++Tokens);}
 public RuleResult Validate(CommandContext c) {return c.IsAuthority && c.ActorId=="host"?RuleResult.Accept():RuleResult.Reject("authority");}
 public bool CanManage(CommandContext c) {return c.ActorId=="host";}
 public SpawnSite[] Sites(string map,bool warmup) {return new[]{new SpawnSite("floor",new Float3(1,0,2),-1,true,false)};}
 public bool IsSafe(string map,string actor,Float3 p) {return p.Y==0;}
 public void Publish(string actor,long epoch,MemberSnapshot m) {Published++;}
}
