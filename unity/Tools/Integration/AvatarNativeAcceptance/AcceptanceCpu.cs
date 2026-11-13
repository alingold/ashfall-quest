using System;
using Ashfall.Core;
using Ashfall.Modules.Avatar;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.AvatarPose;

// External consumer: never compiled into any production owner assembly.
public static class AcceptanceCpu
{
    public sealed class FixturePorts : IAuthorityGate, ISessionManagement, ISessionClock, IReconnectTokens, ISpawnWorld
    {
        int serial;
        public double Now { get { return 0; } }
        public string Issue() { return "synthetic-token-" + (++serial); }
        public RuleResult Validate(CommandContext c) { return c.IsAuthority ? RuleResult.Accept() : RuleResult.Reject("synthetic host only"); }
        public bool CanManage(CommandContext c) { return c.IsAuthority; }
        public SpawnSite[] Sites(string map,bool warm) { return new SpawnSite[0]; }
        public bool IsSafe(string map,string actor,Float3 point) { return false; }
    }
    static long tick;
    public static CommandContext Context(string actor) { return new CommandContext("r7",actor,"c"+(++tick),tick,0,true); }
    public static Session NewSession() { var p=new FixturePorts();return new Session("r7","synthetic",new SessionRules("map","respawn","maphash",0,4,false,false,true,true,1),p,p,p,p,p); }
    static int count;
    static void Need(bool ok,string name) { if(!ok)throw new Exception(name);count++; }
    public static void Main()
    {
        AvatarR4Fixtures.Source bytes;var m=AvatarR4Fixtures.Manifest(out bytes);
        var a=AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot);
        var p=SourceBodyProfile.QuestFixed("camo",new string('a',64));
        var id=new PoseIdentity("r7","actor","actor",0,7,7,a,m,p);
        var s=NewSession();string token,next;
        Need(s.Join(Context("actor"),new JoinRequest(Protocol.Name,1,"synthetic","actor",null,false),out token).Accepted,"join actual Session");
        var lease=new PoseLease();object previous=null;
        for(int i=0;i<32;i++)
        {
            var owner=lease.Bind(id,s);var connection=s.GetConnectionIdentity("actor");
            Need(connection!=null&&Object.ReferenceEquals(connection,s.GetConnectionIdentity("actor")),"stable actual incarnation");
            lease.Revoke(previous);Need(lease.ValidateCurrent(owner,s),"retired callback cannot revoke current");
            Need(s.Disconnect(Context("actor"),false).Accepted,"disconnect");
            // Absolutely no lease observation occurs in this interval.
            Need(s.Join(Context("actor"),new JoinRequest(Protocol.Name,1,"synthetic","actor",token,false),out next).Accepted,"same-name reconnect");
            Need(!Object.ReferenceEquals(connection,s.GetConnectionIdentity("actor")),"actual Session rotates identity");
            Need(!lease.ValidateCurrent(owner,s)&&!lease.Owns(owner),"unobserved reconnect terminally retires owner");
            previous=owner;
        }
        var live=lease.Bind(id,s);var other=NewSession();
        Need(other.Join(Context("actor"),new JoinRequest(Protocol.Name,1,"synthetic","actor",null,false),out next).Accepted,"other session same labels");
        Need(!lease.ValidateCurrent(live,other)&&!lease.Owns(live),"actual Session object substitution terminal");
        live=lease.Bind(id);Need(!lease.ValidateCurrent(live,s),"unattested compatibility bind fails closed");
        live=lease.Bind(id,s);lease.Revoke(live);lease.Revoke(live);Need(!lease.ValidateCurrent(live,s),"double disposal terminal");
        Console.WriteLine("PASS "+count+" public separate-DLL R7 observations; synthetic CPU only");
    }
}
