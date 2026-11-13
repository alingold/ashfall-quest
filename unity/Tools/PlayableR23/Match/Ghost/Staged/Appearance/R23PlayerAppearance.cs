#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Appearance
{
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
    [DefaultExecutionOrder(100)]
    public sealed class R23PlayerAppearance:MonoBehaviour
    {
        static readonly R23AppearanceStyle[] styles={new R23AppearanceStyle("camo","APEX CAMO · ARGON",0),new R23AppearanceStyle("witch_doctor","WITCH DOCTOR · ARGON ORIGINAL",1),new R23AppearanceStyle("eclipse_ronin","ECLIPSE RONIN · ARGON",2),new R23AppearanceStyle("astra","WITCH DOCTOR · GPT-6 ASTRA",3),new R23AppearanceStyle("sol","WITCH DOCTOR · GPT-6.1 SOL",4),new R23AppearanceStyle("argon","WITCH DOCTOR · GEMINI 4 ARGON",5),new R23AppearanceStyle("claude","WITCH DOCTOR · CLAUDE OPUS",6),new R23AppearanceStyle("obsidian","OBSIDIAN",7)};
        // Only real converted R5 kits are selectable. Stable source IDs are not
        // compacted into menu indices; unconverted registry cards stay unavailable.
        static readonly R23AppearanceStyle[] r5Styles={
            new R23AppearanceStyle("pban","PBAN",8),
            new R23AppearanceStyle("pj","PJ",9),
            new R23AppearanceStyle("roy","Roy",10),
            new R23AppearanceStyle("hattie","Hattie",11),
            new R23AppearanceStyle("warthog","Warthog",12),
            new R23AppearanceStyle("jack_o","Jack-O",13),
            new R23AppearanceStyle("broodmother","BROODMOTHER",14),
            new R23AppearanceStyle("the_pilgrim","The Pilgrim",15),
            new R23AppearanceStyle("bone_collector","Bone Collector",16),
            new R23AppearanceStyle("tormentus","Tormentus",17),
            new R23AppearanceStyle("axel_cupid","AXEL (CUPID)",18),
            new R23AppearanceStyle("valentina_cupid","Valentina (Cupid)",19),
            new R23AppearanceStyle("kringle_beach_boy","Kringle (Beach Boy)",20),
            new R23AppearanceStyle("yesbot","Yesbot",21),
            new R23AppearanceStyle("anubis_paw_and_order","Anubis (Paw and Order)",22),
            new R23AppearanceStyle("roy_trickshot","Roy (Trickshot)",23),
            new R23AppearanceStyle("millie_punk","Millie (Punk)",24),
            new R23AppearanceStyle("anna_tabby","Anna (Tabby)",25),
            new R23AppearanceStyle("ace_sergeant","Ace (Sergeant)",26),
            new R23AppearanceStyle("ace_trainee","Ace (Trainee)",27),
            new R23AppearanceStyle("hattie_varsity","Hattie (Varsity)",28),
            new R23AppearanceStyle("pj_adventurer","PJ (Adventurer)",29),
            new R23AppearanceStyle("roy_daring","Roy (Daring)",30),
            new R23AppearanceStyle("anna_rainbow","Anna (Rainbow)",31),
            new R23AppearanceStyle("hattie_incognito","Hattie (Incognito)",32),
            new R23AppearanceStyle("millie_defiant","Millie (Defiant)",33),
            new R23AppearanceStyle("anna","Anna",34),
            new R23AppearanceStyle("smiley","Smiley",35),
            new R23AppearanceStyle("haze_nuclear","Haze (Nuclear)",36),
            new R23AppearanceStyle("ace","Ace",37),
            new R23AppearanceStyle("millie","Millie",38),
            new R23AppearanceStyle("dani_checkers","Dani (Checkers)",39),
            new R23AppearanceStyle("grog_swiller","Grog Swiller",40),
            new R23AppearanceStyle("headless_horseman","Headless Horseman",41),
            new R23AppearanceStyle("tox","Tox",42),
            new R23AppearanceStyle("void_wanderer","Void Wanderer",43),
            new R23AppearanceStyle("wingman","Wingman",44),
            new R23AppearanceStyle("prism","Prism",45),
            new R23AppearanceStyle("blair_pumpkin_spice","Blair (Pumpkin Spice)",46),
            new R23AppearanceStyle("victoria_scarlet","Victoria (Scarlet)",47),
            new R23AppearanceStyle("raven","Raven",48),
            new R23AppearanceStyle("logan_maverick","Logan (Maverick)",49),
            new R23AppearanceStyle("the_phoenix","The Phoenix",50),
            new R23AppearanceStyle("kaiju","Kaiju",51),
            new R23AppearanceStyle("reaper","Reaper",52),
            new R23AppearanceStyle("polymer_gold","Polymer Gold (Draft)",53),
            new R23AppearanceStyle("phoenix_angelic","Phoenix Angelic (Draft)",54),
            new R23AppearanceStyle("daybreak","Daybreak (Draft)",55),
            new R23AppearanceStyle("fender","Fender (Draft)",56),
            new R23AppearanceStyle("boreas","Boreas (Draft)",57),
            new R23AppearanceStyle("bureau_xlr8","Bureau XLR8 (Draft)",58),
            new R23AppearanceStyle("odin","Odin (Draft)",59),
            new R23AppearanceStyle("sir_box_a_lot","Sir Box-a-lot (Draft)",60),
            new R23AppearanceStyle("edge","Edge (Draft)",61),
            new R23AppearanceStyle("hazard_meltdown","Hazard (Meltdown) (Draft)",62),
            new R23AppearanceStyle("gobbler","Gobbler (Draft)",63),
            new R23AppearanceStyle("vyper","Vyper (Draft)",64),
            new R23AppearanceStyle("surtr","Surtr (Draft)",65),
            new R23AppearanceStyle("cherry_bomb","Cherry Bomb (Draft)",66),
            new R23AppearanceStyle("jax_nova","Jax Nova (Draft)",67),
            new R23AppearanceStyle("ajax","Ajax (Draft)",68),
            new R23AppearanceStyle("onryo","Onryo (Draft)",69),
            new R23AppearanceStyle("jay","Jay (Draft)",70),
            new R23AppearanceStyle("pierce_vice","Pierce Vice (Draft)",71),
            new R23AppearanceStyle("morgan_midnight","Morgan Midnight (Draft)",72),
            new R23AppearanceStyle("stacy_throwback","Stacy Throwback (Draft)",73),
            new R23AppearanceStyle("hattie_golden","Hattie Golden (Draft)",74),
            new R23AppearanceStyle("roy_golden","Roy Golden (Draft)",75),
            new R23AppearanceStyle("anna_golden","Anna Golden (Draft)",76),
            new R23AppearanceStyle("akai","Akai (Draft)",77),
            new R23AppearanceStyle("imperator","Imperator (Draft)",78),
            new R23AppearanceStyle("kaiju_y","Kaiju Y (Draft)",79),
            new R23AppearanceStyle("stalker","Stalker (Draft)",80),
            new R23AppearanceStyle("prowler","Prowler (Draft)",81),
            new R23AppearanceStyle("scaredy_cat","Scaredy Cat (Draft)",82),
            new R23AppearanceStyle("phoenix_onyx","Phoenix Onyx (Draft)",83),
            new R23AppearanceStyle("phoenix_darkwing","Phoenix Darkwing (Draft)",84),
            new R23AppearanceStyle("polymer_brass","Pvt. Polymer (Brass) (Draft)",85),
            new R23AppearanceStyle("auditor","The Auditor (Draft)",86),
            new R23AppearanceStyle("nameless_one","Nameless One (Draft)",87),
            new R23AppearanceStyle("wheatley_summer","Wheatley (Summer) (Draft)",88),
            new R23AppearanceStyle("edge_bureau","Edge (Bureau) (Draft)",89),
            new R23AppearanceStyle("destiny","Destiny (Draft)",90),
            new R23AppearanceStyle("onepopularguy","#OnePOPularGuy (Draft)",91),
            new R23AppearanceStyle("kaos","Kaos (Draft)",92),
            new R23AppearanceStyle("hopn_stacy","HopN Stacy (Draft)",93),
            new R23AppearanceStyle("kunzo","Kunzo (Draft)",94),
            new R23AppearanceStyle("argyle","Argyle (Draft)",95),
            new R23AppearanceStyle("scout_recon","Scout (Recon) (Draft)",96),
            new R23AppearanceStyle("zero_forest","Zero (Forest) (Draft)",97),
            new R23AppearanceStyle("dani_aqua","Dani (Aqua) (Draft)",98),
            new R23AppearanceStyle("dee","Dee (Draft)",99),
            new R23AppearanceStyle("rico","Rico (Draft)",100),
            new R23AppearanceStyle("haze_safety","Haze (Safety) (Draft)",101),
            new R23AppearanceStyle("toni","Toni (Draft)",102),
            new R23AppearanceStyle("pj_golden","PJ (Golden) (Draft)",103),
            new R23AppearanceStyle("ace_golden","Ace (Golden) (Draft)",104),
            new R23AppearanceStyle("millie_golden","Millie (Golden) (Draft)",105),
            new R23AppearanceStyle("pj_gilded","PJ (Gilded) (Draft)",106),
            new R23AppearanceStyle("chet_throwback","Chet (Throwback) (Draft)",107),
            new R23AppearanceStyle("polymer_steel","Pvt. Polymer (Steel) (Draft)",108),
            new R23AppearanceStyle("tsuki","Tsuki (Draft)",109),
            new R23AppearanceStyle("tsuki_blood_omen","Tsuki (Blood Omen) (Draft)",110),
            new R23AppearanceStyle("bureau_enforcer","Bureau Enforcer (Draft)",111),
            new R23AppearanceStyle("zara_nightshade","Zara Nightshade (Draft)",112),
            new R23AppearanceStyle("oni_akai","Oni (Akai) (Draft)",113),
            new R23AppearanceStyle("akai_oni","Akai (Oni) (Draft)",114),
        };
        public R23AppearanceStyle[] AvailableStyles {get{var result=new R23AppearanceStyle[styles.Length+r5Styles.Length];Array.Copy(styles,result,styles.Length);Array.Copy(r5Styles,0,result,styles.Length,r5Styles.Length);return result;}}
        public string SelectedStyleId {get;private set;}
        public string RequestedStyleId {get;private set;}
        public bool Ready {get{return current!=null;}}
        public bool Loading {get;private set;}
        public string Failure {get;private set;}
        public bool PreviewMode {get;private set;}
        // Actual selected world-rooted art; no selector preview or mesh duplication.
        public Transform CurrentVisualRoot {get{return current==null||current.Root==null?null:current.Root.transform;}}
        public Material CurrentGloveMaterial {get{return current==null?null:current.GloveMaterial;}}
        // Wire to R23HandsWeapons.SetGloveMaterial. We retain the existing hand mesh owner.
        public event Action<Material> GloveMaterialChanged;
        public event Action<string> StyleChanged;
        Transform root,head,left,right;Asset current,pendingAsset;Coroutine loading;
        UnityWebRequest pendingRead;
        int loadSerial;
        bool tracked=true,paused;float phase;Quaternion yaw=Quaternion.identity;Vector3 previousRoot;
        Vector3 leftPresentationOffset,rightPresentationOffset;
        public void SetFreeHandPresentationOffsets(Vector3 leftWorldOffset,Vector3 rightWorldOffset)
        {leftPresentationOffset=PresentationOffset(leftWorldOffset);rightPresentationOffset=PresentationOffset(rightWorldOffset);}
        static Vector3 PresentationOffset(Vector3 value)
        {return float.IsNaN(value.x)||float.IsNaN(value.y)||float.IsNaN(value.z)||float.IsInfinity(value.x)||float.IsInfinity(value.y)||float.IsInfinity(value.z)||value.sqrMagnitude>16?Vector3.zero:value;}
        public void Initialize(Transform playerRoot,Transform headPose,Transform leftPose,Transform rightPose)
        {InitializeBindings(playerRoot,headPose,leftPose,rightPose);SelectStyle("camo");}
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
            if(!exists)return false;
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
            string path=Application.streamingAssetsPath+"/PlayableR23/Appearance/"+file;
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
        void PublishGlove(Material material)
        {
            var subscribers=GloveMaterialChanged;if(subscribers==null)return;
            foreach(Action<Material> subscriber in subscribers.GetInvocationList())try{subscriber(material);}catch(Exception error){Debug.LogException(error,this);}
        }
        void LateUpdate()
        {
            if(root==null)return;float seconds=Mathf.Max(.0001f,Time.unscaledDeltaTime);
            float speed=paused?0:Mathf.Min(10,new Vector2(root.position.x-previousRoot.x,root.position.z-previousRoot.z).magnitude/seconds);
            previousRoot=root.position;Present(speed);
        }
        void Present(float speed)
        {
            if(current==null)return;
            if(!tracked||!isActiveAndEnabled||root==null||head==null||left==null||right==null){current.Root.SetActive(false);return;}
            if(PreviewMode){PresentPreview();current.Root.SetActive(true);return;}
            Vector3 eye=head.position;float height=Mathf.Clamp(eye.y-root.position.y,.55f,2.2f);
            Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);if(forward.sqrMagnitude>.001f)yaw=Quaternion.LookRotation(forward.normalized,Vector3.up);
            Vector3 rightAxis=yaw*Vector3.right,rear=yaw*Vector3.back;
            Vector3 hip=new Vector3(eye.x,root.position.y+Mathf.Min(height*.49f,height-.28f),eye.z)+rear*current.Profile.bodyRear;
            Vector3 shoulder=eye-Vector3.up*Mathf.Min(current.Profile.eyeToShoulder,height*.25f)+rear*current.Profile.bodyRear;
            float chest=Mathf.Max(.16f,shoulder.y-hip.y);
            Pose("Jacket",hip+Vector3.up*chest*.5f,yaw,new Vector3(1,chest,1));
            Pose("ChestPlate",hip+Vector3.up*chest*.58f-rear*.18f,yaw,new Vector3(1,Mathf.Min(1,chest/.30f),1));
            if(current.Profile.chestplateFollowsJacket)
                Pose("ChestPlate",hip+Vector3.up*chest*(.5f+.04f/.55f)-rear*.17f,yaw,new Vector3(1,chest/.55f,1));
            Pose("Belt",hip,yaw,Vector3.one);
            phase+=paused?0:Mathf.Max(0,Time.unscaledDeltaTime)*Mathf.Min(speed,10)*2.2f;
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;string prefix=i==0?"L":"R";
                Vector3 s=shoulder+rightAxis*side*.205f;
                Transform hand=i==0?left:right;float[] wristOffset=i==0?current.Profile.leftWrist:current.Profile.rightWrist;
                Vector3 wrist=hand.TransformPoint(new Vector3(wristOffset[0],wristOffset[1],wristOffset[2]))+(i==0?leftPresentationOffset:rightPresentationOffset);
                Vector3 offset=wrist-s;float distance=Mathf.Max(.001f,offset.magnitude),length=Mathf.Max(.32f,distance*.501f);Vector3 axis=offset/distance;
                Vector3 hint=rightAxis*side*.75f+rear*.3f-Vector3.up*.55f,bend=hint-axis*Vector3.Dot(hint,axis);
                if(bend.sqrMagnitude<.001f)bend=Vector3.Cross(axis,Vector3.up);bend.Normalize();
                Vector3 elbow=s+axis*distance*.5f+bend*Mathf.Sqrt(Mathf.Max(0,length*length-distance*distance*.25f));
                Segment(prefix+"UpperArm",s,elbow,.105f);Segment(prefix+"Forearm",elbow,wrist,.085f);
                current.Parts[prefix+"UpperArm"].gameObject.SetActive(Distance(eye,s,elbow)>.17f);
                current.Parts[prefix+"Forearm"].gameObject.SetActive(Distance(eye,elbow,wrist)>.17f);
                float wave=Mathf.Sin(phase+i*Mathf.PI),stride=wave*Mathf.Min(speed*.045f,.25f);
                Vector3 foot=new Vector3(eye.x,root.position.y+.07f,eye.z)+rightAxis*side*.115f+rear*(.10f+stride);
                float nearest=float.PositiveInfinity;
                foreach(var hit in Physics.RaycastAll(foot+Vector3.up*.35f,Vector3.down,.85f,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                    if(hit.collider.attachedRigidbody==null&&!(hit.collider is CharacterController)&&!hit.collider.transform.IsChildOf(root)&&hit.distance<nearest){nearest=hit.distance;foot.y=hit.point.y+.07f;}
                foot.y+=Mathf.Max(0,wave)*Mathf.Min(speed*.012f,.065f);
                Vector3 leg=hip+rightAxis*side*.105f,knee=(leg+foot)*.5f-rear*Mathf.Min(.12f,height*.10f);
                Segment(prefix+"Thigh",leg,knee,.14f);Segment(prefix+"Shin",knee,foot,.115f);Pose(prefix+"Boot",foot-rear*.045f,yaw,Vector3.one);
            }
            // No self head renderer reaches the VR eye. All 13 suit pieces remain independent of tracking.
            current.Parts["AvatarHead"].gameObject.SetActive(false);current.Root.SetActive(true);
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
        void OnDisable(){if(current!=null&&current.Root!=null)current.Root.SetActive(false);}
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
            public GameObject Root;public Manifest Profile;public Material GloveMaterial;
            public readonly Dictionary<string,Transform> Parts=new Dictionary<string,Transform>(StringComparer.Ordinal);
            readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
            readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>(StringComparer.Ordinal);
            Texture2D Texture(string file,bool linear,Dictionary<string,byte[]> files)
            {
                Texture2D value;if(textures.TryGetValue(file,out value))return value;
                value=new Texture2D(2,2,TextureFormat.RGBA32,true,linear);resources.Add(value);
                if(!value.LoadImage(files[file],true))throw new IOException("texture "+file);
                value.wrapMode=TextureWrapMode.Repeat;value.filterMode=FilterMode.Trilinear;value.anisoLevel=4;textures.Add(file,value);return value;
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
            public void Build(Manifest profile,byte[] bytes,Dictionary<string,byte[]> files)
            {
                Profile=profile;Root=new GameObject("R23 appearance "+profile.id);Root.SetActive(false);
                var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new IOException("URP Lit missing");
                var materials=new Material[profile.materials.Length];
                for(int i=0;i<materials.Length;i++)
                {
                    var s=profile.materials[i];var actualShader=s.specularFactor!=1?Resources.Load<Shader>("R23AppearanceSpecular"):s.doubleSided?Resources.Load<Shader>("R23AppearanceDoubleSided"):shader;if(actualShader==null)throw new IOException("Source appearance specular shader missing");var m=new Material(actualShader);m.name="R23 "+profile.id+" material "+i;materials[i]=m;resources.Add(m);
                    m.SetVector("_BaseColor",ShaderColor(s.color,1,true));m.SetFloat("_Metallic",s.metallic);m.SetFloat("_Smoothness",1-s.roughness);m.SetFloat("_Cull",s.doubleSided?0:2);
                    if(!String.IsNullOrEmpty(s.baseColor))m.SetTexture("_BaseMap",Texture(s.baseColor,false,files));
                    if(!String.IsNullOrEmpty(s.normal)){m.SetTexture("_BumpMap",Texture(s.normal,true,files));m.SetFloat("_BumpScale",s.normalScale);m.EnableKeyword("_NORMALMAP");}
                    if(!String.IsNullOrEmpty(s.metallicMap)){m.SetTexture("_MetallicGlossMap",Texture(s.metallicMap,true,files));m.SetFloat("_Smoothness",1);m.EnableKeyword("_METALLICSPECGLOSSMAP");}
                    if(!String.IsNullOrEmpty(s.occlusion)){m.SetTexture("_OcclusionMap",Texture(s.occlusion,true,files));m.SetFloat("_OcclusionStrength",s.occlusionStrength);m.EnableKeyword("_OCCLUSIONMAP");}
                    if(!String.IsNullOrEmpty(s.emission)||s.emissionColor[0]>0||s.emissionColor[1]>0||s.emissionColor[2]>0){if(!String.IsNullOrEmpty(s.emission))m.SetTexture("_EmissionMap",Texture(s.emission,false,files));m.SetVector("_EmissionColor",ShaderColor(s.emissionColor,s.emissionEnergy,false));m.EnableKeyword("_EMISSION");}
                    if(s.specularFactor!=1){m.SetFloat("_SpecularFactor",s.specularFactor);m.SetFloat("_SourceDoubleSided",s.doubleSided?1:0);m.SetFloat("_HasNormalMap",String.IsNullOrEmpty(s.normal)?0:1);m.SetFloat("_HasMetallicMap",String.IsNullOrEmpty(s.metallicMap)?0:1);m.SetFloat("_HasOcclusionMap",String.IsNullOrEmpty(s.occlusion)?0:1);m.SetFloat("_HasEmissionMap",String.IsNullOrEmpty(s.emission)?0:1);}
                }
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
                        Transform group;bool exists=Parts.TryGetValue(label,out group);var part=new GameObject(exists?label+" source surface "+p:label);part.transform.SetParent(exists?group:Root.transform,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=materials[mi];if(!exists)Parts.Add(label,part.transform);
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
            {if(Root!=null){Root.SetActive(false);UnityEngine.Object.Destroy(Root);}foreach(var resource in resources)if(resource!=null)UnityEngine.Object.Destroy(resource);resources.Clear();textures.Clear();Parts.Clear();Root=null;}
        }
    }
}
#endif
