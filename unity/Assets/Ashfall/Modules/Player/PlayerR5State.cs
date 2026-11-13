using System;
using Ashfall.Core;
namespace Ashfall.Modules.Player
{
    public sealed class MovementHeading
    {
        bool ready; Float3 local;
        public void Reset() { ready=false; }
        static Float3 Flat(Float3 a) { return new Float3(a.X,0,a.Z); }
        static double Length2(Float3 a) { return (double)a.X*a.X+(double)a.Z*a.Z; }
        static Float3 Unit(Float3 a) { double n=Math.Sqrt(Length2(a));return n==0?new Float3():new Float3((float)(a.X/n),0,(float)(a.Z/n)); }
        static Float3 Neg(Float3 a) {return new Float3(-a.X,0,-a.Z);}
        static Float3 Rotate(Float3 a,double yaw) {double c=Math.Cos(yaw),s=Math.Sin(yaw);return new Float3((float)(c*a.X+s*a.Z),0,(float)(-s*a.X+c*a.Z));}
        // Inputs are world Unity basis vectors: S R S reflection is done by the pose adapter.
        public float Read(ForwardReference reference,bool xr,bool leftReady,Float3 headForward,Float3 leftForward,Float3 leftUp,float rootYaw)
        {
            if(!Enum.IsDefined(typeof(ForwardReference),reference)||!headForward.IsFinite||!leftForward.IsFinite||!leftUp.IsFinite||!PlayerInput.Finite(rootYaw))throw new ArgumentException("heading pose");
            double yaw=(double)rootYaw%(2*Math.PI);Float3 flat=Flat(headForward);
            if(xr&&reference==ForwardReference.Hand&&leftReady)
            {
                flat=Flat(leftForward);Float3 previous=Rotate(local,yaw);
                if(ready) { if(Length2(flat)<.001)flat=Unit(previous);else {flat=Unit(flat);if((double)flat.X*previous.X+(double)flat.Z*previous.Z<0)flat=Neg(flat);} }
                else { if(leftUp.Y<0)flat=Neg(flat);if(Length2(flat)<.001) {float sign=Math.Sign(leftForward.Y);flat=new Float3(-leftUp.X*sign,0,-leftUp.Z*sign);}flat=Unit(flat); }
                if(Length2(flat)>.001) {local=Rotate(flat,-yaw);ready=true;}
            }
            else ready=false;
            if(Length2(flat)<.001)flat=Rotate(new Float3(0,0,1),yaw);
            return (float)Math.Atan2(flat.X,flat.Z);
        }
    }
    // Owns publication and disarming, never poses. Observe is also called from frame getters.
    public sealed class PlayerFrameOwner
    {
        readonly PlayerInput input; PlayerLifecycleState state;bool observed,valid;InputFrame frame;
        public PlayerFrameOwner(PlayerInput input) {if(input==null)throw new ArgumentNullException("input");this.input=input;Invalidate();}
        public long Generation {get;private set;}
        public bool Observe(PlayerLifecycleState next)
        {
            bool changed=!observed||next.Xr!=state.Xr||next.Session!=state.Session||next.Actor!=state.Actor||next.Epoch!=state.Epoch;
            bool allowed=next.Enabled&&next.Focused&&!next.Paused&&next.RigValid&&(!next.Xr||(next.SessionFocused&&next.Present))&& !String.IsNullOrEmpty(next.Session)&&!String.IsNullOrEmpty(next.Actor)&&next.Epoch>=0;
            if(changed||!allowed||(!valid&&allowed))Invalidate();
            state=next;observed=true;valid=allowed;return changed;
        }
        public void Invalidate() {input.Disarm();frame=new InputFrame{Tick=frame.Tick,Sequence=frame.Sequence,Paused=true};valid=false;Generation++;}
        public bool Valid {get {return valid;} }
        public InputFrame Current {get {return frame;} }
        public void Publish(InputFrame value) {if(!valid)throw new InvalidOperationException("Lifecycle unvalidated");frame=value;}
        public InputFrame Read(long tick) {if(!valid||tick!=frame.Tick||frame.Paused)throw new ArgumentException("Frame unavailable");return frame;}
    }
    public sealed class PlayerGeometryPreflight:IRootCollisionPreflight
    {
        readonly IApprovedPlayerGeometry geometry;
        public PlayerGeometryPreflight(IApprovedPlayerGeometry geometry) {this.geometry=geometry;}
        public RuleResult Validate(RootMotionProposal p)
        {
            if(geometry==null||!geometry.Approved)return RuleResult.Reject("Approved source convex geometry required");
            long revision=geometry.Revision;
            Float3 combined=new Float3(p.Translation.X+p.PivotCorrection.X,p.Translation.Y,p.Translation.Z+p.PivotCorrection.Z);
            RuleResult r=geometry.HeightSupportObstruction(p.RootPosition,p.HeadPosition);
            if(r.Accepted)r=geometry.RootSweep(p.RootPosition,combined);
            if(r.Accepted)r=geometry.HeadSweep(p.HeadPosition,combined);
            if(r.Accepted)r=geometry.PivotClearance(p);
            return geometry.Revision==revision&&geometry.Approved?r:RuleResult.Reject("Geometry changed");
        }
    }
}
