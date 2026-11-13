#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace Ashfall.Integration.Playable.Calibration
{
    // STAGED OUTSIDE ASSETS. Host calls Tick after tracked poses, before capsule sizing/motion.
    public sealed class R23VerticalCalibration : MonoBehaviour
    {
        [Serializable] sealed class StoredHeight
        { public int version;public float trackedStandingEye,targetHeadTop,baseOriginY;public string semantics; }
        const string PreferenceKey="ashfall.r23.vertical-combat.height.v1";
        const float ClearanceSkin=.003f;
        Transform origin,head,rig;
        CharacterController body;
        R23CalibrationState state;
        Vector3 previousHead,previousRoot;
        bool havePrevious,initialized;
        public int CollisionMask=Physics.DefaultRaycastLayers;
        // Keep equal to the host's capsule eye-to-top padding. Current rig uses .18 m.
        public float ColliderHeadTopPadding=.18f;
        public bool Calibrated { get { return state!=null&&state.Calibrated; } }
        public bool Pending { get { return state==null||state.Pending!=R23CalibrationPending.None; } }
        public string Status { get;private set; }
        public bool CeilingLimited { get;private set; }
        public bool ReceiptPersisted { get;private set; }
        public R23CalibrationReceipt Receipt { get { return state==null?null:state.Receipt; } }
        public event Action<R23CalibrationReceipt> CalibrationChanged;
        public void Initialize(Transform trackingOrigin,Transform trackedHead,CharacterController characterBody)
        {
            if(initialized)throw new InvalidOperationException("Calibration already initialized");
            if(trackingOrigin==null||trackedHead==null||characterBody==null)throw new ArgumentException("Origin, tracked head and character body required");
            if(trackingOrigin.parent!=characterBody.transform||!trackedHead.IsChildOf(trackingOrigin))throw new ArgumentException("Tracking origin must be a direct child of the body rig, containing the tracked head");
            origin=trackingOrigin;head=trackedHead;body=characterBody;rig=body.transform;
            if(!ValidHierarchy())throw new ArgumentException("Calibration requires unit scale and an upright tracking origin");
            state=new R23CalibrationState(ReadSaved());initialized=true;Status=state.Status;NotifyRootPoseReset();
        }
        public void Initialize(Transform trackingOrigin,Camera trackedHead,CharacterController characterBody)
        { Initialize(trackingOrigin,trackedHead==null?null:trackedHead.transform,characterBody); }
        bool ValidHierarchy()
        {
            return R23CalibrationState.Finite(ColliderHeadTopPadding)&&ColliderHeadTopPadding>=R23CalibrationState.HeadTopToEye&&ColliderHeadTopPadding<=.5f&&
                (origin.lossyScale-Vector3.one).sqrMagnitude<.00001f&&(rig.lossyScale-Vector3.one).sqrMagnitude<.00001f&&
                Vector3.Dot(origin.up,Vector3.up)>.9999f&&Vector3.Dot(rig.up,Vector3.up)>.9999f;
        }
        public void RequestRecalibration()
        { Require();state.RequestRecalibration();Status=state.Status; }
        // Call after a host-owned spawn/map reset, without discarding the standing receipt.
        public void NotifyRootPoseReset()
        { Require();havePrevious=false;previousRoot=rig.position;state.ResetStability(); }
        public void Tick(bool tracked,bool focused,bool paused,float deltaSeconds)
        {
            Require();if(!R23CalibrationState.Finite(deltaSeconds)||deltaSeconds<=0||deltaSeconds>.1f)throw new ArgumentOutOfRangeException("deltaSeconds");
            if(!ValidHierarchy()){Status="Calibration requires upright unit-scale tracking";state.ResetStability();return;}
            float raw=origin.InverseTransformPoint(head.position).y;
            bool valid=tracked&&R23CalibrationState.ValidHeight(raw)&&Finite(head.position);
            if(Vector3.Distance(previousRoot,rig.position)>1.0f)havePrevious=false;
            previousRoot=rig.position;
            bool ready=state.Sample(raw,valid,focused,paused,deltaSeconds);
            Status=state.Status;CeilingLimited=false;
            if(!valid||!focused||paused){havePrevious=false;return;}
            Physics.SyncTransforms();
            if(!havePrevious){previousHead=head.position;havePrevious=true;}
            if(ready)
            {
                float rootEye=head.position.y-rig.position.y;
                var proposal=state.Propose(raw,rootEye,origin.localPosition.y);
                float requested=proposal.BaseOriginY-Mathf.Max(0,raw-proposal.TrackedStandingEye);
                bool limited;float safe=SafeOriginY(requested,out limited);
                if(limited&&state.Pending!=R23CalibrationPending.Restore)
                {
                    // A ceiling must not become a permanently shortened standing-height receipt.
                    Status="Ceiling blocked / stand in open space";CeilingLimited=true;
                    if(state.Calibrated)ApplyExisting(raw);previousHead=head.position;return;
                }
                SetOriginY(safe);state.Accept(proposal);CeilingLimited=limited;
                if(state.Calibrated)Persist(proposal);
                Status=limited?"Calibrated / ceiling clearance limited":"Calibrated";
                previousHead=head.position;
                if(CalibrationChanged!=null)CalibrationChanged(proposal);
                return;
            }
            if(state.Calibrated)ApplyExisting(raw);
            previousHead=head.position;
        }
        void ApplyExisting(float raw)
        {
            bool limited;float safe=SafeOriginY(state.DesiredOriginY(raw),out limited);SetOriginY(safe);
            CeilingLimited=limited;
            if(limited)Status="Calibrated / ceiling clearance limited";
        }
        float SafeOriginY(float requested,out bool limited)
        {
            float current=origin.localPosition.y;
            Vector3 displacement=origin.parent.TransformVector(Vector3.up*(requested-current));
            float desiredEye=head.position.y+displacement.y;
            float priorEye=previousHead.y;
            limited=false;
            if(desiredEye<=priorEye+.0001f)return requested;
            float radius=Mathf.Max(.12f,body.radius);
            Vector3 center=rig.TransformPoint(body.center);
            float actualHullTop=center.y+body.height*.5f;
            // Include the actual CharacterController top as well as the source head-top envelope.
            float headPadding=Mathf.Max(ColliderHeadTopPadding,actualHullTop-head.position.y);
            var start=new Vector3(head.position.x,priorEye+headPadding-radius,head.position.z);
            float rise=desiredEye-priorEye;
            float allowed=rise;
            foreach(var overlap in Physics.OverlapSphere(start,radius,CollisionMask,QueryTriggerInteraction.Ignore))
                if(External(overlap)){allowed=0;break;}
            if(allowed>0)
                foreach(var hit in Physics.SphereCastAll(start,radius,Vector3.up,rise+ClearanceSkin,CollisionMask,QueryTriggerInteraction.Ignore))
                    if(External(hit.collider))allowed=Mathf.Min(allowed,Mathf.Max(0,hit.distance-ClearanceSkin));
            // Casts can miss initial penetration; check the final complete upper hull as well.
            if(allowed>=rise)
                foreach(var overlap in Physics.OverlapSphere(start+Vector3.up*rise,radius,CollisionMask,QueryTriggerInteraction.Ignore))
                    if(External(overlap)){allowed=0;break;}
            float safeEye=R23CalibrationState.SafeRecoveryEye(priorEye,desiredEye,allowed);
            limited=safeEye<desiredEye-.0001f;
            return requested+(safeEye-desiredEye)/origin.parent.TransformVector(Vector3.up).y;
        }
        bool External(Collider collider) { return collider!=null&&collider!=body&&collider.transform!=rig&&!collider.transform.IsChildOf(rig); }
        void SetOriginY(float value) { var position=origin.localPosition;position.y=value;origin.localPosition=position; }
        R23CalibrationReceipt ReadSaved()
        {
            if(!PlayerPrefs.HasKey(PreferenceKey))return null;
            try
            {
                var saved=JsonUtility.FromJson<StoredHeight>(PlayerPrefs.GetString(PreferenceKey));
                if(saved==null||saved.semantics!="standing_head_top")return null;
                var receipt=new R23CalibrationReceipt(saved.version,saved.trackedStandingEye,saved.targetHeadTop,saved.baseOriginY);ReceiptPersisted=true;return receipt;
            }
            catch(ArgumentException){return null;}
        }
        void Persist(R23CalibrationReceipt receipt)
        {
            try
            {
                var saved=new StoredHeight{version=receipt.Version,trackedStandingEye=receipt.TrackedStandingEye,targetHeadTop=receipt.TargetHeadTop,baseOriginY=receipt.BaseOriginY,semantics="standing_head_top"};
                PlayerPrefs.SetString(PreferenceKey,JsonUtility.ToJson(saved));PlayerPrefs.Save();ReceiptPersisted=true;
            }
            catch(PlayerPrefsException error){ReceiptPersisted=false;Debug.LogWarning("Calibration applied but could not save height receipt: "+error.Message);}
        }
        static bool Finite(Vector3 value) { return R23CalibrationState.Finite(value.x)&&R23CalibrationState.Finite(value.y)&&R23CalibrationState.Finite(value.z); }
        void Require() { if(!initialized)throw new InvalidOperationException("Initialize calibration before use"); }
    }
}
#endif
