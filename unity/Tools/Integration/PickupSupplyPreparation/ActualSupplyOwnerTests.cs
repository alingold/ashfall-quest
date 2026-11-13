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
public static class ActualSupplyOwnerTests
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
        public bool Validate(CommandContext c,string connection,string canonical,long h){return Auth&&connection=="member"&&canonical=="member"&&h==HealthConnection;}
        public long Generation{get{return Root;}}
        public bool IsCurrent(string s,string a,long e,PlayerFrameOwner f){return RootCurrent&&s=="s"&&a=="member"&&e==Epoch&&Object.ReferenceEquals(f,Frames);}
        public bool TryRead(InputFrame f,string hand,out BuilderState p){p=new BuilderState(new Float3(Geometry.Side/2,1,Geometry.Side/2),new Float3(0,-1,0),new Float3(Geometry.Side/2,1,Geometry.Side/2),true,true);return true;}
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
        var identity=new SessionLifecycleIdentity(session,health,p,"host","member");
        var binding=new CanonicalBuilder(session,health,p,frames,p,()=>p.Tick,"transport","member","content",7,identity,"host");
        HostBuilderStamp stamp;
        Check(binding.TryRead("member",out stamp),"actual SessionLifecycleIdentity host principal and admitted member mapping");
        var policy=new RetainedPreparedTests.Policy();var occupants=new RetainedPreparedTests.Occupants();
        float side=Geometry.Side;
        var world=RetainedPreparedTests.Binding(policy,occupants,new[]{RetainedPreparedTests.Deck("deck",-.08f,.1f,side-.1f,.1f,side-.1f)});
        var ingressWorld=RetainedPreparedTests.Binding(policy,occupants,new[]{RetainedPreparedTests.Deck("deck",-.08f,.1f,side-.1f,.1f,side-.1f)});
        var ingress=new RuntimeHost("s",session.Epoch,"ingress",p,ingressWorld,null,binding);
        var grants=new Grants();var accounts=new Ashfall.Modules.InventoryItems.BuildChargeAccounts("s",session.Epoch,p,grants);
        var ledger=accounts.ForActor("member");Ashfall.Modules.InventoryItems.BuildChargeLedger.PreparedCharge seed;
        var seedContext=new CommandContext("s","member","seed",100,session.Epoch,true);
        Check(ledger.PrepareGrant(seedContext,"trusted-seed","source-initial999",999,120,out seed).Accepted&&seed.Validate(100),"canonical explicit seed reservation");seed.CommitValidated();
        var structures=new PreparedConstruction("s",session.Epoch,"prepared",p,ingress,world,binding,accounts,false);
        var runtime=new PreparedRuntime(binding,ingress,structures,accounts,1);
        var supplyPorts=new SupplyTests.Ports();
        var pickups=new Ashfall.Modules.Pickups.PickupRules("s",session.Epoch,60,p,supplyPorts,supplyPorts,p,100,10,10);
        Check(pickups.Register("supply","builds","supply-body",new Float3(0,0,1)),"actual supply register");pickups.Advance(100);
        Check(pickups.Execute(new CommandContext("s","member","reserve-supply",100,session.Epoch,true),Ashfall.Modules.Pickups.PickupAction.Reserve,"supply").Accepted,"actual supply reserve");
        var supplyAuthority=new PreparedSupplyGrant(pickups,binding);
        var supplyAccounts=new Ashfall.Modules.InventoryItems.BuildChargeAccounts("s",session.Epoch,p,supplyAuthority);
        var supplyRuntime=new PreparedRuntime(binding,ingress,structures,supplyAccounts,1,supplyAuthority);
        Check(supplyRuntime.CollectBuildSupply(new CommandContext("s","member","canonical-supply",100,session.Epoch,true),"supply").Accepted,"runtime real Session/Health/Player lifetime grant");
        Check(supplyAccounts.ForActor("member").Balance==6&&pickups.Inspect("supply").Removed,"runtime canonical ledger and source together");

        ConstructionLease lease;
        Check(runtime.Claim("left",120,out lease),"actual Player source lease");
        var cmd=new ConstructionCommand(new CommandContext("s","member","place-one",100,session.Epoch,true),1);
        Check(runtime.Place(cmd,lease,120).Accepted&&runtime.Snapshot().Length==1&&ledger.Balance==998&&runtime.Pending.Charges==998,"real service ledger lifecycle publication composition");
        var publication=runtime.Pending;
        Check(!runtime.Place(cmd,lease,120).Accepted&&ledger.Balance==998,"once only same command and full publication");
        Check(Object.ReferenceEquals(publication,runtime.Pending),"publication retry preserves exact object");
        Check(runtime.Acknowledge(publication)&&!runtime.Acknowledge(publication),"once only exact ack");
        Check(!runtime.CollectBuildSupply(seedContext,"box").Accepted&&ledger.Balance==998,"unsupported live source removal refuses before credit");
        Check(runtime.Claim("left",120,out lease),"reclaim before generation change");
        frames.Invalidate();frames.Observe(new PlayerLifecycleState{Enabled=true,Focused=true,RigValid=true,Session="s",Actor="member",Epoch=session.Epoch});frames.Publish(new InputFrame{Tick=100,Sequence=1,Tracked=true});
        Check(!binding.TryRead("member",out stamp),"stale adapter rejects actual Player generation after rearm ABA");
        Check(!runtime.Place(new ConstructionCommand(new CommandContext("s","member","stale-player",100,session.Epoch,true),2),lease,120).Accepted&&ledger.Balance==998&&structures.Snapshot().Length==1&&runtime.Pending==null,"stale Player refuses before debit panel publication");
        var fresh=new CanonicalBuilder(session,health,p,frames,p,()=>p.Tick,"transport","member","content",7,identity,"host");
        Check(fresh.TryRead("member",out stamp),"trusted replacement adapter accepts fresh Player");
        p.Tick=-1;Check(!fresh.TryRead("member",out stamp),"negative trusted clock refuses");p.Tick=100;
        var lagging=new CanonicalBuilder(session,health,p,frames,p,()=>p.Tick,"transport","member","content",7,p,"host");
        Check(lagging.TryRead("member",out stamp),"external lifecycle mapping fixture admitted before replacement");
        Yes(session.Disconnect(C("member"),false));
        Check(!fresh.TryRead("member",out stamp),"revoked actual connection rejects lagging peer registry");
        string ignored;Yes(session.Join(C("member"),new JoinRequest(Protocol.Name,Protocol.Schema,"content","member",token,false),out ignored));Yes(session.Loaded(C("member"),"hash"));
        Check(!fresh.TryRead("member",out stamp),"same-name reconnect cannot revive retained incarnation");
        Check(!lagging.TryRead("member",out stamp),"actual Session incarnation pin rejects lagging lifecycle port after reconnect");
        var newIdentity=new SessionLifecycleIdentity(session,health,p,"host","member");
        var newest=new CanonicalBuilder(session,health,p,frames,p,()=>p.Tick,"transport","member","content",7,newIdentity,"host");
        Check(newest.TryRead("member",out stamp),"fresh trusted Session incarnation binding");
        p.Auth=false;Check(!newest.TryRead("member",out stamp),"revoked authenticated transport refuses");p.Auth=true;
        p.AuthGeneration++;Check(!newest.TryRead("member",out stamp),"replacement transport generation refuses");
        Console.WriteLine("RESULT "+checks+" PASS actual separate DLLs; root geometry/auth provisioning remain fixture ports");return 0;
    }
    sealed class Grants:Ashfall.Modules.InventoryItems.IBuildGrantAuthority
    {public bool Validate(CommandContext c,string receipt,string kind,int amount){return receipt=="trusted-seed"&&kind=="source-initial999"&&amount==999;}}
}
