using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.Playable.Climbing;
class Mechanics
{
    static long serial;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
    sealed class World:IClimbCollision
    {
        public bool Contact=true;
        public bool TryGrab(Float3 point,float radius,out SurfaceHit hit){hit=new SurfaceHit{Point=point,Normal=new Float3(0,0,-1),AuthoredClimbable=true};return Contact;}
        public bool Raycast(Float3 a,Float3 b,QueryKind kind,out SurfaceHit hit){hit=new SurfaceHit();return false;}
        public bool HeadMotionClear(Float3 a,Float3 b){return false;}
        public MotionResult SweepHead(Float3 h,Float3 m,float dt,bool slide){return new MotionResult(m,new Float3(m.X/dt,m.Y/dt,m.Z/dt));}
    }
    static InputFrame Input(bool l,bool r,float leftY,float rightY)
    {return new InputFrame{Tick=++serial,Sequence=serial,Tracked=true,GripLeft=l,GripRight=r,HeadPosition=new Float3(0,1.65f,0),LeftHandPosition=new Float3(-.3f,leftY,.5f),RightHandPosition=new Float3(.3f,rightY,.5f)};}
    static ClimbOptions Options(InputFrame input)
    {var o=ClimbOptions.Default;o.FlingPower=3;o.StandingEyeHeight=1.65f;o.LeftContact=input.LeftHandPosition;o.RightContact=input.RightHandPosition;return o;}
    static Float3 Accept(R23ClimbState state,Float3 delta,bool floor=false,bool ceiling=false)
    {bool grounded;return state.AcceptMotion(delta,.02f,floor,ceiling,out grounded);}
    static void Main()
    {
        var world=new World();var state=new R23ClimbState(world);var frame=Input(true,false,1.2f,1.2f);
        var options=Options(frame);options.LeftReserved=true;var step=state.Evaluate(frame,new Float3(),.02f,options);Accept(state,new Float3());
        Check(!state.LeftAnchored&&!step.OwnsMotion,"weapon/item reservation prevents new wall anchor");
        frame=Input(true,false,1.2f,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        Check(step.Climbing&&state.LeftAnchored,"free tracked hand grip anchors actual controller mechanics");
        frame=Input(true,false,1.0f,1.2f);options=Options(frame);options.LeftReserved=true;step=state.Evaluate(frame,new Float3(),.02f,options);Accept(state,step.RootDisplacement);
        Check(state.LeftAnchored&&Math.Abs(step.RootDisplacement.Y-.2f)<.0001,"existing physical anchor survives new reservation until release");
        frame=Input(false,false,1,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));bool grounded;
        var accepted=state.AcceptMotion(new Float3(),.02f,true,false,out grounded);
        Check(step.Released&&step.Launch&&Math.Abs(step.Velocity.Y-13.125f)<.001,"accepted 10m/s pull smoothed .35 times source300% times1.25");
        Check(!grounded&&Math.Abs(accepted.Y-step.Velocity.Y)<.001,"release momentum transfers to next frame without grounded cancellation");
        state.Reset();frame=Input(true,true,1.2f,1.2f);state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        frame=Input(true,true,.8f,1.0f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        Check(state.LeftAnchored&&state.RightAnchored&&Math.Abs(step.RootDisplacement.Y-.3f)<.0001,"two-hand mean pull caps at source .3m per frame");
        frame=Input(false,false,.8f,1);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        Check(step.Released&&!step.Launch&&step.Velocity.Y==0,"collision-blocked proposed pull produces zero fling");
        state.Reset();frame=Input(true,false,1.2f,1.2f);state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        for(int i=0;i<12;i++){frame=Input(true,false,.2f,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,step.RootDisplacement);}
        frame=Input(false,false,.2f,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        Check(Math.Abs(step.Velocity.Y-26.25f)<.001,"source release limit7 times fling3 times1.25 caps26.25m/s");
        state.Reset();frame=Input(true,false,1.2f,1.2f);state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        frame=Input(true,false,.9f,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,step.RootDisplacement);
        frame=Input(true,false,.9f,1.2f);options=Options(frame);options.LeftTracked=false;step=state.Evaluate(frame,new Float3(),.02f,options);Accept(state,new Float3());
        Check(step.Released&&!step.Launch&&!state.LeftAnchored,"tracking loss releases without launching");
        state.Reset();frame=Input(true,false,1.2f,1.2f);state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        frame=Input(true,false,.9f,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,step.RootDisplacement);
        frame=Input(false,false,.9f,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));accepted=state.AcceptMotion(new Float3(),.02f,false,true,out grounded);
        Check(step.Launch&&accepted.Y==0,"native ceiling feedback clips upward release");
        state.Reset();Check(!state.LeftAnchored&&!state.RightAnchored,"reset clears both anchors and release memory");
        world.Contact=false;frame=Input(true,false,1.2f,1.2f);step=state.Evaluate(frame,new Float3(),.02f,Options(frame));Accept(state,new Float3());
        Check(!step.OwnsMotion,"no wall contact leaves movement with root locomotion");
        Console.WriteLine("Native Unity API compiled separately; no actual engine or headset execution performed.");
    }
}
