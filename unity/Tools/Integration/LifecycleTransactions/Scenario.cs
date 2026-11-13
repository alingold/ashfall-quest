using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.LifecycleTransactions;
sealed class Fixture : IAuthorityGate,IInteractionQuery,IHealthWorld,ISessionClock,IReconnectTokens,ISessionManagement,ISpawnWorld,IAuthenticatedLifecycleIdentity,ILifecycleRootPort
{
    public double Time;public bool Match=true,Auth=true,Safe=true,RootAllowed=true;public int RootCommits;public long RootRevision;private int issued;
    public HealthService H;public Session S;public Coordinator C;public SpawnSite Site=new SpawnSite("safe",new Float3(2,0,3),-1,true,false);public string Token;
    private long command;
    public CommandContext Cmd(string actor="host") { return new CommandContext("s",actor,"c"+(++command),command,S==null?0:S.Epoch,true); }
    public double Now {get{return Time;}} public bool InMatch {get{return Match;}} public bool TeamChangesAllowed {get{return !Match;}}
    public bool TryGetPose(string id,out RescuePose p) { p=new RescuePose {Tracked=true,PaddlesActive=true,Desktop=true,Facing=true,Left=new Float3(0,0,0),Right=new Float3(0,0,0),Chest=new Float3(0,0,0)};return true; }
    public RuleResult Validate(CommandContext c) {return Auth?RuleResult.Accept():RuleResult.Reject("auth");}
    public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float r){return true;}
    public string Issue(){return "token"+(++issued);} public bool CanManage(CommandContext c){return c.ActorId=="host";}
    public SpawnSite[] Sites(string map,bool warm){return new[]{Site};}public bool IsSafe(string m,string a,Float3 p){return Safe;}
    bool IAuthenticatedLifecycleIdentity.Validate(CommandContext c,string conn,string canonical,long generation)
    {var m=S.Get(conn);return Auth&&m!=null&&m.CanonicalActorId==canonical&&H.Read(canonical).ConnectionGeneration==generation;}
    public bool Prepare(RootPlacement exact,out IPreparedRootPlacement token){token=RootAllowed?new Root(this,exact):null;return RootAllowed;}
    sealed class Root : IPreparedRootPlacement
    {
        private readonly Fixture f;private readonly RootPlacement exact;private readonly long revision;private bool aborted,committed;
        public Root(Fixture fixture,RootPlacement p){f=fixture;exact=p;revision=f.RootRevision;}
        public bool Validate(){var m=f.S.Get(exact.Connection);var h=f.H.Read(exact.Canonical);return !aborted&&!committed&&f.RootAllowed&&f.RootRevision==revision&&f.Time<exact.Expiry&&m!=null&&h!=null&&m.CanonicalActorId==exact.Canonical&&exact.Context.SessionId==f.S.Id&&exact.Context.AuthorityEpoch==f.S.Epoch&&m.Team==exact.Team&&h.Team==exact.Team&&exact.SpawnRevision==m.SpawnRevision+1&&exact.HealthRevision==h.Revision+1&&exact.LifeGeneration==h.LifeGeneration+1&&exact.ConnectionGeneration==h.ConnectionGeneration&&exact.MapId==f.S.Rules.MapId&&exact.MapHash==f.S.Rules.MapHash&&exact.Site.Id==f.Site.Id&&exact.Site.Feet.X==f.Site.Feet.X&&exact.Site.Feet.Y==f.Site.Feet.Y&&exact.Site.Feet.Z==f.Site.Feet.Z;}
        public void CommitValidated(){if(committed)return;f.RootRevision++;f.RootCommits++;committed=true;}
        public void Abort(){if(!committed)aborted=true;}
    }
    public Fixture(bool respawn)
    {
        var rules=new SessionRules("map",respawn?"respawn":"revive","hash",2,8,true,false,true,true,1);
        S=new Session("s","content",rules,this,this,this,this,this);
        Must(S.Join(Cmd("p"),new JoinRequest(Protocol.Name,1,"content","P",null,false),out Token).Accepted,"join");
        Must(S.BeginLoading(Cmd(),rules,true).Accepted,"loading");Must(S.Loaded(Cmd("p"),"hash").Accepted,"loaded");Must(S.WorldReady(Cmd(),"hash").Accepted,"world");Must(S.Activate(Cmd()).Accepted,"activate");Must(S.Spawn(Cmd("p")).Accepted,"initial session spawn");
        H=new HealthService("s",S.Epoch,2,respawn,this,this,this,60,1,0);Must(H.Register(Cmd(),"p",0,false).Accepted,"health register");
        C=new Coordinator(H,S,this,this);Must(C.ProjectCurrent(Cmd(),"p",Time+1),"initial projection");
    }
    public void Kill(bool down=false){var h=H.Read("p");Must(H.ApplyLoss(Cmd(),"p",h.Revision,100,h.Shield,down).Accepted,"actual lethal damage");Must(C.ProjectCurrent(Cmd(),"p",Time+1),"actual life projection");}
    public static int Checks;
    public static void Must(bool value,string name){if(!value)throw new Exception(name);Checks++;}
    public static void Main()
    {
        var f=new Fixture(true);f.Kill();var dead=f.H.Read("p");int pending=f.H.PendingTransitionCount;
        Coordinator.PreparedLifecycle a,b;
        Must(!f.C.PrepareRespawn(f.Cmd(),"p",f.Site,1,out a),"not due");Must(f.H.Read("p").Revision==dead.Revision,"denial preserves health");
        f.Time=1;Must(f.C.PrepareRespawn(f.Cmd(),"p",f.Site,2,out a),"due prepare");Must(!f.C.PrepareRespawn(f.Cmd(),"p",f.Site,2,out b),"second prepare denied");
        Must(f.H.Read("p").Life==LifeState.Dead&&f.RootCommits==0&&f.H.PendingTransitionCount==pending,"prepare no mutation");
        Must(a.Validate()&&a.CommitValidated(),"due atomic publication");Must(!f.C.PrepareRespawn(f.Cmd(),"p",f.Site,2,out b),"second root spawn denied");a.Abort();a.Abort();Must(a.CommitValidated(),"committed abort idempotent");
        Must(f.H.Read("p").Life==LifeState.Alive&&f.H.Read("p").LifeGeneration==dead.LifeGeneration+1&&f.S.Get("p").Life==SessionLife.Alive&&f.S.Get("p").Position.X==2&&f.RootCommits==1,"real aggregates changed");
        Must(f.H.PendingTransitionCount==pending+1,"one real outbox publication");
        Must(f.C.ProjectCurrent(f.Cmd(),"p",2)&&f.C.ProjectCurrent(f.Cmd(),"p",2),"late outbox wakeups idempotent current projection");
        f.Kill();f.Time=2;dead=f.H.Read("p");pending=f.H.PendingTransitionCount;f.RootAllowed=false;
        Must(!f.C.PrepareRespawn(f.Cmd(),"p",f.Site,3,out a),"root denial");Must(f.H.Read("p").Revision==dead.Revision&&f.H.PendingTransitionCount==pending,"root denial preserves health/outbox");f.RootAllowed=true;
        Must(!f.C.PrepareRespawn(f.Cmd(),"p",new SpawnSite("invented",f.Site.Feet,-1,true,false),3,out a),"unapproved site");
        Must(!f.C.PrepareRespawn(f.Cmd(),"p",new SpawnSite("safe",f.Site.Feet,4,true,false),3,out a),"wrong team");
        Must(f.C.PrepareRespawn(f.Cmd(),"p",f.Site,3,out a),"expiry prepare");f.Time=3;Must(!a.CommitValidated(),"expired");a.Abort();
        Must(f.C.PrepareRespawn(f.Cmd(),"p",f.Site,4,out a),"root revision prepare");f.RootRevision++;Must(!a.CommitValidated(),"root revision stale");a.Abort();
        Must(f.C.PrepareRespawn(f.Cmd(),"p",f.Site,4,out a),"auth prepare");f.Auth=false;Must(!a.CommitValidated(),"identity expired");f.Auth=true;a.Abort();
        Must(f.C.PrepareRespawn(f.Cmd(),"p",f.Site,4,out a),"Results prepare");Must(f.S.EnterResults(f.Cmd()).Accepted,"Results");Must(!a.CommitValidated(),"Results denies spawn");a.Abort();Must(f.C.ProjectCurrent(f.Cmd(),"p",4),"Results current death projection");
        Must(f.S.BeginLoading(f.Cmd(),f.S.Rules,true).Accepted,"new epoch");Must(!f.C.ProjectCurrent(f.Cmd(),"p",4),"epoch mismatch");
        var d=new Fixture(false);d.Kill(true);Must(d.H.Read("p").Life==LifeState.Downed&&d.S.ProjectedLife("p").Life==LifeState.Downed&&(int)SessionLife.Spectator==3&&Protocol.Schema==1,"downed sidecar wire preserved");
        Must(d.S.Disconnect(d.Cmd("p"),false).Accepted,"disconnect downed");var h=d.H.Read("p");Must(d.H.SetConnected(d.Cmd(),"p",h.Revision,false).Accepted,"health disconnect");
        string token;Must(d.S.Join(d.Cmd("remap"),new JoinRequest(Protocol.Name,1,"content","P",d.Token,false),out token).Accepted,"real retained reconnect");Must(d.S.Get("remap").CanonicalActorId=="p","canonical retained");Must(d.S.Loaded(d.Cmd("remap"),"hash").Accepted,"reload");
        h=d.H.Read("p");Must(d.H.SetConnected(d.Cmd(),"p",h.Revision,true).Accepted,"health reconnect");Must(d.C.ProjectCurrent(d.Cmd(),"remap",1),"reconnect down projection");Must(d.S.ProjectedLife("p").Life==LifeState.Downed&&d.H.Read("p").LifeGeneration==1,"down life retained");
        Session.PreparedLifeProjection sp;Must(!d.S.PrepareLifeProjection(d.Cmd(),"remap","remap",d.H.Read("p"),1,null,out sp),"conflicting principal rejects");
        Must(d.H.Register(d.Cmd(),"rescuer",0,false).Accepted,"register rescuer");for(int i=0;i<60;i++){d.Time=i*.1;Must(d.H.SamplePaddles(d.Cmd("rescuer")).Accepted,"real charge");}
        h=d.H.Read("p");Must(d.H.Revive(d.Cmd("rescuer"),"p",h.Revision).Accepted,"real revive");Must(d.C.ProjectCurrent(d.Cmd(),"remap",d.Time+1),"revive projection");Must(d.S.Get("remap").Life==SessionLife.Alive&&d.S.ProjectedLife("p").Life==LifeState.Alive,"real Session revival");
        var stale=h;Must(!d.S.PrepareLifeProjection(d.Cmd(),"remap","p",stale,d.Time+1,null,out sp),"late down payload rejected");
        var adapter=new MatchDirectorProjection(d.C,d.S,d.H,delegate {return d.Cmd();},delegate {return d.Time+1;});
        Must(adapter.Project("s",d.S.Epoch,"remap",stale).Accepted&&d.S.ProjectedLife("p").Life==LifeState.Alive,"actual Director port late wake projects current");
        Must(!adapter.Project("s",d.S.Epoch-1,"remap",stale).Accepted,"Director old epoch rejects");
        h=d.H.Read("p");var hit=d.Cmd();var receipt=d.H.ApplyDamage(hit,"retained-hit","p",h.Revision,100,0,true);Must(receipt.Result.Accepted,"second down");Must(d.C.ProjectCurrent(d.Cmd(),"remap",d.Time+1),"second down projection");d.Time+=61;Must(d.H.Advance(d.Cmd()).Accepted,"actual bleedout");Must(d.C.ProjectCurrent(d.Cmd(),"remap",d.Time+1),"bleedout projection");Must(d.S.ProjectedLife("p").Life==LifeState.Dead&&d.S.Get("remap").Life==SessionLife.Eliminated,"death sidecar");
        h=d.H.Read("p");Must(Object.ReferenceEquals(receipt,d.H.ApplyDamage(hit,"retained-hit","p",h.Revision,100,0,true))&&d.H.Read("p").Revision==h.Revision,"retained damage replay after lifecycle");
        var x=new Fixture(true);x.Kill();x.Time=1;Must(x.C.PrepareRespawn(x.Cmd(),"p",x.Site,2,out a),"connection prepare");h=x.H.Read("p");Must(x.H.SetConnected(x.Cmd(),"p",h.Revision,false).Accepted,"disconnect invalidation");Must(!a.CommitValidated()&&x.RootCommits==0&&x.H.Read("p").Life==LifeState.Dead,"connection invalidates reservation");a.Abort();
        var t=new Fixture(true);t.Match=false;h=t.H.Read("p");Must(t.H.SetTeam(t.Cmd(),"p",h.Revision,1).Accepted,"actual Health team changes");Must(!t.C.ProjectCurrent(t.Cmd(),"p",1),"canonical team mismatch rejects");
        Console.WriteLine("PASS "+Checks+" assertions; fixture auth/root/geometry only; native/durability UNKNOWN");
    }
}
