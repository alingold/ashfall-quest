from pathlib import Path
p=Path(r'C:\Users\aling\Documents\ashfall\unity\Tools\Integration\RuntimeLoop\Scenario.cs')
s=p.read_text().replace('IWorldBuildPolicy,ISpawnQuery','IWorldBuildPolicy,ISpawnQuery,Ashfall.Modules.Sessions.ISessionClock,Ashfall.Modules.Sessions.ISessionManagement,Ashfall.Modules.Sessions.IReconnectTokens,Ashfall.Modules.Sessions.ISpawnWorld,Ashfall.Integration.MatchDirector.IMatchClock,Ashfall.Integration.MatchDirector.IKnockdownAttribution')
s=s.replace('public bool InMatch{','public long Tick{get{return tick;}} public bool CanManage(CommandContext c){return c.ActorId=="host";} public string Issue(){return "token-"+(++serial);} public Ashfall.Modules.Sessions.SpawnSite[] Sites(string map,bool warmup){return new[]{new Ashfall.Modules.Sessions.SpawnSite("spawn",new Float3(0,.12f,0),-1,true,false)};} public bool IsSafe(string map,string actor,Float3 feet){return feet.IsFinite;} public string KillerFor(HealthTransition e){return null;}\n    public bool InMatch{')
start='''
    void SessionComposition()
    {
        tick=0;Now=0;
        var session=new Ashfall.Modules.Sessions.Session("s","content",new Ashfall.Modules.Sessions.SessionRules("synthetic-loop","respawn","maphash",0,4,true,false,true,true,1),this,this,this,this,this);
        var director=new Ashfall.Integration.MatchDirector.Director(session,new Ashfall.Modules.Modes.ModeRules("respawn",0,0,0,1),"host",this,this,this,128);
        foreach(string id in new[]{"a","b"}){string token;var join=new CommandContext("s",id,"join-"+id,0,session.Epoch,true);Check(session.Join(join,new Ashfall.Modules.Sessions.JoinRequest(Protocol.Name,1,"content",id,null,false),out token).Accepted,"actual Sessions join "+id);Check(director.ObserveMember(id).Accepted,"actual director roster "+id);}
        Check(director.BeginLoading().Accepted&&session.Epoch==1,"actual mode/session loading epoch");
        foreach(string id in new[]{"a","b"})Check(session.Loaded(C(id),"maphash").Accepted,"actual map loaded "+id);
        Check(session.WorldReady(C("host"),"maphash").Accepted&&session.Activate(C("host")).Accepted,"actual session activates");
        var h=new HealthService("s",1,0,true,this,this,this,60,1,0);
        foreach(string id in new[]{"a","b"}){Check(session.Spawn(C(id)).Accepted,"actual Sessions spawn "+id);Check(h.Register(C("host"),id,-1,false).Accepted,"actual health spawn binding "+id);}
        var bridge=new CombatHealthBridge(h,this,this,this,this,"s",1);
        var transfers=new TransferBridge("s",1,10,this,this,this,this,this);transfers.AddActor("a");transfers.AddActor("b");
        var composed=new HostLoop("s",1,"host",this,h,bridge,transfers,this,session,director);var ra=new Root{Position=new Float3(0,.12f,0)};var rb=new Root{Position=new Float3(2,.12f,0)};composed.AddActor("a",ra);composed.AddActor("b",rb);
        ActorResult[] result;tick=1;Now=.1;Check(composed.Tick(C("host"),.1f,new[]{A("b"),A("a")},out result).Accepted,"host tick routes actual registration to director");
        Check(director.LifeSequence("a")==1&&director.LifeSequence("b")==1,"two actual director life bindings");Check(director.StartLoadedRound().Accepted,"actual Modes round starts");
        tick=2;Now=.2;Check(composed.Tick(C("host"),.1f,new[]{A("a",move:1),A("b")},out result).Accepted,"host tick actual director heartbeat");
        Check(composed.RecordPosition(C("a")).Accepted&&session.Get("a").Position.Z==ra.Position.Z,"actual Sessions position uses root port");
        Check(h.ApplyLoss(C("host"),"a",h.Read("a").Revision,100,100,false).Accepted,"actual respawn mode death");
        tick=3;Now=.3;Check(composed.Tick(C("host"),.1f,new[]{A("a"),A("b")},out result).Accepted&&session.Get("a").Life==Ashfall.Modules.Sessions.SessionLife.Eliminated,"actual death outbox eliminates session member");
        Now=2;tick=20;Check(session.Spawn(C("a")).Accepted&&h.Spawn(C("host"),"a",h.Read("a").Revision,100,100).Accepted,"actual due Sessions and Health respawn APIs (not atomic root lifecycle)");
        Check(composed.Tick(C("host"),.1f,new[]{A("a"),A("b")},out result).Accepted&&director.LifeSequence("a")==2,"actual respawn transition reaches director");
    }
'''
s=s.replace('    static int Main(string[] args)',start+'    static int Main(string[] args)').replace('new Scenario().Run();','var scenario=new Scenario();scenario.Run();scenario.SessionComposition();').replace('session-director-binding, ','')
p.write_text(s)
