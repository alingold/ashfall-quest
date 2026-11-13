using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
public static class R2Tests
{
    static int count; static long tick;
    static void Check(bool value,string name) { if(!value)throw new Exception(name); count++; }
    static InputFrame Frame(float turn) { ++tick;return new InputFrame{Tick=tick,Sequence=tick,Tracked=true,TurnX=turn,MoveY=1}; }
    static bool Near(float a,float b){return Math.Abs(a-b)<.0001f;}
    sealed class Query:IRootCollisionPreflight
    { public int Calls; public bool Allow; public RootMotionProposal Last;
      public RuleResult Validate(RootMotionProposal p){Calls++;Last=p;return Allow?RuleResult.Accept():RuleResult.Reject("wall");} }
    public static int Main()
    {
        try {
            Check(typeof(PlayerTuning).GetField("MaximumWalkMultiplier").IsInitOnly,"profile ceiling immutable");
            Check(new PlayerTuning().MaximumWalkMultiplier==5&&PlayerTuning.SkinRuntime().MaximumWalkMultiplier==3,"supported profile limits");
            foreach(var t in new[]{new PlayerTuning(),PlayerTuning.SkinRuntime()}) {
                t.WalkMultiplier=float.MaxValue;
                var s=new Locomotion(t).Step(Frame(0),new MotionEnvironment{Grounded=true},.02f);
                Check(s.Velocity.IsFinite&&Near(s.Velocity.Z,PlayerTuning.WalkSpeed*t.MaximumWalkMultiplier),"huge multiplier clamped per profile");
            }
            foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.MaxValue,-1f}) {
                var t=PlayerTuning.SkinRuntime();t.SmoothRadiansPerSecond=bad;
                var l=new Locomotion(t);var f=Frame(1);bool threw=false;
                try{l.Step(f,new MotionEnvironment{Grounded=true},.02f);}catch(ArgumentException){threw=true;}
                Check(threw&&l.Velocity.X==0,"invalid rate rejected before mutation");
                t.SmoothRadiansPerSecond=1;
                Check(l.Step(f,new MotionEnvironment{Grounded=true},.02f).OwnsMotion,"invalid tuning did not consume tick");
            }
            var tuning=PlayerTuning.SkinRuntime();tuning.Turning=TurnStyle.AutoSnap;
            var sim=new Locomotion(tuning);var external=new MotionEnvironment{ExternalMotionOwner=true};int snaps=0;
            for(int i=0;i<7;i++){var s=sim.Step(Frame(1),external,.1f);if(s.YawRadians>0)snaps++;Check(!s.OwnsMotion&&s.OwnsYaw,"external translation separate yaw");}
            Check(snaps==3,"external auto snap cadence persists");
            var result=sim.Step(Frame(1),new MotionEnvironment{Grounded=true},.1f);
            Check(result.YawRadians==0,"return walking does not duplicate held snap");
            external.TurningBlocked=true;
            Check(!sim.Step(Frame(1),external,.02f).OwnsYaw,"external state may block yaw");external.TurningBlocked=false;
            Check(sim.Step(Frame(1),external,.02f).YawRadians==0,"blocked held turn requires neutral");
            sim.Step(Frame(0),external,.02f);Check(sim.Step(Frame(1),external,.02f).YawRadians>0,"neutral rearms after turn block");
            tuning.Turning=TurnStyle.Smooth;tuning.SmoothRadiansPerSecond=1;tuning.SmoothAnalog=false;
            result=new Locomotion(tuning).Step(Frame(1),new MotionEnvironment{Grounded=true},.1f);
            Check(Near(result.Displacement.X,PlayerTuning.WalkSpeed*(float)Math.Sin(.1)*.1f),"smooth movement uses same tick heading");
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            for(int channel=0;channel<4;channel++) {
                var input=new PlayerInput();var raw=new DeviceSample{Focused=true};
                for(int i=0;i<4;i++)input.Read(++tick,.02f,raw);
                if(channel==0)raw.Left.Grip=invalid;if(channel==1)raw.Right.Grip=invalid;
                if(channel==2)raw.Left.Trigger=invalid;if(channel==3)raw.Right.Trigger=invalid;
                var f=input.Read(++tick,.02f,raw);
                Check(!input.DesktopReady&&!f.GripLeft&&!f.GripRight&&!f.TriggerLeft&&!f.TriggerRight,"invalid desktop analog disarms all");
                raw=new DeviceSample{Focused=true,Right=new HandSample{Trigger=1}};
                for(int i=0;i<5;i++)Check(!input.Read(++tick,.02f,raw).TriggerRight,"held invalid recovery stays disarmed");
                raw.Right.Trigger=0;for(int i=0;i<3;i++)input.Read(++tick,.02f,raw);
                Check(!input.DesktopReady,"recovery 60ms insufficient");input.Read(++tick,.02f,raw);Check(input.DesktopReady,"recovery 80ms ready");
                input.Disarm();Check(!input.DesktopReady,"callback boundary clears readiness");
            }
            var proposal=new RootMotionProposal{OwnsTranslation=true,Translation=new Float3(0,0,1),YawRadians=.5f};var query=new Query();
            Check(!RootMotionSafety.Approve(proposal,true,null).Accepted,"XR no query fails closed");
            Check(!RootMotionSafety.Approve(proposal,true,query).Accepted&&query.Calls==1,"wall rejects planned root motion");
            query.Allow=true;Check(RootMotionSafety.Approve(proposal,true,query).Accepted&&query.Last.YawRadians==.5f,"clear query sees planned yaw");
            proposal.OwnsTranslation=false;Check(!RootMotionSafety.Approve(proposal,true,query).Accepted&&query.Calls==2,"external translation rejected before query");
            proposal.Translation=new Float3();proposal.PivotCorrection=new Float3(.1f,0,0);
            Check(RootMotionSafety.Approve(proposal,true,query).Accepted,"external pivot correction explicitly queried");
            proposal.HeadPosition=new Float3(float.NaN,0,0);Check(!RootMotionSafety.Approve(proposal,true,query).Accepted&&query.Calls==3,"nonfinite pose rejected before collision port");
            bool rejected=false;try{sim.SetVelocity(new Float3(float.MaxValue,0,0));}catch(ArgumentException){rejected=true;}Check(rejected,"unsupported handoff speed rejected");
            Console.WriteLine("PASS "+count+" r2 checks");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
