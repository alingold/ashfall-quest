using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Modes;

internal sealed class Roster : IModeRoster
{
    public Dictionary<string,ModeActor> Rows=new Dictionary<string,ModeActor>();
    public void Set(string id,int team,string life,bool spectator) { Rows[id]=new ModeActor(id,id,team,life,true,spectator); }
    public bool TryGet(string id,out ModeActor row) { return Rows.TryGetValue(id,out row); }
    public ModeActor[] Snapshot() { var rows=new List<ModeActor>(Rows.Values); return rows.ToArray(); }
}
internal sealed class Gate : IAuthorityGate
{
    public bool Allow=true;
    public RuleResult Validate(CommandContext c) { return Allow?RuleResult.Accept():RuleResult.Reject("external_gate"); }
}
internal static class Tests
{
    private static int count,sequence;
    private static long commandEpoch=7;
    private static void Check(bool ok,string label) { if(!ok) throw new Exception(label); count++; }
    private static void Near(double actual,double expected,string label) { Check(Math.Abs(actual-expected)<.000001,label); }
    private static CommandContext C(string actor) { sequence++; return new CommandContext("room",actor,"c"+sequence,sequence,commandEpoch,true); }
    private static Roster TeamRoster() { var r=new Roster(); r.Set("a",0,"alive",false); r.Set("b",1,"alive",false); r.Set("c",1,"alive",false); return r; }
    private static RoundController Round(ModeRules rules,Roster r,Gate gate) { commandEpoch=7; return new RoundController(rules,r,"server","room",7,gate,1000); }
    private static void ScoringAndRounds()
    {
        var r=TeamRoster(); var g=new Gate(); var round=Round(new ModeRules("respawn",2,2,0,2),r,g);
        Check(!round.StartRound(C("a"),0).Accepted,"player cannot start round");
        Check(round.StartRound(C("server"),0).Accepted,"start loading round");
        var command=C("server");
        Check(round.RecordKnockdown(command,new Knockdown("b","a",1),1).Accepted,"hostile knockdown");
        Check(round.PersonalScore("a")==1&&round.Scores()[0].Id=="team:0"&&round.Scores()[0].Score==1,"personal and team score");
        Check(!round.RecordKnockdown(command,new Knockdown("b","a",1),1).Accepted,"packet replay");
        Check(!round.RecordKnockdown(C("server"),new Knockdown("b","a",1),1).Accepted,"same life different command");
        Check(round.RecordKnockdown(C("server"),new Knockdown("c","b",1),2).Accepted,"friendly knockout event");
        Check(round.PersonalScore("b")==0,"friendly kill no score");
        Check(round.RecordKnockdown(C("server"),new Knockdown("a","a",1),3).Accepted,"suicide event");
        Check(round.PersonalScore("a")==1,"suicide no score");
        Check(round.RecordKnockdown(C("server"),new Knockdown("b","a",2),4).Accepted,"new life score");
        Check(round.Stage==RoundStage.Results&&round.EndReason=="score_limit","score cap results");
        Check(!round.RecordKnockdown(C("server"),new Knockdown("b","a",3),4).Accepted,"results freezes score");
        Check(!round.AdvanceResults(C("server"),5,7).Accepted,"next round requires fresh epoch");
        var stale=C("server");
        Check(round.AdvanceResults(C("server"),5,8).Accepted&&round.Stage==RoundStage.Loading,"configured next round");
        Check(!round.StartRound(stale,6).Accepted,"previous round epoch rejected"); commandEpoch=8;
        Check(round.StartRound(C("server"),6).Accepted&&round.RoundIndex==2&&round.Scores().Length==2&&round.Scores()[0].Score==0&&round.Scores()[1].Score==0,"round reset");
        Check(round.RecordKnockdown(C("server"),new Knockdown("b","a",1),7).Accepted,"round resets life dedup");
        Check(round.RecordKnockdown(C("server"),new Knockdown("b","a",2),8).Accepted,"second round cap");
        Check(round.AdvanceResults(C("server"),9,9).Accepted&&round.Stage==RoundStage.Complete,"final round complete");
        Check(!round.StartRound(C("server"),10).Accepted,"cannot exceed rounds");
    }
    private static void Lifecycle()
    {
        var r=TeamRoster(); var g=new Gate(); var round=Round(new ModeRules("revive",2,0,0,1),r,g);
        round.StartRound(C("server"),0); r.Set("b",1,"downed",false);
        round.RecordKnockdown(C("server"),new Knockdown("b","a",1),1);
        Check(round.Stage==RoundStage.Match,"another living teammate keeps revive round open");
        r.Set("c",1,"downed",false); r.Set("observer",2,"alive",true);
        round.RecordKnockdown(C("server"),new Knockdown("c","a",1),2);
        Check(round.Stage==RoundStage.Results&&round.EndReason=="last_side","downed side loses; spectator excluded");
        r=TeamRoster(); round=Round(new ModeRules("respawn",0,0,10,1),r,g); round.StartRound(C("server"),10);
        round.RecordKnockdown(C("server"),new Knockdown("c","b",1),11);
        Check(round.Scores()[0].Id=="b","FFA same source team can score");
        round.Tick(C("server"),19.99); Check(round.Stage==RoundStage.Match,"before timer boundary");
        Check(!round.Tick(C("server"),18).Accepted,"clock regression rejected");
        round.Tick(C("server"),20); Check(round.Stage==RoundStage.Results&&round.EndReason=="time_limit","timer boundary");
        round=Round(ModeRules.Preset("practice"),r,g); round.StartRound(C("server"),0); round.Tick(C("server"),100000);
        Check(round.Stage==RoundStage.Match&&Double.IsPositiveInfinity(round.SecondsRemaining(100000)),"unlimited practice");
        Check(ModeRules.Preset("practice").Damage==0&&ModeRules.Preset("practice").InfiniteAmmo&&!ModeRules.Preset("practice").ZoneEnabled,"practice flags");
        Check(ModeRules.Default.CanDown&&!ModeRules.Default.CanRespawn&&ModeRules.Preset("free_for_all").Teams==0,"mode rules");
        Check(new ModeRules("bad",99,999,9999,0).Mode=="revive"&&new ModeRules("bad",99,999,9999,0).Rounds==1,"validation");
        g.Allow=false; round=Round(ModeRules.Default,r,g); Check(!round.StartRound(C("server"),0).Accepted,"external authority denial");
    }
    private static void Pings()
    {
        var r=TeamRoster(); var g=new Gate(); var p=new PingService(r,g,"room",7,"range",2,.5,1000);
        var point=new Float3(1000,2,-1000); var normal=new Float3(0,2,0); var c=C("a");
        Check(p.Place(c,RoundStage.Match,point,normal,2,-1,1).Accepted,"unbounded R22 ping reach");
        Check(!p.Place(c,RoundStage.Match,point,normal,2,-1,1).Accepted,"ping replay");
        Check(!p.Place(C("a"),RoundStage.Match,point,normal,2,-1,1.499).Accepted,"cooldown before boundary");
        Check(p.Place(C("a"),RoundStage.Match,point,normal,8,29,1.5).Accepted,"cooldown exact boundary");
        var marks=p.Visible("a","range",7,2); Check(marks.Length==1&&marks[0].Action=="weapon"&&marks[0].Catalog==29,"one mark replaces old");
        Near(marks[0].Normal.Y,1,"normalized normal");
        Check(p.Visible("b","range",7,2).Length==0,"enemy ping hidden");
        r.Set("ally",0,"downed",false); Check(p.Visible("ally","range",7,2).Length==1,"downed ally can see ping");
        Check(p.Visible("a","valley",7,2).Length==0&&p.Visible("a","range",8,2).Length==0,"map epoch isolation");
        Check(p.Visible("a","range",7,31.5).Length==1&&p.Visible("a","range",7,31.501).Length==0,"TTL boundary");
        Check(!p.Place(C("a"),RoundStage.Match,point,new Float3(0,0,0),1,-1,3).Accepted,"zero normal rejected");
        Check(!p.Place(C("a"),RoundStage.Match,new Float3(float.NaN,0,0),normal,1,-1,3).Accepted,"nonfinite point");
        Check(!p.Place(C("a"),RoundStage.Match,point,normal,8,-1,3).Accepted,"weapon requires catalog");
        r.Set("a",0,"downed",false); Check(!p.Place(C("a"),RoundStage.Match,point,normal,1,-1,3).Accepted,"downed cannot ping");
        r.Set("a",0,"alive",false); var bad=new CommandContext("room","a","bad",999,8,true);
        Check(!p.Place(bad,RoundStage.Match,point,normal,1,-1,3).Accepted,"old epoch");
        g.Allow=false; Check(!p.Place(C("a"),RoundStage.Match,point,normal,1,-1,3).Accepted,"ping gate denial"); g.Allow=true;
        p=new PingService(r,g,"room",7,"range",0,0,1);
        Check(p.Place(C("a"),RoundStage.Match,point,normal,1,-1,0).Accepted,"zero cooldown parity");
        Check(!p.Place(C("a"),RoundStage.Match,point,normal,1,-1,1).Accepted,"journal capacity fails closed");
        Check(p.Visible("ally","range",7,0).Length==0,"FFA pings self only");
    }
    private static void Map()
    {
        var projection=new TacticalProjection(Float3.FromGodot(0,0,600),-50,-50,50,50);
        var uv=projection.Project(Float3.FromGodot(-50,100,550),false); Near(uv.X,0,"range west"); Near(uv.Y,0,"range north");
        uv=projection.Project(Float3.FromGodot(50,0,650),false); Near(uv.X,1,"range east"); Near(uv.Y,1,"range south");
        uv=projection.Pixel(Float3.FromGodot(0,0,600)); Near(uv.X,635,"pixel center x"); Near(uv.Y,425,"pixel center y");
        uv=projection.Project(Float3.FromGodot(-100,0,700),true); Near(uv.X,0,"clamped x"); Near(uv.Y,1,"clamped y");
        uv=TacticalProjection.Heading(new Float3(0,0,1)); Near(uv.Y,-1,"Unity forward north-up");
        bool threw=false; try { new TacticalProjection(new Float3(0,0,0),1,0,1,10); } catch(ArgumentException) { threw=true; } Check(threw,"degenerate bounds rejected");
        var r=TeamRoster(); ModeActor a=r.Rows["a"],b=r.Rows["b"];
        Check(TacticalProjection.MarkerVisible(a,b,2,true),"source alive opponent visible");
        r.Set("b",1,"downed",false); Check(!TacticalProjection.MarkerVisible(a,r.Rows["b"],2,true),"source downed opponent hidden");
        Check(!TacticalProjection.MarkerVisible(a,b,2,false),"remote requires pose");
    }
    public static int Main()
    {
        try { ScoringAndRounds(); Lifecycle(); Pings(); Map(); Console.WriteLine("PASS: "+count+" behavioral assertions"); return 0; }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
