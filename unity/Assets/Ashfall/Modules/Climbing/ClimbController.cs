using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    public sealed partial class ClimbController
    {
        public const string ApiVersion = "Climbing/3";
        internal const float VrGrabRadius = .18f;
        private readonly IClimbCollision world;
        private readonly bool[] held=new bool[2];
        private readonly Float3[] anchors=new Float3[2];
        private Float3 releaseVelocity;
        private Float3[] route;
        private int segment;
        private float elapsed, travelled, length;
        private long sequence=long.MinValue, tick=long.MinValue;
        public ClimbController(IClimbCollision collision) { if(collision==null) throw new ArgumentNullException("collision"); world=collision; }
        public bool LeftAnchored { get { return held[0]; } }
        public bool RightAnchored { get { return held[1]; } }
        public Float3 LeftAnchor { get { return anchors[0]; } }
        public Float3 RightAnchor { get { return anchors[1]; } }
        public bool Mantling { get { return route!=null; } }
        public void Reset() { revision++; Cancel(); sequence=long.MinValue; tick=long.MinValue; }
        private void Cancel() { held[0]=held[1]=false; route=null; releaseVelocity=new Float3(); }

        // Caller owns the root. Apply returned displacement exactly once before feeding the next
        // world-space frame. Never write tracked local head/hands. No ordinary locomotion while OwnsMotion.
        // Central authority/replay gate must precede this local locomotion simulation.
        public ClimbStep Step(InputFrame input, Float3 root, float seconds, ClimbOptions options)
        { return StepFrame(input,root,seconds,options,false,new Float3()); }

        // Separate desktop input; E is explicit because Core.InputFrame has no E action.
        // Call exactly one entry point per tick; both share replay/state/motion ownership.
        public ClimbStep StepDesktop(InputFrame input, Float3 root, float seconds, ClimbOptions options,
            bool climbPressed, Float3 viewForward)
        {
            input.GripLeft=climbPressed; input.GripRight=false;
            input.LeftHandPosition=input.HeadPosition; input.RightHandPosition=input.HeadPosition;
            return StepFrame(input,root,seconds,options,true,viewForward);
        }
        private ClimbStep StepFrame(InputFrame input, Float3 root, float seconds, ClimbOptions options,
            bool desktop, Float3 forward)
        {
            revision++;
            ClimbStep result=new ClimbStep();
            if (input.Sequence<=sequence || input.Tick<tick) { result.Reason="stale-frame"; return result; }
            if (!V.Finite(seconds)||seconds<=0 || seconds>1 || !V.WorldPoint(root) ||
                !V.Finite(options.FlingPower) || !V.Finite(options.GrabCooldown) ||
                !V.Finite(options.StandingEyeHeight) || options.StandingEyeHeight<.55f || options.StandingEyeHeight>2.2f ||
                !V.Finite(options.FloorNormalY) || options.FloorNormalY<0 || options.FloorNormalY>1)
            { Cancel(); result.Reason="invalid-frame"; result.OwnsMotion=true; return result; }
            sequence=input.Sequence; tick=input.Tick; result.Accepted=true;
            if (!input.Tracked || input.Paused || !options.Enabled || !V.WorldPoint(input.HeadPosition) || !V.Within(input.HeadPosition,root,4))
            { result.OwnsMotion=held[0]||held[1]||Mantling; Cancel(); result.Reason="cancelled"; return result; }
            if(Mantling) return StepMantle(input.HeadPosition,seconds,result);
            bool[] ready={options.LeftTracked&&V.Within(input.LeftHandPosition,input.HeadPosition,3),options.RightTracked&&V.Within(input.RightHandPosition,input.HeadPosition,3)};
            bool[] pressed={input.GripLeft&&ready[0],input.GripRight&&ready[1]};
            Float3[] hands={input.LeftHandPosition,input.RightHandPosition};
            Float3[] contacts={options.LeftContact,options.RightContact};
            bool[] reserved={options.LeftReserved,options.RightReserved};
            // Resolve releases AND eligible claims before deciding whether the frame has a final release.
            bool wasDesktopHeld=desktop&&held[0];
            bool lost=false, released=false; Float3 lastAnchor=new Float3();
            for(int i=0;i<2;i++) if(held[i]&&!pressed[i])
            { held[i]=false; released=true; lost|=!ready[i]; lastAnchor=anchors[i]; }
            for(int i=0;i<2;i++) if(!held[i] && pressed[i] && !reserved[i] && options.GrabCooldown<=0 && (desktop || V.Within(contacts[i],input.HeadPosition,1.499999f)))
            {
                SurfaceHit hit=new SurfaceHit();
                bool found;
                if(desktop)
                {
                    Float3 direction=V.Limit(forward,1);
                    found=V.Within(forward,new Float3(),100) && V.Length(direction)>.999f &&
                        world.Raycast(input.HeadPosition,V.Add(input.HeadPosition,V.Mul(direction,1.35f)),QueryKind.Structure,out hit);
                    // definite assignment when forward was refused before the query
                    if(!found) hit=new SurfaceHit();
                }
                else found=world.TryGrab(contacts[i],VrGrabRadius,out hit);
                if(found && hit.Point.IsFinite && (desktop ? Math.Abs(hit.Normal.Y)<.75f && hit.Normal.IsFinite : (!hit.ArchiveGeometry||hit.AuthoredClimbable)))
                { held[i]=true; anchors[i]=hands[i]; releaseVelocity=new Float3(); }
            }
            if(released && !held[0] && !held[1])
            {
                result.OwnsMotion=true; result.Released=true;
                if(!lost && MantlePlanner.TryPlan(world,root,input.HeadPosition,lastAnchor,options.StandingEyeHeight,options.FloorNormalY,out route))
                {
                    elapsed=travelled=length=0; segment=0;
                    foreach(Float3 motion in route) length+=V.Length(motion);
                    releaseVelocity=new Float3(); result.Mantling=true; return result;
                }
                result.Velocity=lost ? new Float3() : V.Mul(V.Limit(releaseVelocity,7),Math.Max(0,Math.Min(5,options.FlingPower))*1.25f);
                result.Launch=V.Length(result.Velocity)*V.Length(result.Velocity)>.001f;
                releaseVelocity=new Float3(); return result;
            }
            if(!held[0]&&!held[1]) return result;
            // Desktop source advances an already-held camera anchor at 2.2m/s.
            if(wasDesktopHeld && options.GrabCooldown<=0 && held[0] && !released)
                anchors[0]=V.Add(anchors[0],V.Up(2.2f*seconds));
            Float3 pull=V.MeanPull(anchors,hands,held,.3f);
            if(!pull.IsFinite) { Cancel(); result.Accepted=false; result.Reason="invalid-derived-motion"; result.OwnsMotion=true; return result; }
            // Numerical grazing assistance remains swept; never bypass a ceiling.
            SurfaceHit rim; Float3 target=V.Add(input.HeadPosition,pull), lift=new Float3();
            if(world.Raycast(new Float3(target.X,input.HeadPosition.Y+.012f,target.Z),new Float3(target.X,input.HeadPosition.Y-.012f,target.Z),QueryKind.Structure,out rim) &&
                rim.Point.IsFinite && rim.Normal.IsFinite && rim.Normal.Y>=.98f && Math.Abs(rim.Point.Y-input.HeadPosition.Y)<=.0025f && rim.Point.Y-root.Y>=.15f)
            {
                MotionResult assist=world.SweepHead(input.HeadPosition,V.Up(Math.Max(.001f,Math.Min(.006f,rim.Point.Y-input.HeadPosition.Y+.003f))),seconds,false);
                if(assist.Displacement.IsFinite) lift=assist.Displacement;
            }
            MotionResult moved=world.SweepHead(V.Add(input.HeadPosition,lift),pull,Math.Max(seconds,.001f),true);
            if(!moved.Displacement.IsFinite||!moved.Velocity.IsFinite) { Cancel(); result.Accepted=false; result.OwnsMotion=true; result.Reason="invalid-collision-result"; return result; }
            releaseVelocity=V.Add(V.Mul(releaseVelocity,.65f),V.Mul(moved.Velocity,.35f));
            result.Climbing=true; result.OwnsMotion=true; result.RootDisplacement=V.Add(lift,moved.Displacement); result.Velocity=moved.Velocity; return result;
        }
        private ClimbStep StepMantle(Float3 head,float seconds,ClimbStep result)
        {
            result.OwnsMotion=true;
            elapsed=Math.Min(.2f,elapsed+seconds); float t=elapsed/.2f;
            float budget=Math.Max(0,length*t*t*(3-2*t)-travelled);
            while(budget>.000001f && segment<route.Length)
            {
                if(V.Length(route[segment])<.000001f) { segment++; continue; }
                Float3 motion=V.Limit(route[segment],budget);
                MotionResult moved=world.SweepHead(V.Add(head,result.RootDisplacement),motion,seconds,false);
                if(!moved.Displacement.IsFinite) { route=null; result.MantleBlocked=true; return result; }
                result.RootDisplacement=V.Add(result.RootDisplacement,moved.Displacement);
                if(V.Length(V.Sub(moved.Displacement,motion))>.001f)
                { route=null; result.MantleBlocked=true; return result; }
                route[segment]=V.Sub(route[segment],motion); travelled+=V.Length(motion); budget-=V.Length(motion);
            }
            if(elapsed>=.2f) route=null;
            result.Mantling=Mantling; return result;
        }
    }
}
