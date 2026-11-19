#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.Flight;
using UnityEngine;
using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Activities
{
    public struct R23ActivityMotion
    {
        public bool OwnsMotion;
        public Vector3 Velocity,Displacement;
        public FlightMode Mode;
    }
    // Root calls EvaluateMotion before its ONE CharacterController.Move, then AcceptMotion.
    // No Update pump, root transform writes, tracking reads or CharacterController.Move here.
    [DefaultExecutionOrder(1250)]
    public sealed class R23VRActivities:MonoBehaviour,IConstructionWorld,IFlightCollisionAuthorization
    {
        sealed class FlightGate:IAuthorityGate
        {public RuleResult Validate(CommandContext c){return c.IsAuthority&&c.SessionId=="r23-playable"&&c.ActorId=="player"&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local_scope");}}
        Transform root,head,left,right,map;CharacterController body;
        FlightMachine flight;R23ConstructionSession construction;
        readonly R23BuildGesture gesture=new R23BuildGesture();
        readonly R25BuildSelection buildSelection=new R25BuildSelection();
        public int SelectedBuildVariant {get{return buildPieces&&buildSelection.Piece<4?buildSelection.Piece:0;}}
        public bool SelectedDismantle {get{return buildPieces&&buildSelection.Piece==4;}}
        readonly Dictionary<string,GameObject> panels=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        readonly Dictionary<Collider,string> panelColliders=new Dictionary<Collider,string>();
        readonly Dictionary<string,int> presentedTiers=new Dictionary<string,int>(StringComparer.Ordinal);
        readonly Dictionary<string,float> flashes=new Dictionary<string,float>(StringComparer.Ordinal);
        R23ConstructionPreview preview;
        GameObject content;Material builtMaterial;long serial,tick;int previewHand;
        Texture2D panelFinish;bool structurePaused;MaterialPropertyBlock panelProperties;
        R23ConstructionAudio audio;R25BuildDebris debris;
        string localDismantled;
        public Func<string,bool> SharedDismantledProvider;
        float effectiveWalkSpeed=Ashfall.Modules.Player.PlayerTuning.WalkSpeed*2.5f;
        bool constructionEnabled=true,infiniteBuilds,buildPieces=true,tierHeld;int selectedTier;
        public int SelectedBuildTier {get{return buildPieces?selectedTier:0;}}
        public Func<Collider,bool> AdditionalStaticTerrain;
        public Func<int,bool> SharedBuildRequested;public Func<PlacedPanel[]> SharedPanelsProvider;
        internal void ReconcileNetworkConstruction(){if(construction!=null&&SharedPanelsProvider!=null){var rows=SharedPanelsProvider();if(rows!=null&&construction.ReconcileNetworkPreview(rows))ReconcileStructure();}}
        public string BuildStatus {get;private set;}
        public string MotionStatus {get;private set;}
        internal bool ReconcileNetworkBalance(int value){return construction!=null&&construction.ReconcileNetworkBalance(value);}
        public int Balance {get{return construction==null?0:construction.Balance;}}
        public int GrantSupply(string receipt,int amount){return construction==null?0:construction.GrantSupply(receipt,amount,tick);}
        public int WithdrawSupply(int amount){return construction==null?0:construction.WithdrawSupply(amount,tick);}
        public int PanelCount {get{return panels.Count;}}
        public Transform StructureRoot {get{return content==null?null:content.transform;}}
        public PlacedPanel[] PanelSnapshot {get{return SharedPanelsProvider!=null?SharedPanelsProvider():construction==null?new PlacedPanel[0]:construction.Snapshot();}}
        public bool SourceFinishLoaded {get;private set;}
        public string SourceFinishFailure {get;private set;}
        public bool BuildActive {get{return gesture.Active;}}
        public int PreviewPanelCount {get{return preview==null?0:preview.VisiblePanelCount;}}
        public bool PreviewLightningVisible {get{return preview!=null&&preview.LightningVisible;}}
        public Transform PreviewVisualRoot {get{return preview==null?null:preview.Root;}}
        public FlightMode Mode {get{return flight==null?FlightMode.Ground:flight.Mode;}}
        public bool PadLaunchActive {get{return flight!=null&&flight.Mode==FlightMode.Launch;}}
        public void Initialize(Transform playerRoot,Transform headPose,Transform leftPose,Transform rightPose,CharacterController controller,Transform mapRoot)
        {
            if(root!=null)throw new InvalidOperationException("Activities already initialized");
            if(playerRoot==null||headPose==null||leftPose==null||rightPose==null||controller==null||mapRoot==null)throw new ArgumentException("root/head/hands/controller/map required");
            root=playerRoot;head=headPose;left=leftPose;right=rightPose;body=controller;map=mapRoot;
            panelProperties=new MaterialPropertyBlock();
            content=new GameObject("R23 constructed world panels");
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)throw new InvalidOperationException("construction shader missing");
            // construction_piece.apply_panel_material source user crop: neutral
            // complete panel on each cube face, .6 metal/.78 roughness, no glow.
            builtMaterial=new Material(shader);builtMaterial.SetColor("_BaseColor",Color.white);builtMaterial.color=Color.white;builtMaterial.SetFloat("_Metallic",.6f);builtMaterial.SetFloat("_Smoothness",.22f);builtMaterial.SetFloat("_Glossiness",.22f);builtMaterial.SetFloat("_Cull",0);builtMaterial.SetColor("_EmissionColor",Color.black);builtMaterial.EnableKeyword("_EMISSION");
            preview=new R23ConstructionPreview(content.transform);debris=new R25BuildDebris(content.transform,builtMaterial);
            audio=gameObject.AddComponent<R23ConstructionAudio>();
            ResetRound();StartCoroutine(LoadPanelFinish());
        }
        CommandContext Context(string verb){return new CommandContext("r23-playable","player",verb+"-"+(++serial),tick,0,true);}
        public void ResetRound()
        {
            if(audio!=null)audio.Stop();if(debris!=null)debris.Clear();localDismantled=null;
            gesture.Cancel();HidePreview();foreach(var id in new List<string>(panels.Keys))RemovePanel(id);panels.Clear();panelColliders.Clear();flashes.Clear();structurePaused=false;
            flight=new FlightMachine("r23-playable","player",0,new FlightGate(),effectiveWalkSpeed,1);
            construction=new R23ConstructionSession(this,infiniteBuilds);BuildStatus="Free hand: hold trigger to build, release to place; hold A/X 0.4s cancel";MotionStatus="";
        }
        public void ConfigureMatchConstruction(bool enabled,bool infinite,bool expanded=true)
        {
            if(constructionEnabled==enabled&&infiniteBuilds==infinite&&buildPieces==expanded)return;
            constructionEnabled=enabled;infiniteBuilds=infinite;buildPieces=expanded;ResetRound();
        }
        IEnumerator LoadPanelFinish()
        {
            string url=Application.streamingAssetsPath+"/PlayableR23/Construction/build_panel_user.png";if(!url.Contains("://"))url=new Uri(url).AbsoluteUri;
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=30;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){SourceFinishFailure=request.error;yield break;}
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,true);texture.name="Actual R23 build_panel_user";
                if(!texture.LoadImage(request.downloadHandler.data,false)){Destroy(texture);SourceFinishFailure="Source construction finish could not decode";yield break;}
                texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Bilinear;panelFinish=texture;builtMaterial.SetTexture("_BaseMap",texture);builtMaterial.SetTexture("_MainTex",texture);SourceFinishLoaded=true;
            }
        }
        internal bool IsBuildCollider(Collider collider)
        {return collider!=null&&collider.enabled&&!collider.isTrigger&&collider.gameObject.activeInHierarchy&&panelColliders.ContainsKey(collider);}
        public bool TryReadPanel(Collider collider,out PlacedPanel panel)
        {
            panel=null;string id;if(!IsBuildCollider(collider)||!panelColliders.TryGetValue(collider,out id)||construction==null)return false;
            foreach(var candidate in construction.Snapshot())if(candidate.Id==id){panel=candidate;return true;}return false;
        }
        // Call only for a committed physical impact. Preview/map/actor colliders
        // and old withdrawn collider references cannot authorize a panel hit.
        // MATCH RULES BUILD DAMAGE (offline): every source (guns, bots, melee, blasts, vehicle guns) passes raw damage here.
        // The rule is applied exactly once, below. Unbound means 1. Rooms are host-owned (DamageSharedPanel).
        internal Func<float> ReadBuildDamageMultiplier;
        float ScaleBuildDamage(float amount){float m=ReadBuildDamageMultiplier==null?1:ReadBuildDamageMultiplier();return float.IsNaN(m)||float.IsInfinity(m)||m<0?amount:amount*m;}
        public bool TryDamageCollider(Collider collider,float amount)
        {
            PlacedPanel panel;if(SharedBuildRequested!=null)return false;
            if(structurePaused||!TryReadPanel(collider,out panel))return false;
            amount=ScaleBuildDamage(amount);if(amount<=0)return false;
            var result=construction.Damage(panel.Id,amount,tick);if(!result.Accepted)return false;
            flashes[panel.Id]=.12f;ReconcileStructure();return true;
        }
        public bool TryDamagePanel(string id,float amount)
        {
            if(SharedBuildRequested!=null)return false;
            if(structurePaused||construction==null||String.IsNullOrEmpty(id)||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0)return false;
            amount=ScaleBuildDamage(amount);if(amount<=0)return false;
            var result=construction.Damage(id,amount,tick);if(!result.Accepted)return false;flashes[id]=.12f;ReconcileStructure();return true;
        }
        public void TickStructure(float seconds,bool paused)
        {
            structurePaused=paused;if(construction==null||paused){if(audio!=null)audio.Stop();return;}
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0){BuildStatus="Structure: invalid time step";return;}
            if(SharedBuildRequested!=null){ReconcileStructure();return;}
            Physics.SyncTransforms();var result=construction.Advance(seconds,false);if(!result.Accepted){BuildStatus="Structure: "+result.Reason;return;}
            foreach(var id in new List<string>(flashes.Keys)){float remaining=flashes[id]-seconds;if(remaining<=0)flashes.Remove(id);else flashes[id]=remaining;}
            ReconcileStructure();
        }
        void RemovePanel(string id)
        {
            GameObject go;if(!panels.TryGetValue(id,out go))return;
            foreach(var collider in new List<Collider>(panelColliders.Keys))if(panelColliders[collider]==id){if(collider!=null)collider.enabled=false;panelColliders.Remove(collider);}
            if(go!=null){go.SetActive(false);Destroy(go);}panels.Remove(id);flashes.Remove(id);presentedTiers.Remove(id);
        }
        void ReconcileStructure()
        {
            var snapshot=PanelSnapshot;var live=new HashSet<string>(StringComparer.Ordinal);foreach(var panel in snapshot)live.Add(panel.Id);
            bool nativeChanged=false;
            foreach(var id in new List<string>(panels.Keys))if(!live.Contains(id)||panels[id]==null){bool fold=localDismantled==id||SharedDismantledProvider!=null&&SharedDismantledProvider(id);
                if(panels[id]!=null){if(buildPieces&&debris!=null)debris.Emit(panels[id],fold);if(audio!=null){if(fold)audio.BuildCue("dismantle",panels[id].transform.position);else audio.Play(panels[id].transform.position,true);}}RemovePanel(id);nativeChanged=true;}
            foreach(var panel in snapshot)
            {
                GameObject go;if(!panels.TryGetValue(panel.Id,out go)){go=CreatePanel(panel,true);panels.Add(panel.Id,go);foreach(var collider in go.GetComponentsInChildren<Collider>())panelColliders.Add(collider,panel.Id);nativeChanged=true;}
                int oldTier;if(!presentedTiers.TryGetValue(panel.Id,out oldTier)||oldTier!=panel.Shape.Tier){if(audio!=null)audio.BuildCue(presentedTiers.ContainsKey(panel.Id)?"upgrade":"place",U(panel.Shape.Center));presentedTiers[panel.Id]=panel.Shape.Tier;}
                float maximum=panel.Shape.MaximumHealth;float ratio=Mathf.Clamp01(panel.Health/maximum);Color tint=panel.Health>=maximum?Color.white:Color.Lerp(new Color(.9f,.9f,.9f),new Color(.19f,.19f,.19f),1-ratio);
                tint*=panel.Shape.Tier==0?Color.white:panel.Shape.Tier==1?new Color(.55f,.78f,.95f):new Color(.95f,.64f,.28f);
                float flash;panelProperties.Clear();panelProperties.SetColor("_BaseColor",tint);panelProperties.SetColor("_Color",tint);panelProperties.SetColor("_EmissionColor",flashes.TryGetValue(panel.Id,out flash)&&flash>0?new Color(1,.24f,.05f):Color.black);foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.SetPropertyBlock(panelProperties);
            }
            if(nativeChanged)Physics.SyncTransforms();
        }
        public void SetWalkMultiplier(float multiplier)
        {
            if(float.IsNaN(multiplier)||float.IsInfinity(multiplier)||multiplier<.25f||multiplier>5)throw new ArgumentOutOfRangeException("multiplier");
            float speed=Ashfall.Modules.Player.PlayerTuning.WalkSpeed*multiplier;
            if(Mathf.Approximately(speed,effectiveWalkSpeed))return;
            effectiveWalkSpeed=speed;flight=new FlightMachine("r23-playable","player",0,new FlightGate(),speed,1);
        }
        public void ResetCarrierMotion()
        {
            // Source reset_motion clears glide/dive/pad velocity on pod entry,
            // without deleting existing construction or replenishing supplies.
            flight=new FlightMachine("r23-playable","player",0,new FlightGate(),effectiveWalkSpeed,1);
            gesture.Cancel();HidePreview();MotionStatus="";
        }
        public R23ActivityMotion EvaluateMotion(InputFrame input,float seconds,bool grounded,bool ceiling,Vector3 acceptedVelocity,Vector3 walkTarget,Vector3 moveDirection,bool handsReady,bool climbing,bool mantling,Vector3? movementForward=null,bool playerTurned=false)
        {
            var output=new R23ActivityMotion{Velocity=acceptedVelocity,Displacement=acceptedVelocity*seconds,Mode=FlightMode.Suspended};
            if(flight==null||seconds<=0||seconds>.1f||float.IsNaN(seconds))return output;
            tick=Math.Max(tick,input.Tick);
            Vector3 handL=root.InverseTransformPoint(left.position),handR=root.InverseTransformPoint(right.position);
            var frame=new FlightFrame{PlayerTurned=playerTurned,DownhillRun=input.GlideDownhillRun,DeltaSeconds=seconds,Supported=grounded,CeilingBlocked=ceiling,InputEnabled=input.Tracked&&!input.Paused,Tracked=input.Tracked,Paused=input.Paused,Climbing=climbing,Mantling=mantling,XR=true,HandsReady=input.HandsTracked||handsReady,Forward=F(movementForward??head.forward),MoveDirection=F(moveDirection),WalkTarget=F(walkTarget),Velocity=F(acceptedVelocity),LeftHand=F(handL),RightHand=F(handR),DiveForward=false,DiveAxis=float.IsNaN(input.DiveAxis)?0:Mathf.Clamp(input.DiveAxis,-1,1)};
            var result=flight.Step(Context("flight"),frame);
            if(!result.Accepted){MotionStatus=result.Reason;return output;}
            output.Mode=flight.Mode;output.OwnsMotion=flight.Mode!=FlightMode.Ground&&flight.Mode!=FlightMode.Suspended;
            if(output.OwnsMotion){output.Velocity=U(flight.Velocity);output.Displacement=output.Velocity*seconds;}
            MotionStatus=flight.Mode==FlightMode.Glide?"Gliding: bank by raising one hand":flight.Mode==FlightMode.Dive?"Diving":"";
            return output;
        }
        public bool TryLaunchPad(Vector3 normal,float speed,bool inputReady,bool climbing,bool mantling,out Vector3 velocity)
        {
            velocity=Vector3.zero;if(flight==null||flight.PendingMotion!=null)return false;
            var result=flight.Launch(Context("authored-pad"),F(normal),speed,inputReady,climbing,mantling);
            if(!result.Accepted){MotionStatus=result.Reason;return false;}
            velocity=U(flight.Velocity);MotionStatus="Source jump-pad launch";return true;
        }
        public void AcceptMotion(Vector3 actualDisplacement,float seconds,bool grounded,bool ceiling,bool externalOwner)
        {
            if(flight==null||flight.PendingMotion==null||seconds<=0)return;
            Vector3 velocity=actualDisplacement/seconds;
            if(grounded||(ceiling&&velocity.y>0))velocity.y=0;
            var result=flight.ApplyCollisionResult(Context("collision"),flight.PendingMotion,new FlightCollisionResult{Velocity=F(velocity),Grounded=grounded,CeilingBlocked=ceiling,ExternalMotionOwner=externalOwner},this);
            if(!result.Accepted)MotionStatus=result.Reason;
        }
        public RuleResult ValidateCollision(CommandContext context,FlightMotionReceipt receipt,FlightCollisionResult result){return RuleResult.Accept();}
        public void TickConstruction(InputFrame input,bool leftFree,bool rightFree,bool cancelLeft,bool cancelRight,int primaryHand=1,bool tierButton=false)
        {
            bool tierTap=tierButton&&!tierHeld;tierHeld=tierButton;
            if(construction==null)return;tick=Math.Max(tick,input.Tick);
            if(!constructionEnabled){gesture.Cancel();HidePreview();BuildStatus="";return;}
            if(gesture.Active&&tierTap&&buildPieces){selectedTier=(selectedTier+1)%3;if(audio!=null)audio.BuildCue("snap",head.position);}
            bool cancelHold=buildSelection.Step(gesture.Active,cancelLeft||cancelRight,Time.unscaledDeltaTime,buildPieces);
            bool wasActive=gesture.Active;if(wasActive)previewHand=gesture.Hand;
            bool leftPrimary=primaryHand==0;
            gesture.Step(leftPrimary&&input.TriggerLeft,!leftPrimary&&input.TriggerRight,leftPrimary&&leftFree,!leftPrimary&&rightFree,cancelHold||!leftPrimary,cancelHold||leftPrimary,input.Tracked&&!input.Paused,leftPrimary?1:2);
            if(gesture.Active)previewHand=gesture.Hand;
            if(!gesture.Active&&!gesture.Commit){HidePreview();BuildStatus="Free hand: hold trigger, release to build | hold A/X 0.4s cancel | charges "+Balance;return;}
            if(gesture.Commit&&SharedBuildRequested!=null){HidePreview();BuildStatus=SharedBuildRequested(previewHand-1)?"BUILD REQUESTED | WAITING FOR HOST":"HOST REFUSED BUILD";return;}
            if(SelectedDismantle){
                Physics.SyncTransforms();var aim=previewHand==1?left:right;
                var pose=new BuilderState(F(aim.position),F(Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.Forward(aim)),F(head.position),true,true,SelectedBuildTier,buildPieces);
                var target=construction.DismantleTarget(pose);
                if(target==null){HidePreview();BuildStatus="DISMANTLE: aim at your own piece within 6 m | hold A/X 0.4s cancel";return;}
                if(gesture.Commit){var removed=construction.Dismantle(pose,tick);HidePreview();if(removed.Accepted){localDismantled=target.Id;ReconcileStructure();localDismantled=null;}BuildStatus=removed.Accepted?"Dismantled | charges "+Balance:removed.Reason;return;}
                preview.Begin();preview.GhostMaterial.SetColor("_Color",new Color(1,.55f,.08f,.45f));preview.Panel(target.Shape);
                preview.Label("DISMANTLE | "+target.Shape.PieceName+" | REFUND "+(infiniteBuilds?0:target.Shape.Cost/2),head);
                preview.Finish(aim.position,Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.Forward(aim),target.Shape.Distance,Time.realtimeSinceStartupAsDouble*1000);
                BuildStatus="Release trigger: dismantle | hold A/X 0.4s cancel";return;
            }
            Color previewTint=SelectedBuildTier==0?new Color(.8f,.8f,.8f,.35f):SelectedBuildTier==1?new Color(.55f,.78f,.95f,.35f):new Color(.95f,.64f,.28f,.35f);preview.GhostMaterial.SetColor("_Color",previewTint);
            Physics.SyncTransforms();var hand=previewHand==1?left:right;
            PlacedPanel[] plan;int cost;var before=construction.Snapshot();
            // Retain the same corrected source bore used by held weapons.
            var result=construction.Evaluate(new BuilderState(F(hand.position),F(Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.Forward(hand)),F(head.position),true,true,SelectedBuildTier,buildPieces,SelectedBuildVariant),tick,gesture.Commit,out plan,out cost);
            if(!result.Accepted){HidePreview();BuildStatus="Build: "+result.Reason+" | charges "+Balance;return;}
            if(gesture.Commit){HidePreview();ReconcileStructure();}
            else
            {
                preview.Begin();float primaryDistance=0;string previewKind="";
                foreach(var p in plan)
                {bool changed=true;foreach(var old in before)if(old.Id==p.Id&&old.Shape.Tier==p.Shape.Tier){changed=false;break;}if(changed){preview.Panel(p.Shape);primaryDistance=p.Shape.Distance;previewKind=p.Shape.PieceName;}}
                preview.Label(previewKind+" \u00b7 "+(SelectedBuildTier==0?"LIGHT":SelectedBuildTier==1?"REINFORCED":"ARMOURED")+" \u00b7 "+cost+" CHARGES",head);
                preview.Finish(hand.position,Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.Forward(hand),primaryDistance,Time.realtimeSinceStartupAsDouble*1000);
                if(audio!=null)audio.SetLightning(hand.position,preview.LightningVisible);
            }
            BuildStatus=gesture.Commit?"Placed | charges "+Balance:"Release trigger: place/upgrade ("+cost+") | hold A/X 0.4s cancel"+(buildPieces?" | tap A/X piece, B/Y tier":"")+" | charges "+Balance;
        }
        GameObject CreatePanel(PlacedPanel panel,bool solid)
        {
            var go=new GameObject(solid?panel.Id:"R23 build preview");
            go.transform.SetParent(content.transform,false);go.transform.position=U(panel.Shape.Center);
            foreach(var box in CandidateGeometry.Boxes(panel.Shape)){
                var child=GameObject.CreatePrimitive(PrimitiveType.Cube);child.name="Solid panel box";child.layer=8;
                child.transform.SetParent(go.transform,false);child.transform.position=U(box.Center);child.transform.localScale=U(CandidateGeometry.HalfSize(box))*2;
                child.GetComponent<Renderer>().sharedMaterial=builtMaterial;
                if(panel.Shape.Variant>=2){
                    var trim=new GameObject("Opening trim");trim.layer=8;trim.transform.SetParent(child.transform,false);
                    var line=trim.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=4;line.widthMultiplier=.012f;line.sharedMaterial=builtMaterial;
                    line.SetPositions(panel.Shape.Axis==0?new[]{new Vector3(.502f,-.49f,-.49f),new Vector3(.502f,.49f,-.49f),new Vector3(.502f,.49f,.49f),new Vector3(.502f,-.49f,.49f)}:new[]{new Vector3(-.49f,-.49f,.502f),new Vector3(-.49f,.49f,.502f),new Vector3(.49f,.49f,.502f),new Vector3(.49f,-.49f,.502f)});
                }
                if(!solid){var collider=child.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}
            }
            return go;
        }
        void HidePreview(){if(preview!=null)preview.Hide();if(audio!=null)audio.SetLightning(Vector3.zero,false);}
        // The rig resolves walking/turning after TickConstruction. Refresh only
        // presentation after that movement and the latest tracked hand-art pass.
        public void RefreshConstructionPresentation()
        {
            if(!isActiveAndEnabled||preview==null||!gesture.Active||!preview.LightningVisible)return;
            var hand=previewHand==1?left:right;if(hand==null)return;
            preview.RefreshTrackedPose(hand.position,Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.Forward(hand));
            if(audio!=null)audio.SetLightning(hand.position,true);
        }
        void LateUpdate(){if(debris!=null)debris.Tick();RefreshConstructionPresentation();}
        [BeforeRenderOrder(1250)]void BeforeConstructionRender(){RefreshConstructionPresentation();}
        void OnEnable(){Application.onBeforeRender+=BeforeConstructionRender;}
        public void SetSfxVolume(float value){if(audio!=null)audio.SetSfxVolume(value);}
        bool MapCollider(Collider c){if(c==null||c.gameObject.layer==19||c.gameObject.layer==20||c.gameObject.layer==22||!c.enabled||c.isTrigger)return false;bool additional=AdditionalStaticTerrain!=null&&AdditionalStaticTerrain(c);return additional||c.attachedRigidbody==null&&c.transform.IsChildOf(map);}
        bool BuiltCollider(Collider c){return c!=null&&content!=null&&c.transform.IsChildOf(content.transform);}
        public bool HasStaticContact(Panel panel,float tolerance){foreach(var box in CandidateGeometry.Boxes(panel))if(HasStaticContactBox(box,tolerance))return true;return false;}
        bool HasStaticContactBox(Panel panel,float tolerance)
        {
            foreach(var c in Physics.OverlapBox(U(panel.Center),U(CandidateGeometry.HalfSize(panel))+Vector3.one*tolerance,Quaternion.identity,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(MapCollider(c))return true;
            return false;
        }
        public string Clearance(Panel panel,string actor){foreach(var box in CandidateGeometry.Boxes(panel)){string reason=ClearanceBox(box,actor);if(!String.IsNullOrEmpty(reason))return reason;}return "";}
        string ClearanceBox(Panel panel,string actor)
        {
            if(body==null||!body.enabled)return "player_clearance_unavailable";
            // All live CharacterControllers and non-map actor/loot colliders protect occupied space.
            foreach(var c in Physics.OverlapBox(U(panel.Center),U(CandidateGeometry.HalfSize(panel)),Quaternion.identity,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                if(!MapCollider(c)&&!BuiltCollider(c))return "player_objective_clearance";
            return "";
        }
        public bool HasLineOfSight(string actor,Float3 from,Float3 to)
        {
            // Source field_builder._entry_clear and _update_placement explicitly
            // allow world overlap. An extra ray to the panel CENTER incorrectly
            // rejects valid edge-aimed placements as the player moves around cover.
            // Occupancy/support, burial, reach and player/objective clearance are
            // still validated by the existing placement owner before every commit.
            return true;
        }
        bool GroundHeight(Vector3 point,float top,float bottom,out float height)
        {
            height=float.NegativeInfinity;float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(new Vector3(point.x,top,point.z),Vector3.down,top-bottom,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                if((MapCollider(hit.collider)||BuiltCollider(hit.collider))&&hit.distance<distance){distance=hit.distance;height=hit.normal.y>=.25f?hit.point.y:float.NegativeInfinity;}
            return !float.IsNegativeInfinity(height);
        }
        public bool IsBuried(Panel panel){foreach(var box in CandidateGeometry.Boxes(panel))if(!IsBuriedBox(box))return false;return true;}
        bool IsBuriedBox(Panel panel)
        {
            // Map chunks combine terrain and structures, so wall terrain identity is unavailable.
            // Preserve working wall placement; floor burial uses the source nine nearest-hit samples.
            if(panel.Kind!="floor")return false;
            float side=CandidateGeometry.Side;Vector3 center=U(panel.Center);
            foreach(var offset in new[]{Vector3.zero,new Vector3(-.501f,0,-.501f),new Vector3(-.501f,0,.501f),new Vector3(.501f,0,-.501f),new Vector3(.501f,0,.501f),new Vector3(-.501f,0,0),new Vector3(.501f,0,0),new Vector3(0,0,-.501f),new Vector3(0,0,.501f)})
            {float height;if(!GroundHeight(center+offset*side,center.y+side+.25f,center.y-side*3,out height)||height<center.y-.025f)return false;}
            return true;
        }
        void OnDisable(){if(debris!=null)debris.Clear();Application.onBeforeRender-=BeforeConstructionRender;gesture.Cancel();HidePreview();}
        void OnDestroy(){Application.onBeforeRender-=BeforeConstructionRender;if(debris!=null)debris.Dispose();if(preview!=null)preview.Dispose();if(content!=null)Destroy(content);if(builtMaterial!=null)Destroy(builtMaterial);if(panelFinish!=null)Destroy(panelFinish);}
        static Float3 F(Vector3 value){return new Float3(value.x,value.y,value.z);}static Vector3 U(Float3 value){return new Vector3(value.X,value.Y,value.Z);}
    }
}
#endif
