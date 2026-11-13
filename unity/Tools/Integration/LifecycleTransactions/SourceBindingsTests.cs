using System;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.LifecycleTransactions;
using Ashfall.Tools.WorldRuntime;
class SourceBindingsTests
{
    // Analytic contacts only: this validates routing to actual World source APIs,
    // never claims a Unity collision result or production content approval.
    sealed class Contacts : ISpawnQuery
    {
        public bool Room=true,Hazard,Floor=true;public float Slope=1;public int Floors;
        public bool TryFloor(Float3 p,float up,float depth,out Float3 feet,out Float3 normal){Floors++;feet=new Float3(p.X,0,p.Z);normal=new Float3(0,Slope,0);return Floor;}
        public bool HasStandingRoom(Float3 p,float radius,float height){return Room&&radius==.3f&&height==1.8f;}
        public bool IsHazard(Float3 p){return Hazard;}
    }
    sealed class Roots : ILifecycleRootPort
    {
        public int Prepares,Commits;public bool Allowed=true;
        public bool Prepare(RootPlacement exact,out IPreparedRootPlacement prepared){Prepares++;prepared=new Root(this);return true;}
        sealed class Root : IPreparedRootPlacement
        {
            readonly Roots owner;bool done,aborted;
            public Root(Roots o){owner=o;}public bool Validate(){return !done&&!aborted&&owner.Allowed;}
            public void CommitValidated(){if(!done){owner.Commits++;done=true;}}public void Abort(){if(!done)aborted=true;}
        }
    }
    static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static long command;
    static CommandContext C(Session s,string actor="host"){long n=++command;return new CommandContext(s.Id,actor,"source-host/"+n,n,s.Epoch,true);}
    public static void Main()
    {
        var policy=new FixturePolicy();var world=WorldRuntimeFixture.Approved(policy);var contacts=new Contacts();
        var source=new SourceSpawnWorld(world,policy,policy,contacts,1.8f,new[]{new SourceSpawnPolicy("spawn","warmup","shared",-1,true)},WorldRuntimeFixture.Layer);
        var sites=source.Sites(world.Identity.MapId,false);Check(sites.Length==1&&contacts.Floors>0&&sites[0].Feet.Y==.12f,"actual floor-resolved source site");
        SourceSpawnEvidence evidence;Check(source.TryEvidence(world.Identity.MapId,"p",sites[0],out evidence)&&evidence.Validate("p"),"typed source evidence");
        contacts.Room=false;Check(!evidence.Validate("p")&&source.Sites(world.Identity.MapId,false).Length==0,"standing obstruction revokes");contacts.Room=true;
        contacts.Hazard=true;Check(!evidence.Validate("p"),"hazard revokes");contacts.Hazard=false;
        contacts.Floor=false;Check(!evidence.Validate("p"),"missing floor revokes");contacts.Floor=true;
        contacts.Slope=0;Check(!evidence.Validate("p"),"invalid floor normal revokes");contacts.Slope=1;
        policy.Bounds=false;Check(!evidence.Validate("p"),"source bounds revoke");policy.Bounds=true;
        var f=new Fixture(true);var roots=new Roots();var port=new SourceRootPlacement(source,roots);
        var rules=new SessionRules(world.Identity.MapId,"respawn",world.Identity.PayloadSha256,2,8,true,false,true,true,1);
        var exact=new RootPlacement(f.Cmd(),"p",f.H.Read("p"),rules,2,sites[0],1);IPreparedRootPlacement prepared;
        contacts.Room=false;Check(!port.Prepare(exact,out prepared)&&roots.Prepares==0,"contacts reject before root prepare");contacts.Room=true;
        Check(port.Prepare(exact,out prepared)&&prepared.Validate(),"actual source wrapper prepares root");
        contacts.Hazard=true;Check(!prepared.Validate()&&roots.Commits==0,"changed contacts deny before root publication");prepared.Abort();prepared.Abort();contacts.Hazard=false;
        Check(port.Prepare(exact,out prepared)&&prepared.Validate(),"corrected retry");prepared.CommitValidated();prepared.CommitValidated();prepared.Abort();Check(roots.Commits==1,"unique publication preserved");
        var wrong=new SpawnSite("forged",sites[0].Feet,-1,true,false);Check(!source.TryEvidence(world.Identity.MapId,"p",wrong,out evidence),"source identity substitution rejects");
        var session=new Session("s","content",rules,f,f,f,source,f);string reconnect;
        Check(session.Join(C(session,"p"),new JoinRequest(Protocol.Name,1,"content","P",null,false),out reconnect).Accepted,"host actual join");
        Check(session.BeginLoading(C(session),rules,true).Accepted&&session.Loaded(C(session,"p"),rules.MapHash).Accepted&&session.WorldReady(C(session),rules.MapHash).Accepted&&session.Activate(C(session)).Accepted&&session.Spawn(C(session,"p")).Accepted,"host actual source placement");
        var health=new HealthService("s",session.Epoch,2,true,f,f,f,60,1,0);Check(health.Register(C(session),"p",0,false).Accepted,"host actual health");
        var host=new LifecycleHost(health,session,f,"host",source,roots);Check(host.BindCurrent(C(session),"p").Accepted,"host binds actual admitted token");
        foreach(var e in health.DrainTransitions())Check(host.Project(C(session),e,1).Accepted,"host registered projection");
        var before=health.Read("p");Check(health.ApplyLoss(C(session),"p",before.Revision,100,before.Shield,false).Accepted,"host death");f.Time=1;
        Coordinator.PreparedLifecycle life;Check(!host.PrepareRespawn(C(session),"p",sites[0],2,out life),"host rejects undelivered death");
        foreach(var e in health.DrainTransitions())Check(host.Project(C(session),e,2).Accepted,"host current death projection");
        Check(host.PrepareRespawn(C(session),"p",sites[0],2,out life)&&life.Validate()&&life.CommitValidated(),"actual host source health session publication");
        Check(health.Read("p").Life==LifeState.Alive&&session.Get("p").Life==SessionLife.Alive&&session.Get("p").Position.Y==.12f,"actual projected respawn site");
        Check(session.Join(C(session,"watch"),new JoinRequest(Protocol.Name,1,"content","Watcher",null,true),out reconnect).Accepted&&session.Loaded(C(session,"watch"),rules.MapHash).Accepted,"actual spectator admission");
        var watcher=session.Get("watch");Check(health.Register(C(session),"watch",watcher.Team,true).Accepted&&host.BindCurrent(C(session),"watch").Accepted,"actual spectator binding");
        foreach(var e in health.DrainTransitions())Check(host.Project(C(session),e,2).Accepted,"host alive/spectator projection");
        Check(session.ProjectedLife("watch").Life==LifeState.Spectator&&session.Get("watch").Life==SessionLife.Spectator,"spectator remains spectator");
        Check(!host.PrepareRespawn(C(session),"watch",sites[0],2,out life),"spectator has no spawn publication");
        policy.Approved=false;bool denied=false;try{source.Sites(world.Identity.MapId,false);}catch(System.IO.InvalidDataException){denied=true;}Check(denied,"source approval withdrawal rejects");
        Console.WriteLine("PASS "+checks+" source routing assertions; analytic contacts/content/root only");
    }
}
