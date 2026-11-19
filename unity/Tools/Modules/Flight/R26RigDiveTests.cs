using System;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Activities;
using UnityEngine;
sealed class R26RigDiveTests
{
    static int checks,failures;static long tick;static bool baseline;
    static readonly R26QuestHandReader quest=new R26QuestHandReader();
    static void Check(bool value,string name){checks++;if(!value)failures++;Console.WriteLine((value?"PASS ":"FAIL ")+name);}
    static float Pitch(R23VRActivities a){return (float)typeof(FlightMachine).GetField("divePitch",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(a.flight);}
    static R23ActivityMotion Step(R23VRActivities a,PlayerInput input,int hz,float axis,float spread=1.3f,bool grounded=false,bool paused=false,float grip=0,int primary=1,bool rightTracked=true,float rightAxis=0,bool focused=true,bool leftTracked=true,float trigger=0,float turnX=0,float moveX=0)
    {
        float dt=1f/hz;input.Ownership.Dominant=primary==0?DominantHand.Left:DominantHand.Right;
        const float seatedHead=1.06f, calibrationLift=.54f, wristHeight=.66f;
        var l=quest.Read(new HandSample{Tracked=leftTracked,Position=new Float3(-spread*.5f,wristHeight+calibrationLift,0),StickY=axis,StickX=moveX,Grip=grip,Trigger=trigger});
        var r=quest.Read(new HandSample{Tracked=rightTracked,Position=new Float3(spread*.5f,wristHeight+calibrationLift,0),StickY=rightAxis,StickX=turnX,Grip=grip,Trigger=trigger});
        var snapshot=R23InputProjection.Sample(focused,true,new Float3(0,seatedHead+calibrationLift,0),l,r,paused,primary,true);
        if(snapshot.MoveY!=axis||snapshot.TurnY!=rightAxis||snapshot.TurnX!=turnX)throw new Exception("Quest feature/projection axis lost");
        InputFrame f=input.Read(++tick,dt,snapshot);
        a.left.position=new Vector3(l.Position.X,l.Position.Y,0);a.right.position=new Vector3(r.Position.X,r.Position.Y,0);
        a.Motor.SetVelocity(new Float3(a.Accepted.x,a.Accepted.y,a.Accepted.z));
        var walk=a.Motor.Step(f,new MotionEnvironment{Grounded=grounded,IsXr=true,HeadingRadians=a.ViewYaw},dt);
        var motion=a.EvaluateRigMotion(f,dt,grounded,new Float3(a.Accepted.x,a.Accepted.y,a.Accepted.z),walk,a.ViewYaw,input);
        if(walk.OwnsYaw)a.ViewYaw+=walk.YawRadians;
        if(motion.OwnsMotion)a.Accepted=motion.Velocity;
        a.AcceptMotion(a.Accepted*dt,dt,grounded,false,false);
        return motion;
    }
    static double CourseError(Vector3 velocity,float heading)
    {
        return Math.Abs(Math.IEEERemainder(Math.Atan2(velocity.x,velocity.z)-heading,2*Math.PI));
    }
    static void CheckTurns(int hz,TurnStyle style,float sign)
    {
        const float tolerance=1e-5f;
        var tuning=new PlayerTuning{Turning=style,SnapRadians=(float)Math.PI/4};
        var a=new R23VRActivities{Motor=new Locomotion(tuning)};
        var input=new PlayerInput();
        for(int i=0;i<12;i++)Step(a,input,hz,0,1.04f);
        int failedFrames=0,turnedFrames=0,holdFrames=hz/4;
        double maxError=0,firstError=0;
        for(int i=0;i<holdFrames+6;i++)
        {
            var motion=Step(a,input,hz,0,1.04f,turnX:i<holdFrames?sign:0);
            bool shouldTurn=i==0||style==TurnStyle.Smooth&&i<holdFrames;
            if(shouldTurn)turnedFrames++;
            double expected=sign*(style==TurnStyle.Snap?tuning.SnapRadians:tuning.SmoothRadiansPerSecond*Math.Min(i+1,holdFrames)/hz);
            double error=CourseError(a.Accepted,a.ViewYaw);
            if(i==0)firstError=error;
            maxError=Math.Max(maxError,error);
            double speed=Math.Sqrt(a.Accepted.x*a.Accepted.x+a.Accepted.z*a.Accepted.z);
            if(motion.Mode!=FlightMode.Glide||Math.Abs(a.ViewYaw-expected)>tolerance||error>tolerance||
                Math.Abs(speed-a.flight.GlideSpeed)>tolerance||Math.Abs(a.Accepted.y+a.flight.GlideSpeed/7)>tolerance)failedFrames++;
        }
        string label=style+" "+(sign>0?"right":"left")+" "+hz+" Hz";
        Console.WriteLine("MEASURE {0} turned={1} sampled={2} lag_frames={3} first_error_deg={4:F6} max_error_deg={5:F6}",
            label,turnedFrames,holdFrames+6,failedFrames,firstError*180/Math.PI,maxError*180/Math.PI);
        Check(failedFrames==0,"seated glide follows every frame of "+label);
    }
    static void CheckLookStability(int hz)
    {
        var a=new R23VRActivities();var input=new PlayerInput();
        Step(a,input,hz,0);
        a.ViewYaw=(float)Math.PI/4; // Physical heading change with neutral right stick.
        Step(a,input,hz,0);
        Check(a.flight.Mode==FlightMode.Glide&&CourseError(a.Accepted,(float)Math.PI/hz)<1e-5,
            "neutral-stick heading change retains the course limit "+hz+" Hz");
    }
    static int Main(string[] args)
    {
        baseline=Array.IndexOf(args,"--baseline")>=0;
        foreach(int hz in new[]{72,90,120})
        {
            foreach(var style in new[]{TurnStyle.Snap,TurnStyle.Smooth})
                foreach(float sign in new[]{1f,-1f})CheckTurns(hz,style,sign);
            CheckLookStability(hz);
            var a=new R23VRActivities();var input=new PlayerInput();
            for(int i=0;i<hz/4;i++)Step(a,input,hz,0);
            Check(a.flight.Mode==FlightMode.Glide,"neutral spread hands glide "+hz);
            a=new R23VRActivities();input=new PlayerInput();a.Accepted=new Vector3(0,-2,0);
            var probe=new R23VRActivities();var probeInput=new PlayerInput();
            for(int j=0;j<16;j++)Step(probe,probeInput,hz,0,1.04f,true);
            for(int j=0;j<12;j++)Step(probe,probeInput,hz,1,1.04f,true);
            Step(probe,probeInput,hz,1,1.04f,rightTracked:false);
            Check(probe.flight.Mode==FlightMode.Glide,"first unsupported flicker after priming on ground "+hz);
            Step(probe,probeInput,hz,1,1.04f);
            for(int j=0;j<hz/4;j++)Step(probe,probeInput,hz,0,1.04f);
            Step(probe,probeInput,hz,1,1.04f);
            Check(probe.flight.Mode==FlightMode.Glide,"re-push with stable arms "+hz);
            Step(probe,probeInput,hz,0,1.04f);
            Step(probe,probeInput,hz,0,.68f,turnX:.95f,moveX:1);
            Check(probe.flight.Mode==FlightMode.Air,"measured short span releases during right turn and strafe "+hz);
            Step(probe,probeInput,hz,0,1.04f);
            Step(probe,probeInput,hz,0,1.04f,rightTracked:false);
            Check(probe.flight.Mode==FlightMode.Glide,"single-frame optical and inferred pose loss "+hz);
            int firstDive=-1;float firstPitch=0,previousPitch=0;bool monotone=true;
            for(int i=1;i<=3*hz;i++)
            {
                Step(a,input,hz,1,.4f);float pitch=Pitch(a);
                if(a.flight.Mode==FlightMode.Dive&&firstDive<0){firstDive=i;firstPitch=pitch;}
                monotone&=pitch>=previousPitch;previousPitch=pitch;
            }
            Check(firstDive>=hz&&firstDive<=hz+1,"folded-stick one-second dive gate "+hz);
            Check(firstPitch>0&&firstPitch<.02f&&monotone,"pitch ramps gradually "+hz);
            for(int i=0;i<35*hz;i++)Step(a,input,hz,1,.4f);
            Check(a.flight.Mode==FlightMode.Dive&&Math.Abs(-a.Accepted.y-100)<.05f,"dive terminal is 100 m/s "+hz);
            Step(a,input,hz,0);Check(a.flight.Mode==FlightMode.Glide&&Pitch(a)==0,"neutral spread recovers glide "+hz);
            // The original suite always neutralized the grip before use. Real weapon grips
            // can stay held across focus/pause/ownership changes, keeping action Ready false.
            foreach(int primary in new[]{0,1})
            {
                a=new R23VRActivities();input=new PlayerInput();
                for(int i=0;i<hz/4;i++)Step(a,input,hz,0,1.3f,false,false,1,primary);
                Check(!input.LeftReady&&!input.RightReady,"weapon-button rearm remains safe with grip held "+hz+" hand "+primary);
                Check(a.flight.Mode==FlightMode.Glide,"tracked grip-held pose glides before action rearm "+hz+" hand "+primary);
                for(int i=0;i<3*hz;i++)Step(a,input,hz,1,1.3f,false,false,1,primary);
                Check(a.flight.Mode==FlightMode.Glide&&Pitch(a)==0,"forward stick retains glide while grip held "+hz+" hand "+primary);
                Step(a,input,hz,0,1.3f,false,true,1,primary);
                Check(a.flight.Mode==FlightMode.Suspended,"menu pause suspends flight "+hz+" hand "+primary);
                for(int i=0;i<3*hz;i++)Step(a,input,hz,1,1.3f,false,false,1,primary);
                Check(a.flight.Mode==FlightMode.Glide,"held left stick resumes spread glide "+hz+" hand "+primary);
                for(int i=0;i<3*hz;i++)Step(a,input,hz,1,1.3f,false,false,1,1-primary);
                Check(a.flight.Mode==FlightMode.Glide&&!input.LeftReady,"dominant hand swap preserves glide "+hz+" hand "+primary);
                Step(a,input,hz,0,1.3f,false,false,1,1-primary);
                Check(a.flight.Mode==FlightMode.Glide,"grip-held release restores glide "+hz+" hand "+primary);
            }
            a=new R23VRActivities();input=new PlayerInput();
            for(int i=0;i<hz/4;i++)Step(a,input,hz,0,1.0066f);
            Check(a.flight.Mode==FlightMode.Glide,"1.0066 m spread engages glide "+hz);
            a=new R23VRActivities();input=new PlayerInput();
            for(int i=0;i<hz/4;i++)Step(a,input,hz,0);
            Step(a,input,hz,.3f);Check(a.flight.Mode==FlightMode.Glide,"armed full-spread .30 drift never retracts wings "+hz);
            Step(a,input,hz,0,0.9966f);Check(a.flight.Mode==FlightMode.Air,"0.9966 m measured span releases on this frame "+hz);
            bool stable=true;
            for(int i=0;i<2*hz;i++){Step(a,input,hz,.3f,i%2==0?1.0066f:1.0266f,false,false,1);stable&=a.flight.Mode==FlightMode.Glide;}
            Check(stable,"spread jitter and .30 forward drift retain glide "+hz);
            Step(a,input,hz,.35f,1.0066f,false,false,1);Check(a.flight.Mode==FlightMode.Glide,"dead-zone boundary retains glide "+hz);
            Step(a,input,hz,0,.75f,false,false,1);Check(a.flight.Mode==FlightMode.Air,"one measured low sample releases "+hz);
            for(int i=0;i<hz/5;i++)Step(a,input,hz,0,.75f,false,false,1);Check(a.flight.Mode==FlightMode.Air,"folded hands release glide "+hz);
            Step(a,input,hz,0,.86f,false,false,1);Check(a.flight.Mode==FlightMode.Air,"short span stays folded "+hz);
            Step(a,input,hz,0,1.0066f,false,false,1);Check(a.flight.Mode==FlightMode.Glide,"spread beyond entry reengages glide "+hz);
            Step(a,input,hz,0,1.0066f,false,false,1,1,false);Check(a.flight.Mode==FlightMode.Glide,"one missing right hand sample retains glide "+hz);
            for(int i=0;i<3*hz;i++)Step(a,input,hz,1,1.0066f,false,false,1,1,false);
            Check(a.flight.Mode==FlightMode.Dive,"left dive works while right controller untracked "+hz);
            Step(a,input,hz,1,1.0066f,false,false,1,1,true,0,true,false);Check(a.flight.Mode==FlightMode.Dive,"FLY4: left stick keeps the dive while the left controller is not optically tracked "+hz);
            Step(a,input,hz,0,1.0066f,false,false,1);Check(a.flight.Mode==FlightMode.Glide,"tracked hand recovery needs no button release "+hz);
            a=new R23VRActivities();input=new PlayerInput();
            for(int i=0;i<hz/4;i++)Step(a,input,hz,0);
            for(int i=0;i<3*hz;i++)Step(a,input,hz,0,1.3f,false,false,1,1,true,1);
            Check(a.flight.Mode==FlightMode.Glide,"right stick forward never commands dive "+hz);
            Step(a,input,hz,1,1.3f,true,false,1);Check(a.flight.Mode==FlightMode.Ground&&Pitch(a)==0,"ground prevents glide and dive "+hz);
            a.Accepted=new Vector3(0,-1,0);
            for(int i=0;i<hz/2;i++)Step(a,input,hz,1,1.3f,false,false,1);
            Check(a.flight.Mode==FlightMode.Glide,"FLY4: a stick held since the ground does not cancel the run-off glide "+hz);
            Step(a,input,hz,1,1.3f,false,false,1,1,true,0,false);Check(a.flight.Mode==FlightMode.Suspended,"focus loss suspends flight "+hz);
            a=new R23VRActivities();input=new PlayerInput();
            for(int i=0;i<hz;i++)Step(a,input,hz,0,.4f,false,false,1);
            double expected=70*Math.Tanh(13.0/70);
            Console.WriteLine("MEASURE descent-after-1s hz="+hz+" speed="+(-a.Accepted.y));
            Check(Math.Abs(-a.Accepted.y-expected)<.002,"real input path applies 13 m/s2 descent "+hz);
            for(int i=0;i<40*hz;i++)Step(a,input,hz,0,.4f,false,false,1);
            Check(Math.Abs(-a.Accepted.y-70)<.02f,"belly terminal is 70 m/s "+hz);
            a=new R23VRActivities();input=new PlayerInput();a.Accepted=new Vector3(0,6,0);
            Step(a,input,hz,1,.4f,false,false,1);
            Check(a.Accepted.y==6-16.2f/hz,"jump ascent retains 16.2 m/s2 "+hz);
            // Trigger held at acquisition also cannot invalidate continuous pose controls.
            a=new R23VRActivities();input=new PlayerInput();
            for(int i=0;i<hz/4;i++)Step(a,input,hz,0,1.3f,false,false,0,1,true,0,true,true,1);
            Check(a.flight.Mode==FlightMode.Glide&&!input.LeftReady,"trigger-held acquisition still glides without arming weapons "+hz);
        }
        Console.WriteLine("TOTAL "+checks+" checks, "+failures+" failures"+(baseline?" (before-fix control)":""));
        return failures==0?0:1;
    }
}
