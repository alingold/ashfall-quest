using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Combat;
sealed class R2Tests : IHealthWorld,IAuthorityGate,IInteractionQuery,ICombatEvidence
{
 public double Time;public bool Match=true;public bool Tracking=true,Desktop=true,Facing=true;public Float3 Local;public HealthService H;CandidateHealthCombatBridge B;long seq;static int checks;
 public double Now{get{return Time;}}public bool InMatch{get{return Match;}}public bool TeamChangesAllowed{get{return !Match;}}
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float range){return true;}
 public bool TryGetPose(string id,out RescuePose p){p=new RescuePose{Tracked=Tracking,PaddlesActive=true,Desktop=Desktop,Facing=Facing,Left=new Float3(-.13f,1,0),Right=new Float3(.13f,1,0),Chest=new Float3(0,1,0),LocalLeft=Local};return true;}
 public bool TryResolve(CommandContext c,string hit,out HitEvidence e){e=new HitEvidence{TargetId="b",WeaponId="rifle",Kind=DamageKind.Bullet,ActorInputReady=true,MatchActive=true,FriendlyFire=true,BaseDamage=hit.StartsWith("lethal")?200:25,DamageMultiplier=1,HeadRatio=1,MaximumRange=100};return true;}
 CommandContext C(string actor="a"){long n=Interlocked.Increment(ref seq);return new CommandContext("s",actor,"c"+n,n,7,true);}
 void Init(){Time=0;Match=true;Tracking=true;Desktop=true;Facing=true;Local=new Float3();seq=0;H=new HealthService("s",7,2,false,this,this,this);H.Register(C(),"a",0,false);H.Register(C(),"b",0,false);B=new CandidateHealthCombatBridge(H,"s",7,this,this,this);H.DrainTransitions();}
 static void Check(bool b,string label){checks++;if(!b)throw new Exception(label);Console.WriteLine("PASS "+label);}
 void Run(){
 Init();long rev=H.Read("b").Revision;var hit=C();H.ApplyLoss(C(),"b",rev,0,1,true);
 var stale=H.ApplyDamage(hit,"logical","b",rev,0,25,true);
 Check(stale.Retryable&&!stale.Result.Accepted&&H.Read("b").Shield==99,"stale revision does not mutate");
 var retry=H.ApplyDamage(hit,"logical","b",H.Read("b").Revision,0,25,true);
 Check(retry.Result.Accepted&&H.Read("b").Shield==74,"same command same logical damage retries after reevaluation");
 var again=H.ApplyDamage(C(),"logical","b",1,99,99,true);
 Check(Object.ReferenceEquals(again,retry)&&H.Read("b").Shield==74,"logical identity returns original terminal receipt despite changed revision/loss/command");
 Check(!H.ApplyDamage(C(),"logical","a",1,5,0,true).Result.Accepted,"logical identity cannot switch target");
 H.Advance(new CommandContext("s","a","future",1000,7,true));
 Check(Object.ReferenceEquals(H.ApplyDamage(hit,"logical","b",1,5,0,true),retry),"terminal receipt survives command pruning without new mutation");
 Check(!H.ApplyDamage(new CommandContext("s","a","epoch",1,8,true),"logical","b",1,5,0,true).Result.Accepted,"epoch cannot retrieve old receipt");
 Init();hit=C();var denied=H.ApplyDamage(hit,"invalid","b",1,float.NaN,0,true);
 Check(!denied.Result.Accepted&&!denied.Retryable&&Object.ReferenceEquals(denied,H.ApplyDamage(C(),"invalid","b",1,25,0,true))&&H.Read("b").Health==100,"invalid same-revision loss caches terminal rejection");
 Init();H.ApplyLoss(C(),"b",1,100,100,true);H.DrainTransitions();Time=60;
 Check(!H.Revive(C(),"b",2).Accepted,"deadline rejects revive");var events=H.DrainTransitions();
 Check(events.Length==1&&events[0].Kind=="bleedout"&&events[0].BeforeRevision==2&&events[0].After.Revision==3&&events[0].After.Life==LifeState.Dead,"rejected intent drains exact expiry transition");
 H.Advance(C());Check(H.DrainTransitions().Length==0,"expiry transition drains exactly once");
 Init();H.SamplePaddles(C());for(int i=0;i<56;i++){Time+=.2;H.SamplePaddles(C());}
 Check(H.Read("a").ReadyUntil>Time,"56 slow valid desktop samples reach ready via 100ms clamp");
 Init();Desktop=false;H.SamplePaddles(C());Time=.2;Local=new Float3(1,0,0);H.SamplePaddles(C());Check(H.Read("a").Charge==0,"clamped elapsed keeps 8m/s tracking jump validation");
 Tracking=false;Time+=.2;H.SamplePaddles(C());Check(H.Read("a").Charge==0,"missing tracking cannot charge");
 Init();Desktop=false;H.SamplePaddles(C());for(int i=0;i<10;i++){Time+=.2;H.SamplePaddles(C());}Facing=false;for(int i=0;i<3;i++){Time+=.2;H.SamplePaddles(C());}Check(H.Read("a").Charge==0,"slow facing-apart samples drain with clamp");
 Init();H.ApplyLoss(C(),"b",1,0,1,true);hit=C();var br=B.Hit(hit,"shield",hit.Tick);Check(br.Result.Accepted&&H.Read("b").Shield==74,"actual Combat bridge evaluates current shield after interleaving");
 var dup=B.Hit(hit,"shield",hit.Tick);Check(dup.Result.Accepted&&H.Read("b").Shield==74&&dup.Damage.Snapshot.Revision==br.Damage.Snapshot.Revision,"actual Combat retry returns terminal result once");
 Check(!B.Hit(hit,"different",hit.Tick).Result.Accepted,"bridge command cannot change hit identity");
 var redelivery=C();Check(B.Hit(redelivery,"shield",redelivery.Tick).Result.Accepted&&H.Read("b").Shield==74,"bridge same logical hit under new delivery command returns terminal result");
 Init();CommandContext c1=C(),c2=C();var start=new ManualResetEvent(false);int accepted=0;Exception error=null;
 Thread t1;
 Thread t2=new Thread(delegate(){try{start.WaitOne();if(B.Hit(c2,"two",c2.Tick).Result.Accepted)Interlocked.Increment(ref accepted);}catch(Exception e){error=e;}});
 // Authoritative server tick is one stable frame for concurrent commands.
 t1=new Thread(delegate(){try{start.WaitOne();if(B.Hit(c1,"one",c2.Tick).Result.Accepted)Interlocked.Increment(ref accepted);}catch(Exception e){error=e;}});
 t1.Start();t2.Start();start.Set();t1.Join();t2.Join();start.Dispose();
 Check(error==null&&accepted==2&&H.Read("b").Shield==50,"two actual Combat hits serialize snapshot evaluate apply without stale loss");
 Init();hit=C();br=B.Hit(hit,"lethal",hit.Tick);Check(br.Result.Accepted&&H.Read("b").Life==LifeState.Downed&&H.Read("b").Deaths==1,"actual Combat lethal down once");
 H.SamplePaddles(C());for(int i=0;i<56;i++){Time+=.1;H.SamplePaddles(C());}rev=H.Read("b").Revision;var revive=C();var lethal=C();start=new ManualResetEvent(false);bool revived=false;
 t1=new Thread(delegate(){start.WaitOne();revived=H.Revive(revive,"b",rev).Accepted;});t2=new Thread(delegate(){start.WaitOne();B.Hit(lethal,"lethal-race",lethal.Tick);});t1.Start();t2.Start();start.Set();t1.Join();t2.Join();start.Dispose();
 Check(revived&&H.Read("b").Life==LifeState.Alive&&H.Read("b").Health==50&&H.Read("b").Deaths==1,"actual Combat lethal vs revive serializes and protection prevents second death");
 Init();hit=C();br=B.Hit(hit,"first",hit.Tick);rev=H.Read("b").Revision;H.SetConnected(C(),"b",rev,false);H.SetConnected(C(),"b",rev+1,true);
 Check(B.Hit(hit,"first",seq).Result.Accepted&&H.Read("b").Shield==75,"terminal retry after reconnect does not redamage");
 var old=C();Check(H.ApplyDamage(old,"old","b",rev,0,25,true).Retryable,"pre-reconnect decision stale without consuming identity");
 Match=false;H.Spawn(C(),"b",H.Read("b").Revision,100,100);Match=true;
 Check(B.Hit(hit,"first",seq).Result.Accepted&&H.Read("b").Shield==100,"terminal retry after spawn does not redamage new life");
 var cross=new CommandContext("s","a",hit.CommandId,seq,8,true);Check(!B.Hit(cross,"first",seq).Result.Accepted,"bridge epoch mismatch rejects cached result");
 Check(!H.ApplyDamage(old,"old","b",rev,0,25,true).Result.Accepted,"pre-spawn decision cannot mutate new life");
 Init();hit=C();var disconnect=C();start=new ManualResetEvent(false);CandidateHealthCombatBridge.BridgeReceipt raced=null;
 t1=new Thread(delegate(){start.WaitOne();raced=B.Hit(hit,"disconnect-race",disconnect.Tick);});
 t2=new Thread(delegate(){start.WaitOne();H.Serialize(delegate{return H.SetConnected(disconnect,"b",H.Read("b").Revision,false);});});
 t1.Start();t2.Start();start.Set();t1.Join();t2.Join();start.Dispose();
 float retained=H.Read("b").Shield;H.SetConnected(C(),"b",H.Read("b").Revision,true);
 Check(B.Hit(hit,"disconnect-race",seq).Result.Accepted==raced.Result.Accepted&&H.Read("b").Shield==retained,"actual Combat vs disconnect and retry keeps terminal result without second damage");
 Init();hit=C();var spawn=C();start=new ManualResetEvent(false);raced=null;
 t1=new Thread(delegate(){start.WaitOne();raced=B.Hit(hit,"spawn-race",spawn.Tick);});
 t2=new Thread(delegate(){start.WaitOne();H.Serialize(delegate {Match=false;var r=H.Spawn(spawn,"b",H.Read("b").Revision,100,100);Match=true;return r;});});
 t1.Start();t2.Start();start.Set();t1.Join();t2.Join();start.Dispose();
 Check(H.Read("b").Shield==100&&B.Hit(hit,"spawn-race",seq).Result.Accepted==raced.Result.Accepted&&H.Read("b").Shield==100,"actual Combat vs spawn then retry cannot damage new protected incarnation");
 Init();hit=C();br=B.Hit(hit,"lethal",hit.Tick);Time=60;var rejected=C();br=B.Hit(rejected,"after-expiry",rejected.Tick);
 int deaths=0;foreach(var ev in br.Events)if(ev.Kind=="bleedout")deaths++;
 Check(!br.Result.Accepted&&deaths==1,"actual Combat rejection still carries bleedout event");
 Console.WriteLine("R2_REGRESSIONS checks="+checks+" failures=0");
 }
 static int Main(){try{new R2Tests().Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
