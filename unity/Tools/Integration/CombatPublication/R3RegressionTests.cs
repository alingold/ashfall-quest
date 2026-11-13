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

    void ReviewValidation(){
        health=new HealthService("s",2,2,true,this,this,this,60,5,0);health.Register(C(),"b",1,false);
        var bytes=health.CreateCheckpoint(C(),Binding);HealthService cold;
        foreach(int length in new int[]{0,1,4,bytes.Length/2,bytes.Length-1}) {var cut=new byte[length];Array.Copy(bytes,cut,length);Check(!HealthService.TryRestoreCheckpoint(C(),Binding,Hash(cut),cut,2,true,this,this,this,out cold,60,5,0)&&cold==null,"truncated authenticated frame rejected length="+length);}
        var trailing=new byte[bytes.Length+1];Array.Copy(bytes,trailing,bytes.Length);Check(!HealthService.TryRestoreCheckpoint(C(),Binding,Hash(trailing),trailing,2,true,this,this,this,out cold,60,5,0)&&cold==null,"trailing byte rejected");
        Check(!HealthService.TryRestoreCheckpoint(C(),Binding,Hash(bytes),bytes,1,true,this,this,this,out cold,60,5,0),"team configuration mismatch");
        Check(!HealthService.TryRestoreCheckpoint(C(),Binding,Hash(bytes),bytes,2,true,this,this,this,out cold,61,5,0),"timer configuration mismatch");
        HealthService.PreparedGain gain;var p=health.Read("b");Check(health.PrepareGain(C("b"),"review-gain","b",p.Revision,p.LifeGeneration,p.ConnectionGeneration,1,1,out gain),"public gain preparation");
        bool blocked=false;try{health.CreateCheckpoint(C(),Binding);}catch(InvalidOperationException){blocked=true;}Check(blocked,"public gain blocks checkpoint");gain.Abort();gain.Abort();Check(health.CreateCheckpoint(C(),Binding).Length>0,"gain double abort releases checkpoint");
        HealthService.PreparedAdvance advance;Check(health.PrepareAdvance(C(),out advance).Accepted,"public advance preparation");blocked=false;try{health.CreateCheckpoint(C(),Binding);}catch(InvalidOperationException){blocked=true;}Check(blocked,"public advance blocks checkpoint");advance.Abort();Check(health.CreateCheckpoint(C(),Binding).Length>0,"advance abort releases checkpoint");
    }
    void ReviewLifecycle(){
        Now=0;health=new HealthService("s",2,2,true,this,this,this,60,5,0);health.Register(C(),"b",1,false);health.ApplyDamage(C(),"review-kill","b",1,100,100,false);Now=6;tick=7;
        var p=health.Read("b");HealthService.PreparedRespawn respawn;Check(health.PrepareRespawn(C(),"b",p.Revision,p.LifeGeneration,p.ConnectionGeneration,7,out respawn),"public respawn preparation");
        bool blocked=false;try{health.CreateCheckpoint(C(),Binding);}catch(InvalidOperationException){blocked=true;}Check(blocked,"public respawn blocks checkpoint");
        HealthService.PreparedGain gain;Check(!health.PrepareGain(C("b"),"cross","b",p.Revision,p.LifeGeneration,p.ConnectionGeneration,1,1,out gain),"gain refuses dead target during respawn");
        HealthService.PreparedAdvance advance;bool competing=health.PrepareAdvance(C(),out advance).Accepted;Console.WriteLine("OBSERVED competing advance during respawn="+competing);if(competing)advance.Abort();
        respawn.Abort();respawn.Abort();Check(health.CreateCheckpoint(C(),Binding).Length>0,"respawn double abort releases checkpoint");
    }
    void ReviewAck(string dir){
        string path=Path.Combine(dir,"ack.json");var io=new CutFiles();Setup(path,io);Hit();Check(lane.Combat(C(),C(),"hit",tick,true).Accepted,"ACK cut actual damage");var offered=lane.Offer(C());string digest=lane.CheckpointDigest;Check(offered.Length>0,"ACK cut offered events");
        io.FailAt=io.Commits+1;Check(!lane.Acknowledge(C(),offered[0].Sequence).Accepted&&lane.RecoveryRequired,"ACK persistence cut freezes lane");Check(Restore(path,digest),"cold restore before lost ACK");var retried=lane.Offer(C());Check(retried.Length==offered.Length,"lost ACK retains exact pending count");for(int i=0;i<retried.Length;i++)Check(retried[i].Id==offered[i].Id&&retried[i].Payload==offered[i].Payload,"lost ACK stable ID/payload "+i);
        Check(lane.Acknowledge(C(),retried[0].Sequence).Accepted,"retry ACK commits prefix");digest=lane.CheckpointDigest;Check(Restore(path,digest),"cold restore after ACK");Check(lane.Offer(C()).Length==offered.Length-1,"persisted ACK retains suffix only");
    }
    void ReviewReopen(string dir,bool ack){
        string path=Path.Combine(dir,ack?"bad-ack.json":"bad-offer.json");Setup(path);Hit();Check(lane.Combat(C(),C(),"hit",tick,true).Accepted,"reopen fixture actual damage");var packets=lane.Offer(C());string trusted=lane.CheckpointDigest;
        // Supported public constructor detects existing journal and marks recovery required.
        var empty=new HealthService("s",2,2,true,this,this,this,60,5,0);var freshBridge=new CombatHealthBridge(empty,this,this,this,this,"s",2);var reopenedLane=new HealthPublicationLane(empty,freshBridge,new AtomicSettingsStore(path),"s",2,"host",2048,Binding);
        Check(reopenedLane.RecoveryRequired,"reopened lane requires restoration");
        byte[] primary=File.ReadAllBytes(path),backup=File.ReadAllBytes(path+".backup");
        bool refused=false;try{reopenedLane.Offer(C());}catch(InvalidOperationException){refused=true;}
        Check(refused,"reopened Offer quarantined");
        Check(!reopenedLane.Acknowledge(C(),packets[0].Sequence).Accepted,"reopened ACK quarantined");
        Check(!reopenedLane.Advance(C()).Accepted,"reopened producer quarantined");
        Check(Hash(primary)==Hash(File.ReadAllBytes(path))&&Hash(backup)==Hash(File.ReadAllBytes(path+".backup")),"reopened calls preserve primary AND backup bytes");
        Check(Restore(path,trusted)&&health.Read("b").Shield==90,"original trusted checkpoint still recoverable");
    }

    sealed class BetweenLoads : IAtomicSettingsFiles {
        readonly PhysicalSettingsFiles real=new PhysicalSettingsFiles();readonly byte[] replacement;public int locks;public bool Active=true;
        public BetweenLoads(byte[] b){replacement=b;}
        public IDisposable AcquireFamily(string p){if(++locks==2&&Active)File.WriteAllBytes(p,replacement);return real.AcquireFamily(p);}
        public bool Exists(string p){return real.Exists(p);}public byte[] Read(string p,int n){return real.Read(p,n);}public void CreateDurable(string p,byte[] b){real.CreateDurable(p,b);}public void Commit(string p,string d,string b,bool r){real.Commit(p,d,b,r);}
    }
    void Race(string dir){
        string a=Path.Combine(dir,"a.json"),b=Path.Combine(dir,"b.json");Setup(a);Check(lane.Damage(C(),"a-loss","b",1,0,10,true).Accepted,"A actual Health shield90 committed");var expected=lane.Offer(C());string digestA=lane.CheckpointDigest;
        Setup(b);Check(lane.Damage(C(),"b-loss","b",1,0,20,true).Accepted,"B actual Health shield80 committed at same sequence");var other=lane.Offer(C());Check(other.Length==expected.Length&&other[other.Length-1].Payload!=expected[expected.Length-1].Payload,"two genuine persisted checkpoints have different payloads");
        // A cooperative writer may acquire the family between the two Load calls.
        // Fixture schedules its completed physical image at exactly that unlocked boundary.
        var io=new BetweenLoads(File.ReadAllBytes(b));HealthService s;CombatHealthBridge bridgeResult;HealthPublicationLane l;
        bool accepted=HealthPublicationLane.TryRestoreCold(C(),Binding,digestA,0,new AtomicSettingsStore(a,io),2,true,this,this,this,this,out s,out bridgeResult,out l,60,5,0);
        Check(accepted,"single immutable image restore accepted");Check(io.locks==1,"restore acquires family once");io.Active=false;Check(s.Read("b").Shield==90,"Health came from verified A");
        var offered=l.Offer(C());Check(offered[offered.Length-1].Payload==expected[expected.Length-1].Payload,"publication came from verified A; no second load");
        // Build a mismatched pair through real Persistence. Its outer frame is valid.
        var sa=new AtomicSettingsStore(a);Check(sa.Load(false).Accepted,"load A for mixed pair");
        var sb=new AtomicSettingsStore(b);Check(sb.Load(false).Accepted,"load B for mixed pair");string journal;Check(sb.TryRead("combat-publication/1",out journal),"read B journal");
        sa.Write("combat-publication/1",journal);Check(sa.Flush().Accepted,"persist mismatched journal pair");
        string latest=l.CheckpointDigest;
        Check(!HealthPublicationLane.TryRestoreCold(C(),Binding,latest,0,new AtomicSettingsStore(a),2,true,this,this,this,this,out s,out bridgeResult,out l,60,5,0)&&s==null&&bridgeResult==null&&l==null,"mismatched checkpoint journal pair rejects without exposure");
    }
    void GenerationAndIO(string dir){
        string path=Path.Combine(dir,"generation.json");Setup(path);Check(lane.Advance(C()).Accepted,"generation fixture capture");
        long generation=lane.CheckpointGeneration;string digest=lane.CheckpointDigest;HealthService s;CombatHealthBridge b;HealthPublicationLane l;
        Check(generation>1,"successful writes advance generation");
        Check(!HealthPublicationLane.TryRestoreCold(C(),Binding,digest,0,new AtomicSettingsStore(path),2,true,this,this,this,this,out s,out b,out l,60,5,0,2048,generation+1),"stale generation rejects independently of sequence");
        Check(HealthPublicationLane.TryRestoreCold(C(),Binding,digest,0,new AtomicSettingsStore(path),2,true,this,this,this,this,out s,out b,out l,60,5,0,2048,generation)&&l.CheckpointGeneration==generation,"exact generation restored");
        byte[] primary=File.ReadAllBytes(path);var disk=new AtomicSettingsStore(path);Check(disk.Load(false).Accepted,"read prior checkpoint");string prior;Check(disk.TryRead("combat-checkpoint-sha/2",out prior),"read prior receipt fixture");
        l.Offer(C());string current=l.CheckpointDigest;
        File.WriteAllText(path,"torn");
        Check(HealthPublicationLane.TryRestoreCold(C(),Binding,prior,0,new AtomicSettingsStore(path),2,true,this,this,this,this,out s,out b,out l,60,5,0,2048,0,true),"torn primary restores verified backup");
        Check(!HealthPublicationLane.TryRestoreCold(C(),Binding,current,0,new AtomicSettingsStore(path),2,true,this,this,this,this,out s,out b,out l,60,5,0),"newer receipt cannot silently authorize older backup");
        File.WriteAllBytes(path,primary);File.WriteAllText(path+".backup","corrupt");File.WriteAllText(path,"corrupt");
        Check(!Restore(path,digest),"both corrupt images reject");
        File.WriteAllBytes(path,new byte[4194305]);Check(!Restore(path,digest),"oversized physical image rejects");
    }
    void Routing(string dir){
        string path=Path.Combine(dir,"routing.json");Setup(path);WeaponLease weapon;HostEvent ev;HostHit hit;
        Check(lane.Equip(C(),"route-weapon","host","frag",1,tick,out weapon).Accepted&&weapon!=null,"durable equip returns real capability");
        Check(lane.PublishSourceExplosion(C(),50,600,weapon,new SourceRadialProvenance("source","frag",new string('a',64)),new Float3(),10,10,SourceRadialFalloff.Constant,tick,out ev).Accepted,"durable typed radial event");
        Check(lane.PublishHit(C(),"routed-hit",ev,"body","b",0,tick,out hit).Accepted&&hit!=null,"durable physical publication");
        var receipt=lane.SuccessfulCheckpoint;
        HealthPublicationLane.CheckpointReceipt retired;Check(!lane.PrepareEpochRetirement(C(),3,Binding,out retired).Accepted&&retired==null,"retirement fails closed without root quiescence");
        Check(Restore(path,receipt.Digest),"route capability checkpoint cold restored");
        Check(lane.Combat(C(),C(),"routed-hit",tick,true).Accepted&&health.Read("b").Shield==90,"routed physical capability applies after cold restart");
        Check(receipt.Generation<lane.CheckpointGeneration&&receipt.Digest!=lane.CheckpointDigest&&receipt.SourceBinding==Binding,"retained immutable receipt stays coherent after later writes");
        Check(!lane.Equip(new CommandContext("s","host","stale",tick,1,true),"bad","host","frag",1,tick,out weapon).Accepted&&weapon==null,"stale epoch routed producer rejects");
    }
    static void Main(string[] args){var t=new CheckpointTests();Directory.CreateDirectory(args[0]);t.ReviewValidation();t.ReviewLifecycle();t.ReviewAck(args[0]);t.ReviewReopen(args[0],false);t.ReviewReopen(args[0],true);t.Race(args[0]);t.GenerationAndIO(args[0]);t.Routing(args[0]);Console.WriteLine("TOTAL "+count+" R3 regression observations");}
}
