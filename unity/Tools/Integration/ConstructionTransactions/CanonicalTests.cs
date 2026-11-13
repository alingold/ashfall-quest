using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Construction;
using Ashfall.Integration.SessionReplication;
using Ashfall.Integration.LifecycleTransactions;
using Ashfall.Integration.ConstructionRuntime;
using Ashfall.Integration.ConstructionTransactions;
public static class CanonicalTests
{
    sealed class Ports:IAuthorityGate,ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IHealthWorld,IInteractionQuery,IAuthenticatedPeerPort,IAuthenticatedLifecycleIdentity,IConstructionRootPose
    {
        public long Tick=100,AuthGeneration=7,HealthConnection=1,Root=1;public bool Auth=true,RootCurrent=true;int tokens;
        public long Epoch;public string Member="member";public PlayerFrameOwner Frames;
        public double Now{get{return 1;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return true;}}
        public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
        public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
        public bool CanManage(CommandContext c){return c.ActorId=="host";}
        public string Issue(){return "token/"+(++tokens);}
        public SpawnSite[] Sites(string m,bool w){return new[]{new SpawnSite("safe",new Float3(),-1,true,false)};}
        public bool IsSafe(string m,string a,Float3 f){return true;}
        public bool HasLineOfSight(string a,string b,Float3 f,Float3 t,float r){return true;}
        public bool TryResolve(string c,out AuthenticatedPeer p){p=Auth?new AuthenticatedPeer(Member,"s","content",Epoch,AuthGeneration,false):null;return c=="transport"&&Auth;}
        public bool Validate(CommandContext c,string connection,string canonical,long h){return Auth&&connection=="transport"&&canonical=="member"&&h==HealthConnection;}
        public long Generation{get{return Root;}}
        public bool IsCurrent(string s,string a,long e,PlayerFrameOwner f){return RootCurrent&&s=="s"&&a=="member"&&e==Epoch&&Object.ReferenceEquals(f,Frames);}
        public bool TryRead(InputFrame f,string hand,out BuilderState p){p=new BuilderState(new Float3(),new Float3(0,-1,0),new Float3(),true,true);return true;}
    }
    static int n,checks;static Session session;
    static CommandContext C(string a){return new CommandContext("s",a,"canonical-test/"+(++n),n,session.Epoch,true);}
    static void Check(bool b,string s){if(!b)throw new Exception(s);checks++;Console.WriteLine("PASS "+s);}
    static void Yes(RuleResult r){Check(r.Accepted,"setup "+r.Reason);}
    public static int Main()
    {
        var p=new Ports();session=new Session("s","content",new SessionRules("map","respawn","hash",0,8,true,false,true,true,5),p,p,p,p,p);
        string token;Yes(session.Join(C("member"),new JoinRequest(Protocol.Name,Protocol.Schema,"content","member","",false),out token));
        Yes(session.BeginLoading(C("host"),session.Rules,true));p.Epoch=session.Epoch;
        Yes(session.Loaded(C("member"),"hash"));Yes(session.WorldReady(C("host"),"hash"));Yes(session.Activate(C("host")));Yes(session.Spawn(C("member")));
        var health=new HealthService("s",session.Epoch,0,true,p,p,p);Yes(health.Register(C("host"),"member",-1,false));
        var frames=new PlayerFrameOwner(new PlayerInput());p.Frames=frames;
        frames.Observe(new PlayerLifecycleState{Enabled=true,Focused=true,RigValid=true,Session="s",Actor="member",Epoch=session.Epoch});
        frames.Publish(new InputFrame{Tick=100,Sequence=1,Tracked=true});
        var binding=new CanonicalBuilder(session,health,p,frames,p,()=>p.Tick,"transport","member","content",7,p);
        HostBuilderStamp stamp;
        Check(binding.TryRead("member",out stamp)&&stamp.Connection==1,"actual session health player binding with distinct auth7 health1 generations");
        Check(!binding.TryRead("forged",out stamp),"forged canonical actor rejected");
        p.RootCurrent=false;Check(!binding.TryRead("member",out stamp),"wrong root/frame owner rejected");p.RootCurrent=true;
        p.Tick=101;Check(!binding.TryRead("member",out stamp),"queued old Player pose rejects at new host tick");p.Tick=100;
        frames.Publish(new InputFrame{Tick=100,Sequence=2,Tracked=true});Check(binding.TryRead("member",out stamp)&&stamp.PoseRevision==2,"actual Player publication sequence changes stamp");
        Yes(health.SetConnected(C("host"),"member",health.Read("member").Revision,false));
        Check(!binding.TryRead("member",out stamp),"actual Health disconnect invalidates producer");
        Yes(health.SetConnected(C("host"),"member",health.Read("member").Revision,true));
        Check(!binding.TryRead("member",out stamp),"old health-generation mapping rejects reconnect");p.HealthConnection=health.Read("member").ConnectionGeneration;
        p.AuthGeneration=8;Check(!binding.TryRead("member",out stamp),"replacement authenticated generation invalidates old adapter");
        Yes(session.Disconnect(C("member"),false));
        Check(!binding.TryRead("member",out stamp),"actual Session disconnect rejects retained authenticated registry");
        string ignored;Yes(session.Join(C("remapped"),new JoinRequest(Protocol.Name,Protocol.Schema,"content","member",token,false),out ignored));
        p.Member="remapped";
        var replacement=new CanonicalBuilder(session,health,p,frames,p,()=>p.Tick,"transport","member","content",8,p);
        Check(!replacement.TryRead("member",out stamp),"remapped member requires actual loaded readiness");
        Yes(session.Loaded(C("remapped"),"hash"));
        Check(replacement.TryRead("member",out stamp)&&stamp.Connection==3,"actual Session remap preserves canonical health history");
        Yes(health.ApplyLoss(C("host"),"member",health.Read("member").Revision,100,0,false));
        Check(!replacement.TryRead("member",out stamp),"actual Health death invalidates construction");
        Console.WriteLine("RESULT "+checks+" PASS; root/auth provision ports are fixtures; no native execution");return 0;
    }
}
