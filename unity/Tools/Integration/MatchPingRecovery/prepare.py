import pathlib,json,shutil,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchPingRecoveryR12';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchPingRecoveryR12'
rows=json.loads((D/'inputs.json').read_text())
p=R/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs';q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
if not q.exists():
 shutil.copyfile(p,q);rows.append(dict(source=p.relative_to(R).as_posix(),snapshot=q.relative_to(R).as_posix(),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));(D/'inputs.json').write_text(json.dumps(rows,indent=2))
p=S/'before/campaign-staging/20261104-three-chats/unity/reviews/DirectorArchiveReviewR11/IndependentTests.cs'
t=p.read_text().split('    static int Main')[0]
extra=r'''
    static Director.QuarantinedPingArchive RestorePair(byte[] bytes,Environment e,HealthService h,Ashfall.RecoveryContracts.CheckpointBinding binding=null)
    {return Director.RestorePartialPingArchive(bytes,Ashfall.RecoveryContracts.RecoveryWire.Hash(bytes),binding??AB(),new ModeRules("respawn",0,10,100,2),"host",8,e,e,e,e,e,e,e,h,null);}
    static void ReadPair(string folder)
    {
        var e=new Environment();byte[] hb=System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,"health"));HealthService h;
        Check(HealthService.TryRestoreCheckpoint(new CommandContext("test","host","restore",9999,1,true),new string('a',64),Ashfall.RecoveryContracts.RecoveryWire.Hash(hb),hb,0,true,e,e,e,out h,60,1,0),"cold Health owner");
        byte[] bytes=System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder,"pair"));var x=RestorePair(bytes,e,h);
        Check(x.HistoricalLife("a").Life==LifeState.Alive&&h.Read("a").Life==LifeState.Dead,"original historical life retained");
        int score=0;foreach(var row in x.Scores())if(row.Id=="b")score=row.Score;
        Check(score==(folder.EndsWith("after")?1:0)&&x.PendingScore>0,"score cut retained without replay");
        Check(x.PhysicalPending("test",1)==1,"actual physical pending");
        Check(x.Visible("b","map",1,0).Length==1,"cold private ping state");
        var point=new Float3(1,2,3);var normal=new Float3(0,1,0);
        Check(!x.ProbePing(new CommandContext("test","b","ping-original",1,1,true),RoundStage.Match,point,normal,1,-1,0).Accepted,"replay retained");
        Check(x.ProbePing(new CommandContext("test","b","fresh",2,1,true),RoundStage.Match,point,normal,1,-1,0).Reason=="cooldown","cooldown retained");
        Check(x.ProbePing(new CommandContext("test","b","fresh",2,1,true),RoundStage.Match,point,normal,1,-1,6).Accepted,"quarantined owner retry after cooldown");
        Check(x.AdoptionBlocker.Contains("physical-source-binding"),"adoption still refused");
    }
    static int Main(string[] args)
    {
        try {
            if(args.Length>0){ReadPair(args[0]);Console.WriteLine("PAIR COLD PASS "+assertions);return 0;}
            foreach(bool after in new[]{false,true}){
                string path=PathFor("pair-receipt");var store=new Ashfall.Modules.Persistence.AtomicSettingsStore(path,after?(Ashfall.Modules.Persistence.IAtomicSettingsFiles)new FailSecondWrite():new Ashfall.Modules.Persistence.PhysicalSettingsFiles());Check(store.Load(false).Accepted,"store");
                var r=new Rig("respawn",0,10,100,2,8,new ScoreReceiptJournal(store));r.Join("a",false,null);r.Join("b",false,null);r.Load("a","b");
                var ping=r.D.CreateRecoveryPingOwner(5,8192);Check(ping.Place(new CommandContext("test","b","ping-original",1,1,true),new Float3(1,2,3),new Float3(0,1,0),1,-1).Accepted,"new versioned owner ping");
                var ev=Kill(r);r.E.DenyScore=!after;Check(!r.D.Deliver(ev).Accepted,"actual deferred/ACK cut");
                byte[] family=ReceiptFamilyArchive.Capture(path,new Ashfall.Modules.Persistence.PhysicalSettingsFiles());byte[] bytes=ping.CapturePartialArchive(AB(),family);
                string folder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Tests).Assembly.Location),after?"pair-after":"pair-before");System.IO.Directory.CreateDirectory(folder);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"pair"),bytes);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"health"),r.H.CreateCheckpoint(r.C("host"),new string('a',64)));
                ReadPair(folder);ReadPair(folder);
                using(var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(typeof(Tests).Assembly.Location,"\""+folder+"\""){UseShellExecute=false,RedirectStandardOutput=true})){Console.Write(child.StandardOutput.ReadToEnd());child.WaitForExit();Check(child.ExitCode==0,"cold subprocess");}
                for(int n=0;n<bytes.Length;n++){byte[] bad=new byte[n];Array.Copy(bytes,bad,n);Refuse(()=>RestorePair(bad,r.E,r.H),"truncated envelope "+n);}
                Refuse(()=>RestorePair(bytes,r.E,r.H,new Ashfall.RecoveryContracts.CheckpointBinding("other","catalog","test","match",1,0,0,100)),"source mismatch");
                Refuse(()=>RestorePair(bytes,r.E,r.H,new Ashfall.RecoveryContracts.CheckpointBinding("build","catalog","test","match",2,0,0,100)),"epoch mismatch");
                Refuse(()=>ping.CapturePartialArchive(AB(),new byte[4*1024*1024+1]),"maximum family bound");
                r.E.Now=1;Check(r.D.Heartbeat().Accepted,"advance director");Refuse(()=>ping.CapturePartialArchive(AB(),family),"partial owner clocks reject rather than trust capture time");
                r.E.DenyScore=false;Check(r.D.Deliver(ev).Accepted&&Score(r.D,"b")==1&&r.S.Get("a").Deaths==1,"original retry never duplicates credited score");
            }
            bool gate=false;try{Director.RequireCompleteRecovery();}catch(InvalidOperationException){gate=true;}Check(gate,"full proof unavailable");
            Console.WriteLine("PAIR PASS "+assertions);return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
'''
(R/'unity/Tools/Integration/MatchPingRecovery/Tests.cs').write_text(t+extra)
