using System;
using System.IO;
using System.Diagnostics;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Modes;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.MatchRecovery;
using RoundController=Ashfall.Integration.MatchRounds.RoundController;
public static class RecoveryTests
{
    // External authority/world/clock facts only; all state/score/replay behavior is production.
    sealed class Facts:IAuthorityGate,ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IModeRoster,IHealthWorld,IInteractionQuery
    {
        public double Now{get;set;}int tokens;
        public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
        public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
        public bool TryGetPose(string id,out RescuePose pose){pose=default(RescuePose);return false;}
        public bool HasLineOfSight(string a,string b,Float3 p,Float3 q,float range){return true;}
        public bool CanManage(CommandContext c){return c.ActorId=="host";}
        public string Issue(){return "new-token-"+(++tokens);}
        public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("floor",new Float3(1,0,2),-1,true,false)};}
        public bool IsSafe(string map,string actor,Float3 feet){return true;}
        public bool TryGet(string id,out ModeActor a){a=new ModeActor(id,id,0,"alive",true,false);return id=="a"||id=="b";}
        public ModeActor[] Snapshot(){ModeActor a,b;TryGet("a",out a);TryGet("b",out b);return new[]{a,b};}
    }
    static int count;static void Check(bool b,string name){count++;if(!b)throw new Exception(name);}
    static CommandContext C(string actor,string id,long tick){return new CommandContext("cold",actor,id,tick,0,true);}
    static CheckpointBinding Binding(long seq){return new CheckpointBinding("build","catalog","cold","match",0,0,0,seq);}
    static void Refuse(Action a,string name){bool refused=false;try{a();}catch(IOException){refused=true;}catch(InvalidDataException){refused=true;}catch(ArgumentException){refused=true;}Check(refused,name);}
    static Session Restore(byte[] b,Facts f){return Session.RestoreCheckpoint(b,Binding(1),f,f,f,f,f);}
    static void Cold(string directory)
    {
        var f=new Facts{Now=4};var s=Restore(File.ReadAllBytes(Path.Combine(directory,"session.bin")),f);Check(s.Get("a").Name=="Alice"&&s.Get("a").Loaded,"cold actual membership");string token;
        Check(s.Join(C("a","join",2),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",null,false),out token).Reason=="replay","cold Session replay");
        var rules=new ModeRules("respawn",0,10,100,2);var round=RoundController.RestoreCheckpoint(File.ReadAllBytes(Path.Combine(directory,"round.bin")),Binding(1),rules,f,"host",f,32);
        Check(round.PersonalScore("b")==1,"cold actual round total");Check(!round.RecordKnockdown(C("host","new-death",3),new Knockdown("a","b",1),4).Accepted,"cold death ledger");
        Check(!round.RecordKnockdown(C("host","death",2),new Knockdown("a","b",2),4).Accepted,"cold round replay");
    }
    static void ActualLife()
    {
        var f=new Facts{Now=4};var s=new Session("cold","content",new SessionRules("map","respawn","maphash",0,8,true,false,true,true,1),f,f,f,f,f);string reconnect;
        Check(s.Join(C("a","j",1),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",null,false),out reconnect).Accepted,"life join");
        Check(s.BeginLoading(C("host","loading",2),s.Rules,true).Accepted,"life loading");
        Func<string,string,long,CommandContext> c=(actor,id,tick)=>new CommandContext("cold",actor,id,tick,1,true);
        Check(s.Loaded(c("a","loaded",3),"maphash").Accepted&&s.WorldReady(c("host","world",3),"maphash").Accepted&&s.Activate(c("host","activate",4)).Accepted&&s.Spawn(c("a","spawn",4)).Accepted,"actual spawn setup");
        var h=new HealthService("cold",1,0,true,f,f,f,60,1,0);Check(h.Register(c("host","health",5),"a",-1,false).Accepted,"actual Health registration fixture");
        var binding=new CheckpointBinding("build","catalog","cold","match",1,0,0,2);Session.PreparedLifeProjection token;
        Check(s.PrepareLifeProjection(c("host","project",6),"a","a",h.Read("a"),9,null,out token)&&token.Validate(),"actual life token validated");Refuse(()=>s.CaptureCheckpoint(binding),"validated life blocks capture");f.Now=5;Check(token.Validate(),"later validatedAt");token.CommitValidated();token.Abort();
        byte[] state=s.CaptureCheckpoint(binding);f.Now=4;Refuse(()=>Session.RestoreCheckpoint(state,binding,f,f,f,f,f),"validatedAt survives checkpoint");f.Now=5;
        Check(h.ApplyLoss(c("host","lethal",7),"a",h.Read("a").Revision,100,0,false).Accepted,"actual lethal");Check(s.PrepareLifeProjection(c("host","death",8),"a","a",h.Read("a"),9,null,out token)&&token.Validate(),"actual death projection");token.CommitValidated();Check(s.Get("a").Deaths==1,"actual Session death");
        Check(s.Disconnect(c("a","disconnect",9),false).Accepted,"actual disconnect");state=s.CaptureCheckpoint(binding);var restored=Session.RestoreCheckpoint(state,binding,f,f,f,f,f);
        string next;Check(restored.Join(c("new-a","remap",10),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",reconnect,false),out next).Accepted,"cold canonical reconnect");Check(restored.Get("new-a").CanonicalActorId=="a"&&restored.Get("new-a").Deaths==1,"canonical death retained");
    }
    public static int Main(string[] args)
    {
        try {if(args.Length>0){Cold(args[0]);Console.WriteLine("cold PASS "+count);return 0;}ActualLife();
        var f=new Facts{Now=4};var s=new Session("cold","content",new SessionRules("map","respawn","maphash",0,8,true,false,true,true,1),f,f,f,f,f);string token;
        Check(s.Join(C("a","join",1),new JoinRequest(Protocol.Name,Protocol.Schema,"content","Alice",null,false),out token).Accepted,"actual join");Check(s.Loaded(C("a","loaded",2),"maphash").Accepted,"loaded");
        object identity=s.GetConnectionIdentity("a");byte[] image=s.CaptureCheckpoint(Binding(1));var restored=Restore(image,f);Check(restored.Get("a").CanonicalActorId=="a"&&!ReferenceEquals(identity,restored.GetConnectionIdentity("a")),"fresh incarnation");
        Check(restored.Join(C("a","join",2),null,out token).Reason=="replay","real replay");
        Refuse(()=>Session.RestoreCheckpoint(image,Binding(2),f,f,f,f,f),"wrong generation");Refuse(()=>Restore(new byte[]{1,2,3},f),"corrupt");byte[] trailing=new byte[image.Length+1];Array.Copy(image,trailing,image.Length);Refuse(()=>Restore(trailing,f),"trailing");f.Now=3;Refuse(()=>Restore(image,f),"clock rollback");f.Now=4;
        Session.PreparedJoin a,b;var req=new JoinRequest(Protocol.Name,Protocol.Schema,"content","Bob",null,false);
        Check(s.PrepareJoinTrusted(C("host","sessions-host/1",1),req,out a).Accepted,"prepare join");Check(s.ValidateJoin(a).Accepted,"validate join");Refuse(()=>s.CaptureCheckpoint(Binding(1)),"validated join blocks capture");
        Check(s.PrepareJoinTrusted(C("other","sessions-host/2",2),req,out b).Accepted,"second join");s.AbortJoin(b);Refuse(()=>s.CaptureCheckpoint(Binding(1)),"abort removes own reservation only");s.CommitJoinValidated(a);s.AbortJoin(a);Check(s.CaptureCheckpoint(Binding(1)).Length>0,"commit releases token");
        var rules=new ModeRules("respawn",0,10,100,2);var round=new RoundController(rules,f,"host","cold",0,f,32);Check(round.StartRound(C("host","start",1),4).Accepted,"round start");Check(round.RecordKnockdown(C("host","death",2),new Knockdown("a","b",1),4).Accepted,"actual scoring");var rb=round.CaptureCheckpoint(Binding(1));
        var rr=RoundController.RestoreCheckpoint(rb,Binding(1),rules,f,"host",f,32);Check(rr.PersonalScore("b")==1&&rr.CumulativeScores()[0].Score==1,"actual totals restored");Check(!rr.RecordKnockdown(C("host","fresh",3),new Knockdown("a","b",1),4).Accepted,"death ledger retained");
        Check(rr.AdvanceClock(104).Accepted,"round reaches Results");RoundController.PreparedRollover rollover;
        Check(rr.PrepareRollover(C("host","roll",4),104,105,1,out rollover).Accepted&&rr.ValidateRollover(rollover,104).Accepted,"validated round rollover");Refuse(()=>rr.CaptureCheckpoint(Binding(1)),"validated round rollover blocks capture");rr.AbortRollover(rollover);Check(rr.CaptureCheckpoint(Binding(1)).Length>0,"round abort releases own reservation");
        Session.PreparedMatchRollover sr;Check(restored.PrepareMatchRollover(C("host","sessions-lifecycle/0",0),5,out sr).Accepted&&restored.ValidateMatchRollover(sr).Accepted,"validated Session rollover");Refuse(()=>restored.CaptureCheckpoint(Binding(1)),"validated Session rollover blocks capture");restored.AbortMatchRollover(sr);Check(restored.CaptureCheckpoint(Binding(1)).Length>0,"Session rollover abort releases own reservation");
        var binding=new MatchOwnerBinding(Binding(1),new string('a',64),2,1,4);byte[] pair=MatchOwnerCheckpoint.Capture(restored,rr,binding);Session ps;RoundController pr;
        MatchOwnerCheckpoint.Reconstruct(pair,RecoveryWire.Hash(pair),binding,rules,f,"host",32,f,f,f,f,f,out ps,out pr);Check(ps.Get("a")!=null&&pr.PersonalScore("b")==1,"coherent partial participant reconstruction");
        Refuse(()=>MatchOwnerCheckpoint.Reconstruct(pair,new string('b',64),binding,rules,f,"host",32,f,f,f,f,f,out ps,out pr),"wrong trusted digest");Check(ps==null&&pr==null,"no outputs on digest failure");
        var wrong=new MatchOwnerBinding(Binding(1),new string('b',64),2,1,4);Refuse(()=>MatchOwnerCheckpoint.Reconstruct(pair,RecoveryWire.Hash(pair),wrong,rules,f,"host",32,f,f,f,f,f,out ps,out pr),"wrong producer binding");
        string dir=Path.GetDirectoryName(typeof(RecoveryTests).Assembly.Location);File.WriteAllBytes(Path.Combine(dir,"session.bin"),image);File.WriteAllBytes(Path.Combine(dir,"round.bin"),rb);
        var psi=new ProcessStartInfo(typeof(RecoveryTests).Assembly.Location,"\""+dir+"\""){UseShellExecute=false,RedirectStandardOutput=true};using(var child=Process.Start(psi)){string output=child.StandardOutput.ReadToEnd();child.WaitForExit();Check(child.ExitCode==0,"actual cold subprocess: "+output);Console.Write(output);}
        Console.WriteLine("PASS "+count+" recovery assertions");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}




