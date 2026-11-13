#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace Ashfall.Integration.Playable.Calibration
{
    // Host calls Tick after actual native tracked poses, before capsule sizing/motion.
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
        public Ashfall.Integration.Playable.Climbing.R23HeadCollision HeadCollision;
        public bool SuppressPersistence;
        // R7 (playtest 6): when the rig's crouch owns the view height, the
        // calibration only supplies the standing origin (TryStandingOriginY)
        // and never writes the origin itself, apart from applying a new receipt.
        public bool ExternalHeightOwner;
        public bool TryStandingOriginY(out float y)
        {
            y=0;if(!initialized||state==null||!state.Calibrated)return false;
            float raw=origin.InverseTransformPoint(head.position).y;
            if(!R23CalibrationState.ValidHeight(raw))return false;
            y=state.DesiredOriginY(raw);return true;
        }
        // Keep equal to the host's capsule eye-to-top padding. Current rig uses .18 m.
        public float ColliderHeadTopPadding=.18f;
        public bool Calibrated { get { return state!=null&&state.Calibrated; } }
        public bool Pending { get { return state==null||state.Pending!=R23CalibrationPending.None; } }
        public string Status { get;private set; }
        public bool CeilingLimited { get;private set; }
        public bool ReceiptPersisted { get;private set; }
        public R23CalibrationReceipt Receipt { get { return state==null?null:state.Receipt; } }
        public event Action<R23CalibrationReceipt> CalibrationChanged;
        AudioSource completionVoice;AudioClip completionClip;float sfxVolume=1;
        public int ManualRequestCount {get;private set;}public int ManualAppliedCount {get;private set;}public int CompletionPlayRequests {get;private set;}
        public float LastRawHeight {get;private set;}public float LastRootEyeBefore {get;private set;}public float LastOriginBefore {get;private set;}public float LastOriginAfter {get;private set;}
        public bool CompletionCueReady {get{return completionClip!=null&&completionVoice!=null;}}
        public void SetSfxVolume(float value){sfxVolume=R23CalibrationState.Finite(value)?Mathf.Clamp01(value):0;if(completionVoice!=null)completionVoice.volume=R23CalibrationCompletionCue.SourceGain*sfxVolume;}
        void PrepareCompletionCue()
        {
            var pcm=R23CalibrationCompletionCue.BuildPcm16();var samples=new float[pcm.Length];for(int i=0;i<pcm.Length;i++)samples[i]=pcm[i]/32768f;
            completionClip=AudioClip.Create("R6 height calibration completion chime",samples.Length,1,R23CalibrationCompletionCue.SampleRate,false);completionClip.SetData(samples,0);
            completionVoice=gameObject.AddComponent<AudioSource>();completionVoice.playOnAwake=false;completionVoice.loop=false;completionVoice.spatialBlend=0;completionVoice.dopplerLevel=0;completionVoice.ignoreListenerPause=true;completionVoice.priority=64;completionVoice.clip=completionClip;SetSfxVolume(sfxVolume);
        }
        void PlayCompletionCue(){if(!CompletionCueReady)return;completionVoice.Stop();completionVoice.Play();CompletionPlayRequests++;}

        public void Initialize(Transform trackingOrigin,Transform trackedHead,CharacterController characterBody)
        {
            if(initialized)throw new InvalidOperationException("Calibration already initialized");
            if(trackingOrigin==null||trackedHead==null||characterBody==null)throw new ArgumentException("Origin, tracked head and character body required");
            if(trackingOrigin.parent!=characterBody.transform||!trackedHead.IsChildOf(trackingOrigin))throw new ArgumentException("Tracking origin must be a direct child of the body rig, containing the tracked head");
            origin=trackingOrigin;head=trackedHead;body=characterBody;rig=body.transform;
            if(!ValidHierarchy())throw new ArgumentException("Calibration requires unit scale and an upright tracking origin");
            // Source boot samples the current posture. Restoring last session's
            // physical baseline would leave a seated player permanently too short.
            state=new R23CalibrationState();initialized=true;Status=state.Status;NotifyRootPoseReset();PrepareCompletionCue();
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
        { Require();state.RequestRecalibration();havePrevious=false;Status=state.Status;ManualRequestCount++;
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,this,"R24 HEIGHT request={0} raw={1:F5} rootEye={2:F5} origin={3} pending={4}",ManualRequestCount,origin.InverseTransformPoint(head.position).y,head.position.y-rig.position.y,origin.localPosition,state.Pending);
            // UI callback runs after the rig pose pump. An actual native HMD
            // sample allows immediate calibration without PC session-focus assumptions.
            Vector3 position;Quaternion rotation;
            if(TryReadNativeTrackedHeadPose(out position,out rotation)&&Vector3.Distance(position,origin.InverseTransformPoint(head.position))<.05f)
                Tick(true,true,false,.0001f);
        }
        // Call after a host-owned spawn/map reset, without discarding the standing receipt.
        public void NotifyRootPoseReset()
        { Require();havePrevious=false;previousRoot=rig.position;state.ResetStability(); }
        public void Tick(bool tracked,bool focused,bool paused,float deltaSeconds)
        {
            Require();if(!R23CalibrationState.Finite(deltaSeconds)||deltaSeconds<=0||deltaSeconds>.1f)throw new ArgumentOutOfRangeException("deltaSeconds");
            if(!ValidHierarchy()){Status="Calibration requires upright unit-scale tracking";state.ResetStability();return;}
            float raw=origin.InverseTransformPoint(head.position).y;
            bool manual=state.Pending==R23CalibrationPending.Manual;
            // Native fallback requires a real complete tracked pose matching this
            // current camera sample; a finite cached/default camera alone never counts.
            if(manual&&!tracked){Vector3 position;Quaternion rotation;if(TryReadNativeTrackedHeadPose(out position,out rotation)&&Vector3.Distance(position,origin.InverseTransformPoint(head.position))<.05f)tracked=true;}
            bool valid=tracked&&R23CalibrationState.ValidHeight(raw)&&Finite(head.position);
            if(Vector3.Distance(previousRoot,rig.position)>1.0f)havePrevious=false;
            previousRoot=rig.position;
            bool ready=state.Sample(raw,tracked&&Finite(head.position),focused,paused,deltaSeconds);
            Status=state.Status;CeilingLimited=false;
            if(!valid||(!manual&&(!focused||paused))){havePrevious=false;return;}
            Physics.SyncTransforms();
            if(!havePrevious){previousHead=head.position;havePrevious=true;}
            if(ready)
            {
                float rootEye=head.position.y-rig.position.y;LastRawHeight=raw;LastRootEyeBefore=rootEye;LastOriginBefore=origin.localPosition.y;
                var proposal=state.Propose(raw,rootEye,origin.localPosition.y);
                float requested=proposal.BaseOriginY-Mathf.Max(0,raw-proposal.TrackedStandingEye);
                // Source explicit initialization is an origin-coordinate change, not
                // a physical head movement. It directly applies the target lift;
                // ordinary recovery/movement retains native ceiling collision.
                bool limited=false;float safe=manual||ExternalHeightOwner?requested:SafeOriginY(requested,out limited);
                if(limited&&state.Pending!=R23CalibrationPending.Restore)
                {
                    // A ceiling must not become a permanently shortened standing-height receipt.
                    Status="Height blocked / move to open space";CeilingLimited=true;
                    if(state.Calibrated)ApplyExisting(raw);previousHead=head.position;return;
                }
                SetOriginY(safe);state.Accept(proposal);CeilingLimited=limited;
                if(state.Calibrated)Persist(proposal);
                Status=limited?"Calibrated / ceiling clearance limited":"Calibrated";
                previousHead=head.position;LastOriginAfter=origin.localPosition.y;
                if(manual){ManualAppliedCount++;PlayCompletionCue();Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,this,"R24 HEIGHT applied request={0} raw={1:F5} eyeBefore={2:F5} eyeAfter={3:F5} originBefore={4:F5} originAfter={5:F5} cueRequest={6}",ManualRequestCount,raw,rootEye,head.position.y-rig.position.y,LastOriginBefore,LastOriginAfter,CompletionPlayRequests);}
                if(CalibrationChanged!=null)CalibrationChanged(proposal);
                return;
            }
            if(state.Calibrated)ApplyExisting(raw);
            previousHead=head.position;
        }
        static readonly List<XRNodeState> nativeNodes=new List<XRNodeState>();
        // Root may reuse this to publish its actual tracked camera sample. No
        // DisplayRunning/desktop/OpenXR-session-focus inference is used here.
        public static bool TryReadNativeTrackedHeadPose(out Vector3 position,out Quaternion rotation)
        {
            position=Vector3.zero;rotation=Quaternion.identity;
            var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);bool tracked;InputTrackingState bits;
            bool hasTracked=device.TryGetFeatureValue(CommonUsages.isTracked,out tracked);
            bool hasBits=device.TryGetFeatureValue(CommonUsages.trackingState,out bits);
            bool fullBits=hasBits&&(bits&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);
            if(device.isValid&&(hasTracked?tracked:fullBits)&&(!hasBits||fullBits)&&device.TryGetFeatureValue(CommonUsages.devicePosition,out position)&&device.TryGetFeatureValue(CommonUsages.deviceRotation,out rotation)&&NativeValid(position,rotation))return true;
            nativeNodes.Clear();InputTracking.GetNodeStates(nativeNodes);
            foreach(var node in nativeNodes)if(node.nodeType==XRNode.Head&&node.tracked&&node.TryGetPosition(out position)&&node.TryGetRotation(out rotation)&&NativeValid(position,rotation))return true;
            foreach(var input in UnityEngine.InputSystem.InputSystem.devices)
            {
                var hmd=input as UnityEngine.InputSystem.XR.XRHMD;if(hmd==null||!hmd.added||!hmd.enabled||!hmd.isTracked.isPressed||(hmd.trackingState.ReadValue()&3)!=3)continue;
                position=hmd.devicePosition.ReadValue();rotation=hmd.deviceRotation.ReadValue();if(NativeValid(position,rotation))return true;
            }
            position=Vector3.zero;rotation=Quaternion.identity;return false;
        }
        static bool NativeValid(Vector3 p,Quaternion q){return R23CalibrationState.ValidNativePose(true,p.x,p.y,p.z,q.x,q.y,q.z,q.w);}
        void ApplyExisting(float raw)
        {
            if(ExternalHeightOwner){CeilingLimited=false;return;}
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
            if(HeadCollision!=null)
            {
                bool ceiling;var before=new Vector3(head.position.x,priorEye,head.position.z);
                var actual=HeadCollision.Sweep(before,Vector3.up*(desiredEye-priorEye),false,out ceiling);
                float safe=R23CalibrationState.SafeRecoveryEye(priorEye,desiredEye,Mathf.Max(0,actual.y));limited=safe<desiredEye-.0001f;
                return requested+(safe-desiredEye)/origin.parent.TransformVector(Vector3.up).y;
            }
            float radius=Mathf.Max(.12f,body.radius);
            Vector3 center=rig.TransformPoint(body.center);
            float actualHullTop=center.y+body.height*.5f;
            // Include the actual CharacterController top as well as the source head-top envelope.
            float headPadding=Mathf.Max(ColliderHeadTopPadding,actualHullTop-head.position.y);
            var start=new Vector3(head.position.x,priorEye+headPadding-radius,head.position.z);
            float rise=desiredEye-priorEye;
            float allowed=rise;
            foreach(var overlap in Physics.OverlapSphere(start,radius,CollisionMask&Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                if(External(overlap)){allowed=0;break;}
            if(allowed>0)
                foreach(var hit in Physics.SphereCastAll(start,radius,Vector3.up,rise+ClearanceSkin,CollisionMask&Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                    if(External(hit.collider))allowed=Mathf.Min(allowed,Mathf.Max(0,hit.distance-ClearanceSkin));
            // Casts can miss initial penetration; check the final complete upper hull as well.
            if(allowed>=rise)
                foreach(var overlap in Physics.OverlapSphere(start+Vector3.up*rise,radius,CollisionMask&Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
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
            if(SuppressPersistence)return;
            try
            {
                var saved=new StoredHeight{version=receipt.Version,trackedStandingEye=receipt.TrackedStandingEye,targetHeadTop=receipt.TargetHeadTop,baseOriginY=receipt.BaseOriginY,semantics="standing_head_top"};
                PlayerPrefs.SetString(PreferenceKey,JsonUtility.ToJson(saved));PlayerPrefs.Save();ReceiptPersisted=true;
            }
            catch(PlayerPrefsException error){ReceiptPersisted=false;Debug.LogWarning("Calibration applied but could not save height receipt: "+error.Message);}
        }
        static bool Finite(Vector3 value) { return R23CalibrationState.Finite(value.x)&&R23CalibrationState.Finite(value.y)&&R23CalibrationState.Finite(value.z); }
        void OnDisable(){if(completionVoice!=null)completionVoice.Stop();}
        void OnDestroy(){if(completionClip!=null)Destroy(completionClip);}
        void Require() { if(!initialized)throw new InvalidOperationException("Initialize calibration before use"); }
    }
}
#endif
