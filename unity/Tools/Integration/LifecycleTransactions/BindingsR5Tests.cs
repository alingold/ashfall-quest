using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.LifecycleTransactions;
sealed class BindingsR5Tests
{
    static void Check(bool value,string name){Fixture.Must(value,name);}
    public static void Main()
    {
        var f=new Fixture(true);var auth=new SessionLifecycleIdentity(f.S,f.H,f,"host","p");
        var h=f.H.Read("p");Check(auth.Validate(f.Cmd(),"p","p",h.ConnectionGeneration),"actual admitted identity");
        Check(!auth.Validate(f.Cmd("p"),"p","p",h.ConnectionGeneration),"client cannot manage lifecycle");
        Session.PreparedLifeProjection projection;
        Check(f.S.PrepareLifeProjection(f.Cmd(),"p","p",h,1,null,out projection),"prepare exact projection");
        object old=f.S.GetConnectionIdentity("p");
        Check(f.S.Disconnect(f.Cmd("p"),false).Accepted,"disconnect actual Session");
        string token;Check(f.S.Join(f.Cmd("p"),new JoinRequest(Protocol.Name,1,"content","P",f.Token,false),out token).Accepted,"same-name reconnect actual Session");
        Check(!ReferenceEquals(old,f.S.GetConnectionIdentity("p")),"opaque token rotates");
        Check(!auth.Validate(f.Cmd(),"p","p",h.ConnectionGeneration),"binding does not follow reused Member");
        Check(!projection.Validate(),"projection cannot cross actual connection incarnation");projection.Abort();projection.Abort();
        var d=new Fixture(true);h=d.H.Read("p");Check(d.H.ApplyLoss(d.Cmd(),"p",h.Revision,100,h.Shield,false).Accepted,"death without delivery");d.Time=1;
        Coordinator.PreparedLifecycle respawn;
        Check(!d.C.PrepareRespawn(d.Cmd(),"p",d.Site,2,out respawn)&&d.RootCommits==0,"delayed projection blocks before root prepare");
        Check(d.C.ProjectCurrent(d.Cmd(),"p",2),"deliver exact death projection");
        Check(d.C.PrepareRespawn(d.Cmd(),"p",d.Site,2,out respawn),"now prepare");
        old=d.S.GetConnectionIdentity("p");Check(d.S.Disconnect(d.Cmd("p"),false).Accepted,"disconnect prepared actor");
        Check(d.S.Join(d.Cmd("p"),new JoinRequest(Protocol.Name,1,"content","P",d.Token,false),out token).Accepted,"rejoin prepared actor");
        Check(!respawn.CommitValidated()&&d.RootCommits==0&&d.H.Read("p").Life==LifeState.Dead,"incarnation change denies before health/root writes");respawn.Abort();respawn.Abort();
        var g=new Fixture(true);g.Kill();g.Time=1;Check(g.C.PrepareRespawn(g.Cmd(),"p",g.Site,2,out respawn),"health generation prepare");
        h=g.H.Read("p");Check(g.H.SetConnected(g.Cmd(),"p",h.Revision,false).Accepted,"real health disconnect");h=g.H.Read("p");Check(g.H.SetConnected(g.Cmd(),"p",h.Revision,true).Accepted,"real health reconnect");
        Check(!respawn.CommitValidated()&&g.RootCommits==0&&g.H.Read("p").Life==LifeState.Dead,"numeric generation denies before publication");respawn.Abort();
        Console.WriteLine("PASS "+Fixture.Checks+" actual aggregate setup/regression assertions; root and geometry fixtures are synthetic");
    }
}
