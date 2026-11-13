#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using UnityEngine;
using UnityEngine.XR;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    [DefaultExecutionOrder(1000)]
    public sealed partial class R23HandsWeapons : MonoBehaviour,IInteractionQuery
    {
        int slotCapacity=3;
        public int SlotCapacity {get{return slotCapacity;}set{slotCapacity=Math.Max(3,Math.Min(6,value));}}
        Transform player,left,right,weapon,muzzle,leftVisual,rightVisual,content;
        readonly List<Transform> weaponModels=new List<Transform>();
        readonly List<Renderer>[] magazineParts={new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>(),new List<Renderer>()};
        readonly List<Transform>[] boltParts={new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>(),new List<Transform>()};
        readonly Dictionary<Transform,Vector3> boltRestPositions=new Dictionary<Transform,Vector3>();
        readonly Transform[] offeredMagazines=new Transform[21],heldMagazines=new Transform[21];
        readonly Vector3[] magazineHomes={new Vector3(-.00045859f,-.0960232f,.17046988f),new Vector3(0,-.0897345f,.20337826f),new Vector3(0,-.05928609f,.11562737f),Vector3.zero,new Vector3(-.0000000174634852f,-.12042805f,-.0107801325f),new Vector3(0,-.0662978311f,.091493664f),Vector3.zero,new Vector3(9.047493905e-05f,-0.1478707243f,0.2143906034f),new Vector3(1.25047113e-08f,0.05543516979f,-0.09955194718f),new Vector3(-0.002643062233f,-0.1256524375f,0.2434360492f),new Vector3(-3.599971533e-05f,-0.09888900344f,0.03798250389f),new Vector3(-1.541333151e-05f,-0.1020505595f,0.03945997842f),new Vector3(-2.209026644e-09f,-0.1264717493f,0.208577757f),new Vector3(1.379053223e-05f,-0.08628115157f,-0.2188923141f),new Vector3(-0.0002909407327f,-0.03597471001f,0.1644864421f),new Vector3(8.975620269e-09f,-0.02506277805f,0.1307706665f),Vector3.zero,Vector3.zero,Vector3.zero,Vector3.zero,Vector3.zero};
        readonly HashSet<string> ownedWeapons=new HashSet<string>(new[]{"cx4","px4","m1014"});
        readonly List<string> ownedWeaponOrder=new List<string>(new[]{"cx4","px4","m1014"});
        sealed class WorldWeapon {internal Vector3 Position;internal Quaternion Rotation;internal Transform Visual;internal bool Visible,CenterSet;internal Vector3 LocalCenter;}
        readonly Dictionary<string,WorldWeapon> droppedWeapons=new Dictionary<string,WorldWeapon>();
        readonly Dictionary<string,R23WeaponFeedbackState> instanceFeedback=new Dictionary<string,R23WeaponFeedbackState>();
        string activeActionId="cx4";
        R23WeaponFeedbackState ActiveFeedback {get{R23WeaponFeedbackState state;if(!instanceFeedback.TryGetValue(activeActionId,out state)){state=new R23WeaponFeedbackState(ActiveWeaponId);instanceFeedback.Add(activeActionId,state);}return state;}}
        float ActiveRecoilScale {get{R23WeaponInstanceInfo info;return round!=null&&round.TryReadWeaponInstance(activeActionId,out info)?info.RecoilScale:1;}}
        public string ActiveWeaponActionId {get{return activeActionId;}}
        public string ResolveWeaponProfile(string actionId){return round==null?null:round.ResolveWeaponProfile(actionId);}
        public bool TryReadWeaponInstance(string actionId,out R23WeaponInstanceInfo info){info=null;return round!=null&&round.TryReadWeaponInstance(actionId,out info);}
        public string[] OwnedWeaponActionIds {get{return ownedWeaponOrder.ToArray();}}
        public string[] WorldWeaponActionIds {get{var result=new List<string>();if(round!=null)foreach(string id in round.WeaponActionIds)if(droppedWeapons.ContainsKey(id)||starterPickup&&!holstered&&id==activeActionId)result.Add(id);return result.ToArray();}}
        bool holstered,starterPickup=true,presentationSuppressed,sourceArtReady;
        readonly HashSet<string> starterWorldAtHome=new HashSet<string>();
        readonly Dictionary<string,Vector3> starterWorldOffsets=new Dictionary<string,Vector3>{{"magnum",Vector3.right*.5f},{"mk18",Vector3.right*.85f},{"akm",Vector3.right*1.2f},{"spr",Vector3.right*1.55f},{"ump",Vector3.left*.5f},{"p90",Vector3.left*.85f},{"mp5",Vector3.left*1.2f},{"mp9",Vector3.left*1.55f},{"1911",new Vector3(-0.5f,0,.45f)},{"tec9",new Vector3(-0.85f,0,.45f)},{"rfb",new Vector3(-1.2f,0,.45f)},{"fal",new Vector3(-1.55f,0,.45f)},{"s85",new Vector3(-1.9f,0,.45f)},{"smaw",new Vector3(.85f,0,.75f)},{"knife",new Vector3(.50f,0,.75f)},{"katana",new Vector3(.15f,0,.75f)},{"excalibur",new Vector3(-.2f,0,.75f)},{"cyberblade",new Vector3(-.55f,0,.75f)}};
        R23MagnumView magnumView;R23M1014View m1014View;readonly R23M1014Controls m1014Controls=new R23M1014Controls();
        readonly bool[] pickupRearmRequired=new bool[2];
        int primaryHand=1;
        readonly string[] weaponIds={"mk18","akm","spr","magnum","cx4","px4","m1014","ump","p90","mp5","mp9","1911","tec9","rfb","fal","s85","smaw","knife","katana","excalibur","cyberblade"};
        static readonly string[] StarterWeaponIds={"cx4","px4","m1014"};
        public string[] InitialOwnedWeaponIds {get{return (string[])StarterWeaponIds.Clone();}}
        readonly float[] modelScales={.92f,.945f,1.32f,R23MagnumRuntime.ModelScale,.855f,.7f,1,0.7182f,0.72f,0.684f,1f,0.55f,0.72f,0.8f,1.037875f,1.12f,1.6f,1f,1f,1f,1f};
        readonly Vector3[] sourceMuzzles={new Vector3(0,.045f,.69f),new Vector3(0,.045f,.76f),new Vector3(0,.045f,.69f),new Vector3(0,.065f,.392f),new Vector3(0,.045f,.62f),new Vector3(0,.045f,.29f),new Vector3(0,.045f,.77f),new Vector3(0f,0.045f,0.46f),new Vector3(0f,0.01555242941f,0.2433011482f),new Vector3(0f,0.045f,0.62f),new Vector3(0f,0.045f,0.42f),new Vector3(0f,0.045f,0.42f),new Vector3(0f,0.045f,0.42f),new Vector3(0f,0.045f,0.62f),new Vector3(0f,0.045f,0.76f),new Vector3(0f,0.045f,0.76f),new Vector3(0,.045f,.85f),new Vector3(0,-.057f,-.025f),new Vector3(0,-.057f,-.025f),new Vector3(0,-.057f,-.025f),new Vector3(0,-.057f,-.025f)};
        int selectedWeapon=4;
        Camera head;
        R23Visuals visuals;
        R23RoundState round;
        readonly List<R23PracticeTarget> targets=new List<R23PracticeTarget>();
        readonly List<Material> targetMaterials=new List<Material>();
        readonly Dictionary<Renderer,Material> gloveDefaults=new Dictionary<Renderer,Material>();
        Material leftGloveStyle,rightGloveStyle;
        Vector3 leftContactDelta,rightContactDelta,leftGloveHome,rightGloveHome;
        Vector3 pickupPosition;Quaternion pickupRotation;
        bool initialized,previousReload,previousRestart,previousSelection,hadFocus=true,gameplayPaused,inputTrackingUnavailable;
        public bool ExternalInputPump;
        public Func<bool> CanCollectWorldWeapon;
        // Called only for an actual aimed, reachable, still-live world instance. Root
        // stages/drops the selected whole slot when full; no fourth-slot entitlement.
        public Func<string,int,bool> MakeRoomForWorldWeapon;
        public Func<string,int,bool> CanClaimWorldGrip;
        // Explicit offline training fixtures, ported from source practice_dummy.gd.
        public bool EnablePracticeTargets=true;
        public int PracticeTargetCount { get { return targets.Count; } }
        public void SetPracticeTargetsVisible(bool visible){foreach(var target in targets)if(target!=null)target.gameObject.SetActive(visible);}
        R23Audio audioCues;
        R23GunFeedback gunFeedback;
        R23ImpactMarks impactMarks;
        public int ImpactMarkCount {get{return impactMarks==null?0:impactMarks.Count;}}
        public long ImpactMarksAccepted {get{return impactMarks==null?0:impactMarks.Accepted;}}
        public long ImpactMarksRejected {get{return impactMarks==null?0:impactMarks.Rejected;}}
        public long ImpactMarkSequence {get{return impactMarks==null?0:impactMarks.Sequence;}}
        public int ImpactLastProjectionRays {get{return impactMarks==null?0:impactMarks.LastProjectionRays;}}
        public int ImpactLastSlot {get{return impactMarks==null?-1:impactMarks.LastSlot;}}
        public void ClearImpactMarks(){if(impactMarks!=null)impactMarks.Clear();}
        public bool TryReadImpactMarkVertex(int slot,int vertex,out Vector3 point){point=Vector3.zero;return impactMarks!=null&&impactMarks.TryReadWorldVertex(slot,vertex,out point);}
        public float ReadImpactMarkAge(int slot){return impactMarks==null?-1:impactMarks.ReadAge(slot);}
#if UNITY_EDITOR
        public void AdvanceImpactMarksForFixture(float seconds){if(impactMarks!=null)impactMarks.Advance(seconds);}
#endif
        readonly R23WeaponFeedbackState[] feedbackStates={new R23WeaponFeedbackState("mk18"),new R23WeaponFeedbackState("akm"),new R23WeaponFeedbackState("spr"),new R23WeaponFeedbackState("magnum"),new R23WeaponFeedbackState("cx4"),new R23WeaponFeedbackState("px4"),new R23WeaponFeedbackState("m1014"),new R23WeaponFeedbackState("ump"),new R23WeaponFeedbackState("p90"),new R23WeaponFeedbackState("mp5"),new R23WeaponFeedbackState("mp9"),new R23WeaponFeedbackState("1911"),new R23WeaponFeedbackState("tec9"),new R23WeaponFeedbackState("rfb"),new R23WeaponFeedbackState("fal"),new R23WeaponFeedbackState("s85"),new R23WeaponFeedbackState("smaw"),new R23WeaponFeedbackState("knife"),new R23WeaponFeedbackState("katana"),new R23WeaponFeedbackState("excalibur"),new R23WeaponFeedbackState("cyberblade")};
        int feedbackAdvancedFrame=-1,shotgunPresentedFrame=-1;
        public bool GunAmmoPanelReady {get{return gunFeedback!=null;}}
        public bool GunAmmoPanelVisible {get{return IsSmaw?smawView!=null&&smawView.AmmoPanelVisible:gunFeedback!=null&&gunFeedback.PanelVisible;}}
        public string GunAmmoPanelText {get{return IsSmaw&&smawView!=null?smawView.AmmoText:gunFeedback==null?"":gunFeedback.AmmoText;}}
        public Vector3 GunAmmoPanelPosition {get{return IsSmaw&&smawView!=null?smawView.AmmoPanelPosition:gunFeedback==null?Vector3.zero:gunFeedback.PanelPosition;}}
        public bool MuzzleFlashVisible {get{return gunFeedback!=null&&gunFeedback.FlashVisible;}}
        public bool WorldReticleVisible {get{return IsSmaw?smawView!=null&&smawView.ReticleVisible:gunFeedback!=null&&gunFeedback.ReticleVisible;}}
        public float VisualRecoilDistance {get{return ActiveFeedback.Distance*ActiveRecoilScale;}}
        public float VisualRecoilPitchRadians {get{return ActiveFeedback.PitchRadians*ActiveRecoilScale;}}
        public long FeedbackAcceptedShots {get{return ActiveFeedback.AcceptedShots;}}
        public bool TracerVisible {get{return tracer!=null&&tracer.enabled;}}
        public Vector3 TracerWorldStart {get{return tracer==null?Vector3.zero:tracer.GetPosition(0);}}
        public Vector3 TracerWorldEnd {get{return tracer==null?Vector3.zero:tracer.GetPosition(1);}}
        public event Action<int,float,float> HapticRequested;
        readonly Transform[] feedbackLenses=new Transform[21],feedbackRearSights=new Transform[21],feedbackFrontSights=new Transform[21];
        float sfxVolume=1;
        readonly R23MagazineControls magazineControls=new R23MagazineControls();
        Transform p90InsertArrow,p90ChargeArrow;R23Mp9Laser mp9Laser;
        public bool Mp9LaserVisible {get{return mp9Laser!=null&&mp9Laser.Visible;}}
        public Vector3 Mp9LaserStart {get{return mp9Laser==null?Vector3.zero:mp9Laser.WorldStart;}}
        public Vector3 Mp9LaserEnd {get{return mp9Laser==null?Vector3.zero:mp9Laser.WorldEnd;}}
        float LogicalReloadScale {get{var definition=WeaponDefinition(ActiveWeaponId);return definition==null?modelScales[selectedWeapon]:definition.ReloadScale;}}
        R23SprScope sprScope;R23ReflexOptic cx4Optic;
        readonly R23SemiTriggerLatch sprTrigger=new R23SemiTriggerLatch();
        bool previousTrigger,weaponHandReserved;
        HandlingInput trackedControls;
        bool haveTrackedControls,desktopReloadRequest;
        R23HandsInputSnapshot lastInput;
        bool haveInputSnapshot,pickupIntentSampled;
        Transform PrimaryTransform { get { return primaryHand==0?left:right; } }
        Transform SupportTransform { get { return primaryHand==0?right:left; } }
        Transform HandTransform(int hand) { return hand==0?left:right; }
        public int PrimaryHand { get { return primaryHand; } }
        public int SupportHand { get { return 1-primaryHand; } }
        public bool LeftHandHeld { get { return HasWeapon&&primaryHand==0; } }
        public bool RightHandHeld { get { return HasWeapon&&primaryHand==1; } }
        public bool LeftHandOccupied { get { return LeftHandHeld||(HasWeapon&&primaryHand==1&&SupportHandOccupied); } }
        public bool RightHandOccupied { get { return RightHandHeld||(HasWeapon&&primaryHand==0&&SupportHandOccupied); } }
        public string ActiveWeaponId { get { return weaponIds[selectedWeapon]; } }
        public string ActiveWeaponName { get { return WeaponDisplayName(ActiveWeaponId); } }
        public static int WeaponCatalogIndex(string profile){return R23WeaponPickupRules.CatalogIndex(profile);}
        public static string WeaponDisplayName(string id) { var melee=R24MeleeProfiles.ForId(id);if(melee!=null)return melee.Name;if(id=="smaw")return "SMAW";if(id=="m1014")return "M1014";if(id=="magnum")return R23MagnumRuntime.DisplayName;var definition=SourceRifleDefinition.ForId(id);return definition==null?id:definition.DisplayName; }
        public static int WeaponCapacity(string id) { if(id=="smaw")return 1;if(id=="m1014")return 6;if(id=="magnum")return R23MagnumRuntime.Capacity;var definition=SourceRifleDefinition.ForId(id);return definition==null?0:definition.Capacity; }
        public int ReadReserve(string id) { return round==null?0:round.ReadReserve(id); }
        public static SourceRifleDefinition WeaponDefinition(string id) { return SourceRifleDefinition.ForId(id); }
        public static string WeaponAmmoFamily(string id){if(R24MeleeProfiles.ForId(id)!=null)return "melee";if(id=="smaw")return "rockets";if(id=="m1014")return "shells";if(id=="magnum")return R23MagnumRuntime.AmmoFamily;var definition=WeaponDefinition(id);return definition==null?"":definition.AmmoFamily;}
        Vector3 GripPoint(int index){return Vector3.zero;}
        bool IsShotgun {get{return ActiveWeaponId=="m1014";}}
        bool IsMagnum {get{return ActiveWeaponId=="magnum";}}
        float ActiveRange {get{return IsMelee?2.5f:IsSmaw?180:IsShotgun?40:IsMagnum?R23MagnumRuntime.MaximumRange:WeaponDefinition(ActiveWeaponId).MaximumRange;}}
        float ActiveBodyDamage {get{R23WeaponInstanceInfo info;return round!=null&&round.TryReadWeaponInstance(activeActionId,out info)?info.BodyDamage:IsShotgun?77:IsMagnum?R23MagnumRuntime.BodyDamage:WeaponDefinition(ActiveWeaponId).BodyDamage;}}
        bool TransientWeaponArt(int index,Transform item){return BladeTransient(index,item)||index==3&&magnumView!=null&&magnumView.IsTransient(item)||index==6&&m1014View!=null&&m1014View.IsTransient(item)||index==8&&(p90InsertArrow!=null&&(item==p90InsertArrow||item.IsChildOf(p90InsertArrow))||p90ChargeArrow!=null&&(item==p90ChargeArrow||item.IsChildOf(p90ChargeArrow)));}
        public bool ScopeActive { get { return sprScope!=null&&sprScope.Active; } }
#if UNITY_EDITOR
        internal R23SprScope SprScopeFixture { get { return sprScope; } }
        internal bool FixturePaused { get { return gameplayPaused; } }
        internal bool FixtureFocused { get { return hadFocus; } }
#endif
        public void SetSfxVolume(float value) { if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentOutOfRangeException("value");sfxVolume=Mathf.Clamp01(value);if(audioCues!=null)audioCues.SetSfxVolume(sfxVolume);if(smawView!=null)smawView.SetSfxVolume(sfxVolume);if(knifeAudio!=null)knifeAudio.SetVolume(sfxVolume);if(bladeAudio!=null)bladeAudio.SetVolume(sfxVolume); }
        public string[] AvailableWeaponIds { get { return (string[])weaponIds.Clone(); } }
        public string[] OwnedWeaponIds {get{return OwnedWeaponActionIds;}}
        public bool IsHolstered { get { return holstered; } }
        public bool OwnsWeapon(string id) { return id!=null&&ownedWeapons.Contains(id); }
        public HandlingSnapshot ReadWeaponState(string id) { if(round==null)throw new InvalidOperationException("Hands/weapons not initialized");return round.ReadWeapon(id); }
        public bool TryGetWeaponBounds(string id,out Bounds localToPickupRoot) {
            localToPickupRoot=new Bounds();int index=Array.IndexOf(weaponIds,ResolveWeaponProfile(id));if(!Ready||index<0)return false;bool found=false;
            foreach(var filter in weaponModels[index].GetComponentsInChildren<MeshFilter>(true)) {
                if(filter.sharedMesh==null||filter.name.StartsWith("R23 feedback")||TransientWeaponArt(index,filter.transform))continue;var bounds=filter.sharedMesh.bounds;var pose=Matrix4x4.Scale(Vector3.one*modelScales[index])*weaponModels[index].worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int corner=0;corner<8;corner++){var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));p=pose.MultiplyPoint3x4(p);if(!found){localToPickupRoot=new Bounds(p,Vector3.zero);found=true;}else localToPickupRoot.Encapsulate(p);}
            }
            return found;
        }
        public bool TryGetDroppedPose(string id,out Vector3 position,out Quaternion rotation) { WorldWeapon drop;if(id!=null&&droppedWeapons.TryGetValue(id,out drop)){position=drop.Position;rotation=drop.Rotation;return true;}position=Vector3.zero;rotation=Quaternion.identity;return false; }
        public string MechanicsDescription { get { return "First rarity: MK18 18-round burst / AKM 20-round automatic / SPR 4-round semi; source rifle/sniper pools / MAGNUM 7-round semi / CX4 25-round auto / PX4 16-round semi; shared finite pistol pool"; } }
        public void SetGameplayPaused(bool value){if(value&&!gameplayPaused&&round!=null){round.SuspendActiveWeapon();sprTrigger.Suspend();m1014Controls.Reset();magazineControls.Reset();}gameplayPaused=value;if(value)pickupRearmRequired[0]=pickupRearmRequired[1]=true;if(impactMarks!=null)impactMarks.SetPaused(value||!hadFocus);if(value&&audioCues!=null)audioCues.Stop();UpdateWeaponPresentation();}
        public void NotifyFocus(bool value){hadFocus=value;if(!value)pickupRearmRequired[0]=pickupRearmRequired[1]=true;if(impactMarks!=null)impactMarks.SetPaused(gameplayPaused||!value);if(!value&&round!=null){round.SuspendActiveWeapon();sprTrigger.Suspend();m1014Controls.Reset();magazineControls.Reset();}if(!value&&audioCues!=null)audioCues.Stop();UpdateWeaponPresentation();}
        // A lobby inspection owns a separate weapon; retain carried inventory without drawing it.
        public void SetWeaponPresentationSuppressed(bool value){if(value&&!presentationSuppressed&&round!=null){round.SuspendActiveWeapon();sprTrigger.Suspend();m1014Controls.Reset();magazineControls.Reset();}presentationSuppressed=value;UpdateWeaponPresentation();if(value&&tracer!=null)tracer.enabled=false;}
        float flashUntil,muzzleFlashUntil;
        readonly List<LineRenderer> pelletTracers=new List<LineRenderer>();
        public int LastShotgunPelletCount {get;private set;}public int LastShotgunTargetGroups {get;private set;}
        public int ShotgunTracerCount {get{int count=0;if(tracer!=null&&tracer.enabled)count++;foreach(var line in pelletTracers)if(line.enabled)count++;return IsShotgun?count:0;}}
        public bool TryReadShotgunTracer(int pellet,out Vector3 start,out Vector3 end){start=end=Vector3.zero;if(!IsShotgun||pellet<0||pellet>=8)return false;var line=pellet==0?tracer:pelletTracers[pellet-1];if(line==null||!line.enabled)return false;start=line.GetPosition(0);end=line.GetPosition(1);return true;}
        LineRenderer tracer;
        public bool Ready { get; private set; }
        public Vector3 WeaponGripPosition { get { return weapon==null?Vector3.zero:weapon.TransformPoint(GripPoint(selectedWeapon)*modelScales[selectedWeapon]); } }
        public bool HasWeapon { get; private set; }
        public float Health { get { return round==null?100:round.Health; } }
        public float Shield { get { return round==null?100:round.Shield; } }
        public float MaxHealth { get { return round==null?100:round.MaxHealth; } }
        public float MaxShield { get { return round==null?100:round.MaxShield; } }
        public int Hits { get { return round==null?0:round.Hits; } }
        public int Ammo { get { return round==null?0:round.ActiveLoaded; } }
        public int MagazineCapacity { get { return round==null?WeaponCapacity(ActiveWeaponId):round.ActiveCapacity; } }
        public ReloadPhase ReloadStage { get { return round==null?ReloadPhase.Ready:round.ActiveSnapshot.Phase; } }
        public bool SupportHandOccupied { get { return HasWeapon&&round!=null&&(IsShotgun?round.M1014.ReloadGripInProgress||round.ActiveSnapshot.Supported:round.ActiveSnapshot.ClaimsSupport); } }
        public string ReloadInstruction { get { if(IsMelee)return "";if(IsSmaw)return ReloadStage==ReloadPhase.Ready?"TRIGGER: LAUNCH ROCKET":ReloadStage==ReloadPhase.HeldMagazine?"PUSH ROCKET NOSE INTO LAUNCHER":"GRIP OFFERED ROCKET / PUSH INTO FRONT";if(IsShotgun)return ReloadStage==ReloadPhase.NeedsMagazine?"GRIP SIX-SHELL PACK / PUSH UP":ReloadStage==ReloadPhase.NeedsRack?"RELEASE / GRIP HANDLE / PULL BACK":"RIGHT STICK CLICK: EJECT";if(IsMagnum&&round!=null&&round.Magnum!=null)return round.Magnum.ReloadInstruction;var definition=WeaponDefinition(ActiveWeaponId);if(definition!=null&&definition.VrOpeningOnly){switch(ReloadStage){case ReloadPhase.NeedsMagazine:return definition.TopFeed?"GRIP TOP MAGAZINE / PUSH FORWARD":"GRIP FRESH MAGAZINE / PUSH UP";case ReloadPhase.HeldMagazine:return definition.TopFeed?"PUSH MAGAZINE FORWARD INTO TOP SEAT":"PUSH MAGAZINE INTO WELL";case ReloadPhase.NeedsRack:return definition.Pistol?"RELEASE GRIP / GRIP SLIDE / PULL BACK":"RELEASE GRIP / GRIP HANDLE / PULL BACK";default:return "RIGHT STICK CLICK: EJECT";}}string button="Right stick click";switch(ReloadStage){case ReloadPhase.NeedsMagazine:return "Grip fresh magazine below rifle; push upward. "+button+": insert.";case ReloadPhase.HeldMagazine:return "Push magazine into the well. "+button+": insert.";case ReloadPhase.NeedsRack:return "Release grip, then pull charging handle back. "+button+": charge.";default:return "";} } }
        public string WeaponInstanceId { get { return round==null?"":round.ActiveInstanceId; } }
        public Vector3 ReloadOfferPosition { get { return IsMelee?WeaponGripPosition:IsSmaw&&smawView!=null?smawView.ReloadOfferPosition:weapon==null?Vector3.zero:weapon.TransformPoint(From(IsShotgun?R23M1014Definition.Offer:IsMagnum?R23MagnumRuntime.Offer(primaryHand==0):SourceRifleDefinition.ForId(ActiveWeaponId).Offer)*LogicalReloadScale); } }
        public Vector3 ReloadInsertionPosition { get { return IsMelee?WeaponGripPosition:IsSmaw&&smawView!=null?smawView.ReloadInsertionPosition:weapon==null?Vector3.zero:weapon.TransformPoint(From(IsShotgun?R23M1014Definition.Socket:IsMagnum?R23MagnumRuntime.OpenPoint(primaryHand==0):SourceRifleDefinition.ForId(ActiveWeaponId).ReloadGuide)*LogicalReloadScale); } }
        public Vector3 ChargingGripPosition { get { return IsMelee?WeaponGripPosition:IsSmaw?ReloadInsertionPosition:weapon==null?Vector3.zero:weapon.TransformPoint(From(IsShotgun?new Float3(primaryHand==0?-.06f:.06f,.045f,.10f):IsMagnum?R23MagnumRuntime.OpenPoint(primaryHand==0):SourceRifleDefinition.ForId(ActiveWeaponId).ChargingPoint(primaryHand==0))*LogicalReloadScale); } }
        public int Reserve { get { return round==null?0:round.ActiveReserve; } }
        public int MagnumChamberMask {get{return round==null||round.Magnum==null?0:round.Magnum.ChamberMask;}}
        public int MagnumOfferedRounds {get{return round==null||round.Magnum==null?0:round.Magnum.OfferedRoundCount;}}
        public bool MagnumVisualReady {get{return magnumView!=null&&magnumView.Ready;}}
        public bool ReloadHandOccupied {get{return HasWeapon&&round!=null&&(IsSmaw?round.Smaw.ReloadHandOccupied:IsShotgun?round.M1014.ReloadGripInProgress:IsMagnum?round.Magnum.ReloadHandOccupied:ReloadStage==ReloadPhase.HeldMagazine||ReloadStage==ReloadPhase.NeedsRack&&round.ActiveSnapshot.ClaimsSupport);}}
        public bool Reloading { get { return round!=null&&round.ActiveSnapshot.Phase!=ReloadPhase.Ready; } }
        public event Action Restarted;
        public event Action<string,bool,float,float> TargetDamaged;
        // Emitted only by an accepted carried-gun shot at its nearest physical obstruction.
        // Gallery previews have a different owner and never publish world damage.
        public event Action<Collider,float> WorldHit;
        public HealthSnapshot ReadPracticeTarget(int index) { return round==null||index<0||index>=targets.Count?null:round.ReadTarget(targets[index].TargetId); }
        public Vector3 PracticeAimPoint(int index,bool headshot) { if(index<0||index>=targets.Count)throw new ArgumentOutOfRangeException("index");return targets[index].transform.TransformPoint(new Vector3(0,headshot?1.72f:1.14f,0)); }
        public void RepositionTrainingTargets(Vector3[] positions,Quaternion rotation) {
            if(positions==null||positions.Length!=targets.Count)throw new ArgumentException("One position per training target required");
            for(int i=0;i<positions.Length;i++){targets[i].transform.position=positions[i];targets[i].transform.rotation=rotation;}
        }
        public void SetPickupHome(Vector3 worldPosition,Quaternion rotation) {
            if(!To(worldPosition).IsFinite||!ValidRotation(rotation))throw new ArgumentException("Finite pickup pose required");
            pickupPosition=worldPosition;pickupRotation=rotation;
            if(weapon!=null&&!HasWeapon&&!holstered){weapon.position=worldPosition;weapon.rotation=rotation;}
            foreach(string id in starterWorldAtHome){WorldWeapon drop;if(droppedWeapons.TryGetValue(id,out drop)){drop.Position=worldPosition+rotation*starterWorldOffsets[id];drop.Rotation=rotation;if(drop.Visual!=null)drop.Visual.SetPositionAndRotation(drop.Position,drop.Rotation);}}
        }
        // Appearance owns supplied material lifetime. Null restores the source glove material.
        public void SetGloveMaterial(Material material) { ApplyGloveStyle(material,material); }
        // Absolute world-space contact displacement of free glove ART only. The root
        // supplies climb proxy minus raw hand; repeated identical calls never accumulate.
        public void SetFreeHandContactOffsets(Vector3 leftWorldDelta,Vector3 rightWorldDelta){if(!To(leftWorldDelta).IsFinite||!To(rightWorldDelta).IsFinite)throw new ArgumentException("Finite free-hand contact offsets required");leftContactDelta=leftWorldDelta;rightContactDelta=rightWorldDelta;ApplyFreeHandContactOffsets();}
        void ApplyFreeHandContactOffsets(){ApplyGloveArt(0,leftVisual,left,leftGloveHome,leftContactDelta,LeftHandOccupied);ApplyGloveArt(1,rightVisual,right,rightGloveHome,rightContactDelta,RightHandOccupied);}
        void ApplyGloveArt(int hand,Transform visual,Transform tracked,Vector3 home,Vector3 contactDelta,bool occupied){
            if(visual==null||tracked==null)return;
            Quaternion grip=R23TrackedGripPose.LocalRotation;
            if(HasWeapon&&!presentationSuppressed&&primaryHand==hand&&selectedWeapon<weaponModels.Count){
                var fit=R23WeaponGripFit.ForProfile(ActiveWeaponId);var model=weaponModels[selectedWeapon];
                visual.position=model.TransformPoint(From(fit.ModelContact(modelScales[selectedWeapon])));
                // Preserve the accepted glove mesh calibration relative to grip. Source
                // per-profile hand basis/contact follows actual visible model recoil.
                visual.rotation=model.rotation*Quaternion.AngleAxis(fit.PitchRadians*Mathf.Rad2Deg,Vector3.right)*Quaternion.Inverse(grip)*R23TrackedGripPose.GloveLocalRotation(hand);
            }else if(HasWeapon&&!presentationSuppressed&&IsMelee&&round.Melee!=null&&round.Melee.Supported&&hand!=primaryHand){var model=weaponModels[selectedWeapon];visual.position=model.TransformPoint(From(R24MeleeRuntime.SupportPoint));visual.rotation=model.rotation*Quaternion.AngleAxis(hand==0?90:-90,Vector3.forward)*Quaternion.Inverse(grip)*R23TrackedGripPose.GloveLocalRotation(hand);}else{visual.localPosition=home+(occupied?Vector3.zero:tracked.InverseTransformVector(contactDelta));visual.localRotation=R23TrackedGripPose.GloveLocalRotation(hand);}
        }
        public void ApplyGloveStyle(Material leftMaterial,Material rightMaterial) {
            leftGloveStyle=leftMaterial;rightGloveStyle=rightMaterial;
            ApplyGloveMaterial(leftVisual,leftMaterial);ApplyGloveMaterial(rightVisual,rightMaterial);
        }
        void ApplyGloveMaterial(Transform root,Material material) {
            if(root==null)return;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                Material original;if(!gloveDefaults.TryGetValue(renderer,out original)){original=renderer.sharedMaterial;gloveDefaults.Add(renderer,original);}
                renderer.sharedMaterial=material==null?original:material;
            }
        }
        public void Initialize(Transform playerRoot,Camera view,Transform leftHand,Transform rightHand) {
            Initialize(playerRoot,view,leftHand,rightHand,EnablePracticeTargets);
        }
        public void Initialize(Transform playerRoot,Camera view,Transform leftHand,Transform rightHand,bool trainingRange) {
            if(initialized)throw new InvalidOperationException("R23HandsWeapons already initialized");
            if(playerRoot==null||view==null||leftHand==null||rightHand==null)throw new ArgumentException("Root, head and two tracked transforms required");
            initialized=true;EnablePracticeTargets=trainingRange;player=playerRoot;head=view;left=leftHand;right=rightHand;
            content=new GameObject("R23 hands-weapons content").transform;// World content stays independent of the tracked rig.
            leftVisual=new GameObject("R23 left glove").transform;leftVisual.SetParent(left,false);
            rightVisual=new GameObject("R23 right glove").transform;rightVisual.SetParent(right,false);
            leftGloveHome=leftVisual.localPosition;rightGloveHome=rightVisual.localPosition;
            // Align original glove bone axes to the already accepted gun bore/up.
            // Only the render children rotate; runtime tracked poses stay untouched.
            leftVisual.localRotation=R23TrackedGripPose.GloveLocalRotation(0);
            rightVisual.localRotation=R23TrackedGripPose.GloveLocalRotation(1);
            weapon=new GameObject("R23 MK18 pickup").transform;weapon.SetParent(content,false);
            // R23 _build_model creates unit scale, then model_scale_084 applies .92/.945.
            // The .6 text in an older mesh provenance is stale; it is not current runtime code.
            for(int i=0;i<weaponIds.Length;i++) {
                var model=new GameObject("R23 source "+weaponIds[i]).transform;model.SetParent(weapon,false);model.localScale=Vector3.one*modelScales[i];model.gameObject.SetActive(i==0);weaponModels.Add(model);
            }
            muzzle=new GameObject("Actual source muzzle").transform;muzzle.SetParent(weapon,false);
            var forward=Vector3.ProjectOnPlane(head.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=player.forward;
            pickupPosition=head.transform.position+forward*.50f+head.transform.right*.20f-Vector3.up*.42f;
            pickupRotation=Quaternion.LookRotation(forward,Vector3.up);
            for(int i=0;i<(EnablePracticeTargets?3:0);i++) {
                var go=new GameObject("R23 source PRACTICE TARGET "+(i+1));go.transform.SetParent(content,false);var position=player.position+forward*(5+i*3)+head.transform.right*((i-1)*1.4f);RaycastHit floor;
                if(Nearest(position+Vector3.up,Vector3.down,4,out floor))position.y=floor.point.y;
                go.transform.position=position;go.transform.rotation=Quaternion.LookRotation(forward,Vector3.up);
                var target=go.AddComponent<R23PracticeTarget>();target.Initialize("target/"+i,head);targets.Add(target);
            }
            var ids=new string[targets.Count];for(int i=0;i<ids.Length;i++)ids[i]="target/"+i;
            round=new R23RoundState(ids,this,true);round.SetGripToggle(requestedGripToggle);
            var line=new GameObject("R23 shot trace");line.transform.SetParent(content,false);tracer=line.AddComponent<LineRenderer>();tracer.positionCount=2;tracer.startWidth=.006f;tracer.endWidth=.002f;tracer.useWorldSpace=true;
            var traceMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));traceMaterial.SetColor("_BaseColor",Color.yellow);targetMaterials.Add(traceMaterial);tracer.sharedMaterial=traceMaterial;tracer.enabled=false;
            for(int i=1;i<8;i++){var go=new GameObject("R23 accepted pellet trace "+i);go.transform.SetParent(content,false);var extra=go.AddComponent<LineRenderer>();extra.positionCount=2;extra.startWidth=.006f;extra.endWidth=.002f;extra.useWorldSpace=true;extra.sharedMaterial=traceMaterial;extra.enabled=false;pelletTracers.Add(extra);}
            impactMarks=new R23ImpactMarks(content,ImpactActorOrIgnored);impactMarks.SetPaused(gameplayPaused||!hadFocus);
            visuals=new R23Visuals();ResetRound();Application.onBeforeRender+=RefreshLateTrackedPresentation;StartCoroutine(LoadVisuals());
        }
        IEnumerator LoadVisuals() {
            yield return visuals.Load("left",leftVisual);yield return visuals.Load("right",rightVisual);
            ApplyGloveStyle(leftGloveStyle,rightGloveStyle);
            for(int i=0;i<weaponIds.Length;i++)if(WeaponDefinition(weaponIds[i])!=null)yield return visuals.Load(weaponIds[i],weaponModels[i]);
            magnumView=gameObject.AddComponent<R23MagnumView>();magnumView.InitializeModel(weaponModels[3]);
            while(magnumView.Loading)yield return null;
            if(!magnumView.Ready)throw new InvalidOperationException("Source Magnum art unavailable: "+magnumView.Failure);
            feedbackRearSights[3]=new GameObject("Source Magnum rear sight reference").transform;feedbackRearSights[3].SetParent(weaponModels[3],false);feedbackRearSights[3].localPosition=new Vector3(0,.104f,.025f);
            feedbackFrontSights[3]=new GameObject("Source Magnum front sight reference").transform;feedbackFrontSights[3].SetParent(weaponModels[3],false);feedbackFrontSights[3].localPosition=new Vector3(0,.103707717f,.365f);
            m1014View=gameObject.AddComponent<R23M1014View>();m1014View.InitializeModel(weaponModels[6]);
            while(!m1014View.Ready&&m1014View.Failure==null)yield return null;if(!m1014View.Ready)throw new InvalidOperationException("Source M1014 art unavailable: "+m1014View.Failure);
            var shotgunOptic=weaponModels[6].gameObject.AddComponent<R23ReflexOptic>();shotgunOptic.Initialize(1,m1014View.Muzzle,.139f);
            var scopeRoot=new GameObject("Accepted Gemini SPR scope").transform;scopeRoot.SetParent(weaponModels[2],false);yield return visuals.Load("spr-scope",scopeRoot);
            sprScope=scopeRoot.gameObject.AddComponent<R23SprScope>();sprScope.Initialize(head,muzzle,content,ScopePathClear,delegate{return displayedAimPoint;});
            cx4Optic=weaponModels[4].gameObject.AddComponent<R23ReflexOptic>();cx4Optic.InitializeCx4Rings(muzzle);
            feedbackRearSights[4]=cx4Optic.RearReference;feedbackFrontSights[4]=cx4Optic.FrontReference;
            feedbackRearSights[5]=new GameObject("Source PX4 rear sight reference").transform;feedbackRearSights[5].SetParent(weaponModels[5],false);feedbackRearSights[5].localPosition=new Vector3(0,.083f,.015f);
            feedbackFrontSights[5]=new GameObject("Source PX4 front sight reference").transform;feedbackFrontSights[5].SetParent(weaponModels[5],false);feedbackFrontSights[5].localPosition=new Vector3(0,.083f+(.045f-.083f)*(.245f-.015f)/(35.29f-.015f),.245f);
            feedbackRearSights[7]=new GameObject("Source ump rear reference").transform;feedbackRearSights[7].SetParent(weaponModels[7],false);feedbackRearSights[7].localPosition=new Vector3(0f,0.14f,-0.03f);
            feedbackFrontSights[7]=new GameObject("Source ump front reference").transform;feedbackFrontSights[7].SetParent(weaponModels[7],false);feedbackFrontSights[7].localPosition=new Vector3(0f,0.1388528404f,0.393f);
            feedbackRearSights[8]=new GameObject("Source p90 rear reference").transform;feedbackRearSights[8].SetParent(weaponModels[8],false);feedbackRearSights[8].localPosition=new Vector3(0f,0.16f,0.025f);
            feedbackFrontSights[8]=new GameObject("Source p90 front reference").transform;feedbackFrontSights[8].SetParent(weaponModels[8],false);feedbackFrontSights[8].localPosition=new Vector3(0f,0.1589127981f,0.36f);
            weaponModels[9].gameObject.AddComponent<R23ReflexOptic>().Initialize(modelScales[9],muzzle,0.1562321866f,.75f);
            weaponModels[10].gameObject.AddComponent<R23ReflexOptic>().Initialize(modelScales[10],muzzle,0.1180628157f,.75f);
            p90InsertArrow=new GameObject("Source P90 FORWARD reload cue").transform;p90InsertArrow.SetParent(weaponModels[8],false);p90InsertArrow.gameObject.SetActive(false);yield return visuals.Load("p90-insert-arrow",p90InsertArrow);
            p90ChargeArrow=new GameObject("Source P90 BACK reload cue").transform;p90ChargeArrow.SetParent(weaponModels[8],false);p90ChargeArrow.gameObject.SetActive(false);yield return visuals.Load("p90-charge-arrow",p90ChargeArrow);
            foreach(var cue in new[]{p90InsertArrow,p90ChargeArrow})foreach(var renderer in cue.GetComponentsInChildren<Renderer>(true)){renderer.sharedMaterial.SetColor("_EmissionColor",new Color(.8980392f,.6745098f,.1333333f)*.45f);renderer.sharedMaterial.EnableKeyword("_EMISSION");}
            var optics11=weaponModels[11].gameObject.AddComponent<SourceCatalog5Optics>();optics11.Initialize("1911",modelScales[11],muzzle);
            feedbackRearSights[11]=new GameObject("Source 1911 rear reference").transform;feedbackRearSights[11].SetParent(weaponModels[11],false);feedbackRearSights[11].localPosition=new Vector3(0f,0.11f,0.015f);
            feedbackFrontSights[11]=new GameObject("Source 1911 front reference").transform;feedbackFrontSights[11].SetParent(weaponModels[11],false);feedbackFrontSights[11].localPosition=new Vector3(0f,0.1097978981f,0.265f);
            var optics12=weaponModels[12].gameObject.AddComponent<SourceCatalog5Optics>();optics12.Initialize("tec9",modelScales[12],muzzle);
            feedbackRearSights[12]=new GameObject("Source tec9 rear reference").transform;feedbackRearSights[12].SetParent(weaponModels[12],false);feedbackRearSights[12].localPosition=new Vector3(0f,0.11f,0.015f);
            feedbackFrontSights[12]=new GameObject("Source tec9 front reference").transform;feedbackFrontSights[12].SetParent(weaponModels[12],false);feedbackFrontSights[12].localPosition=new Vector3(0f,0.1097978981f,0.265f);
            var optics13=weaponModels[13].gameObject.AddComponent<SourceCatalog5Optics>();optics13.Initialize("rfb",modelScales[13],muzzle);
            var optics14=weaponModels[14].gameObject.AddComponent<SourceCatalog5Optics>();optics14.Initialize("fal",modelScales[14],muzzle);
            feedbackRearSights[14]=new GameObject("Source fal rear reference").transform;feedbackRearSights[14].SetParent(weaponModels[14],false);feedbackRearSights[14].localPosition=new Vector3(0f,0.11f,0.015f);
            feedbackFrontSights[14]=new GameObject("Source fal front reference").transform;feedbackFrontSights[14].SetParent(weaponModels[14],false);feedbackFrontSights[14].localPosition=new Vector3(0f,0.109525048f,0.605f);
            var optics15=weaponModels[15].gameObject.AddComponent<SourceCatalog5Optics>();optics15.Initialize("s85",modelScales[15],muzzle);
            feedbackRearSights[15]=optics15.RearReference;feedbackFrontSights[15]=optics15.FrontReference;
            mp9Laser=new R23Mp9Laser(content);
            BuildMagazinePresentation();
            var reflex=weaponModels[0].gameObject.AddComponent<R23ReflexOptic>();reflex.Initialize(modelScales[0],muzzle);
            weaponModels[1].gameObject.AddComponent<R23ReflexOptic>().InitializeAkRings();
            for(int i=0;i<weaponModels.Count;i++)foreach(var renderer in weaponModels[i].GetComponentsInChildren<Renderer>(true)){if(renderer.name=="Clear reflex window and stereo bore dot")feedbackLenses[i]=renderer.transform;else if(renderer.name=="Source rear luminous alignment ring")feedbackRearSights[i]=renderer.transform;else if(renderer.name=="Source solid red front bead")feedbackFrontSights[i]=renderer.transform;}
            gunFeedback=new R23GunFeedback(content,muzzle);
            audioCues=new R23Audio(weapon,muzzle);audioCues.SetSfxVolume(sfxVolume);yield return audioCues.Load();
            yield return LoadSmawPresentation();yield return LoadKnifePresentation();yield return LoadBladePresentation();visuals.ReleaseLoadedBytes();sourceArtReady=true;CreateWorldStarterPickups();ApplyWeaponSelection();Ready=true;UpdateWeaponPresentation();
        }
        void BuildMagazinePresentation() {
            for(int i=0;i<weaponIds.Length;i++) {
                if(WeaponDefinition(weaponIds[i])==null)continue;
                var offered=new GameObject("Source offered magazine "+weaponIds[i]).transform;offered.SetParent(weapon,false);offered.localScale=Vector3.one*modelScales[i];offered.localPosition=From(SourceRifleDefinition.ForId(weaponIds[i]).Offer)*SourceRifleDefinition.ForId(weaponIds[i]).ReloadScale;offeredMagazines[i]=offered;
                var held=new GameObject("Source support-hand magazine "+weaponIds[i]).transform;held.SetParent(SupportTransform,false);held.localScale=Vector3.one*modelScales[i];heldMagazines[i]=held;
                foreach(var renderer in weaponModels[i].GetComponentsInChildren<MeshRenderer>(true)) {
                    if(renderer.name.StartsWith("Bolt/")||renderer.name.StartsWith("Slide/")){boltParts[i].Add(renderer.transform);boltRestPositions.Add(renderer.transform,renderer.transform.localPosition);}
                    if(!renderer.name.StartsWith("Magazine/"))continue;magazineParts[i].Add(renderer);
                    foreach(var parent in new[]{offered,held}) {
                        var go=new GameObject("Source magazine mesh");go.layer=18;go.transform.SetParent(parent,false);go.transform.localPosition=-magazineHomes[i];go.AddComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;go.AddComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterial;
                    }
                }
                if(boltParts[i].Count==0) {
                    // download_mk18 has no moving partition, so weapon.gd retains this authored handle.
                    var handle=GameObject.CreatePrimitive(PrimitiveType.Cube);handle.name="Source authored charging handle";handle.layer=18;handle.transform.SetParent(weaponModels[i],false);handle.transform.localPosition=From(SourceRifleDefinition.ForId(weaponIds[i]).Charging);handle.transform.localScale=new Vector3(.032f,.012f,.022f);Destroy(handle.GetComponent<Collider>());
                    var steel=new Material(Shader.Find("Universal Render Pipeline/Lit"));steel.SetColor("_BaseColor",new Color(.29f,.32f,.33f));steel.SetFloat("_Metallic",.65f);steel.SetFloat("_Smoothness",.64f);targetMaterials.Add(steel);handle.GetComponent<Renderer>().sharedMaterial=steel;boltParts[i].Add(handle.transform);boltRestPositions.Add(handle.transform,handle.transform.localPosition);
                }
                offered.gameObject.SetActive(false);held.gameObject.SetActive(false);
            }
        }
        void UpdateWeaponPresentation() {
            SyncBladePresentation();
            if(round==null||content==null||weapon==null||heldMagazines.Length!=weaponModels.Count)return;
            if(weapon!=null)weapon.gameObject.SetActive(!presentationSuppressed&&!holstered);
            for(int i=0;i<weaponIds.Length;i++) {
                if(WeaponDefinition(weaponIds[i])==null)continue;
                var state=round.ReadWeapon(i==selectedWeapon?activeActionId:weaponIds[i]);bool selected=i==selectedWeapon&&HasWeapon&&!presentationSuppressed&&!gameplayPaused&&hadFocus&&isActiveAndEnabled;
                foreach(var renderer in magazineParts[i])if(renderer!=null)renderer.enabled=weaponIds[i]!="s85"&&(state.Phase==ReloadPhase.Ready||state.Phase==ReloadPhase.NeedsRack);
                foreach(var part in boltParts[i])if(part!=null)part.localPosition=boltRestPositions[part]+new Vector3(weaponIds[i]=="p90"&&primaryHand==0?.1f:0,0,-state.BoltTravel-((weaponIds[i]=="px4"||weaponIds[i]=="1911")&&state.Phase==ReloadPhase.Ready?(i==selectedWeapon?ActiveFeedback:feedbackStates[i]).SlideTravel:0));
                if(offeredMagazines[i]!=null)offeredMagazines[i].gameObject.SetActive(selected&&state.Phase==ReloadPhase.NeedsMagazine&&round.ReadReserve(i==selectedWeapon?activeActionId:weaponIds[i])>0);
                if((weaponIds[i]=="p90"||weaponIds[i]=="s85")&&heldMagazines[i]!=null)heldMagazines[i].rotation=weapon.rotation;
                if(weaponIds[i]=="ump")foreach(var renderer in weaponModels[i].GetComponentsInChildren<MeshRenderer>(true))if(renderer.name.StartsWith("SourceCarrier/"))renderer.transform.localPosition=new Vector3(0,0,-(i==selectedWeapon?ActiveFeedback:feedbackStates[i]).CarrierTravel);
                if(heldMagazines[i]!=null)heldMagazines[i].gameObject.SetActive(selected&&state.Phase==ReloadPhase.HeldMagazine&&(!haveTrackedControls||trackedControls.SupportTracked&&!trackedControls.SupportReserved));
            }
            if(p90InsertArrow!=null){var definition=SourceRifleDefinition.P90;var state=round.ReadWeapon(ActiveWeaponId=="p90"?activeActionId:"p90");bool allowed=ActiveWeaponId=="p90"&&FeedbackAllowed;p90InsertArrow.gameObject.SetActive(allowed&&(state.Phase==ReloadPhase.NeedsMagazine||state.Phase==ReloadPhase.HeldMagazine));p90ChargeArrow.gameObject.SetActive(allowed&&state.Phase==ReloadPhase.NeedsRack);p90InsertArrow.localPosition=From(state.Phase==ReloadPhase.NeedsMagazine?definition.Offer:definition.Insert)+Vector3.up*.04f/definition.ModelScale;p90InsertArrow.localRotation=Quaternion.FromToRotation(Vector3.up,Vector3.forward);p90ChargeArrow.localPosition=From(definition.ChargingPoint(primaryHand==0))+Vector3.up*.04f/definition.ModelScale;}
            PresentShotgun(0);
            if(magnumView!=null)magnumView.Present(round.Magnum,selectedWeapon==3,FeedbackAllowed,primaryHand,SupportTransform);
            if(sprScope!=null)sprScope.SetAllowed(selectedWeapon==2&&HasWeapon&&!presentationSuppressed&&!gameplayPaused&&hadFocus&&isActiveAndEnabled&&(!haveTrackedControls||trackedControls.PrimaryTracked&&!trackedControls.PrimaryClimbing));
            if(IsMelee){SyncKnifePresentation();return;}if(IsSmaw){SyncSmawPresentation();return;}if(smawView!=null)smawView.Holster();if(!FeedbackAllowed)ClearFeedbackPresentation();else {RenderDisplayedPose();if(gunFeedback!=null)gunFeedback.PresentAmmo(weaponModels[selectedWeapon],modelScales[selectedWeapon],Ammo,Reserve,primaryHand==0,true);}
        }
        void PresentShotgun(float seconds){if(m1014View==null||round==null)return;var snapshot=round.ReadWeapon(IsShotgun?activeActionId:"m1014");var feedback=IsShotgun?ActiveFeedback:feedbackStates[6];snapshot.RecoilDistance=feedback.Distance;snapshot.RecoilPitch=feedback.PitchRadians;snapshot.RecoilYaw=feedback.YawRadians;m1014View.Present(snapshot,round.ReadReserve(IsShotgun?activeActionId:"m1014"),primaryHand==0,selectedWeapon==6&&!holstered&&!presentationSuppressed,seconds);if(!FeedbackAllowed)m1014View.Suspend();}
        // Selects persistent source loadout instances. Switching never grants ammunition.
        public bool SelectWeapon(string id) {
            if(!Ready||gameplayPaused||!hadFocus||Health<=0)return false;
            int index=Array.IndexOf(weaponIds,ResolveWeaponProfile(id));if(index<0||!ownedWeapons.Contains(id))return false;
            if(id!=activeActionId){if(!round.SelectWeapon(id))return false;ClearFeedbackPresentation();if(audioCues!=null)audioCues.Stop();sprTrigger.Suspend();m1014Controls.Reset();magazineControls.Reset();activeActionId=id;selectedWeapon=index;if(IsShotgun&&m1014View!=null)m1014View.SynchronizeInstance(round.ActiveSnapshot);ApplyWeaponSelection();UpdateWeaponPresentation();}
            return holstered?EquipOwned(id,primaryHand):true;
        }
        public bool SelectNextWeapon(){var ids=OwnedWeaponActionIds;if(ids.Length==0)return false;int current=Array.IndexOf(ids,activeActionId);return SelectWeapon(ids[(current+1)%ids.Length]);}
        // These are custody/presentation changes over persistent instances, never new grants.
        public bool HolsterCurrent() {
            if(!Ready||!HasWeapon)return false;round.SuspendActiveWeapon();sprTrigger.Suspend();m1014Controls.Reset();magazineControls.Reset();if(audioCues!=null)audioCues.Stop();HasWeapon=false;holstered=true;starterPickup=false;weapon.SetParent(content,true);weapon.gameObject.SetActive(false);if(tracer!=null)tracer.enabled=false;UpdateWeaponPresentation();return true;
        }
        bool HandAvailable(int hand) { return hand>=0&&hand<=1&&(!haveInputSnapshot||lastInput.Hand(hand).Tracked&&!lastInput.Hand(hand).Reserved); }
        void AttachWeapon(int hand) {
            primaryHand=hand;HasWeapon=true;holstered=false;starterPickup=false;weapon.gameObject.SetActive(true);weapon.SetParent(PrimaryTransform,false);weapon.localRotation=R23TrackedGripPose.LocalRotation;weapon.localScale=Vector3.one;
            foreach(var held in heldMagazines)if(held!=null){held.SetParent(SupportTransform,false);held.localPosition=Vector3.zero;held.localRotation=R23TrackedGripPose.LocalRotation;}
            if(haveInputSnapshot){RefreshTrackedControls(lastInput);previousReload=lastInput.ReloadRequested||lastInput.Right.StickClick;}
            ApplyWeaponSelection();UpdateWeaponPresentation();
        }
        public bool EquipOwned(string id) { return EquipOwned(id,1); }
        public bool EquipOwned(string id,int hand) {
            int index=Array.IndexOf(weaponIds,ResolveWeaponProfile(id));
            if(!Ready||index<0||!ownedWeapons.Contains(id)||gameplayPaused||!hadFocus||!HandAvailable(hand)||Health<=0)return false;
            if(!round.SelectWeapon(id))return false;round.SuspendActiveWeapon();sprTrigger.Suspend();m1014Controls.Reset();magazineControls.Reset();activeActionId=id;selectedWeapon=index;if(IsShotgun&&m1014View!=null)m1014View.SynchronizeInstance(round.ActiveSnapshot);AttachWeapon(hand);return true;
        }
        public bool DropCurrent(Vector3 position,Quaternion rotation) { return HasWeapon&&DropOwned(activeActionId,position,rotation); }
        public bool DropOwned(string id,Vector3 position,Quaternion rotation) {
            int index=Array.IndexOf(weaponIds,ResolveWeaponProfile(id));
            if(!Ready||index<0||!ownedWeapons.Contains(id)||!To(position).IsFinite||!ValidRotation(rotation))return false;
            if(!round.SuspendWeapon(id))return false;ClearFeedbackPresentation();
            var record=new WorldWeapon{Position=position,Rotation=rotation,Visible=true};record.Visual=CreateWorldWeaponVisual(id,index,position,rotation);droppedWeapons.Add(id,record);ownedWeapons.Remove(id);ownedWeaponOrder.Remove(id);
            if(id==activeActionId){if(HasWeapon)HolsterCurrent();else{holstered=true;starterPickup=false;weapon.gameObject.SetActive(false);}pickupRearmRequired[primaryHand]=true;}
            UpdateWeaponPresentation();return true;
        }
        // Spawning creates one real source lifetime; visibility never grants a second magazine.
        public bool SpawnWorldWeapon(string profile,int stars,Vector3 position,Quaternion rotation,out string actionId,bool visible=true){
            actionId=null;int index=Array.IndexOf(weaponIds,profile);if(!Ready||index<0||!To(position).IsFinite||!ValidRotation(rotation))return false;
            string created;if(!round.CreateWeaponInstance(profile,stars,out created))return false;
            var record=new WorldWeapon{Position=position,Rotation=rotation,Visible=visible,CenterSet=!visible};droppedWeapons.Add(created,record);if(visible)record.Visual=CreateWorldWeaponVisual(created,index,position,rotation);actionId=created;UpdateWeaponPresentation();return true;
        }
        public bool RegisterWorldWeapon(string actionId,string profile,int stars,Vector3 position,Quaternion rotation,bool visible=true){int index=Array.IndexOf(weaponIds,profile);if(!Ready||index<0||!To(position).IsFinite||!ValidRotation(rotation)||!round.RegisterWeaponInstance(actionId,profile,stars))return false;var record=new WorldWeapon{Position=position,Rotation=rotation,Visible=visible,CenterSet=!visible};droppedWeapons.Add(actionId,record);if(visible)record.Visual=CreateWorldWeaponVisual(actionId,index,position,rotation);UpdateWeaponPresentation();return true;}
        public bool SetWorldWeaponVisible(string actionId,bool visible){WorldWeapon record;if(!Ready||actionId==null||!droppedWeapons.TryGetValue(actionId,out record))return false;if(record.Visible==visible)return true;record.Visible=visible;if(visible)record.Visual=CreateWorldWeaponVisual(actionId,Array.IndexOf(weaponIds,ResolveWeaponProfile(actionId)),record.Position,record.Rotation);else if(record.Visual!=null){WorldPickupCenter(record);record.Visual.gameObject.SetActive(false);Destroy(record.Visual.gameObject);record.Visual=null;}UpdateWeaponPresentation();return true;}
        public bool DespawnWorldWeapon(string actionId){
            WorldWeapon record;if(!Ready||actionId==null||ownedWeapons.Contains(actionId)||!droppedWeapons.TryGetValue(actionId,out record))return false;
            if(actionId==activeActionId){string fallback=null;foreach(string id in OwnedWeaponActionIds)if(id!=actionId){fallback=id;break;}if(fallback==null)foreach(string id in round.WeaponActionIds)if(id!=actionId&&Array.IndexOf(weaponIds,ResolveWeaponProfile(id))>=0){fallback=id;break;}if(fallback==null||!round.SelectWeapon(fallback))return false;activeActionId=fallback;selectedWeapon=Array.IndexOf(weaponIds,ResolveWeaponProfile(fallback));HasWeapon=false;holstered=true;starterPickup=false;ApplyWeaponSelection();}
            if(!round.RetireWeaponInstance(actionId))return false;if(record.Visual!=null){record.Visual.gameObject.SetActive(false);Destroy(record.Visual.gameObject);}droppedWeapons.Remove(actionId);starterWorldAtHome.Remove(actionId);instanceFeedback.Remove(actionId);UpdateWeaponPresentation();return true;
        }
        Transform CreateWorldWeaponVisual(string actionId,int index,Vector3 position,Quaternion rotation) {
            string profile=ResolveWeaponProfile(actionId);var state=round.ReadWeapon(actionId);
            if(profile=="magnum")magnumView.Present(round.ReadMagnumInstance(actionId),true,false,1,null);
            if(profile=="m1014"){m1014View.SynchronizeInstance(state);m1014View.Present(state,round.ReadReserve(actionId),false,true,0);}
            var drop=new GameObject("R23 dropped source instance "+round.WeaponInstanceId(actionId)).transform;drop.SetParent(content,false);drop.position=position;drop.rotation=rotation;
            foreach(var renderer in weaponModels[index].GetComponentsInChildren<MeshRenderer>(true)) {
                if(renderer.name.StartsWith("R23 feedback")||TransientWeaponArt(index,renderer.transform)||renderer.name=="Source live 2x SPR aperture")continue;
                bool magazine=magazineParts[index].Contains(renderer);if(magazine&&profile=="s85")continue;if(magazine&&state.Phase!=ReloadPhase.Ready&&state.Phase!=ReloadPhase.NeedsRack)continue;
                if(!magazine&&!renderer.enabled&&!(profile=="spr"&&renderer.name.StartsWith("RearOcularGlass/")))continue;
                var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null)continue;
                // Strip active-instance visual recoil. The actual copy retains only its own
                // mechanical part pose; a nearby held copy must not deform this ground copy.
                var pose=Matrix4x4.Scale(Vector3.one*modelScales[index])*weaponModels[index].worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                var visible=new GameObject("Source pickup "+renderer.name);visible.transform.SetParent(drop,false);visible.transform.localPosition=pose.GetColumn(3);visible.transform.localRotation=pose.rotation;visible.transform.localScale=pose.lossyScale;
                Vector3 home;if(boltRestPositions.TryGetValue(renderer.transform,out home))visible.transform.localPosition=(home-Vector3.forward*state.BoltTravel)*modelScales[index];
                visible.layer=18;visible.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;var copy=visible.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.shadowCastingMode=renderer.shadowCastingMode;copy.receiveShadows=renderer.receiveShadows;
            }
            R23WeaponInstanceInfo info;if(round.TryReadWeaponInstance(actionId,out info))ApplyGroundRarity(drop,info.Stars);
            if(profile=="m1014")m1014View.SynchronizeInstance(round.ReadWeapon(IsShotgun?activeActionId:"m1014"));
            return drop;
        }
        void CreateWorldStarterPickups(){
            if(!sourceArtReady||magnumView==null||!magnumView.Ready)return;
            foreach(var pair in starterWorldOffsets){string id=pair.Key;if(droppedWeapons.ContainsKey(id)||ownedWeapons.Contains(id))continue;int index=Array.IndexOf(weaponIds,id);var record=new WorldWeapon{Position=pickupPosition+pickupRotation*pair.Value,Rotation=pickupRotation,Visible=true};record.Visual=CreateWorldWeaponVisual(id,index,record.Position,record.Rotation);droppedWeapons.Add(id,record);starterWorldAtHome.Add(id);}
            magnumView.Present(round.Magnum,selectedWeapon==3,false,primaryHand,null);
        }
        void ApplyWeaponSelection() {
            for(int i=0;i<weaponModels.Count;i++)weaponModels[i].gameObject.SetActive(i==selectedWeapon&&weaponIds[i]!="smaw");
            weapon.name="R23 "+ActiveWeaponName+(HasWeapon?" held":" pickup");
            muzzle.localPosition=sourceMuzzles[selectedWeapon]*modelScales[selectedWeapon];muzzle.localRotation=Quaternion.identity;
            if(HasWeapon)weapon.localPosition=R23TrackedGripPose.GripCenteredOffset(GripPoint(selectedWeapon),modelScales[selectedWeapon]);
        }
        public void ResetRound() {
            if(!initialized)return;if(smawView!=null){smawView.Holster();smawView.ClearProjectiles();}
            foreach(var drop in droppedWeapons.Values)if(drop.Visual!=null){drop.Visual.gameObject.SetActive(false);Destroy(drop.Visual.gameObject);}droppedWeapons.Clear();ownedWeapons.Clear();ownedWeaponOrder.Clear();foreach(var id in StarterWeaponIds){ownedWeapons.Add(id);ownedWeaponOrder.Add(id);}starterWorldAtHome.Clear();holstered=false;starterPickup=true;pickupRearmRequired[0]=pickupRearmRequired[1]=haveInputSnapshot;primaryHand=1;weapon.gameObject.SetActive(true);
            lastAcceptedPoseValid=displayedPoseValid=false;LastAcceptedShotActionId=null;LastAcceptedNativeShot=0;ClearImpactMarks();round.Reset();round.EnsureMagnumState("r23-world/magnum");if(!ownedWeapons.Contains(activeActionId))selectedWeapon=Array.IndexOf(weaponIds,"cx4");activeActionId=ActiveWeaponId;round.SelectWeapon(activeActionId);HasWeapon=false;weapon.SetParent(content,true);weapon.position=pickupPosition;weapon.rotation=pickupRotation;weapon.localScale=Vector3.one;ApplyWeaponSelection();
            pickupIntentSampled=false;previousReload=previousRestart=previousSelection=previousTrigger=false;sprTrigger.Reset();m1014Controls.Reset();magazineControls.Reset();LastShotgunPelletCount=LastShotgunTargetGroups=0;if(tracer!=null)tracer.enabled=false;if(audioCues!=null)audioCues.Stop();
            leftContactDelta=rightContactDelta=Vector3.zero;ApplyFreeHandContactOffsets();
            foreach(var feedback in feedbackStates)feedback.Reset();instanceFeedback.Clear();ClearFeedbackPresentation();
            foreach(var target in targets)target.Refresh(round);
            CreateWorldStarterPickups();UpdateWeaponPresentation();
        }
        float PickupDistance(int hand) { string id;Transform drop;float distance;FindNearestPickup(hand,out id,out drop,out distance);return pickupRearmRequired[hand]||id!=null&&CanClaimWorldGrip!=null&&!CanClaimWorldGrip(id,hand)?float.PositiveInfinity:distance; }
        Vector3 PickupCenter(Transform prop){bool found=false;var bounds=new Bounds();foreach(var renderer in prop.GetComponentsInChildren<MeshRenderer>(false)){if(!renderer.enabled||renderer.name.StartsWith("R23 feedback"))continue;if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);}return found?bounds.center:prop.position;}
        Vector3 WorldPickupCenter(WorldWeapon record){if(!record.CenterSet){record.LocalCenter=record.Visual==null?Vector3.zero:Quaternion.Inverse(record.Rotation)*(PickupCenter(record.Visual)-record.Position);record.CenterSet=true;}return record.Position+record.Rotation*record.LocalCenter;}
        bool PickupLineClear(Vector3 from,Vector3 to){Vector3 delta=to-from;if(delta.sqrMagnitude<.000001f)return true;foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore))if(!ImpactActorOrIgnored(hit.collider))return false;return true;}
        public bool WorldPickupHandEligible(int hand){return Ready&&Health>0&&PickupHandAvailable(hand);}
        bool PickupHandAvailable(int hand){return HandAvailable(hand)&&!(HasWeapon&&hand==SupportHand&&SupportHandOccupied)&&!ReloadHandOccupied;}
        bool FindNearestPickup(int hand,out string selectedId,out Transform selectedDrop,out float distance) {
            selectedId=null;selectedDrop=null;distance=float.PositiveInfinity;if(!PickupHandAvailable(hand))return false;
            var tracked=HandTransform(hand);Vector3 position=tracked.position,forward=R23TrackedGripPose.Forward(tracked);if(!PickupLineClear(head.transform.position,position))return false;
            float best=float.NegativeInfinity;int bestIndex=int.MaxValue;
            foreach(string id in round.WeaponActionIds){
                WorldWeapon record;Transform prop;bool carriedStarter=starterPickup&&!holstered&&id==activeActionId;
                Vector3 point;if(carriedStarter){prop=weapon;if(!prop.gameObject.activeInHierarchy)continue;point=PickupCenter(prop);}else if(droppedWeapons.TryGetValue(id,out record)){prop=record.Visual;point=WorldPickupCenter(record);}else continue;
                Vector3 offset=point-position;float candidate=offset.magnitude,alignment=candidate>.0001f?Vector3.Dot(forward,offset/candidate):1,score;
                if(candidate>R23WeaponPickupRules.Reach||candidate>R23WeaponPickupRules.Proximity&&alignment<=R23WeaponPickupRules.AimDot)continue;
                if(!R23WeaponPickupRules.TryScore(candidate,alignment,true,PickupLineClear(position,point),out score))continue;int catalog=R23WeaponPickupRules.CatalogIndex(ResolveWeaponProfile(id));
                if(score>best||Mathf.Approximately(score,best)&&catalog<bestIndex){best=score;bestIndex=catalog;distance=candidate;selectedId=id;selectedDrop=carriedStarter?null:prop;}
            }
            return selectedId!=null;
        }
        // Read-only source target seam lets the root arbitrate the same grip against consumables.
        public bool TryReadWorldWeapon(string id,out string instanceId,out Vector3 center,out Vector3 position,out Quaternion rotation){
            instanceId=null;center=position=Vector3.zero;rotation=Quaternion.identity;if(!Ready||id==null)return false;WorldWeapon record;Transform prop;
            if(droppedWeapons.TryGetValue(id,out record)){position=record.Position;rotation=record.Rotation;center=WorldPickupCenter(record);instanceId=round.WeaponInstanceId(id);return true;}
            else{if(!starterPickup||holstered||HasWeapon||id!=activeActionId)return false;prop=weapon;}
            if(prop==null||!prop.gameObject.activeInHierarchy)return false;instanceId=round.WeaponInstanceId(id);center=PickupCenter(prop);position=prop.position;rotation=prop.rotation;return true;
        }
        public bool IsWorldWeaponVisible(string actionId){WorldWeapon record;return actionId!=null&&droppedWeapons.TryGetValue(actionId,out record)&&record.Visible&&record.Visual!=null;}
        public bool TryReadAimedWorldWeapon(int hand,out string id,out Vector3 center,out float score){id=null;center=Vector3.zero;score=float.NegativeInfinity;if(!Ready||gameplayPaused||!hadFocus||Health<=0||hand<0||hand>1)return false;Transform drop;float distance;if(!FindNearestPickup(hand,out id,out drop,out distance))return false;WorldWeapon record;center=droppedWeapons.TryGetValue(id,out record)?WorldPickupCenter(record):PickupCenter(weapon);Vector3 offset=center-HandTransform(hand).position;float alignment=distance>.0001f?Vector3.Dot(R23TrackedGripPose.Forward(HandTransform(hand)),offset/distance):1;return R23WeaponPickupRules.TryScore(distance,alignment,true,true,out score);}
        public bool TryPickup() { return TryPickup(1); }
        public bool TryPickup(int hand) {
            if(!Ready||gameplayPaused||!hadFocus||!PickupHandAvailable(hand)||pickupRearmRequired[hand]||Health<=0)return false;
            string id;Transform drop;float distance;if(!FindNearestPickup(hand,out id,out drop,out distance))return false;
            if(CanClaimWorldGrip!=null&&!CanClaimWorldGrip(id,hand))return false;
            WorldWeapon candidate;if(droppedWeapons.TryGetValue(id,out candidate)){
                // Root can first make_room by staging/dropping the selected full slot.
                // No capacity precheck may prevent that source replacement operation.
                if(MakeRoomForWorldWeapon!=null&&!MakeRoomForWorldWeapon(id,hand))return false;
                WorldWeapon current;if(!droppedWeapons.TryGetValue(id,out current)||!object.ReferenceEquals(current,candidate)||current.Visual!=drop||ownedWeapons.Contains(id)||!PickupHandAvailable(hand))return false;
                if(ownedWeapons.Count>=SlotCapacity||CanCollectWorldWeapon!=null&&!CanCollectWorldWeapon())return false;
                ownedWeapons.Add(id);if(!EquipOwned(id,hand)){ownedWeapons.Remove(id);return false;}ownedWeaponOrder.Add(id);droppedWeapons.Remove(id);starterWorldAtHome.Remove(id);if(drop!=null){drop.gameObject.SetActive(false);Destroy(drop.gameObject);}pickupRearmRequired[hand]=true;return true;
            }
            round.SuspendActiveWeapon();AttachWeapon(hand);pickupRearmRequired[hand]=true;return true;
        }
        // Smoke seams use exactly the same owners, proximity, raycasts and health mutations as XR input.
        public bool Fire(float deltaSeconds=.12f) { return TickControls(deltaSeconds,true,false); }
        // Explicit desktop/test shortcut: one call advances one real source stage.
        // XR stick click only opens and keeps the physical support/flick path.
        public void Reload() { desktopReloadRequest=true;try{if(IsMagnum||IsShotgun||WeaponDefinition(ActiveWeaponId)!=null&&WeaponDefinition(ActiveWeaponId).SourceManualControls)TickControls(0,false,false);TickControls(0,false,true);}finally{desktopReloadRequest=false;} }
        public void ApplyPlayerDamage(float amount) { if(round!=null){float before=round.Health;round.Damage(amount);if(round.Health<before&&audioCues!=null&&!gameplayPaused&&hadFocus)audioCues.Play(ActiveWeaponId,"hurt",false);} }
        public bool ApplyPlayerGain(float healthGain,float shieldGain) { return Ready&&round!=null&&round.ApplyPlayerGain(healthGain,shieldGain); }
        public bool TickControls(float dt,bool fire,bool reload) {
            if(round==null)return false;if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            AdvanceSmawProjectiles(dt);if(IsMelee)return TickKnife(dt);if(IsSmaw)return TickSmaw(dt,fire,reload);
            bool semi=ActiveWeaponId=="spr";
            if(semi){sprTrigger.Sample(haveInputSnapshot?lastInput.Hand(primaryHand).TriggerReleased:!fire);fire=sprTrigger.Allows(fire);}
            bool suspended=!Ready||!HasWeapon||!hadFocus||gameplayPaused||presentationSuppressed||inputTrackingUnavailable||weaponHandReserved||!isActiveAndEnabled;
            var beforeState=round.ActiveSnapshot;var beforePhase=beforeState.Phase;
            ActiveFeedback.Advance(dt);feedbackAdvancedFrame=Time.frameCount;ApplySourceAimTuning(dt,suspended);RenderDisplayedPose();var acceptedPose=displayedShot;
            var controls=haveTrackedControls?trackedControls:new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true};controls.Trigger=fire;controls.Desktop|=desktopReloadRequest;
            bool shot;
            if(IsShotgun){var hand=haveInputSnapshot?lastInput.Hand(primaryHand):new R23HandInput();var projection=m1014Controls.Project(!suspended,haveTrackedControls&&reload,true,hand.StickX,hand.StickY,desktopReloadRequest||!haveTrackedControls?reload:false);if(projection.Action==HandlingAction.ReloadStep)controls.Desktop=true;var pose=new R23M1014Pose{Origin=acceptedPose.Origin,Forward=acceptedPose.Forward,Right=acceptedPose.Right,Up=acceptedPose.Up};shot=round.StepM1014(dt,controls,projection.Action,pose,projection.EjectAfterShot,suspended);}
            else if(IsMagnum){var firing=haveInputSnapshot?lastInput.Hand(primaryHand):new R23HandInput();
                // In VR click only opens; actual support strokes/flick perform load/close.
                // Desktop/explicit Reload retains the source staged three-press shortcut.
                shot=round.StepMagnum(dt,new R23MagnumInput(controls,primaryButton:haveTrackedControls&&!desktopReloadRequest&&reload,triggerBlocksFlick:haveInputSnapshot?firing.TriggerPressed||firing.Trigger>.2f:fire,stickX:firing.StickX,stickY:firing.StickY,desktopReload:(!haveTrackedControls||desktopReloadRequest)&&reload),suspended);
            }else if(ActiveWeaponId=="rfb"&&!desktopReloadRequest&&haveTrackedControls){var action=magazineControls.Project(!suspended,reload,true,0,0,false);shot=round.Step(dt,controls,false,suspended);if(action==HandlingAction.Eject)round.EjectActiveMagazineAfterShot(controls,suspended);}
            else if(WeaponDefinition(ActiveWeaponId).SourceManualControls){var hand=haveInputSnapshot?lastInput.Hand(primaryHand):new R23HandInput();var action=magazineControls.Project(!suspended,haveTrackedControls&&reload,true,hand.StickX,hand.StickY,(desktopReloadRequest||!haveTrackedControls)&&reload);controls.Desktop=action==HandlingAction.ReloadStep;shot=round.Step(dt,controls,action==HandlingAction.Eject||action==HandlingAction.ReloadStep,suspended);}
            else shot=round.Step(dt,controls,reload,suspended);
            var afterState=round.ActiveSnapshot;
            bool shotSupported=afterState.Supported||(controls.SupportTracked&&!controls.SupportReserved&&controls.Grip&&controls.ForegripDistance<(beforeState.Supported?.45f:.25f));
            if(shot){lastAcceptedShot=acceptedPose;lastAcceptedPoseValid=true;LastAcceptedShotActionId=activeActionId;LastAcceptedNativeShot=afterState.Shots;ActiveFeedback.AcceptedShot(afterState.Shots,shotSupported);if(IsShotgun)ActiveFeedback.SetAcceptedSourceRecoil(afterState.RecoilDistance,afterState.RecoilPitch,afterState.RecoilYaw);else Pulse(primaryHand,IsMagnum?.65f:.5f,IsMagnum?.06f:.055f);}
            if(!suspended&&IsShotgun&&round.M1014.HapticAmplitude>0)Pulse(round.M1014.HapticSupport?SupportHand:primaryHand,round.M1014.HapticAmplitude,round.M1014.HapticSeconds);
            if(!suspended&&!IsShotgun){
                if((afterState.Supported||shot&&shotSupported)&&!beforeState.Supported)Pulse(1-primaryHand,.16f,.035f);
                if(beforePhase!=afterState.Phase){switch(afterState.Phase){case ReloadPhase.NeedsMagazine:Pulse(primaryHand,.24f,.07f);break;case ReloadPhase.HeldMagazine:Pulse(1-primaryHand,.34f,.06f);break;case ReloadPhase.NeedsRack:Pulse(1-primaryHand,.65f,.085f);break;case ReloadPhase.Ready:if(!IsMagnum)Pulse(1-primaryHand,.5f,.08f);break;}}
                else if(afterState.Phase==ReloadPhase.NeedsRack&&afterState.ClaimsSupport&&!beforeState.ClaimsSupport)Pulse(1-primaryHand,.25f,.04f);
            }
            UpdateWeaponPresentation();
            foreach(var target in targets)target.Refresh(round);
            if(!suspended&&audioCues!=null) {
                var afterPhase=round.ActiveSnapshot.Phase;
                if(afterPhase!=beforePhase)audioCues.Play(ActiveWeaponId,ReloadCue(beforePhase,afterPhase),false);
                if(!shot&&fire&&!previousTrigger&&Ammo==0&&!Reloading)audioCues.Play(ActiveWeaponId,"dry",false);
            }
            previousTrigger=fire;
            if(!shot)return false;
            if(semi)sprTrigger.Shot();
            if(audioCues!=null)audioCues.Play(ActiveWeaponId,"shot",true);
            if(IsShotgun)return TraceShotgun(round.LastM1014Volley);
            Vector3 origin=From(acceptedPose.Origin),direction=From(acceptedPose.Forward),end=origin+direction*100;RaycastHit hit;
            // A bore inside a wall must not shoot through that wall. Check head-to-muzzle segment first.
            Vector3 segment=origin-head.transform.position;
            bool blocked=Nearest(head.transform.position,segment.normalized,segment.magnitude,out hit);
            if(blocked||Nearest(origin,direction,ActiveRange,out hit)) {
                end=hit.point;
                if(gunFeedback!=null)gunFeedback.Impact(hit.point,hit.normal,Time.unscaledTime);
                if(impactMarks!=null)impactMarks.AddHit(hit.point,hit.normal,hit.collider);
                if(!blocked&&WorldHit!=null)WorldHit(hit.collider,ActiveBodyDamage);
                for(int i=0;!blocked&&i<targets.Count;i++)if(targets[i].Owns(hit.collider)) {
                    var target=targets[i];var before=round.ReadTarget(target.TargetId);bool headshot=hit.collider==target.Head;
                    if(round.Hit(target.TargetId,To(origin),To(hit.point),headshot,ActiveWeaponId)) {
                        var after=round.ReadTarget(target.TargetId);target.Refresh(round);if(audioCues!=null)audioCues.Play(ActiveWeaponId,"hit",false);
                        if(TargetDamaged!=null)TargetDamaged(target.TargetId,headshot,before.Shield-after.Shield,before.Health-after.Health);
                    }
                    break;
                }
            }
            tracer.SetPosition(0,origin);tracer.SetPosition(1,end);tracer.enabled=true;flashUntil=Time.unscaledTime+R23WeaponFeedbackState.TracerSeconds;muzzleFlashUntil=Time.unscaledTime+R23WeaponFeedbackState.FlashSeconds;return true;
        }
        bool TraceShotgun(R23M1014Volley volley){
            if(volley==null)return false;LastShotgunPelletCount=LastShotgunTargetGroups=0;Vector3 origin=From(volley.Origin),segment=origin-head.transform.position;RaycastHit blocked;
            muzzleFlashUntil=Time.unscaledTime+.055f;flashUntil=Time.unscaledTime+R23WeaponFeedbackState.TracerSeconds;
            if(Nearest(head.transform.position,segment.normalized,segment.magnitude,out blocked)){
                if(gunFeedback!=null)gunFeedback.Impact(blocked.point,blocked.normal,Time.unscaledTime);if(impactMarks!=null)impactMarks.AddHit(blocked.point,blocked.normal,blocked.collider,true);
                tracer.enabled=false;foreach(var line in pelletTracers)line.enabled=false;round.ApplyM1014Contacts(volley,new R23M1014Contact[0],true);return true;
            }
            var contacts=new List<R23M1014Contact>();var before=new Dictionary<string,HealthSnapshot>();var touched=new Dictionary<string,R23PracticeTarget>();var worldDamage=new Dictionary<Collider,float>();var directions=volley.Directions;
            for(int pellet=0;pellet<directions.Length;pellet++){
                Vector3 direction=From(directions[pellet]),end=origin+direction*volley.Range;RaycastHit hit;LastShotgunPelletCount++;
                if(Nearest(origin,direction,volley.Range,out hit)){
                    end=hit.point;if(gunFeedback!=null)gunFeedback.Impact(hit.point,hit.normal,Time.unscaledTime);if(impactMarks!=null)impactMarks.AddHit(hit.point,hit.normal,hit.collider,true);
                    R23PracticeTarget target=null;foreach(var candidate in targets)if(candidate.Owns(hit.collider)){target=candidate;break;}
                    if(target!=null){if(!before.ContainsKey(target.TargetId))before.Add(target.TargetId,round.ReadTarget(target.TargetId));touched[target.TargetId]=target;contacts.Add(new R23M1014Contact(pellet,target.TargetId,To(hit.point),hit.collider==target.Head));}
                    else{float amount;worldDamage.TryGetValue(hit.collider,out amount);worldDamage[hit.collider]=amount+R23M1014Definition.DamageAtDistance(hit.distance);}
                }
                var line=pellet==0?tracer:pelletTracers[pellet-1];line.SetPosition(0,origin);line.SetPosition(1,end);line.enabled=true;
            }
            LastShotgunTargetGroups=round.ApplyM1014Contacts(volley,contacts.ToArray());
            foreach(var pair in touched){var after=round.ReadTarget(pair.Key);var prior=before[pair.Key];pair.Value.Refresh(round);float shield=prior.Shield-after.Shield,health=prior.Health-after.Health;if(shield+health>0&&TargetDamaged!=null)TargetDamaged(pair.Key,round.LastTargetHeadshot(pair.Key),shield,health);}
            if(LastShotgunTargetGroups>0&&audioCues!=null)audioCues.Play("m1014","hit",false);
            if(WorldHit!=null)foreach(var pair in worldDamage)if(pair.Key!=null&&pair.Value>0)WorldHit(pair.Key,pair.Value);
            return true;
        }
        bool FeedbackAllowed {get{return Ready&&HasWeapon&&!holstered&&!presentationSuppressed&&!gameplayPaused&&hadFocus&&!inputTrackingUnavailable&&!weaponHandReserved&&isActiveAndEnabled&&Health>0;}}
        void Pulse(int hand,float amplitude,float duration)
        {
            if(!FeedbackAllowed||!haveInputSnapshot||!lastInput.Hand(hand).Tracked)return;
            if(HapticRequested!=null)HapticRequested(hand,amplitude,duration);
            var device=InputDevices.GetDeviceAtXRNode(hand==0?XRNode.LeftHand:XRNode.RightHand);HapticCapabilities capabilities;
            if(device.isValid&&device.TryGetHapticCapabilities(out capabilities)&&capabilities.supportsImpulse&&capabilities.numChannels>0)device.SendHapticImpulse(0,amplitude,duration);
        }
        void ClearFeedbackPresentation(){displayedPoseValid=false;if(mp9Laser!=null)mp9Laser.Hide();if(gunFeedback!=null)gunFeedback.Hide();if(tracer!=null)tracer.enabled=false;foreach(var line in pelletTracers)line.enabled=false;flashUntil=muzzleFlashUntil=0;foreach(var feedback in feedbackStates)feedback.ClearPresentation();foreach(var feedback in instanceFeedback.Values)feedback.ClearPresentation();foreach(var model in weaponModels){model.localPosition=Vector3.zero;model.localRotation=Quaternion.identity;}}
        // A native fixture may render synchronously before Unity's first LateUpdate.
        public void RefreshFeedbackPresentation(){LateUpdate();}
        void LateUpdate()
        {
            ApplyFreeHandContactOffsets();
            if(gunFeedback==null)return;gunFeedback.Tick(Time.unscaledTime);
            if(!FeedbackAllowed){ClearFeedbackPresentation();return;}
            if(IsMelee){SyncKnifePresentation();ApplyFreeHandContactOffsets();return;}if(IsSmaw){SyncSmawPresentation();ApplyFreeHandContactOffsets();return;}var feedback=ActiveFeedback;feedback.SetSupported(round.ActiveSnapshot.Supported);if(feedbackAdvancedFrame!=Time.frameCount){feedback.Advance(Mathf.Clamp(Time.unscaledDeltaTime,0,.25f));feedbackAdvancedFrame=Time.frameCount;}
            if(IsShotgun){float seconds=shotgunPresentedFrame==Time.frameCount?0:Mathf.Clamp(Time.unscaledDeltaTime,0,.25f);shotgunPresentedFrame=Time.frameCount;PresentShotgun(seconds);}
            RenderDisplayedPose();ApplyFreeHandContactOffsets();
            var model=weaponModels[selectedWeapon];gunFeedback.PresentAmmo(model,modelScales[selectedWeapon],Ammo,Reserve,primaryHand==0,true);bool fitted=selectedWeapon>=7,compact=ActiveWeaponId=="ump"||ActiveWeaponId=="p90";float flashScale=fitted?modelScales[selectedWeapon]:1;gunFeedback.PresentFlash(Time.unscaledTime<muzzleFlashUntil,muzzle,(fitted&&compact?.055f:.07f)*flashScale,(fitted&&compact?.015f:.018f)*flashScale);
            ResolveDisplayedAim();PresentDisplayedAim();
        }
        bool ViewingSight(Vector3 point)
        {
            if(ActiveWeaponId=="cx4"&&cx4Optic!=null)cx4Optic.SetAimTarget(point);
            Transform lens=feedbackLenses[selectedWeapon],rear=feedbackRearSights[selectedWeapon],front=feedbackFrontSights[selectedWeapon];
            if(lens!=null)lens.GetComponent<Renderer>().sharedMaterial.SetVector("_AimTarget",point);
            for(int eye=0;eye<(head.stereoEnabled?2:1);eye++){
                Vector3 eyePosition=head.transform.position,eyeForward=head.transform.forward;if(head.stereoEnabled){var inverse=head.GetStereoViewMatrix(eye==0?Camera.StereoscopicEye.Left:Camera.StereoscopicEye.Right).inverse;eyePosition=inverse.GetColumn(3);eyeForward=-inverse.GetColumn(2);}
                if(lens!=null){Vector3 localEye=lens.InverseTransformPoint(eyePosition);float relief=-localEye.z*lens.lossyScale.z;Vector3 ray=point-eyePosition;float divisor=Vector3.Dot(ray,lens.forward);if(relief<.035f||relief>.85f||Mathf.Abs(divisor)<.00001f||Vector3.Dot(eyeForward,(point-eyePosition).normalized)<Mathf.Cos(10*Mathf.Deg2Rad))continue;float travel=Vector3.Dot(lens.position-eyePosition,lens.forward)/divisor;Vector3 local=lens.InverseTransformPoint(eyePosition+ray*travel);if(travel>0&&travel<1&&Mathf.Abs(local.x)<=.5f&&Mathf.Abs(local.y)<=.5f)return true;}
                else if(rear!=null&&front!=null){Vector3 axis=(front.position-rear.position).normalized,offset=rear.position-eyePosition;float relief=Vector3.Dot(offset,axis);if(relief>.025f&&relief<.65f&&(offset-axis*relief).magnitude<.024f&&Vector3.Dot(eyeForward.normalized,axis)>.985f)return true;}
            }
            return false;
        }
        static string ReloadCue(ReloadPhase before,ReloadPhase after) {
            if(before==ReloadPhase.Ready&&(after==ReloadPhase.NeedsRack||after==ReloadPhase.NeedsBolt))return "";
            switch(after){case ReloadPhase.CylinderOpen:return "mag_eject";case ReloadPhase.CylinderLoaded:return "mag_insert";case ReloadPhase.NeedsMagazine:return "mag_eject";case ReloadPhase.HeldMagazine:return "pouch";case ReloadPhase.NeedsRack:case ReloadPhase.NeedsBolt:return "mag_insert";case ReloadPhase.Ready:return "bolt";default:return "";}
        }
        void RefreshTrackedControls(R23HandsInputSnapshot input) {
            var primary=input.Hand(primaryHand);inputTrackingUnavailable=!primary.Tracked;weaponHandReserved=primary.Reserved;
            haveTrackedControls=true;trackedControls=R23HandRoleRules.Project(input,primaryHand,To(weapon.InverseTransformPoint(SupportTransform.position)/LogicalReloadScale),Vector3.Distance(SupportTransform.position,weapon.TransformPoint((IsMelee||IsSmaw?Vector3.zero:IsShotgun?From(R23M1014Definition.Foregrip):IsMagnum?new Vector3(0,-.065f,.095f):From(WeaponDefinition(ActiveWeaponId).SupportPoint))*LogicalReloadScale)));
            // Source closure flick is measured in the player locomotion basis, with
            // player-right +X for both dominant hands. Absolute translation cancels.
            var corrected=PrimaryTransform.rotation*R23TrackedGripPose.LocalRotation;
            trackedControls.HasPrimaryPose=true;trackedControls.PrimaryPoint=To(player.InverseTransformPoint(PrimaryTransform.position));
            trackedControls.PrimaryForward=To(player.InverseTransformDirection(corrected*Vector3.forward));trackedControls.PrimaryUp=To(player.InverseTransformDirection(corrected*Vector3.up));
        }
        public bool TickInput(float dt,R23HandsInputFrame input) { return TickInput(dt,input.Snapshot); }
        // Root custody changes refresh hand availability without firing, pickup or time.
        public void UpdateInputAvailability(R23HandsInputFrame input){lastInput=input.Snapshot;haveInputSnapshot=true;RefreshTrackedControls(lastInput);}
        public bool TickInput(float dt,R23HandsInputSnapshot input) {
            if(round==null)return false;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            if(!pickupIntentSampled){pickupRearmRequired[0]=pickupRearmRequired[1]=true;pickupIntentSampled=true;}
            lastInput=input;haveInputSnapshot=true;RefreshTrackedControls(input);
            for(int hand=0;hand<2;hand++)if(!input.Hand(hand).Tracked)pickupRearmRequired[hand]=true;else if(R23WeaponPickupRules.GripNeutral(input.Hand(hand)))pickupRearmRequired[hand]=false;
            bool restart=input.RestartRequested,selection=input.NextWeaponRequested;
            if(!gameplayPaused&&hadFocus) {
                int hand=R23HandRoleRules.ChoosePickupHand(input,R23WeaponPickupRules.GripClaim(input.Left)?PickupDistance(0):float.PositiveInfinity,R23WeaponPickupRules.GripClaim(input.Right)?PickupDistance(1):float.PositiveInfinity,R23WeaponPickupRules.Reach);if(hand>=0)TryPickup(hand);
                if(selection&&!previousSelection)SelectNextWeapon();
                if(restart&&!previousRestart){ResetRound();if(Restarted!=null)Restarted();}
            }
            for(int hand=0;hand<2;hand++)if(input.Hand(hand).Tracked&&R23WeaponPickupRules.GripClaim(input.Hand(hand)))pickupRearmRequired[hand]=true;
            RefreshTrackedControls(input);var firing=input.Hand(primaryHand);bool reload=input.ReloadRequested||input.Right.Tracked&&input.Right.StickClick;
            bool shot=TickControls(dt,firing.Tracked&&firing.TriggerHeld,reload&&!previousReload);
            if(leftVisual!=null)leftVisual.gameObject.SetActive(input.Left.Tracked);
            if(rightVisual!=null)rightVisual.gameObject.SetActive(input.Right.Tracked);
            previousReload=reload;previousRestart=restart;previousSelection=selection;
            return shot;
        }
        void Update() {
            if(impactMarks!=null)impactMarks.Advance(Time.deltaTime);
            if(tracer!=null&&Time.unscaledTime>=flashUntil){tracer.enabled=false;foreach(var line in pelletTracers)line.enabled=false;}
            if(round==null||ExternalInputPump)return;
            var r=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var l=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var input=new R23HandsInputFrame();
            r.TryGetFeatureValue(CommonUsages.isTracked,out input.RightTracked);l.TryGetFeatureValue(CommonUsages.isTracked,out input.LeftTracked);
            r.TryGetFeatureValue(CommonUsages.gripButton,out input.GripPressed);r.TryGetFeatureValue(CommonUsages.grip,out input.Grip);
            r.TryGetFeatureValue(CommonUsages.triggerButton,out input.TriggerPressed);r.TryGetFeatureValue(CommonUsages.trigger,out input.Trigger);
            Vector2 rightStick,leftStick;r.TryGetFeatureValue(CommonUsages.primary2DAxis,out rightStick);l.TryGetFeatureValue(CommonUsages.primary2DAxis,out leftStick);input.RightStickX=rightStick.x;input.RightStickY=rightStick.y;input.LeftStickX=leftStick.x;input.LeftStickY=leftStick.y;
            r.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out input.RightStickClick);l.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out input.LeftStickClick);
            l.TryGetFeatureValue(CommonUsages.gripButton,out input.SupportGripPressed);l.TryGetFeatureValue(CommonUsages.grip,out input.SupportGrip);
            l.TryGetFeatureValue(CommonUsages.triggerButton,out input.LeftTriggerPressed);l.TryGetFeatureValue(CommonUsages.trigger,out input.LeftTrigger);l.TryGetFeatureValue(CommonUsages.primaryButton,out input.LeftPrimary);
            TickInput(Mathf.Min(Time.deltaTime,.25f),input);
        }
        bool ImpactActorOrIgnored(Collider collider){if(collider==null||Excluded(collider))return true;foreach(var target in targets)if(target.Owns(collider))return true;return false;}
        bool Excluded(Collider c) { var t=c.transform;return c.gameObject.layer==20||c.gameObject.layer==21||c.gameObject.layer==22|| t==player||t.IsChildOf(player)||t==head.transform||t.IsChildOf(head.transform)||t==left||t.IsChildOf(left)||t==right||t.IsChildOf(right); }
        bool Nearest(Vector3 origin,Vector3 direction,float distance,out RaycastHit nearest) {
            nearest=new RaycastHit();float best=float.PositiveInfinity;
            foreach(var h in Physics.RaycastAll(origin,direction,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))if(!Excluded(h.collider)&&h.distance<best){best=h.distance;nearest=h;}
            return best<float.PositiveInfinity;
        }
        bool ScopePathClear(Vector3 origin,Vector3 point) { RaycastHit hit;var ray=point-origin;return !Nearest(origin,ray.normalized,ray.magnitude,out hit); }
        public bool HasLineOfSight(string actor,string owner,Float3 origin,Float3 point,float range) {
            if(!origin.IsFinite||!point.IsFinite||range<=0)return false;
            Vector3 start=new Vector3(origin.X,origin.Y,origin.Z),end=new Vector3(point.X,point.Y,point.Z);RaycastHit hit;
            if(!Nearest(start,(end-start).normalized,Mathf.Min(range,Vector3.Distance(start,end)+.02f),out hit))return false;
            for(int i=0;i<targets.Count;i++)if(owner==targets[i].TargetId)return targets[i].Owns(hit.collider);return false;
        }
        static Float3 To(Vector3 v) { return new Float3(v.x,v.y,v.z); }
        static Vector3 From(Float3 v) { return new Vector3(v.X,v.Y,v.Z); }
        static bool ValidRotation(Quaternion value) { float length=value.x*value.x+value.y*value.y+value.z*value.z+value.w*value.w;return To(new Vector3(value.x,value.y,value.z)).IsFinite&&!float.IsNaN(value.w)&&!float.IsInfinity(value.w)&&!float.IsInfinity(length)&&length>=.0001f; }
        void OnApplicationFocus(bool value) { if(!ExternalInputPump)NotifyFocus(value); }
        void OnApplicationPause(bool value) { if(!ExternalInputPump)NotifyFocus(!value); }
        void OnEnable(){if(impactMarks!=null){impactMarks.SetVisible(true);impactMarks.SetPaused(gameplayPaused||!hadFocus);}}
        void OnDisable() { if(impactMarks!=null){impactMarks.SetVisible(false);impactMarks.SetPaused(true);}if(round!=null)round.SuspendActiveWeapon();if(audioCues!=null)audioCues.Stop();UpdateWeaponPresentation(); }
        void OnDestroy() {
            Ready=false;DisposeBladePresentation();if(knifePhysics!=null)knifePhysics.Dispose();if(knifeAudio!=null)knifeAudio.Dispose();Application.onBeforeRender-=RefreshLateTrackedPresentation;
            if(mp9Laser!=null){mp9Laser.Dispose();mp9Laser=null;}
            if(impactMarks!=null){impactMarks.Dispose();impactMarks=null;}
            if(gunFeedback!=null){gunFeedback.Dispose();gunFeedback=null;}
            StopAllCoroutines();DisposeGroundRarity();if(round!=null)round.Retire();if(leftVisual!=null)Destroy(leftVisual.gameObject);if(rightVisual!=null)Destroy(rightVisual.gameObject);if(weapon!=null)Destroy(weapon.gameObject);if(content!=null)Destroy(content.gameObject);
            if(visuals!=null)visuals.Dispose();foreach(var m in targetMaterials)Destroy(m);
            if(audioCues!=null)audioCues.Dispose();
            foreach(var held in heldMagazines)if(held!=null)Destroy(held.gameObject);
        }
    }
}
#endif

