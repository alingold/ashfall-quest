using System;
using Ashfall.Core;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.SceneManagement;
#endif
namespace Ashfall.Modules.Player
{
    public enum NativeRootStatus { Refused, AppliedPendingAccounting }
    public enum NativeRootPhase { Reserved, PhysicalEntered, PhysicalReturned, PivotEntered, PivotReturned, YawEntered, Completed }
    public enum NativeRootTerminalReason { OwnerRetired, UnrecoverableAccounting }

    // Pure admission arithmetic shared by the actual native owner and external tests.
    public static class NativeRootAdmission
    {
        public static bool Valid(RootMotionProposal p,float seconds)
        {
            if(!PlayerInput.Finite(seconds)||seconds<=0||seconds>.1f||!RootMotionSafety.Approve(p,false,null).Accepted)return false;
            RootMotionProposal expected;
            try{expected=RootMotionSafety.Plan(p.RootPosition,p.HeadPosition,p.Translation,p.YawRadians,p.OwnsTranslation);}
            catch(ArgumentException){return false;}
            return expected.PivotCorrection.X==p.PivotCorrection.X&&expected.PivotCorrection.Y==p.PivotCorrection.Y&&expected.PivotCorrection.Z==p.PivotCorrection.Z&&
                Math.Abs(p.YawRadians)<=Math.PI&&Length(p.Translation)<=10&&Length(p.PivotCorrection)<=10;
        }
        static double Length(Float3 v){return Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z);}
    }
