using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Modes;
sealed class R2Roster : IModeRoster {
 public Dictionary<string,ModeActor> Rows=new Dictionary<string,ModeActor>();
 public void Set(string id,string name,int team,bool spectator) { Rows[id]=new ModeActor(id,name,team,"alive",true,spectator); }
 public bool TryGet(string id,out ModeActor a) { return Rows.TryGetValue(id,out a); }
 public ModeActor[] Snapshot() { return new List<ModeActor>(Rows.Values).ToArray(); }
}
sealed class R2Gate : IAuthorityGate { public bool Allow=true; public RuleResult Validate(CommandContext c) { return Allow?RuleResult.Accept():RuleResult.Reject("denied"); } }
static class R2Tests {
 static int n,seq;
 static CommandContext C(long epoch,string actor="server") { seq++; return new CommandContext("s",actor,"c"+seq,seq,epoch,true); }
 static void T(bool b,string s) { if(!b)throw new Exception(s); n++; }
 static R2Roster R() { var r=new R2Roster();r.Set("a","Zed",0,false);r.Set("b","amy",1,false);r.Set("c","Bob",1,false);return r; }
 static RoundController New(R2Roster r,R2Gate g,int cap,int seconds,int rounds=2,int teams=2) { return new RoundController(new ModeRules("respawn",teams,0,seconds,rounds),r,"server","s",1,g,cap); }
 public static int Main() { try {
 var r=R(); var g=new R2Gate(); var v=New(r,g,1,10,3); long epoch=1;
 for(int round=0;round<3;round++) {
 var start=C(epoch); T(v.StartRound(start,round*20).Accepted,"start tiny capacity");
 T(!v.RecordKnockdown(start,new Knockdown("b","a",1),round*20).Accepted,"cross lifecycle receipt replay rejected"); var hit=C(epoch);
 T(v.RecordKnockdown(hit,new Knockdown("b","a",1),round*20+1).Accepted,"fill external journal");
 T(!v.RecordKnockdown(hit,new Knockdown("b","a",2),round*20+2).Accepted,"no replay active eviction");
 T(!v.RecordKnockdown(C(epoch),new Knockdown("b","a",2),round*20+2).Accepted,"full command journal fails closed");
 T(!v.UpdateLimits(C(epoch),new LiveLimits(0,0),round*20+2).Accepted,"limits share external admission");
 for(int i=2;i<10;i++)T(v.AdvanceClock(round*20+i).Accepted,"trusted ticks independent of journal");
 T(v.Tick(C(epoch),round*20+10).Accepted&&v.Stage==RoundStage.Results,"timer after full journal");
 T(!v.AdvanceResults(hit,round*20+11,epoch+1).Accepted,"cross gameplay receipt replay rejected");
 var ack=C(epoch);T(v.AdvanceResults(ack,round*20+11,epoch+1).Accepted,"ack independent of saturation");
 T(!v.AdvanceResults(ack,round*20+11,epoch+1).Accepted,"ack receipt cannot repeat");
 T(!v.StartRound(C(epoch),round*20+12).Accepted,"old epoch rejected");epoch++;
 } T(v.Stage==RoundStage.Complete,"three epochs complete");
 v=New(r,g,1,0);v.StartRound(C(1),0);var tick=C(1);
 T(v.Tick(tick,1).Accepted&&!v.Tick(tick,2).Accepted,"clock packet floor rejects replay");
 g.Allow=false;T(!v.Tick(C(1),2).Accepted,"clock adapter authenticates");g.Allow=true;
 T(!v.AdvanceClock(double.NaN).Accepted&&!v.AdvanceClock(0).Accepted,"trusted clock finite monotonic");
 v=New(r,g,1,0);v.StartRound(C(1),0);var first=C(1);v.RecordKnockdown(first,new Knockdown("b","a",1),1);
 for(int i=2;i<10002;i++)T(v.AdvanceClock(i).Accepted,"unlimited practice progression");
 T(v.Stage==RoundStage.Match&&Double.IsPositiveInfinity(v.SecondsRemaining(10001)),"zero time unlimited after saturation");
 T(!v.RecordKnockdown(first,new Knockdown("b","a",2),10002).Accepted,"ancient replay still rejected");
 v=New(r,g,20,100,2); v.StartRound(C(1),10);v.RecordKnockdown(C(1),new Knockdown("b","a",1),11);
 var edit=C(1);T(v.UpdateLimits(edit,new LiveLimits(0,0),12).Accepted,"zero limits live");
 T(v.PersonalScore("a")==1&&v.RoundIndex==1&&Double.IsPositiveInfinity(v.SecondsRemaining(12)),"update preserves score round");
 T(!v.UpdateLimits(edit,new LiveLimits(1,1),12).Accepted,"update replay rejected");
 g.Allow=false;T(!v.UpdateLimits(C(1),new LiveLimits(1,1),12).Accepted,"edit gate denial");g.Allow=true;
 T(!v.UpdateLimits(C(1,"a"),new LiveLimits(1,1),12).Accepted,"edit director only");
 T(!v.UpdateLimits(C(1),new LiveLimits(1,1),11).Accepted,"edit time regression");
 T(v.UpdateLimits(C(1),new LiveLimits(500,3600),13).Accepted&&v.SecondsRemaining(13)==3597,"time uses original start");
 T(v.UpdateLimits(C(1),new LiveLimits(1,3600),14).Accepted&&v.EndReason=="score_limit","lower already crossed score cap");
 v=New(r,g,10,0);v.StartRound(C(1),10);v.AdvanceClock(25);
 T(v.UpdateLimits(C(1),new LiveLimits(0,5),25).Accepted&&v.EndReason=="time_limit","unlimited to crossed time cap");
 var limits=new LiveLimits(-5,9999);T(limits.ScoreLimit==0&&limits.TimeLimit==3600,"source range clamps");
 v=New(r,g,10,0,2,0);v.StartRound(C(1),0);var rows=v.Scores();T(rows.Length==3&&rows[0].Id=="b"&&rows[1].Id=="c","zero FFA names ranked");
 v.RecordKnockdown(C(1),new Knockdown("b","a",1),1);r.Rows.Remove("a");
 T(v.Scores().Length==2&&v.CumulativeScores()[0].Score==1,"disconnected scorer totals separate");
 r.Set("d","Aaron",2,false);r.Set("watch","A",0,true);T(v.Scores().Length==3&&v.Scores()[0].Id=="d","join and spectator exclusion");
 r.Set("b","amy",3,false);v=New(r,g,10,0);v.StartRound(C(1),0);T(v.Scores().Length==4,"configured empty and roster teams");
 var p=new PingService(r,g,"s",1,"m",2,0,10);var pt=new Float3(0,0,0);var normal=new Float3(0,1,0);
 T(p.Place(C(1,"b"),RoundStage.Lobby,pt,normal,0,-1,0).Accepted,"lobby ping");
 T(p.Place(C(1,"b"),RoundStage.Countdown,pt,normal,0,-1,1).Accepted,"countdown ping");
 T(p.Place(C(1,"b"),RoundStage.Match,pt,normal,0,-1,2).Accepted,"match transition");
 T(!p.Place(C(1,"b"),RoundStage.Loading,pt,normal,0,-1,3).Accepted,"loading rejected");
 T(!p.Place(C(1,"b"),RoundStage.Results,pt,normal,0,-1,3).Accepted,"results rejected");
 T(!p.Place(C(1,"watch"),RoundStage.Lobby,pt,normal,0,-1,3).Accepted,"warmup spectator rejected");
 p=new PingService(r,g,"s",2,"m",2,0,10);T(p.Visible("b","m",2,3).Length==0,"new loading epoch clears marks");
 T(!p.Place(C(1,"b"),RoundStage.Lobby,pt,normal,0,-1,3).Accepted,"warmup old epoch rejected");
 Console.WriteLine("PASS R2 assertions="+n); return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;} }
}
