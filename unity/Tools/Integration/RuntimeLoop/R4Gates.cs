using System;
using Ashfall.Core;
using Ashfall.Integration.RuntimeLoop;
partial class Scenario
{
 sealed class Frames:IHostFrameSource
 {
  internal readonly Scenario Owner;internal object Identity=new object();internal long Sequence;internal int Captures;
  internal bool StaleTick,StaleSequence,Rotate,Null,WrongIdentity;internal RuntimeAction Action;
  internal HostScheduler Reenter;internal ScheduleReceipt Nested;
  public object SourceIdentity{get{return Identity;}}
  internal Frames(Scenario owner){Owner=owner;}
  public HostFrameBatch Capture(long next){Captures++;Owner.tick=next;Owner.Now=next*.1;
   if(Reenter!=null)Nested=Reenter.Run(.1f);
   var batch=new HostFrameBatch(WrongIdentity?new object():Identity,StaleTick?next-1:next,StaleSequence?Sequence:++Sequence,new[]{Owner.A("b"),Owner.A("a",Action)});
   if(Rotate)Identity=new object();return Null?null:batch;}
 }
 sealed class FixedSettings:Ashfall.Modules.Player.IPlayerSettingsPort
 {public Ashfall.Modules.Player.ForwardReference ForwardReference{get{return Ashfall.Modules.Player.ForwardReference.Head;}}public float GroundSpeedGain(float sourceGain){return sourceGain*.5f;}}
 public void RunR4()
 {
  SetupR2();var source=new Frames(this);var scheduler=new HostScheduler(loop,source,"s","host",1,1);
  Check(scheduler.Run(float.NaN).Disposition==ScheduleDisposition.Rejected&&source.Captures==0,"R4 invalid dt never captures");
  var r=scheduler.Run(.1f);Check(r.Disposition==ScheduleDisposition.Committed&&a.Commits==1&&b.Commits==1,"R4 genuine two actor scheduled commit");
  source.StaleTick=true;int roots=a.Commits+b.Commits;var image=health.CreateCheckpoint(C("host"),new string('a',64));
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Rejected&&a.Commits+b.Commits==roots&&scheduler.CommittedTick==1,"R4 stale tick no actor publication");
  Check(Convert.ToBase64String(image)==Convert.ToBase64String(health.CreateCheckpoint(C("host"),new string('a',64))),"R4 stale batch preserves full Health checkpoint");
  source.StaleTick=false;source.StaleSequence=true;source.Sequence=1;
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Rejected&&a.Commits+b.Commits==roots,"R4 stale sequence rejects");source.StaleSequence=false;
  source.WrongIdentity=true;Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Rejected,"R4 foreign identity rejects");source.WrongIdentity=false;
  source.Null=true;Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Rejected,"R4 missing batch rejects");source.Null=false;
  source.Reenter=scheduler;r=scheduler.Run(.1f);Check(source.Nested.Disposition==ScheduleDisposition.Rejected&&r.Disposition==ScheduleDisposition.Committed,"R4 reentry rejected; owner progresses once");source.Reenter=null;
  source.Action=RuntimeAction.Respawn;r=scheduler.Run(.1f);Check(r.Disposition==ScheduleDisposition.CommittedWithActionFailures&&!r.Actors[0].Action.Accepted&&r.Result.Accepted,"R4 aggregate publication distinguishes unsupported action");source.Action=RuntimeAction.None;
  b.InvalidHeading=true;roots=a.Commits+b.Commits;long before=scheduler.CommittedTick;
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Rejected&&scheduler.CommittedTick==before&&roots==a.Commits+b.Commits,"R4 second actor invalid root rejects entire tick");b.InvalidHeading=false;
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Committed,"R4 corrected source retries unspent tick");
  roots=a.Commits+b.Commits;source.Rotate=true;
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.SourceRetired&&roots==a.Commits+b.Commits,"R4 lifetime switch during capture rejects before effects");source.Rotate=false;
  int captures=source.Captures;Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.SourceRetired&&source.Captures==captures,"R4 retired source terminal");
  SetupR2();source=new Frames(this);scheduler=new HostScheduler(loop,source,"s","host",1,1);scheduler.Run(.1f);
  health.SetConnected(C("host"),"a",health.Read("a").Revision,false);rejectSink=true;
  Check(!loop.Drain().Accepted&&loop.HasPendingPublication,"R4 external-producer denied drain pins pending flag");
  roots=a.Commits+b.Commits;captures=source.Captures;source.Identity=new object();
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Draining&&source.Captures==captures,"R4 disconnected retired source still drains first");
  rejectSink=false;Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.SourceRetired&&health.PendingTransitionCount==0&&roots==a.Commits+b.Commits&&audio.SetEquals(ui),"R4 drain completes without disconnected root replay");
  SetupR2();source=new Frames(this);scheduler=new HostScheduler(loop,source,"s","host",1,1);scheduler.Run(.1f);roots=a.Commits+b.Commits;
  health.SetConnected(C("host"),"b",health.Read("b").Revision,false);
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Rejected&&roots==a.Commits+b.Commits,"R4 actual disconnected Health rejects both roots");
  SetupR2();source=new Frames(this);scheduler=new HostScheduler(loop,source,"s","host",1,1);
  Check(loop.PublishSettings(C("a"),0,new Ashfall.Modules.Player.PlayerTuning(),new FixedSettings(),Ashfall.Modules.Player.DominantHand.Right,Ashfall.Modules.Player.SprintBinding.LeftClick).Accepted,"R4 actual immutable settings publication");
  Check(!loop.PublishSettings(C("a"),0,new Ashfall.Modules.Player.PlayerTuning(),new FixedSettings(),Ashfall.Modules.Player.DominantHand.Right,Ashfall.Modules.Player.SprintBinding.LeftClick).Accepted,"R4 actual settings revision replay rejected");
  Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Committed,"R4 settings motor remains callable");
  SetupR2();source=new Frames(this);scheduler=new HostScheduler(loop,source,"s","host",1,1);roots=a.Commits+b.Commits;
  for(int i=0;i<17408;i++){tick++;if(!health.SetConnected(C("host"),"a",health.Read("a").Revision,true).Accepted)throw new Exception("capacity admission");if(i%512==0)combat.Pump(C("host"));}
  rejectSink=true;Check(scheduler.Run(.1f).Disposition==ScheduleDisposition.Draining&&source.Captures==0,"R4 full producer queues drain before input sampling");rejectSink=false;
  int drains=0;while(loop.HasPendingPublication&&drains++<100)scheduler.Run(.1f);
  Check(!loop.HasPendingPublication&&health.PendingTransitionCount==0&&combat.Pump(C("host")).Events.Length==0&&drains>1&&roots==a.Commits+b.Commits,"R4 full queues empty without actor replay");
  Console.WriteLine("R4 scheduler gates "+checks+" PASS; synthetic CPU only; full Flight/Climb/consumable integration OPEN");
 }
}
class R4Tests{static int Main(){try{new Scenario().RunR4();return 0;}catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}}
