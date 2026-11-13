#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Ashfall.Integration.Playable.Calibration;
namespace Ashfall.Integration.Playable.Appearance
{
    // Read-only access to actual live glove ART; never a tracked/controller pose writer.
    public delegate bool R23HandArtPointReader(int hand,Vector3 meshLocalPoint,out Vector3 worldPoint);
    public sealed class R23AppearanceStyle
    {
        public readonly string Id,Label;public readonly int SourceIndex;
        internal R23AppearanceStyle(string id,string label,int sourceIndex){Id=id;Label=label;SourceIndex=sourceIndex;}
    }
    public sealed class R23AppearanceReceipt
    {
        public readonly string ProfileId,RequestedId,RootName,MeshNames,MaterialNames;
        public readonly int Renderers,VisibleRenderers,SourceIndex;
        public readonly bool HeadVisible;
        internal R23AppearanceReceipt(string profileId,string requestedId,string rootName,string meshNames,string materialNames,int renderers,int visibleRenderers,bool headVisible,int sourceIndex)
        {ProfileId=profileId;RequestedId=requestedId;RootName=rootName;MeshNames=meshNames;MaterialNames=materialNames;Renderers=renderers;VisibleRenderers=visibleRenderers;HeadVisible=headVisible;SourceIndex=sourceIndex;}
    }
    // Render-only adapter of actual R23 player_body.gd and gemini_player_models_099.gd.
    // Live tracked poses and the sole collision/root owner remain untouched.
    [DefaultExecutionOrder(1200)]
    public sealed class R23PlayerAppearance:MonoBehaviour
    {
        // 24 ASHFALL Originals, followed by three owner-requested private-use recreations. Archival originals: ashfall/retired/20261110-recreated-skins.
        public const string DefaultStyleId="cinder_scout";
        static readonly R23AppearanceStyle[] styles={new R23AppearanceStyle("cinder_scout","CINDER SCOUT",0),new R23AppearanceStyle("slag_warden","SLAG WARDEN",1),new R23AppearanceStyle("ember_courier","EMBER COURIER",2),new R23AppearanceStyle("kiln_duelist","KILN DUELIST",3),new R23AppearanceStyle("glasswalker","GLASSWALKER",4),new R23AppearanceStyle("tephra_diver","TEPHRA DIVER",5),new R23AppearanceStyle("caldera_knight","CALDERA KNIGHT",6),new R23AppearanceStyle("soot_sparrow","SOOT SPARROW",7),new R23AppearanceStyle("pumice_monk","PUMICE MONK",8),new R23AppearanceStyle("verdant_reclaimer","VERDANT RECLAIMER",9),new R23AppearanceStyle("neon_smelter","NEON SMELTER",10),new R23AppearanceStyle("frostline_surveyor","FROSTLINE SURVEYOR",11),new R23AppearanceStyle("dune_signal","DUNE SIGNAL",12),new R23AppearanceStyle("tidewrecker","TIDEWRECKER",13),new R23AppearanceStyle("circuit_jester","CIRCUIT JESTER",14),new R23AppearanceStyle("lantern_keeper","LANTERN KEEPER",15),new R23AppearanceStyle("rivet_bruiser","RIVET BRUISER",16),new R23AppearanceStyle("aurora_medic","AURORA MEDIC",17),new R23AppearanceStyle("thunderhead","THUNDERHEAD",18),new R23AppearanceStyle("mirage_thief","MIRAGE THIEF",19),new R23AppearanceStyle("koi_lancer","KOI LANCER",20),new R23AppearanceStyle("static_owl","STATIC OWL",21),new R23AppearanceStyle("magma_golem","MAGMA GOLEM",22),new R23AppearanceStyle("paper_crane","PAPER CRANE",23),new R23AppearanceStyle("pban","PBAN",24),new R23AppearanceStyle("obsidian","OBSIDIAN",25),new R23AppearanceStyle("claude","WITCH DOCTOR",26)};
        // Only real converted R5 kits are selectable. Stable source IDs are not
        // compacted into menu indices; unconverted registry cards stay unavailable.
        static readonly R23AppearanceStyle[] r5Styles={};
        public R23AppearanceStyle[] AvailableStyles {get{var legacy=UI.R25LegacyPack.Skins;var result=new R23AppearanceStyle[styles.Length+r5Styles.Length+legacy.Length];Array.Copy(styles,result,styles.Length);Array.Copy(r5Styles,0,result,styles.Length,r5Styles.Length);for(int i=0;i<legacy.Length;i++)result[styles.Length+r5Styles.Length+i]=new R23AppearanceStyle(legacy[i].id,legacy[i].label+" [RETIRED]",legacy[i].sourceIndex);return result;}}
        public string SelectedStyleId {get;private set;}
        public string RequestedStyleId {get;private set;}
        public bool Ready {get{return current!=null;}}
        public bool Loading {get;private set;}
        public string Failure {get;private set;}
        public bool PreviewMode {get;private set;}
        public bool RemoteMode {get;private set;}
        // Actual selected world-rooted art; no selector preview or mesh duplication.
        public Transform CurrentVisualRoot {get{return current==null||current.Root==null?null:current.Root.transform;}}
        public Material CurrentGloveMaterial {get{return current==null?null:current.GloveMaterial;}}
        // Wire to R23HandsWeapons.SetGloveMaterial. We retain the existing hand mesh owner.
        public event Action<Material> GloveMaterialChanged;
        public event Action<string> StyleChanged;
        Transform root,head,left,right;Asset current,pendingAsset;Coroutine loading;
        R23HandArtPointReader handArtPointReader;
        public void SetHandArtPointReader(R23HandArtPointReader reader){handArtPointReader=reader;}
        UnityWebRequest pendingRead;
        int loadSerial;
        bool tracked=true,paused;float phase;Quaternion yaw=Quaternion.identity;Vector3 previousRoot;
        bool motionStateBound,motionEnabled=true,motionSupported=true,motionGliding;
        Vector3 acceptedMotionVelocity;float proneBlend;bool crouchFloorValid;float crouchFloorY;
        public void SetCrouchFloor(bool valid,float floorY){crouchFloorValid=valid&&!float.IsNaN(floorY)&&!float.IsInfinity(floorY);crouchFloorY=floorY;}
        // Render-only accepted locomotion snapshot. No inference from hand spread,
        // camera lean, root teleport distance or predicted flight displacement.
        public bool VehicleSeated;
        public void SetMotionState(bool supported,bool gliding,Vector3 acceptedVelocity,bool enabled=true)
        {
            motionStateBound=true;motionEnabled=enabled;motionSupported=supported;motionGliding=gliding&&!supported;
            acceptedMotionVelocity=FiniteVelocity(acceptedVelocity)?acceptedVelocity:Vector3.zero;
        }
        static bool FiniteVelocity(Vector3 p){return !float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);}
        public float PronePoseBlend {get{return proneBlend;}}
        public float WalkingPoseSpeed {get;private set;}
        public bool AirbornePose {get{return motionStateBound&&motionEnabled&&!motionSupported;}}
        Vector3 leftPresentationOffset,rightPresentationOffset;
        public void SetFreeHandPresentationOffsets(Vector3 leftWorldOffset,Vector3 rightWorldOffset)
        {leftPresentationOffset=PresentationOffset(leftWorldOffset);rightPresentationOffset=PresentationOffset(rightWorldOffset);}
        static Vector3 PresentationOffset(Vector3 value)
        {return float.IsNaN(value.x)||float.IsNaN(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.x)||float.IsInfinity(value.y)||float.IsInfinity(value.z)||value.sqrMagnitude>16?Vector3.zero:value;}
        public void Initialize(Transform playerRoot,Transform headPose,Transform leftPose,Transform rightPose)
        {InitializeBindings(playerRoot,headPose,leftPose,rightPose);SelectStyle(DefaultStyleId);}
        public void InitializeRemote(Transform playerRoot,Transform headPose,Transform leftArtPose,Transform rightArtPose,string initialStyleId)
        {InitializeBindings(playerRoot,headPose,leftArtPose,rightArtPose);RemoteMode=true;SelectStyle(initialStyleId);}
        // Separate presentation instance; supplied poses belong to the preview, never to XR.
        public void InitializePreview(Transform previewRoot,Transform headPose,Transform leftPose,Transform rightPose,string initialStyleId)
        {InitializeBindings(previewRoot,headPose,leftPose,rightPose);PreviewMode=true;SelectStyle(initialStyleId);}
        void InitializeBindings(Transform playerRoot,Transform headPose,Transform leftPose,Transform rightPose)
        {
            if(root!=null)throw new InvalidOperationException("appearance already initialized");
            if(playerRoot==null||headPose==null||leftPose==null||rightPose==null)throw new ArgumentException("accepted root/head/hands required");
            root=playerRoot;head=headPose;left=leftPose;right=rightPose;previousRoot=root.position;
        }
        public bool SelectStyle(string id)
        {
            if(root==null||!isActiveAndEnabled)return false;
            bool exists=false;foreach(var style in AvailableStyles)if(style.Id==id)exists=true;
            if(!exists){if(RemoteMode&&id!=DefaultStyleId)return SelectStyle(DefaultStyleId);return false;}
            if(Loading&&RequestedStyleId==id)return true;
            loadSerial++;
            CancelRead();
            if(loading!=null){StopCoroutine(loading);loading=null;}
            if(pendingAsset!=null){pendingAsset.Dispose();pendingAsset=null;}
            Loading=false;Failure=null;RequestedStyleId=id;
            if(current!=null&&SelectedStyleId==id)return true;
            Loading=true;loading=StartCoroutine(LoadStyle(id,loadSerial));return true;
        }
        public void SetPresentationState(bool trackingAvailable,bool isPaused)
        {tracked=trackingAvailable;paused=isPaused;if(current!=null&&current.Root!=null&&!tracked)current.Root.SetActive(false);}
        IEnumerator Read(string file,Dictionary<string,byte[]> files,int serial)
        {
            if(files.ContainsKey(file))yield break;
            string path=Application.streamingAssetsPath+"/PlayableR23/"+(UI.R25LegacyPack.IsSkin(RequestedStyleId)?"Legacy/Appearance/":"Appearance/")+file;
            if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;
            using(var request=UnityWebRequest.Get(path))
            {
                pendingRead=request;
                request.timeout=30;yield return request.SendWebRequest();
                if(ReferenceEquals(pendingRead,request))pendingRead=null;
                if(serial!=loadSerial)yield break;
                if(request.result!=UnityWebRequest.Result.Success){Failure="Appearance "+file+": "+request.error;yield break;}
                files.Add(file,request.downloadHandler.data);
            }
        }
        void CancelRead(){if(pendingRead==null)return;var request=pendingRead;pendingRead=null;request.Abort();request.Dispose();}
        IEnumerator LoadStyle(string id,int serial)
        {
            var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            yield return Read(id+".json",files,serial);
            if(serial!=loadSerial)yield break;
            if(Failure!=null){Loading=false;yield break;}
            Manifest manifest=null;
            try{manifest=JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(files[id+".json"]));if(manifest==null||manifest.version!=1||manifest.id!=id||manifest.materials==null)throw new IOException("appearance manifest");}
            catch(Exception error){Failure=error.Message;}
            if(Failure!=null){Loading=false;yield break;}
            yield return Read(id+".meshbin",files,serial);
            if(serial!=loadSerial)yield break;
            if(Failure!=null){Loading=false;yield break;}
            foreach(var material in manifest.materials)
                foreach(string file in new[]{material.baseColor,material.normal,material.metallicMap,material.occlusion,material.emission})
                    if(!String.IsNullOrEmpty(file))
                    {yield return Read(file,files,serial);if(serial!=loadSerial)yield break;if(Failure!=null){Loading=false;yield break;}}
            pendingAsset=new Asset();
            try{pendingAsset.Build(manifest,files[id+".meshbin"],files);}
            catch(Exception error){Failure=error.ToString();pendingAsset.Dispose();pendingAsset=null;Loading=false;}
            files.Clear();
            if(Failure!=null)yield break;
            if(serial!=loadSerial){pendingAsset.Dispose();pendingAsset=null;yield break;}
            var old=current;current=pendingAsset;pendingAsset=null;SelectedStyleId=id;Loading=false;loading=null;
            PublishGlove(current.GloveMaterial);if(old!=null)old.Dispose();
            Present(0);
            var changed=StyleChanged;if(changed!=null)foreach(Action<string> subscriber in changed.GetInvocationList())try{subscriber(id);}catch(Exception error){Debug.LogException(error,this);}
        }
        // Native diagnostic reads the actual committed asset and renderers, never just the request/caption.
        public R23AppearanceReceipt InspectPresentation()
        {
            if(current==null||current.Root==null)return null;
            var meshes=new System.Text.StringBuilder();var finishes=new System.Text.StringBuilder();int total=0,visible=0;
            foreach(var renderer in current.Root.GetComponentsInChildren<MeshRenderer>(true))
            {
                total++;if(renderer.enabled&&renderer.gameObject.activeInHierarchy)visible++;
                var filter=renderer.GetComponent<MeshFilter>();if(filter!=null&&filter.sharedMesh!=null)meshes.Append(filter.sharedMesh.name).Append(';');
                if(renderer.sharedMaterial!=null)finishes.Append(renderer.sharedMaterial.name).Append(';');
            }
            return new R23AppearanceReceipt(current.Profile.id,RequestedStyleId,current.Root.name,meshes.ToString(),finishes.ToString(),total,visible,current.Parts["AvatarHead"].gameObject.activeInHierarchy,current.Profile.sourceIndex);
        }
        // Opt-in diagnostics from the committed live or podium renderer, not a catalog assumption.
        public string InspectMaterialBindings()
        {
            if(current==null||current.Root==null)return "not committed";
            var text=new System.Text.StringBuilder();
            text.Append("requested=").Append(RequestedStyleId).Append(" committed=").Append(current.Profile.id).Append(" sourceIndex=").Append(current.Profile.sourceIndex)
                .Append(" owner=").Append(gameObject.name).Append(" root=").Append(current.Root.GetEntityId().ToString()).AppendLine();
            for(int i=0;i<current.Materials.Length;i++)
            {
                var m=current.Materials[i];text.Append("slot=").Append(i).Append(" material=").Append(m.GetEntityId().ToString()).Append(" shader=").Append(m.shader.name)
                    .Append(" keywords=").Append(String.Join(",",m.shaderKeywords)).Append(" glove=").Append(m==CurrentGloveMaterial);
                if(m.HasProperty("_SpecColor"))text.Append(" specResponse=").Append(m.GetVector("_SpecColor"));
                if(m.HasProperty("_AppearanceResponse"))text.Append(" response=").Append(m.GetVector("_AppearanceResponse"));
                foreach(var property in new[]{"_BaseMap","_MetallicGlossMap","_BumpMap"})if(m.HasProperty(property))
                {var t=m.GetTexture(property);text.Append(' ').Append(property).Append('=').Append(t==null?"none":t.name+":"+t.width+"x"+t.height);}
                text.AppendLine();
            }
            return text.ToString();
        }
        void PublishGlove(Material material)
        {
            var subscribers=GloveMaterialChanged;if(subscribers==null)return;
            foreach(Action<Material> subscriber in subscribers.GetInvocationList())try{subscriber(material);}catch(Exception error){Debug.LogException(error,this);}
        }
        void LateUpdate()
        {
            if(root==null)return;if(!RemoteMode&&!PreviewMode&&!UI.R25LegacyPack.SkinsEnabled&&UI.R25LegacyPack.IsSkin(RequestedStyleId))SelectStyle(DefaultStyleId);float seconds=Mathf.Max(.0001f,Time.unscaledDeltaTime);
            float speed=paused?0:Mathf.Min(10,new Vector2(root.position.x-previousRoot.x,root.position.z-previousRoot.z).magnitude/seconds);
            previousRoot=root.position;
            if(!PreviewMode)
            {
                proneBlend=R23AppearanceMotionPolicy.AdvanceProne(proneBlend,motionStateBound&&motionEnabled,motionSupported,motionGliding,paused,seconds);
                if(motionStateBound)speed=R23AppearanceMotionPolicy.GaitSpeed(motionEnabled,motionSupported,motionGliding,paused,acceptedMotionVelocity.x,acceptedMotionVelocity.z,proneBlend);
            }
            WalkingPoseSpeed=speed;Present(speed);
        }
        void Present(float speed)
        {
            if(current==null)return;
            if(!tracked||!isActiveAndEnabled||root==null||head==null||left==null||right==null){current.Root.SetActive(false);return;}
            if(PreviewMode){PresentPreview();current.Root.SetActive(true);return;}
            var frame=ReadBodyFrame();Vector3 eye=frame.Eye,rightAxis=frame.Right,rear=frame.Rear,hip=frame.Hip,shoulder=frame.Shoulder;
            float height=frame.Height,chest=frame.Chest;var bodyRotation=frame.Rotation;
            Pose("Jacket",hip+frame.Up*chest*.5f,bodyRotation,new Vector3(1,chest,1));
            Pose("ChestPlate",hip+frame.Up*chest*.58f+frame.Front*.18f,bodyRotation,new Vector3(1,Mathf.Min(1,chest/.30f),1));
            if(current.Profile.chestplateFollowsJacket)
                Pose("ChestPlate",hip+frame.Up*chest*(.5f+.04f/.55f)+frame.Front*.17f,bodyRotation,new Vector3(1,chest/.55f,1));
            Pose("Belt",hip,bodyRotation,Vector3.one);
            phase+=paused?0:Mathf.Max(0,Time.unscaledDeltaTime)*Mathf.Min(speed,10)*2.2f;
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;string prefix=i==0?"L":"R";
                PresentArm(i,eye,shoulder,rightAxis,rear);
                float wave=Mathf.Sin(phase+i*Mathf.PI),stride=wave*Mathf.Min(speed*.045f,.25f);
                Vector3 foot=new Vector3(eye.x,frame.GroundY+.07f,eye.z)+rightAxis*side*.115f+rear*(.10f+stride);
                // Airborne feet never run ground probing or a walking cycle.
                if(!RemoteMode&&(!motionStateBound||motionEnabled&&motionSupported)&&proneBlend<.001f)
                {
                    float nearest=float.PositiveInfinity;
                    foreach(var hit in Physics.RaycastAll(foot+Vector3.up*.35f,Vector3.down,.85f,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                        if(hit.collider.attachedRigidbody==null&&!(hit.collider is CharacterController)&&!hit.collider.transform.IsChildOf(root)&&hit.distance<nearest){nearest=hit.distance;foot.y=hit.point.y+.07f;}
                }
                foot.y+=Mathf.Max(0,wave)*Mathf.Min(speed*.012f,.065f);
                // Rotate only the authored suit pieces around the shoulder line;
                // actual tracked head/controller/weapon/grip transforms stay fixed.
                foot=shoulder+frame.Tilt*(foot-shoulder);
                Vector3 leg=hip+rightAxis*side*.105f;float kneeForward=Mathf.Min(.12f,height*.10f);
                if(!RemoteMode&&crouchFloorValid&&proneBlend<.001f)kneeForward=Ashfall.Integration.Playable.Climbing.R23GrabCrouchLimit.KneeForward(R23CalibrationState.TargetEye,Vector3.Distance(leg,foot));
                Vector3 knee=(leg+foot)*.5f+frame.Front*kneeForward;
                if(RemoteMode&&VehicleSeated){knee=leg+frame.Front*.35f-Vector3.up*.12f;foot=knee-Vector3.up*.38f;}
                Segment(prefix+"Thigh",leg,knee,.14f);Segment(prefix+"Shin",knee,foot,.115f);Pose(prefix+"Boot",foot+frame.Front*.045f,bodyRotation,Vector3.one);
            }
            // No self head renderer reaches the VR eye. All 13 suit pieces remain independent of tracking.
            if(RemoteMode)Pose("AvatarHead",head.position-head.rotation*new Vector3(0,1.68f,0),head.rotation,Vector3.one);
            current.Parts["AvatarHead"].gameObject.SetActive(RemoteMode);current.Root.SetActive(true);
        }
        struct BodyFrame
        {
            public Vector3 Eye,Shoulder,Hip,Right,Rear,Up,Front;public Quaternion Rotation,Tilt;public float Height,Chest,GroundY;
        }
        BodyFrame ReadBodyFrame()
        {
            var f=new BodyFrame();f.Eye=head.position;f.GroundY=!RemoteMode&&crouchFloorValid&&proneBlend<.001f&&f.Eye.y-crouchFloorY<R23CalibrationState.TargetEye?crouchFloorY:root.position.y;f.Height=Mathf.Clamp(f.Eye.y-f.GroundY,.55f,2.2f);
            Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);if(forward.sqrMagnitude>.001f)yaw=Quaternion.LookRotation(forward.normalized,Vector3.up);
            f.Right=yaw*Vector3.right;f.Rear=yaw*Vector3.back;
            f.Shoulder=f.Eye-Vector3.up*Mathf.Min(current.Profile.eyeToShoulder,f.Height*.25f)+f.Rear*current.Profile.bodyRear;
            var uprightHip=new Vector3(f.Eye.x,f.GroundY+Mathf.Min(f.Height*.49f,f.Height-.28f),f.Eye.z)+f.Rear*current.Profile.bodyRear;
            f.Chest=Mathf.Max(.16f,f.Shoulder.y-uprightHip.y);
            f.Up=Vector3.up*R23AppearanceMotionPolicy.UpY(proneBlend)+(yaw*Vector3.forward)*R23AppearanceMotionPolicy.UpForward(proneBlend);
            f.Front=Vector3.Cross(f.Right,f.Up);f.Rotation=Quaternion.LookRotation(f.Front,f.Up);f.Tilt=f.Rotation*Quaternion.Inverse(yaw);
            f.Hip=f.Shoulder+f.Tilt*(uprightHip-f.Shoulder);return f;
        }
        void PresentArm(int i,Vector3 eye,Vector3 shoulder,Vector3 rightAxis,Vector3 rear)
        {
                float side=i==0?-1:1;string prefix=i==0?"L":"R";
                Vector3 s=shoulder+rightAxis*side*.205f;
                Transform hand=i==0?left:right;float[] wristOffset=i==0?current.Profile.leftWrist:current.Profile.rightWrist;
                Vector3 localWrist=new Vector3(wristOffset[0],wristOffset[1],wristOffset[2]),wrist;
                // Actual art already includes contact/support/recoil placement: never add it twice.
                if(handArtPointReader==null||!handArtPointReader(i,localWrist,out wrist))
                    wrist=hand.TransformPoint(localWrist)+(i==0?leftPresentationOffset:rightPresentationOffset);
                Vector3 offset=wrist-s;float distance=Mathf.Max(.001f,offset.magnitude),length=Mathf.Max(.32f,distance*.501f);Vector3 axis=offset/distance;
                Vector3 hint=rightAxis*side*.75f+rear*.3f-Vector3.up*.55f,bend=hint-axis*Vector3.Dot(hint,axis);
                if(bend.sqrMagnitude<.001f)bend=Vector3.Cross(axis,Vector3.up);bend.Normalize();
                Vector3 elbow=s+axis*distance*.5f+bend*Mathf.Sqrt(Mathf.Max(0,length*length-distance*distance*.25f));
                Segment(prefix+"UpperArm",s,elbow,.105f);Segment(prefix+"Forearm",elbow,wrist,.085f);
                current.Parts[prefix+"UpperArm"].gameObject.SetActive(RemoteMode||Distance(eye,s,elbow)>.17f);
                current.Parts[prefix+"Forearm"].gameObject.SetActive(RemoteMode||Distance(eye,elbow,wrist)>.17f);
        }
        // HandsWeapons' existing before-render art refresh is order1100.
        // Refit only the two render arms afterwards; no input, IK authority, legs or raycasts.
        [BeforeRenderOrder(1200)]
        void RefreshTrackedArmsBeforeRender()
        {
            if(current==null||PreviewMode||!tracked||!isActiveAndEnabled||root==null||head==null||left==null||right==null||!current.Root.activeInHierarchy)return;
            var frame=ReadBodyFrame();
            for(int i=0;i<2;i++)PresentArm(i,frame.Eye,frame.Shoulder,frame.Right,frame.Rear);
        }
        void PresentPreview()
        {
            // Actual lobby_map_091.gd::make_avatar coordinates, reflected Z exactly once.
            PosePreview("Jacket",new Vector3(0,1.20f,0),new Vector3(1,.55f,1));
            PosePreview("ChestPlate",new Vector3(0,1.24f,.17f),Vector3.one);
            PosePreview("Belt",new Vector3(0,.91f,0),Vector3.one);
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;string prefix=i==0?"L":"R";
                PreviewSegment(prefix+"UpperArm",new Vector3(side*.205f,1.47f,0),new Vector3(side*.30f,1.20f,-.015f),.105f);
                PreviewSegment(prefix+"Forearm",new Vector3(side*.30f,1.20f,-.015f),new Vector3(side*.26f,.97f,.04f),.085f);
                PreviewSegment(prefix+"Thigh",new Vector3(side*.105f,.91f,0),new Vector3(side*.12f,.51f,.02f),.14f);
                PreviewSegment(prefix+"Shin",new Vector3(side*.12f,.51f,.02f),new Vector3(side*.13f,.12f,-.015f),.115f);
                PosePreview(prefix+"Boot",new Vector3(side*.13f,.08f,.03f),Vector3.one);
                current.Parts[prefix+"UpperArm"].gameObject.SetActive(true);current.Parts[prefix+"Forearm"].gameObject.SetActive(true);
            }
            // Source kit head vertices already include standing-avatar height (~1.68m eyes).
            Pose("AvatarHead",root.position,root.rotation,Vector3.one);current.Parts["AvatarHead"].gameObject.SetActive(true);
        }
        void PosePreview(string label,Vector3 at,Vector3 scale) { Pose(label,root.TransformPoint(at),root.rotation,scale); }
        void PreviewSegment(string label,Vector3 from,Vector3 to,float diameter)
        {
            if(current.Profile.stabilizePreviewLegs&&(label.EndsWith("Thigh",StringComparison.Ordinal)||label.EndsWith("Shin",StringComparison.Ordinal)))
            {
                // R5 _lobby_leg_frames: preserve the avatar-side X axis instead
                // of the shortest-arc ambiguity of almost downward leg segments.
                Vector3 along=(to-from).normalized,across=Vector3.right-along*Vector3.Dot(Vector3.right,along);
                if(across.sqrMagnitude>.000001f)
                {across.Normalize();Pose(label,root.TransformPoint((from+to)*.5f),root.rotation*Quaternion.LookRotation(Vector3.Cross(across,along),along),new Vector3(diameter,(to-from).magnitude,diameter));return;}
            }
            Segment(label,root.TransformPoint(from),root.TransformPoint(to),diameter);
        }
        void Segment(string label,Vector3 from,Vector3 to,float diameter)
        {var offset=to-from;Pose(label,(from+to)*.5f,offset.sqrMagnitude>.000001f?Quaternion.FromToRotation(Vector3.up,offset.normalized):Quaternion.identity,new Vector3(diameter,Mathf.Max(.001f,offset.magnitude),diameter));}
        void Pose(string label,Vector3 position,Quaternion rotation,Vector3 scale)
        {var part=current.Parts[label];part.SetPositionAndRotation(position,rotation);part.localScale=scale;}
        static float Distance(Vector3 point,Vector3 a,Vector3 b)
        {var offset=b-a;return Vector3.Distance(point,a+offset*Mathf.Clamp01(Vector3.Dot(point-a,offset)/Mathf.Max(offset.sqrMagnitude,.000001f)));}
        void OnEnable(){Application.onBeforeRender+=RefreshTrackedArmsBeforeRender;}
        void OnDisable(){Application.onBeforeRender-=RefreshTrackedArmsBeforeRender;if(current!=null&&current.Root!=null)current.Root.SetActive(false);}
        void OnDestroy()
        {loadSerial++;CancelRead();StopAllCoroutines();PublishGlove(null);if(pendingAsset!=null)pendingAsset.Dispose();if(current!=null)current.Dispose();pendingAsset=current=null;}

        [Serializable] sealed class Manifest
        {
            public int version=0,sourceIndex=0,gloveMaterialIndex=0;
            public string id="",label="";
            public float eyeToShoulder=.29f,bodyRear=.12f;
            public bool chestplateFollowsJacket=false,stabilizePreviewLegs=false;
            public float[] leftWrist=null,rightWrist=null;
            public Surface[] materials=null;
        }
        [Serializable] sealed class Surface
        {
            public float[] color=null,emissionColor=null;
            public float metallic=1,roughness=1,normalScale=1,occlusionStrength=1,emissionEnergy=1,specularFactor=1;
            public bool doubleSided=false;
            public string baseColor="",normal="",metallicMap="",occlusion="",emission="";
        }
        sealed class Asset:IDisposable
        {
            public GameObject Root;public Manifest Profile;public Material GloveMaterial;public Material[] Materials;
            public readonly Dictionary<string,Transform> Parts=new Dictionary<string,Transform>(StringComparer.Ordinal);
            readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
            readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
            Texture2D Texture(string file,bool linear,Dictionary<string,byte[]> files)
            {
                Texture2D value;if(textures.TryGetValue(file,out value))return value;
                value=new Texture2D(2,2,TextureFormat.RGBA32,true,linear);resources.Add(value);
                if(!value.LoadImage(files[file],true))throw new IOException("texture "+file);
                value.name=file;value.wrapMode=TextureWrapMode.Repeat;value.filterMode=FilterMode.Trilinear;value.anisoLevel=4;textures.Add(file,value);return value;
            }
            static Vector4 ShaderColor(float[] linear,float energy,bool alpha)
            {
                bool gamma=QualitySettings.activeColorSpace==ColorSpace.Gamma;
                // SetVector bypasses shader-property Color/HDR auto-conversion;
                // the shader-domain value is explicit for both Lit and source specular.
                return new Vector4(R23AppearanceColorDomain.ShaderRgb(linear[0]*energy,gamma),
                    R23AppearanceColorDomain.ShaderRgb(linear[1]*energy,gamma),
                    R23AppearanceColorDomain.ShaderRgb(linear[2]*energy,gamma),alpha?linear[3]:1);
            }
            [Serializable] sealed class ResponseTable { public int version=0;public ResponseRule[] entries=null; }
            [Serializable] sealed class ResponseRule
            {
                public string styleId="",role="";public int sourceIndex=0,materialIndex=0,materialCount=0;
                public bool rawMapFactors=false,publishedNonmetal=false;public float roughnessFloor=0,dielectricScale=1;
                public Surface expected=null;
                static bool Same(float a,float b){return Mathf.Abs(a-b)<=.000001f;}
                static bool Same(float[] a,float[] b)
                {if(a==null||b==null)return a==b;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!Same(a[i],b[i]))return false;return true;}
                public bool Matches(Manifest profile,int index)
                {
                    if(profile.id!=styleId||profile.sourceIndex!=sourceIndex||profile.materials.Length!=materialCount||index!=materialIndex||expected==null)return false;
                    var s=profile.materials[index];var e=expected;
                    return Same(s.color,e.color)&&Same(s.emissionColor,e.emissionColor)&&Same(s.metallic,e.metallic)&&Same(s.roughness,e.roughness)&&Same(s.specularFactor,e.specularFactor)
                        &&Same(s.normalScale,e.normalScale)&&Same(s.occlusionStrength,e.occlusionStrength)&&Same(s.emissionEnergy,e.emissionEnergy)&&s.doubleSided==e.doubleSided
                        &&s.baseColor==e.baseColor&&s.normal==e.normal&&s.metallicMap==e.metallicMap&&s.occlusion==e.occlusion&&s.emission==e.emission;
                }
            }
            static Dictionary<string,ResponseRule> responseRules;
            static ResponseRule SurfaceResponse(Manifest profile,int index)
            {
                if(responseRules==null)
                {
                    var text=Resources.Load<TextAsset>("R23AppearanceMaterialResponses");if(text==null)throw new IOException("Appearance material responses missing");
                    var table=JsonUtility.FromJson<ResponseTable>(text.text);if(table==null||table.version!=1||table.entries==null)throw new IOException("Appearance material response schema");
                    var rules=new Dictionary<string,ResponseRule>(StringComparer.Ordinal);
                    foreach(var row in table.entries)
                    {
                        if(row==null||String.IsNullOrEmpty(row.styleId)||row.expected==null||row.roughnessFloor<=0||row.roughnessFloor>1||row.dielectricScale<0||row.dielectricScale>1)throw new IOException("Invalid appearance response row");
                        rules.Add(row.styleId+"/"+row.materialIndex,row);
                    }
                    responseRules=rules;
                }
                if(UI.R25LegacyPack.IsSkin(profile.id)&&UI.R25LegacyPack.Responses.Length>0){var old=JsonUtility.FromJson<ResponseTable>(UI.R25LegacyPack.Responses);foreach(var row in old.entries)if(row.styleId==profile.id&&row.materialIndex==index&&row.Matches(profile,index))return row;}
                ResponseRule value;

                // Body/glove identity is explicit; material array ordering is not identity.
                // A changed source slot retains its authored response until its binding is renewed.
                return responseRules.TryGetValue(profile.id+"/"+index,out value)&&value.Matches(profile,index)?value:null;
            }
            [Serializable] sealed class FinishTable {public int version=0;public FinishRule[] entries=null;}
            [Serializable] sealed class FinishRule {public ResponseRule binding=null;public string part="",mode="";public float roughnessCap=0;}
            static Dictionary<string,FinishRule> finishRules;
            readonly Dictionary<string,Material> partFinishes=new Dictionary<string,Material>(StringComparer.Ordinal);
            static FinishRule SurfaceFinish(Manifest profile,int index,string part)
            {
                if(finishRules==null){var text=Resources.Load<TextAsset>("R23AppearanceFinishes");if(text==null)throw new IOException("Appearance finishes missing");var table=JsonUtility.FromJson<FinishTable>(text.text);if(table==null||table.version!=1||table.entries==null)throw new IOException("Appearance finish schema");var rules=new Dictionary<string,FinishRule>(StringComparer.Ordinal);
                    foreach(var row in table.entries){if(row==null||row.binding==null||String.IsNullOrEmpty(row.binding.styleId)||row.binding.expected==null)throw new IOException("Invalid appearance finish binding");bool scalar=row.mode=="metal_scalar",cap=row.mode=="part_cap",dry=row.mode=="dry_part";if(!scalar&&!cap&&!dry||scalar&&!String.IsNullOrEmpty(row.part)||!scalar&&String.IsNullOrEmpty(row.part))throw new IOException("Invalid appearance finish mode");if(dry?(row.binding.roughnessFloor<=0||row.binding.roughnessFloor>1||row.binding.dielectricScale<0||row.binding.dielectricScale>1):(row.roughnessCap<=0||row.roughnessCap>1))throw new IOException("Invalid appearance finish value");rules.Add(row.binding.styleId+"/"+row.binding.materialIndex+"/"+(row.part??""),row);}finishRules=rules;}
                if(UI.R25LegacyPack.IsSkin(profile.id)&&UI.R25LegacyPack.Finishes.Length>0){var old=JsonUtility.FromJson<FinishTable>(UI.R25LegacyPack.Finishes);foreach(var row in old.entries)if(row.binding.styleId==profile.id&&row.binding.materialIndex==index&&(row.part??"")==part&&row.binding.Matches(profile,index))return row;}
                FinishRule value;return finishRules.TryGetValue(profile.id+"/"+index+"/"+part,out value)&&value.binding.Matches(profile,index)?value:null;
            }
            void ApplyScalarFinish(Manifest profile,int index,Material material)
            {
                var row=SurfaceFinish(profile,index,"");if(row==null)return;var source=profile.materials[index];if(row.mode!="metal_scalar"||!String.IsNullOrEmpty(source.metallicMap)||source.metallic<.45f)throw new IOException("Metal finish requires exact scalar metal slot");material.SetFloat("_Smoothness",1-Mathf.Min(source.roughness,row.roughnessCap));material.name+=" / named metal finish";
            }
            Material PartMaterial(Manifest profile,string part,int index,Material original)
            {
                var row=SurfaceFinish(profile,index,part);if(row==null)return original;string key=part+"/"+index;Material material;if(partFinishes.TryGetValue(key,out material))return material;var source=profile.materials[index];material=new Material(original);material.name=original.name+" / "+part+" "+row.mode;resources.Add(material);partFinishes.Add(key,material);
                if(row.mode=="part_cap"){
                    float minimumSmoothness=1-row.roughnessCap;if(source.specularFactor!=1){var response=material.GetVector("_AppearanceResponse");response.z=minimumSmoothness;response.w=1;material.SetVector("_AppearanceResponse",response);}
                    else{if(material.shader.name!="Ashfall/R23AppearanceDoubleSided")throw new IOException("Part finish wrapper missing");material.DisableKeyword("_SPECULAR_SETUP");material.SetVector("_SpecColor",new Vector4(minimumSmoothness,-1,0,0));}
                }else if(row.mode=="dry_part"){
                    var binding=row.binding;if(source.specularFactor!=1){material.SetVector("_AppearanceResponse",new Vector4(binding.roughnessFloor,binding.dielectricScale,0,0));material.SetVector("_SourceMapFactors",new Vector4(source.metallic,source.roughness,binding.rawMapFactors?1:0,0));}
                    else{if(material.shader.name!="Ashfall/R23AppearanceDoubleSided")throw new IOException("Part response wrapper missing");material.SetVector("_SpecColor",new Vector4(binding.roughnessFloor,binding.dielectricScale,source.roughness,binding.rawMapFactors?1:0));material.EnableKeyword("_SPECULAR_SETUP");}
                }else throw new IOException("Part finish mode");return material;
            }
            public void Build(Manifest profile,byte[] bytes,Dictionary<string,byte[]> files)
            {
                Profile=profile;Root=new GameObject("R23 appearance "+profile.id);Root.SetActive(false);
                var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new IOException("URP Lit missing");
                var materials=new Material[profile.materials.Length];Materials=materials;
                for(int i=0;i<materials.Length;i++)
                {
                    var s=profile.materials[i];var response=SurfaceResponse(profile,i);var soft=response==null?new Vector2(.78f,.4f):new Vector2(response.roughnessFloor,response.publishedNonmetal?-response.dielectricScale:response.dielectricScale);var actualShader=s.specularFactor!=1?Resources.Load<Shader>("R23AppearanceSpecular"):(soft.x>0||s.doubleSided||!String.IsNullOrEmpty(s.normal))?Resources.Load<Shader>("R23AppearanceDoubleSided"):shader;if(actualShader==null)throw new IOException("Source appearance specular shader missing");var m=new Material(actualShader);m.name="R23 "+profile.id+" material "+i;materials[i]=m;resources.Add(m);
                    m.SetVector("_BaseColor",ShaderColor(s.color,1,true));m.SetFloat("_Metallic",s.metallic);m.SetFloat("_Smoothness",1-s.roughness);m.SetFloat("_Cull",s.doubleSided?0:2);
                    if(!String.IsNullOrEmpty(s.baseColor))m.SetTexture("_BaseMap",Texture(s.baseColor,false,files));
                    if(!String.IsNullOrEmpty(s.normal)){m.SetTexture("_BumpMap",Texture(s.normal,true,files));m.SetFloat("_BumpScale",s.normalScale);m.EnableKeyword("_NORMALMAP");}
                    if(!String.IsNullOrEmpty(s.metallicMap)){m.SetTexture("_MetallicGlossMap",Texture(s.metallicMap,true,files));m.SetFloat("_Smoothness",1);m.EnableKeyword("_METALLICSPECGLOSSMAP");}
                    if(!String.IsNullOrEmpty(s.occlusion)){m.SetTexture("_OcclusionMap",Texture(s.occlusion,true,files));m.SetFloat("_OcclusionStrength",s.occlusionStrength);m.EnableKeyword("_OCCLUSIONMAP");}
                    if(!String.IsNullOrEmpty(s.emission)||s.emissionColor[0]>0||s.emissionColor[1]>0||s.emissionColor[2]>0){if(!String.IsNullOrEmpty(s.emission))m.SetTexture("_EmissionMap",Texture(s.emission,false,files));m.SetVector("_EmissionColor",ShaderColor(s.emissionColor,s.emissionEnergy,false));m.EnableKeyword("_EMISSION");}
                    if(s.specularFactor!=1){m.SetFloat("_SpecularFactor",s.specularFactor);m.SetFloat("_SourceDoubleSided",s.doubleSided?1:0);m.SetFloat("_HasNormalMap",String.IsNullOrEmpty(s.normal)?0:1);m.SetFloat("_HasMetallicMap",String.IsNullOrEmpty(s.metallicMap)?0:1);m.SetFloat("_HasOcclusionMap",String.IsNullOrEmpty(s.occlusion)?0:1);m.SetFloat("_HasEmissionMap",String.IsNullOrEmpty(s.emission)?0:1);}
                    // Both appearance wrappers use one response function. Raw initial maps
                    // apply glTF factors once; later converted maps already contain factors.
                    if(soft.x>0)
                    {
                        if(s.specularFactor!=1)
                        {
                            m.SetVector("_AppearanceResponse",new Vector4(soft.x,soft.y,0,0));
                            m.SetVector("_SourceMapFactors",new Vector4(s.metallic,s.roughness,response!=null&&response.rawMapFactors?1:0,0));
                        }
                        else
                        {
                            m.SetVector("_SpecColor",new Vector4(soft.x,soft.y,s.roughness,response!=null&&response.rawMapFactors?1:0));
                            m.EnableKeyword("_SPECULAR_SETUP");
                        }
                    }
                }
                for(int i=0;i<materials.Length;i++)ApplyScalarFinish(profile,i,materials[i]);
                GloveMaterial=materials[profile.gloveMaterialIndex];
                using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadInt32()!=0x52323341)throw new IOException("appearance mesh header");int count=reader.ReadInt32();if(count<1||count>128)throw new IOException("source body surface count");
                    for(int p=0;p<count;p++)
                    {
                        int length=reader.ReadInt32();if(length<1||length>128)throw new IOException("part name length");string label=System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
                        int nv=reader.ReadInt32(),nt=reader.ReadInt32(),mi=reader.ReadInt32();
                        if(nv<3||nv>100000||nt<3||nt>300000||nt%3!=0||mi<0||mi>=materials.Length)throw new IOException("part mesh limits");
                        var positions=new Vector3[nv];var normals=new Vector3[nv];var uv=new Vector2[nv];var indices=new int[nt];
                        for(int v=0;v<nv;v++){positions[v]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());normals[v]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());uv[v]=new Vector2(reader.ReadSingle(),reader.ReadSingle());}
                        for(int t=0;t<nt;t++){indices[t]=reader.ReadInt32();if(indices[t]<0||indices[t]>=nv)throw new IOException("part index");}
                        var mesh=new Mesh();resources.Add(mesh);mesh.name=profile.id+"/"+label;mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();mesh.RecalculateTangents();
                        Transform group;bool exists=Parts.TryGetValue(label,out group);var part=new GameObject(exists?label+" source surface "+p:label);part.transform.SetParent(exists?group:Root.transform,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=PartMaterial(profile,label,mi,materials[mi]);if(!exists)Parts.Add(label,part.transform);
                    }
                    if(stream.Position!=stream.Length)throw new IOException("trailing mesh data");
                }
                foreach(string required in new[]{"Jacket","ChestPlate","Belt","LUpperArm","RUpperArm","LForearm","RForearm","LThigh","RThigh","LShin","RShin","LBoot","RBoot","AvatarHead"})if(!Parts.ContainsKey(required)){
                    // Actual R23 apply_style empties missing variant pieces; these original
                    // Witch Doctor kits intentionally have no separate ChestPlate mesh.
                    if(required!="ChestPlate"||profile.sourceIndex<3)throw new IOException("source part missing "+required);
                    var empty=new GameObject(required+" (absent in source)");empty.transform.SetParent(Root.transform,false);Parts.Add(required,empty.transform);
                }
                Parts["AvatarHead"].gameObject.SetActive(false);
            }
            public void Dispose()
            {if(Root!=null){Root.SetActive(false);UnityEngine.Object.Destroy(Root);}foreach(var resource in resources)if(resource!=null)UnityEngine.Object.Destroy(resource);resources.Clear();textures.Clear();Parts.Clear();partFinishes.Clear();Root=null;}
        }
    }
}
#endif

