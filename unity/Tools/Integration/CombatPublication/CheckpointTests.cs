using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Persistence;
using Ashfall.Integration;
using Ashfall.Integration.CombatPublication;
class CheckpointTests : IHealthWorld,IAuthorityGate,IInteractionQuery,ICombatEvidence
{
    public double Now {get;set;} public bool InMatch {get{return true;}}public bool TeamChangesAllowed {get{return false;}}
    bool rescue;
    public bool TryGetPose(string id,out RescuePose p){p=new RescuePose{Tracked=true,PaddlesActive=true,Desktop=true,Facing=true};return rescue;}
    public RuleResult Validate(CommandContext c){return c.IsAuthority&&(c.ActorId=="host"||c.ActorId=="b"||c.ActorId=="c")?RuleResult.Accept():RuleResult.Reject("auth");}
    public bool HasLineOfSight(string a,string b,Float3 p,Float3 q,float r){return rescue;}
    CombatHealthBridge bridge;HealthService health;HealthPublicationLane lane;int command;long tick=1;
    CommandContext C(string actor="host"){return new CommandContext("s",actor,"cold-"+(++command),tick,2,true);}
    public bool TryResolve(CommandContext c,string id,out HitEvidence e){var h=bridge.FindRestoredHit(C(),id);e=h==null?null:new HitEvidence{Identity=h,ActorInputReady=true,DamageMultiplier=1,HeadRatio=1,TargetHead=new Float3(0,.5f,0)};return e!=null;}
    static readonly string Binding=Environment.GetEnvironmentVariable("ASHFALL_CHECKPOINT_BINDING");
    static int count;static void Check(bool b,string text){if(!b)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
    void Setup(string path,IAtomicSettingsFiles files=null){Now=0;health=new HealthService("s",2,2,true,this,this,this,60,5,0);health.Register(C(),"b",1,false);bridge=new CombatHealthBridge(health,this,this,this,this,"s",2);lane=new HealthPublicationLane(health,bridge,files==null?new AtomicSettingsStore(path):new AtomicSettingsStore(path,files),"s",2,"host",2048,Binding);}
    void Hit(){var w=bridge.Equip(C(),"w","host","frag",1,tick);var ev=bridge.PublishSourceExplosion(C(),1,600,w,new SourceRadialProvenance("source","frag",new string('a',64)),new Float3(),10,10,SourceRadialFalloff.Constant,tick);bridge.PublishHit(C(),"hit",ev,"physical","b",0,tick);}
    bool Restore(string path,string digest,long min=0){return HealthPublicationLane.TryRestoreCold(C(),Binding,digest,min,new AtomicSettingsStore(path),2,true,this,this,this,this,out health,out bridge,out lane,60,5,0);}
    static string Hash(byte[] b){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
    void HealthValidation(){
        health=new HealthService("s",2,2,true,this,this,this,60,5,0);health.Register(C(),"b",1,false);
        byte[] state=health.CreateCheckpoint(C(),Binding);string digest=Hash(state);HealthService restored;
        Check(HealthService.TryRestoreCheckpoint(C(),Binding,digest,state,2,true,this,this,this,out restored,60,5,0)&&!Object.ReferenceEquals(health,restored)&&restored.Read("b").Revision==1,"cold Health state reconstructed without Register-as-restore");
        Check(Hash(restored.CreateCheckpoint(C(),Binding))==digest,"all Health checkpoint fields roundtrip exact bytes");
        var corrupt=(byte[])state.Clone();corrupt[0]=99;
        Check(!HealthService.TryRestoreCheckpoint(C(),Binding,digest,corrupt,2,true,this,this,this,out restored,60,5,0)&&restored==null,"corrupt hash rejected before exposure");
        Check(!HealthService.TryRestoreCheckpoint(C(),Binding,Hash(corrupt),corrupt,2,true,this,this,this,out restored,60,5,0),"future schema rejected with valid hash");
        Check(!HealthService.TryRestoreCheckpoint(C(),Binding+"wrong",digest,state,2,true,this,this,this,out restored,60,5,0),"source binding rejected");
        Check(!HealthService.TryRestoreCheckpoint(new CommandContext("other","host","x",100,2,true),Binding,digest,state,2,true,this,this,this,out restored,60,5,0),"session mismatch rejected");
        Check(!HealthService.TryRestoreCheckpoint(new CommandContext("s","host","x",100,3,true),Binding,digest,state,2,true,this,this,this,out restored,60,5,0),"epoch mismatch rejected");
        Check(!HealthService.TryRestoreCheckpoint(C("b"),Binding,digest,state,2,true,this,this,this,out restored,60,5,0),"nonhost rejected");
        Check(!HealthService.TryRestoreCheckpoint(C(),Binding,Hash(new byte[2097153]),new byte[2097153],2,true,this,this,this,out restored,60,5,0),"oversize rejected");
        HealthService.PreparedPublicationLease lease;Check(health.TryReservePublication(1,out lease),"actual publication reservation acquired");bool rejected=false;try{health.CreateCheckpoint(C(),Binding);}catch(InvalidOperationException){rejected=true;}Check(rejected,"checkpoint rejects outstanding global reservation");lease.Abort();Check(health.CreateCheckpoint(C(),Binding).Length>0,"abort releases checkpoint barrier");
    }
    sealed class CutFiles : IAtomicSettingsFiles {
        readonly PhysicalSettingsFiles real=new PhysicalSettingsFiles();public int Commits,FailAt=Int32.MaxValue;
        public bool Exists(string p){return real.Exists(p);}public IDisposable AcquireFamily(string p){return real.AcquireFamily(p);}public byte[] Read(string p,int n){return real.Read(p,n);}public void CreateDurable(string p,byte[] b){real.CreateDurable(p,b);}public void Commit(string p,string d,string b,bool replace){if(++Commits==FailAt)throw new IOException("postcommit-cut");real.Commit(p,d,b,replace);}
    }
    void Child(string path,string digest,bool cut){tick=100;command=10000;Check(Restore(path,digest),"separate process PhysicalSettingsFiles reopen with cold objects");Check(health.Read("b").Shield==(cut?100:90),"cold authoritative state matches last successful checkpoint");long rev=health.Read("b").Revision;Check(lane.Combat(C(),C(),"hit",tick,true).Accepted,"pending or completed physical hit retries after cold restore");Check(health.Read("b").Shield==90&&health.Read("b").Revision==(cut?rev+1:rev),"physical retry applies once across process cut");var packets=lane.Offer(C());Check(packets.Length>=1,"cold publication retained");Check(lane.Acknowledge(C(),packets[0].Sequence).Accepted,"cold ACK prefix accepted");var suffix=lane.Offer(C());Check(suffix.Length==packets.Length-1,"ACK removes only prefix");}
    void ProcessGate(string dir,bool cut){string path=Path.Combine(dir,cut?"cut.json":"clean.json");var io=new CutFiles();Setup(path,io);Hit();Check(lane.Advance(C()).Accepted,"checkpoint captures real published physical event");string digest=lane.CheckpointDigest;
        if(cut)io.FailAt=io.Commits+3;
        var result=lane.Combat(C(),C(),"hit",tick,true);Check(cut?!result.Accepted&&lane.RecoveryRequired:result.Accepted,"postcommit persistence disposition");Check(health.Read("b").Shield==90,"parent actual Combat mutated Health");
        // After cut the persisted dirty admission is a complete PRE-operation checkpoint.
        // Its digest is read from an independently retained trusted local receipt fixture.
        if(cut){var disk=new AtomicSettingsStore(path);Check(disk.Load(false).Accepted,"physical dirty checkpoint load");Check(disk.TryRead("combat-checkpoint-sha/2",out digest),"checkpoint digest receipt fixture available");}else digest=lane.CheckpointDigest;
        var info=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"child \""+path+"\" "+digest+" "+(cut?"cut":"clean")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};using(var process=Process.Start(info)){string output=process.StandardOutput.ReadToEnd()+process.StandardError.ReadToEnd();process.WaitForExit();Console.Write(output);Check(process.ExitCode==0,"cold subprocess exit0");}
    }
    void Lifecycle(string dir){Setup(Path.Combine(dir,"lifecycle.json"));Hit();Check(lane.Advance(C()).Accepted,"lifecycle initial checkpoint");Check(lane.Damage(C(),"lethal","b",1,100,100,false).Accepted,"actual lethal mutation");Now=6;tick=7;var p=health.Read("b");Check(lane.Respawn(C(),C(),"b",p.Revision,p.LifeGeneration,p.ConnectionGeneration,7).Accepted,"typed prepared respawn publication");p=health.Read("b");Check(lane.SetConnected(C(),"b",p.Revision,false).Accepted&&lane.SetConnected(C(),"b",p.Revision+1,true).Accepted,"actual reconnect generations checkpointed");string hash=lane.CheckpointDigest;Check(Restore(Path.Combine(dir,"lifecycle.json"),hash),"cold restore after lifecycle");p=health.Read("b");Check(!lane.Combat(C(),C(),"hit",tick,true).Accepted&&health.Read("b").Revision==p.Revision,"pending old physical collision cannot damage new life/connection");}
    void Pressure(){
        Now=0;tick=1;health=new HealthService("s",2,2,true,this,this,this,60,5,0);health.Register(C(),"b",1,false);health.DrainTransitions();
        for(int i=0;i<8192;i++){tick++;var p=health.Read("b");var r=health.ApplyDamage(C(),"retained"+i,"b",p.Revision,0,.001f,true);if(!r.Result.Accepted)throw new Exception("pressure fixture damage "+i);health.DrainTransitions();}
        byte[] bytes=health.CreateCheckpoint(C(),Binding);HealthService cold;Check(HealthService.TryRestoreCheckpoint(C(),Binding,Hash(bytes),bytes,2,true,this,this,this,out cold,60,5,0),"full 8192 damage receipt history cold-restored");
        Check(cold.FindDamage(C(),"retained0","b")!=null,"first canonical receipt retained at saturation");var before=cold.Read("b");Check(!cold.ApplyDamage(C(),"overflow","b",before.Revision,0,.001f,true).Result.Accepted&&cold.Read("b").Revision==before.Revision,"8193rd damage still rejects after cold restore; no history clearing");
        health=new HealthService("s",2,2,true,this,this,this,60,5,0);health.Register(C(),"b",1,false);health.DrainTransitions();
        for(int i=0;i<4096;i++){tick++;health.Synchronize(C());var p=health.Read("b");HealthService.PreparedGain gain;if(!health.PrepareGain(C("b"),"gain"+i,"b",p.Revision,p.LifeGeneration,p.ConnectionGeneration,1,1,out gain)||!gain.Validate())throw new Exception("gain pressure "+i);gain.CommitValidated();}
        bytes=health.CreateCheckpoint(C(),Binding);Check(HealthService.TryRestoreCheckpoint(C(),Binding,Hash(bytes),bytes,2,true,this,this,this,out cold,60,5,0),"4096 gain receipts and command identities cold-restored");Check(cold.FindGain(C("b"),"gain0","b").Outcome==GainOutcome.CappedNoOp,"capped outcome retained across cold restore");
    }
    void GainRestart(string dir){string path=Path.Combine(dir,"gain-cold.json");Setup(path);var p=health.Read("b");Check(lane.Gain(C(),C("b"),"capped","b",p.Revision,p.LifeGeneration,p.ConnectionGeneration,1,1).Accepted,"capped gain committed before checkpoint");Check(lane.Damage(C(),"loss","b",p.Revision,0,20,true).Accepted,"actual loss following capped gain");string digest=lane.CheckpointDigest;HealthService old=health;HealthService s;CombatHealthBridge b;HealthPublicationLane l;
        Check(!HealthPublicationLane.TryRestoreCold(C(),Binding,digest,9999,new AtomicSettingsStore(path),2,true,this,this,this,this,out s,out b,out l,60,5,0)&&s==null&&b==null&&l==null&&old.Read("b").Shield==80,"stale minimum sequence rejects with no live mutation");
        Check(Restore(path,digest),"cold restore gains/damage/publication");p=health.Read("b");Check(lane.Gain(C(),C("b"),"capped","b",p.Revision,p.LifeGeneration,p.ConnectionGeneration,1,1).Accepted&&health.Read("b").Shield==80,"cold capped gain retry cannot replenish after loss");
    }
    void ReviveCold(string dir){
        Now=0;tick=1;rescue=true;health=new HealthService("s",2,2,false,this,this,this,60,5,0);health.Register(C(),"b",1,false);health.Register(C(),"c",1,false);bridge=new CombatHealthBridge(health,this,this,this,this,"s",2);string path=Path.Combine(dir,"revive.json");lane=new HealthPublicationLane(health,bridge,new AtomicSettingsStore(path),"s",2,"host",2048,Binding);Hit();Check(lane.Advance(C()).Accepted,"pending physical collision captured before downing");Check(lane.Damage(C(),"down","b",1,100,100,true).Accepted,"actual downed mutation");
        for(int i=0;i<63;i++){Now=i*.1;tick=i+2;if(!lane.SamplePaddles(C(),C("c")).Accepted)throw new Exception("paddle fixture "+i);}
        Check(lane.Revive(C(),C("c"),"b",health.Read("b").Revision).Accepted&&health.Read("b").Health==50,"actual charged revive before cold restore");string digest=lane.CheckpointDigest;HealthService s;CombatHealthBridge b;HealthPublicationLane l;
        Check(HealthPublicationLane.TryRestoreCold(C(),Binding,digest,0,new AtomicSettingsStore(path),2,false,this,this,this,this,out s,out b,out l,60,5,0),"cold restore charged timers and revived generation");health=s;bridge=b;lane=l;rescue=false;var before=health.Read("b");Check(!lane.Combat(C(),C(),"hit",tick,true).Accepted&&health.Read("b").Revision==before.Revision,"old physical collision remains rejected after cold revived life");
        var ev=bridge.FindRestoredHit(C(),"hit").Event;bridge.PublishHit(C(),"fresh",ev,"fresh-body","b",0,tick);Check(lane.Combat(C(),C(),"fresh",tick,true).Accepted&&health.Read("b").Health==40,"fresh physical subhit on restored lingering event damages revived life once");
    }
    static void Main(string[] args){var t=new CheckpointTests();if(args[0]=="child"){t.Child(args[1],args[2],args[3]=="cut");return;}Directory.CreateDirectory(args[0]);t.HealthValidation();t.ProcessGate(args[0],false);t.ProcessGate(args[0],true);t.Lifecycle(args[0]);t.GainRestart(args[0]);t.Pressure();t.ReviveCold(args[0]);Console.WriteLine("TOTAL "+count+" checkpoint parent observations plus subprocess gates");}
}