#if UNITY_6000_0_OR_NEWER
    // Isolated, conservative empty-envelope prototype. NOT Session/life/connection authority.
    // One main-thread owner for the lifetime of each actual controller, including retirement.
    public sealed class NativeRootPublication:IDisposable
    {
        static readonly System.Collections.Generic.Dictionary<CharacterController,NativeRootPublication> owners=
            new System.Collections.Generic.Dictionary<CharacterController,NativeRootPublication>();
        readonly CharacterController body;readonly Transform root;readonly Collider[] nearby=new Collider[128];
        readonly int thread;long revision;bool busy,retired;Prepared pending;Receipt retained;TerminalRecord terminal;
        NativeRootPublication(CharacterController body){this.body=body;root=body.transform;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        public static NativeRootPublication CreateIsolated(CharacterController body)
        {
            if(body==null)throw new ArgumentException("body");
            NativeRootPublication owner;
            if(owners.TryGetValue(body,out owner))return owner;
            owner=new NativeRootPublication(body);owners.Add(body,owner);return owner;
        }
        public Receipt PendingResult {get{return retained!=null&&retained.Sealed?retained:null;}}
        public TerminalRecord Terminal {get{return terminal;}}
        public RuleResult PrepareProduction(out Prepared plan)
        {plan=null;return RuleResult.Reject("Canonical Session/life/connection/source and prepared mechanics binding unavailable");}
        bool Lane {get{return thread==System.Threading.Thread.CurrentThread.ManagedThreadId;}}
        bool Live {get{return Lane&&!retired&&body!=null&&body.enabled&&body.detectCollisions&&body.transform==root&&body.gameObject.activeInHierarchy&&root.parent==null&&
            body.gameObject.scene.GetPhysicsScene()==Physics.defaultPhysicsScene&&
            root.localScale.Equals(Vector3.one)&&root.up.Equals(Vector3.up)&&Finite(root.position)&&Finite(root.rotation)&&
            PlayerInput.Finite(body.radius)&&body.radius>0&&PlayerInput.Finite(body.height)&&body.height>=2*body.radius&&
            PlayerInput.Finite(body.skinWidth)&&body.skinWidth>=0&&Finite(body.center);}}
        // Conservative complete swept-envelope admission. Unsupported occupied geometry refuses;
        // this does not approximate source collision or predict exact Move output.
        bool Clear(RootMotionProposal p)
        {
            Vector3 start=root.position;float radius=body.height+body.center.magnitude+body.skinWidth+
                (U(p.HeadPosition)-start).magnitude+U(p.Translation).magnitude+U(p.PivotCorrection).magnitude+.2f;
            if(!PlayerInput.Finite(radius))return false;
            Physics.SyncTransforms();
            int count=Physics.OverlapSphereNonAlloc(start,radius,nearby,~0,QueryTriggerInteraction.Collide);
            if(count>=nearby.Length)return false;
            for(int i=0;i<count;i++)if(nearby[i]!=body)return false;
            return true;
        }
        public RuleResult PrepareIsolated(RootMotionProposal proposal,float seconds,out Prepared plan)
        {
            plan=null;if(!Lane||busy||retired||pending!=null||retained!=null||revision==long.MaxValue)return RuleResult.Reject("owner unavailable/outstanding");
            if(!Live||!NativeRootAdmission.Valid(proposal,seconds)||!root.position.Equals(U(proposal.RootPosition)))return RuleResult.Reject("pose/hull/proposal");
            if(body.gameObject.scene.GetPhysicsScene()!=Physics.defaultPhysicsScene)return RuleResult.Reject("unsupported physics scene");
            if(!Clear(proposal))return RuleResult.Reject("occupied or unsupported geometry envelope");
            // Allocate both plan and retained receipt before the first physical call.
            plan=new Prepared(this,proposal,seconds,revision,Time.frameCount);pending=plan;return RuleResult.Accept();
        }
        bool Matches(Prepared p)
        {return Live&&p!=null&&p.Owner==this&&pending==p&&!p.Finished&&p.Revision==revision&&p.Frame==Time.frameCount&&
            root.position.Equals(p.Position)&&root.rotation.Equals(p.Rotation)&&body.center.Equals(p.Center)&&body.radius==p.Radius&&body.height==p.Height&&
            body.skinWidth==p.Skin&&body.stepOffset==p.Step&&body.slopeLimit==p.Slope&&body.minMoveDistance==p.Minimum&&
            body.detectCollisions==p.Collisions&&body.enableOverlapRecovery==p.Recovery;}
        public NativeRootStatus Publish(Prepared p,out Receipt result)
        {
            result=null;if(!Lane||busy||!Matches(p)||!Clear(p.Proposal))return NativeRootStatus.Refused;
            busy=true;p.Finished=true;retained=p.Result;pending=null;
            Receipt r=retained;Vector3 physicalEnd=p.Position,pivotEnd=p.Position;
            try
            {
                r.PhaseValue=NativeRootPhase.PhysicalEntered;
                r.PhysicalFlagsValue=body.Move(U(p.Proposal.Translation));r.PhysicalFlagsKnownValue=true;
                physicalEnd=root.position;r.PhysicalValue=F(physicalEnd-p.Position);r.PhaseValue=NativeRootPhase.PhysicalReturned;
                // A callback may disable, retire, move geometry, or change the hull during Move.
                // In that event keep the actual physical result and omit the remaining phase.
                if(!retired&&Live&&HullMatches(p)&&Clear(p.Proposal))
                {
                    if(U(p.Proposal.PivotCorrection)!=Vector3.zero)
                    {r.PhaseValue=NativeRootPhase.PivotEntered;r.PivotFlagsValue=body.Move(U(p.Proposal.PivotCorrection));r.PivotFlagsKnownValue=true;}
                    pivotEnd=root.position;r.PivotValue=F(pivotEnd-physicalEnd);r.PhaseValue=NativeRootPhase.PivotReturned;
                    if(!retired&&Live&&HullMatches(p)&&(pivotEnd-physicalEnd-U(p.Proposal.PivotCorrection)).sqrMagnitude<=1e-10f)
                    {r.PhaseValue=NativeRootPhase.YawEntered;root.rotation=p.Rotation*Quaternion.AngleAxis(p.Proposal.YawRadians*Mathf.Rad2Deg,Vector3.up);r.PhaseValue=NativeRootPhase.Completed;}
                }
            }
            catch(Exception){r.InterruptedValue=true;}
            finally
            {
                // No allocation, callback, second solver, replay, or rewind after physical entry.
                // Native destruction can make the final pose unreadable; retain explicit uncertainty.
                try
                {
                    if(root!=null)
                    {r.PositionValue=F(root.position);r.RotationValue=root.rotation;r.PoseKnownValue=true;
                        if(r.PhaseValue==NativeRootPhase.PhysicalEntered)r.PhysicalValue=F(root.position-p.Position);
                        if(r.PhaseValue==NativeRootPhase.PivotEntered)r.PivotValue=F(root.position-physicalEnd);}
                }
                catch(Exception){r.InterruptedValue=true;}
                r.Sealed=true;revision++;busy=false;result=r;
            }
            return NativeRootStatus.AppliedPendingAccounting;
        }
        bool HullMatches(Prepared p){return body.center.Equals(p.Center)&&body.radius==p.Radius&&body.height==p.Height&&body.skinWidth==p.Skin&&
            body.stepOffset==p.Step&&body.slopeLimit==p.Slope&&body.minMoveDistance==p.Minimum&&body.detectCollisions==p.Collisions&&body.enableOverlapRecovery==p.Recovery;}
        public RuleResult Account(Receipt receipt)
        {return RuleResult.Reject("Actual prepared Player/Flight/climb/capsule/pad accounting participants unavailable; receipt retained");}
        public TerminalRecord Retire(NativeRootTerminalReason reason)
        {
            if(!Lane||busy)return null;
            if(reason!=NativeRootTerminalReason.OwnerRetired&&reason!=NativeRootTerminalReason.UnrecoverableAccounting)return null;
            if(terminal!=null)return terminal;
            terminal=new TerminalRecord(retained,reason);retired=true;
            if(pending!=null){pending.Finished=true;pending=null;}return terminal;
        }
        public void Dispose(){if(!Lane)return;retired=true;if(!busy&&pending!=null){pending.Finished=true;pending=null;}}
        public sealed class Prepared:IDisposable
        {
            internal readonly NativeRootPublication Owner;internal readonly RootMotionProposal Proposal;internal readonly long Revision;
            internal readonly int Frame;internal readonly Vector3 Position,Center;internal readonly Quaternion Rotation;
            internal readonly float Radius,Height,Skin,Step,Slope,Minimum;internal readonly bool Collisions,Recovery;
            internal readonly Receipt Result;internal bool Finished;
            internal Prepared(NativeRootPublication o,RootMotionProposal p,float seconds,long revision,int frame)
            {Owner=o;Proposal=p;Revision=revision;Frame=frame;Position=o.root.position;Rotation=o.root.rotation;Center=o.body.center;
                Radius=o.body.radius;Height=o.body.height;Skin=o.body.skinWidth;Step=o.body.stepOffset;Slope=o.body.slopeLimit;Minimum=o.body.minMoveDistance;
                Collisions=o.body.detectCollisions;Recovery=o.body.enableOverlapRecovery;Result=new Receipt(this,seconds);}
            public void Dispose(){if(Owner.Lane&&!Finished&&!Owner.busy){Finished=true;if(Owner.pending==this)Owner.pending=null;}}
        }
        public sealed class Receipt
        {
            readonly Prepared source;public readonly float Seconds;
            internal bool Sealed,InterruptedValue,PoseKnownValue,PhysicalFlagsKnownValue,PivotFlagsKnownValue;internal Float3 PhysicalValue,PivotValue,PositionValue;
            internal Quaternion RotationValue;internal CollisionFlags PhysicalFlagsValue,PivotFlagsValue;internal NativeRootPhase PhaseValue;
            internal Receipt(Prepared source,float seconds){this.source=source;Seconds=seconds;}
            public object OperationIdentity {get{return source;}}
            public object BodyIdentity {get{return source.Owner.body;}}public object RootIdentity {get{return source.Owner.root;}}
            public long OwnerRevision {get{return source.Revision;}}
            public int SourceFrame {get{return source.Frame;}}
            public Vector3 SourcePosition {get{return source.Position;}}public Quaternion SourceRotation {get{return source.Rotation;}}
            public Vector3 HullCenter {get{return source.Center;}}public float HullRadius {get{return source.Radius;}}
            public float HullHeight {get{return source.Height;}}public float HullSkin {get{return source.Skin;}}
            public RootMotionProposal Proposal {get{return source.Proposal;}}
            public Float3 PhysicalDelta {get{return PhysicalValue;}}public Float3 PivotDelta {get{return PivotValue;}}
            public Float3 RootPosition {get{return PositionValue;}}public Quaternion RootRotation {get{return RotationValue;}}
            public CollisionFlags PhysicalFlags {get{return PhysicalFlagsValue;}}public CollisionFlags PivotFlags {get{return PivotFlagsValue;}}
            public NativeRootPhase Phase {get{return PhaseValue;}}public bool Interrupted {get{return InterruptedValue;}}
            public bool PoseKnown {get{return PoseKnownValue;}}
            public bool PhysicalFlagsKnown {get{return PhysicalFlagsKnownValue;}}public bool PivotFlagsKnown {get{return PivotFlagsKnownValue;}}
        }
        public sealed class TerminalRecord
        {
            public Receipt Result {get;private set;}public NativeRootTerminalReason Reason {get;private set;}
            internal TerminalRecord(Receipt result,NativeRootTerminalReason reason){Result=result;Reason=reason;}
        }
        static bool Finite(Vector3 v){return PlayerInput.Finite(v.x)&&PlayerInput.Finite(v.y)&&PlayerInput.Finite(v.z);}
        static bool Finite(Quaternion q){return PlayerInput.Finite(q.x)&&PlayerInput.Finite(q.y)&&PlayerInput.Finite(q.z)&&PlayerInput.Finite(q.w);}
        static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
    }
#endif
}