// Pure render policy shares the exact scalar blend/axis math with the live presenter.
// Deliberate user-requested prone glide; this does not change source flight mechanics.
namespace Ashfall.Integration.Playable.Appearance
{
    public static class R23AppearanceMotionPolicy
    {
        static bool Finite(float value){return !System.Single.IsNaN(value)&&!System.Single.IsInfinity(value);}
        static float Clamp01(float value){return !Finite(value)?0:System.Math.Max(0,System.Math.Min(1,value));}
        public static float AdvanceProne(float current,bool enabled,bool supported,bool gliding,bool paused,float seconds)
        {
            current=Clamp01(current);if(!enabled)return 0;if(paused||!Finite(seconds)||seconds<=0)return current;
            float target=enabled&&!supported&&gliding?1:0,step=System.Math.Min(.1f,seconds)*4;
            return current<target?System.Math.Min(target,current+step):System.Math.Max(target,current-step);
        }
        public static float GaitSpeed(bool enabled,bool supported,bool gliding,bool paused,float vx,float vz,float prone)
        {
            if(!enabled||!supported||gliding||paused||!Finite(vx)||!Finite(vz)||!Finite(prone)||Clamp01(prone)>.001f)return 0;
            return (float)System.Math.Min(10,System.Math.Sqrt((double)vx*vx+(double)vz*vz));
        }
        public static float UpY(float prone){return (float)System.Math.Cos(Clamp01(prone)*System.Math.PI*.5);}
        public static float UpForward(float prone){return (float)System.Math.Sin(Clamp01(prone)*System.Math.PI*.5);}
    }
}
