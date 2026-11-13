import pathlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
p=R/'unity/Tools/Integration/MatchDirector/r5/AdditionalTests.cs'
t=(R/'campaign-staging/20261104-three-chats/unity/MatchDirectorR4/r5/BuilderTests.cs').read_text().split('    static void WarmupAndRounds()')[0]
t=t.replace('IKnockdownAttribution\n','IKnockdownAttribution,ILogicalPlayerIdentity\n').replace('public double Now {get;set;}', 'public bool TryResolve(string s,long e,string c,out string id){id=c.StartsWith("new-")?"a":c;return s=="test";}\n        public double Now {get;set;}')
t=t.replace('E,E,E,capacity);','E,E,E,capacity,E,null,null);')
t=t.replace('int rounds,int capacity)','int rounds,int capacity,ScoreReceiptJournal journal=null)').replace('capacity,E,null,null);','capacity,E,null,journal);')
t+='''
    static void Reconnect(string mode,bool downed)
    {
        var r=new Rig(mode,2,0,0,1,2);string token=r.Join("a",false,null);r.Join("b",false,null);r.Join("c",false,null);r.Join("d",false,null);r.Load("a","b","c","d");
        if(downed)r.Loss("a",true);
        else {var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,23,0,false).Accepted,"injured");r.Deliver();}
        long life=r.D.LifeSequence("a"),gen=r.H.Read("a").LifeGeneration;float health=r.H.Read("a").Health;int team=r.H.Read("a").Team;
        Check(r.S.Disconnect(r.C("a"),false).Accepted,"disconnect session");Check(r.H.SetConnected(r.C("host"),"a",r.H.Read("a").Revision,false).Accepted,"disconnect health");r.Deliver();
        r.Join("new-a",false,token);Check(r.D.BindConnection("new-a").Accepted,"authenticated bind");Check(r.S.Loaded(r.C("new-a"),"maphash").Accepted,"reload");
        Check(r.H.SetConnected(r.C("host"),r.D.CanonicalActor("new-a"),r.H.Read("a").Revision,true).Accepted,"canonical reconnect health");r.Deliver();
        ModeActor a;Check(r.D.TryGet("new-a",out a)&&a.Id=="a"&&a.Team==team,"canonical roster alias");
        Check(r.H.Read("new-a")==null&&r.H.Read("a").Health==health&&r.H.Read("a").LifeGeneration==gen&&r.D.LifeSequence("a")==life,"no new health or life");
        Check(r.D.BindConnection("new-a").Accepted,"binding replay");Check(r.D.LifeSequence("new-a")==life,"alias life sequence");Check(!r.D.BindConnection("unknown").Accepted,"unknown rejected");
        Check(r.S.Disconnect(r.C("new-a"),false).Accepted,"disconnect new actor");Check(r.H.SetConnected(r.C("host"),"a",r.H.Read("a").Revision,false).Accepted,"disconnect again health");r.Deliver();
        r.Join("new-again",false,token);Check(r.D.BindConnection("new-again").Accepted,"second remap");Check(r.D.CanonicalActor("new-again")=="a","canonical survives remaps");
    }
    static void DurableReceipt()
    {
        var r=new Rig("respawn",0,0,0,1,1);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
        var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,100,0,false).Accepted,"real committed death");Check(r.E.Attribution.Capture(hit,hit.CommandId,"a").Accepted,"capture");var e=r.H.DrainTransitions()[0];
        string path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Tests).Assembly.Location),"journal-"+Guid.NewGuid().ToString("N")+".json");
        var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(store.Load(false).Accepted,"load missing real store");var j=new ScoreReceiptJournal(store);
        Check(j.Retain(e,"b",1).Accepted,"retain committed uncredited receipt durably");
        var reopened=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(reopened.Load(false).Accepted,"disk reopen before credit");var recovered=new ScoreReceiptJournal(reopened);string payload;bool ack;
        Check(recovered.TryRead(ScoreReceiptJournal.Key(e),out payload,out ack)&&!ack&&payload.Length>0,"recover uncredited receipt");
        Check(recovered.Retain(e,"b",1).Accepted,"exact retained retry");Check(!recovered.Retain(e,"a",1).Accepted,"cannot replace attribution");
        Check(r.D.Deliver(e).Accepted&&r.D.Deliver(e).Accepted,"real score and lost ack replay");Check(Score(r.D,"b")==1,"exactly once score");
        Check(recovered.Acknowledge(e).Accepted,"durable acknowledge");
        var third=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(third.Load(false).Accepted,"reopen acknowledged");Check(new ScoreReceiptJournal(third).TryRead(ScoreReceiptJournal.Key(e),out payload,out ack)&&ack,"ack retains original receipt");
    }
    static void LateAndBudget(bool late)
    {
        string path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Tests).Assembly.Location),"late-"+Guid.NewGuid().ToString("N")+".json");
        var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(store.Load(false).Accepted,"late store load");var journal=new ScoreReceiptJournal(store);
        var r=new Rig("respawn",0,0,1,2,1,journal);r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
        Check(r.D.UpdateLimits(r.C("host"),new LiveLimits(0,1)).Accepted,"fill limit budget");Check(!r.D.UpdateLimits(r.C("host"),new LiveLimits(0,1)).Accepted,"limit budget remains bounded");
        var hit=r.C("b");Check(r.H.ApplyLoss(hit,"a",r.H.Read("a").Revision,100,0,false).Accepted,"damage committed before clock");Check(r.E.Attribution.Capture(hit,hit.CommandId,"a").Accepted,"attribution");var e=r.H.DrainTransitions()[0];
        if(late){r.E.Now=1;Check(r.D.Heartbeat().Accepted,"clock before retained delivery");}
        using(var held=new System.IO.FileStream(path+".lock",System.IO.FileMode.OpenOrCreate,System.IO.FileAccess.ReadWrite,System.IO.FileShare.None))
        {Check(!r.D.Deliver(e).Accepted,"real Persistence family contention rejects publication");Check(Score(r.D,"b")==0&&r.D.LifeSequence("a")==1,"no score or new life on durable failure");}
        Check(r.D.Deliver(e).Accepted&&r.D.Deliver(e).Accepted,"delivery and lost ack");Check(Score(r.D,"b")== (late?0:1),"late remains uncredited; timely score independent from limit budget");
        Check(journal.Pending("test",r.S.Epoch).Length==(late?1:0),"durable uncredited index");
        var reopened=new Ashfall.Modules.Persistence.AtomicSettingsStore(path);Check(reopened.Load(false).Accepted,"reopen real receipt family");Check(new ScoreReceiptJournal(reopened).Pending("test",r.S.Epoch).Length==(late?1:0),"discover retained uncredited after reopen");
        if(!late){r.E.Now=1;Check(r.D.Heartbeat().Accepted,"clock after scoring");}
        Check(!r.D.IntegrationReadiness().Accepted,"missing real Session projection fails readiness");
        r.Load("a","b");Check(r.D.Epoch==2,"next round not blocked by retained receipt");r.E.Now=2;Check(r.D.Heartbeat().Accepted,"next round deadline");Check(r.D.AcknowledgeComplete().Accepted,"completion independent lane");
    }
    static int Main(){try{Reconnect("respawn",false);Reconnect("revive",true);DurableReceipt();LateAndBudget(false);LateAndBudget(true);Console.WriteLine("PASS "+assertions+" additional assertions; canonical identity and real Persistence disk reopen; Session down projection remains blocked");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
'''
p.write_text(t)
