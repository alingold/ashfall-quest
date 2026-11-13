using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Local pilot lane reuses existing rifle, combat and health owners. No network authority.
    public sealed partial class R23RoundState : IAuthorityGate, IHealthWorld
    {
        readonly IInteractionQuery query;
        readonly string[] targets;
        readonly bool sourceLoadout;
        readonly Dictionary<string,LocalRifleSandbox> rifles=new Dictionary<string,LocalRifleSandbox>();
        readonly Dictionary<string,R23MagnumRuntime> magnums=new Dictionary<string,R23MagnumRuntime>();
        readonly Dictionary<string,R23M1014Owner> shotguns=new Dictionary<string,R23M1014Owner>();
        readonly System.Collections.Generic.HashSet<string> usedActions=new System.Collections.Generic.HashSet<string>(),usedNativeIds=new System.Collections.Generic.HashSet<string>();
        bool retiredRound,gripToggle;
        readonly System.Collections.Generic.HashSet<string> appliedSupplyEvents=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        readonly List<string> instanceOrder=new List<string>();
        readonly Dictionary<string,R23WeaponInstanceInfo> instances=new Dictionary<string,R23WeaponInstanceInfo>();
        readonly Dictionary<string,IRevisionedAmmoReserve> reserves=new Dictionary<string,IRevisionedAmmoReserve>();
        string selectedRifle="mk18",selectedAction="mk18",magnumInstanceId;long copySerial;
        public R23MagnumRuntime Magnum {get{R23MagnumRuntime result;magnums.TryGetValue(selectedRifle=="magnum"?selectedAction:"magnum",out result);return result;}}
        public R23M1014Owner M1014 {get{R23M1014Owner result;shotguns.TryGetValue(selectedRifle=="m1014"?selectedAction:"m1014",out result);return result;}}
        public R23M1014Volley LastM1014Volley {get;private set;}
        public string ActiveWeaponId {get{return sourceLoadout?selectedRifle:"service_rifle";}}
        public string ActiveActionId {get{return sourceLoadout?selectedAction:"service_rifle";}}
        public HandlingSnapshot ActiveSnapshot {get{return selectedRifle=="awp"?Awp.Snapshot:selectedRifle=="dt11"?Dt11.Snapshot:R24MeleeProfiles.ForId(selectedRifle)!=null?Melee.Snapshot:selectedRifle=="smaw"?Smaw.HandlingSnapshot:selectedRifle=="m1014"&&M1014!=null?M1014.Snapshot:selectedRifle=="magnum"&&Magnum!=null?Magnum.Snapshot:Rifle.Snapshot;}}
        public int ActiveLoaded {get{return selectedRifle=="awp"?Awp.Snapshot.Loaded:selectedRifle=="dt11"?(Dt11.Snapshot.Phase==ReloadPhase.NeedsRack?Dt11.Snapshot.Seated:Dt11.Snapshot.Loaded):R24MeleeProfiles.ForId(selectedRifle)!=null?0:selectedRifle=="smaw"?Smaw.Loaded:selectedRifle=="m1014"&&M1014!=null?(M1014.Snapshot.Phase==ReloadPhase.NeedsRack?M1014.Snapshot.Seated:M1014.Snapshot.Loaded):selectedRifle=="magnum"&&Magnum!=null?Magnum.Loaded:Rifle.LoadedRounds;}}
        public int ActiveCapacity {get{R23WeaponInstanceInfo info;return instances.TryGetValue(selectedAction,out info)?info.Capacity:Rifle.MagazineCapacity;}}
        public int ActiveReserve {get{return ReadReserve(selectedAction);}}
        public string ActiveInstanceId {get{return sourceLoadout?instances[selectedAction].InstanceId:Rifle.InstanceId;}}
        public string ResolveWeaponProfile(string actionId){R23WeaponInstanceInfo info;return actionId!=null&&instances.TryGetValue(actionId,out info)?info.ProfileId:null;}
        public bool TryReadWeaponInstance(string actionId,out R23WeaponInstanceInfo info){info=null;return actionId!=null&&instances.TryGetValue(actionId,out info);}
        public string[] WeaponActionIds {get{return instanceOrder.ToArray();}}
        // Construction is the sole initial magazine entitlement. Custody changes retain it.
        public void SetGripToggle(bool enabled){gripToggle=enabled;foreach(var rifle in rifles.Values)rifle.GripToggle=enabled;foreach(var magnum in magnums.Values)magnum.GripToggle=enabled;foreach(var shotgun in shotguns.Values)shotgun.GripToggle=enabled;foreach(var dt in dt11s.Values)dt.GripToggle=enabled;foreach(var awp in awps.Values)awp.GripToggle=enabled;}
        public bool TryReadAmmoReserveFamily(string family,out int available){available=0;IRevisionedAmmoReserve reserve;if(!sourceLoadout||family==null||!reserves.TryGetValue(family,out reserve))return false;available=reserve.Available;return true;}
        public int ApplyReserveSupply(string eventId,string family,int requested){IRevisionedAmmoReserve reserve;if(retiredRound||string.IsNullOrEmpty(eventId)||eventId.Length>256||requested<=0||family==null||appliedSupplyEvents.Contains(eventId)||!reserves.TryGetValue(family,out reserve))return 0;int accepted=reserve.Refund(requested);if(accepted>0)appliedSupplyEvents.Add(eventId);return accepted;}
        public int WithdrawReserveSupply(string family,int requested){IRevisionedAmmoReserve reserve;return retiredRound||requested<=0||family==null||!reserves.TryGetValue(family,out reserve)?0:reserve.Take(requested);}
        public int ReadAmmoReserveFamily(string family){int available;TryReadAmmoReserveFamily(family,out available);return available;}
        public bool CreateWeaponInstance(string profile,int stars,out string actionId){actionId=null;if(copySerial==long.MaxValue)return false;string proposed="r23-copy/"+(++copySerial).ToString(System.Globalization.CultureInfo.InvariantCulture);if(!RegisterWeaponInstance(proposed,profile,stars,proposed))return false;actionId=proposed;return true;}
        public bool RegisterWeaponInstance(string actionId,string profile,int stars,string nativeId=null){
            if(R24MeleeProfiles.ForId(profile)!=null&&stars>=1&&stars<=5)stars=1;R23RarityStats stats;if(retiredRound||!sourceLoadout||string.IsNullOrEmpty(actionId)||actionId.Length>128||usedActions.Contains(actionId)||!R23WeaponRarity.TryRead(profile,stars,out stats))return false;
            nativeId=nativeId??actionId;if(string.IsNullOrEmpty(nativeId)||nativeId.Length>128||usedNativeIds.Contains(nativeId))return false;
            var definition=SourceRifleDefinition.ForId(profile);if(definition==null&&profile!="magnum"&&profile!="dt11"&&profile!="m1014"&&profile!="smaw"&&R24MeleeProfiles.ForId(profile)==null)return false;
            if(profile=="dt11"){dt11s.Add(actionId,new R24Dt11Owner(Command(),nativeId,reserves["shells"],this,stats.SpreadScale));dt11s[actionId].GripToggle=gripToggle;instances.Add(actionId,new R23WeaponInstanceInfo(actionId,nativeId,profile,stars,stats));instanceOrder.Add(actionId);usedActions.Add(actionId);usedNativeIds.Add(nativeId);return true;}if(R24MeleeProfiles.ForId(profile)!=null){knives.Add(actionId,profile=="knife"?(R24MeleeRuntime)new R24KnifeRuntime(nativeId):new R24MeleeRuntime(nativeId,R24MeleeProfiles.ForId(profile)));instances.Add(actionId,new R23WeaponInstanceInfo(actionId,nativeId,profile,stars,stats));instanceOrder.Add(actionId);usedActions.Add(actionId);usedNativeIds.Add(nativeId);return true;}if(profile=="smaw"){smaws.Add(actionId,new R24SmawRuntime(nativeId,stars));instances.Add(actionId,new R23WeaponInstanceInfo(actionId,nativeId,profile,stars,stats));instanceOrder.Add(actionId);usedActions.Add(actionId);usedNativeIds.Add(nativeId);return true;}
            string family=profile=="magnum"?"pistol":profile=="m1014"?"shells":definition.AmmoFamily;IRevisionedAmmoReserve reserve;if(!reserves.TryGetValue(family,out reserve))return false;
            if(profile=="awp")awps.Add(actionId,new R24AwpOwner(Command(),nativeId,reserve,this));else if(profile=="magnum")magnums.Add(actionId,new R23MagnumRuntime(reserve,nativeId,stats.Capacity,stars));
            else if(profile=="m1014")shotguns.Add(actionId,new R23M1014Owner(Command(),nativeId,reserve,this,stats.SpreadScale));
            else rifles.Add(actionId,new LocalRifleSandbox(new string[0],query,definition.WithRarity(stats.Capacity,stats.BodyDamage,stats.HeadDamage),reserve,nativeId));
            LocalRifleSandbox configuredRifle;R23MagnumRuntime configuredMagnum;R23M1014Owner configuredShotgun;if(rifles.TryGetValue(actionId,out configuredRifle))configuredRifle.GripToggle=gripToggle;if(magnums.TryGetValue(actionId,out configuredMagnum))configuredMagnum.GripToggle=gripToggle;if(shotguns.TryGetValue(actionId,out configuredShotgun))configuredShotgun.GripToggle=gripToggle;
            R24AwpOwner configuredAwp;if(awps.TryGetValue(actionId,out configuredAwp))configuredAwp.GripToggle=gripToggle;instances.Add(actionId,new R23WeaponInstanceInfo(actionId,nativeId,profile,stars,stats));instanceOrder.Add(actionId);usedActions.Add(actionId);usedNativeIds.Add(nativeId);return true;
        }
        public bool RetireWeaponInstance(string actionId){if(actionId==null||actionId==selectedAction||!instances.ContainsKey(actionId))return false;R24Dt11Owner dt;if(dt11s.TryGetValue(actionId,out dt)){dt.Retire();dt11s.Remove(actionId);}R24MeleeRuntime knife;if(knives.TryGetValue(actionId,out knife)){knife.Retire();knives.Remove(actionId);}R24SmawRuntime smaw;if(smaws.TryGetValue(actionId,out smaw)){smaw.Retire();smaws.Remove(actionId);}LocalRifleSandbox rifle;R23MagnumRuntime magnum;R23M1014Owner shotgun;if(rifles.TryGetValue(actionId,out rifle)){rifle.Retire();rifles.Remove(actionId);}if(magnums.TryGetValue(actionId,out magnum)){magnum.Retire();magnums.Remove(actionId);}if(shotguns.TryGetValue(actionId,out shotgun)){shotgun.Retire();shotguns.Remove(actionId);}R24AwpOwner removedAwp;if(awps.TryGetValue(actionId,out removedAwp)){removedAwp.Retire();awps.Remove(actionId);}instances.Remove(actionId);instanceOrder.Remove(actionId);return true;}
        public bool EnsureMagnumState(string stableId){if(!sourceLoadout||string.IsNullOrEmpty(stableId)||stableId.Length>128)return false;R23WeaponInstanceInfo info;if(instances.TryGetValue("magnum",out info))return info.InstanceId==stableId;if(!RegisterWeaponInstance("magnum","magnum",1,stableId))return false;magnumInstanceId=stableId;return true;}
        public void SuspendActiveWeapon(){LastDt11Volley=null;LastM1014Volley=null;if(!sourceLoadout){if(Rifle!=null)Rifle.SuspendControls();}else SuspendWeapon(selectedAction);pendingShot=false;}
        public void Retire(){retiredRound=true;foreach(var awp in awps.Values)awp.Retire();foreach(var dt in dt11s.Values)dt.Retire();foreach(var weapon in rifles.Values)weapon.Retire();if(!sourceLoadout&&Rifle!=null)Rifle.Retire();foreach(var weapon in magnums.Values)weapon.Retire();foreach(var weapon in shotguns.Values)weapon.Retire();foreach(var weapon in smaws.Values)weapon.Retire();foreach(var weapon in knives.Values)weapon.Retire();LastM1014Volley=null;pendingShot=false;}
        HealthService health;
        long sequence;
        double now;
        bool pendingShot,resettingPractice;
        sealed class Target { public float Last,Total,ResetLeft;public bool Head; }
        readonly Dictionary<string,Target> practice=new Dictionary<string,Target>();
        public LocalRifleSandbox Rifle { get; private set; }
        public int Hits { get; private set; }
        public float Health { get { return health.Read("player").Health; } }
        public float Shield { get { return health.Read("player").Shield; } }
        public HealthSnapshot PlayerHealthState { get { return health.Read("player"); } }
        public float MaxHealth { get { return 100; } }
        public float MaxShield { get { return 100; } }
        public R23RoundState(string[] ids,IInteractionQuery interactions,bool useSourceLoadout=false) { targets=ids;query=interactions;sourceLoadout=useSourceLoadout;Reset(); }
        CommandContext Command() { return new CommandContext("r23-player","player","damage/"+(++sequence),sequence,0,true); }
        public RuleResult Validate(CommandContext c) { return !retiredRound&&c.IsAuthority&&c.SessionId=="r23-player"&&c.ActorId=="player"&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local player only"); }
        public double Now { get { return now; } }
        public bool InMatch { get { return !resettingPractice; } }
        public bool TeamChangesAllowed { get { return false; } }
        public bool TryGetPose(string id,out RescuePose pose) { pose=currentPaddlePose;if(id=="player")return havePaddlePose;R24ReviveTarget contact;if(!reviveContacts.TryGetValue(id,out contact))return false;pose=new RescuePose{Tracked=true,Chest=R23WeaponPoseMath.Scale(R23WeaponPoseMath.Add(contact.LeftPad,contact.RightPad),.5f)};return true; }
        public void Reset() {
            Retire();awps.Clear();LastAwpShot=null;havePaddlePose=false;reviveContacts.Clear();appliedSupplyEvents.Clear();rifles.Clear();magnums.Clear();shotguns.Clear();smaws.Clear();knives.Clear();dt11s.Clear();LastDt11Volley=null;instances.Clear();usedActions.Clear();usedNativeIds.Clear();instanceOrder.Clear();reserves.Clear();retiredRound=false;LastM1014Volley=null;
            now=0;sequence=0;Hits=0;pendingShot=false;practice.Clear();
            if(sourceLoadout){
                reserves.Add("rifle",new R23RifleReserve());reserves.Add("sniper",new R23RifleReserve());reserves.Add("smg",new R23RifleReserve(1200));reserves.Add("pistol",R23MagnumRuntime.CreatePistolReserve());reserves.Add("shells",new R23RifleReserve(480));
                foreach(var definition in SourceRifleDefinition.All)RegisterWeaponInstance(definition.Id,definition.Id,1,"r23-loadout/"+definition.Id);
                RegisterWeaponInstance("m1014","m1014",1,"r23-loadout/m1014");RegisterWeaponInstance("dt11","dt11",1,"r24-loadout/dt11");RegisterWeaponInstance("smaw","smaw",1,"r24-loadout/smaw");RegisterWeaponInstance("knife","knife",1,"r24-loadout/knife");foreach(var blade in new[]{"katana","excalibur","cyberblade"})RegisterWeaponInstance(blade,blade,1,"r24-loadout/"+blade);if(magnumInstanceId!=null)EnsureMagnumState(magnumInstanceId);
                if(!instances.ContainsKey(selectedRifle))selectedRifle="mk18";selectedAction=selectedRifle;Rifle=rifles[selectedRifle=="awp"||selectedRifle=="magnum"||selectedRifle=="dt11"||selectedRifle=="m1014"||selectedRifle=="smaw"||R24MeleeProfiles.ForId(selectedRifle)!=null?"mk18":selectedRifle];
            }else Rifle=new LocalRifleSandbox(new string[0],query);
            health=new HealthService("r23-player",0,0,false,this,this,query,60,5,0);
            health.Register(Command(),"player",-1,false);
            foreach(var id in targets){health.Register(Command(),id,-1,false);practice.Add(id,new Target());}
            health.DrainTransitions();
        }
        internal R23MagnumRuntime ReadMagnumInstance(string actionId){R23MagnumRuntime result;return actionId!=null&&magnums.TryGetValue(actionId,out result)?result:null;}
        public bool Step(float dt,bool trigger,bool reload,bool paused) {
            return Step(dt,new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=trigger},reload,paused);
        }
        public bool SelectWeapon(string id){R23WeaponInstanceInfo info;if(!sourceLoadout||id==null||!instances.TryGetValue(id,out info))return false;if(selectedAction==id)return true;SuspendActiveWeapon();selectedAction=id;selectedRifle=info.ProfileId;LocalRifleSandbox next;if(rifles.TryGetValue(id,out next))Rifle=next;SuspendActiveWeapon();return true;}
        public HandlingSnapshot ReadWeapon(string id){R24AwpOwner awp;if(id!=null&&awps.TryGetValue(id,out awp))return awp.Snapshot;R24Dt11Owner dt;if(id!=null&&dt11s.TryGetValue(id,out dt))return dt.Snapshot;R24MeleeRuntime knife;if(id!=null&&knives.TryGetValue(id,out knife))return knife.Snapshot;R24SmawRuntime smaw;if(id!=null&&smaws.TryGetValue(id,out smaw))return smaw.HandlingSnapshot;R23MagnumRuntime magnum;R23M1014Owner shotgun;LocalRifleSandbox rifle;if(id!=null&&magnums.TryGetValue(id,out magnum))return magnum.Snapshot;if(id!=null&&shotguns.TryGetValue(id,out shotgun))return shotgun.Snapshot;if(id!=null&&rifles.TryGetValue(id,out rifle))return rifle.Snapshot;throw new ArgumentException("Unknown source weapon instance");}
        public string WeaponInstanceId(string id){R23WeaponInstanceInfo info;if(id==null||!instances.TryGetValue(id,out info))throw new ArgumentException("Unknown source weapon instance");return info.InstanceId;}
        public int ReadReserve(string id){if(!sourceLoadout)return Rifle.Reserve;R23WeaponInstanceInfo info;if(id==null||!instances.TryGetValue(id,out info))throw new ArgumentException("Unknown source weapon instance");if(knives.ContainsKey(id))return 0;if(dt11s.ContainsKey(id))return reserves["shells"].Available;R24SmawRuntime smaw;if(smaws.TryGetValue(id,out smaw))return smaw.Reloads;string family=info.ProfileId=="magnum"?"pistol":info.ProfileId=="m1014"?"shells":SourceRifleDefinition.ForId(info.ProfileId).AmmoFamily;return reserves[family].Available;}
        public bool SuspendWeapon(string id){LocalRifleSandbox rifle;R23MagnumRuntime magnum;R23M1014Owner shotgun;if(id==null)return false;bool found=false;R24AwpOwner awp;if(awps.TryGetValue(id,out awp)){awp.SuspendControls();found=true;}R24Dt11Owner dt;if(dt11s.TryGetValue(id,out dt)){dt.SuspendControls();found=true;}R24MeleeRuntime knife;if(knives.TryGetValue(id,out knife)){knife.SuspendControls();found=true;}R24SmawRuntime smaw;if(smaws.TryGetValue(id,out smaw)){smaw.SuspendControls();found=true;}if(rifles.TryGetValue(id,out rifle)){rifle.SuspendControls();found=true;}if(magnums.TryGetValue(id,out magnum)){magnum.SuspendControls();found=true;}if(shotguns.TryGetValue(id,out shotgun)){shotgun.SuspendControls();found=true;}if(id==selectedAction){pendingShot=false;LastM1014Volley=null;LastDt11Volley=null;}return found;}
        public bool Step(float dt,HandlingInput input,bool reload,bool paused) {
            if(selectedRifle=="dt11")return StepDt11(dt,input,reload?(input.Desktop?HandlingAction.ReloadStep:HandlingAction.Eject):HandlingAction.Sample,new R23M1014Pose{Forward=new Float3(0,0,1),Right=new Float3(1,0,0),Up=new Float3(0,1,0)},false,paused);
            if(selectedRifle=="awp")return StepAwp(dt,input,reload?(input.Desktop?HandlingAction.ReloadStep:HandlingAction.Eject):HandlingAction.Sample,new R23ShotPose(new Float3(),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1)),paused);
            if(R24MeleeProfiles.ForId(selectedRifle)!=null){AdvanceSmawWorld(dt,paused);return false;}
            if(selectedRifle=="smaw")return StepSmaw(dt,input,new R23ShotPose(new Float3(),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1)),true,reload&&input.Desktop,paused)!=null;
            if(selectedRifle=="m1014")return StepM1014(dt,input,reload?(input.Desktop?HandlingAction.ReloadStep:HandlingAction.Eject):HandlingAction.Sample,new R23M1014Pose{Forward=new Float3(0,0,1),Right=new Float3(1,0,0),Up=new Float3(0,1,0)},false,paused);
            if(selectedRifle=="magnum")return StepMagnum(dt,new R23MagnumInput(input,primaryButton:!input.Desktop&&reload,triggerBlocksFlick:input.Trigger,desktopReload:input.Desktop&&reload),paused);
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            bool suspended=paused||Health<=0;
            if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}
            // Actual VR stick-click ejects; physical support performs the new
            // source magazine/slide flow. Explicit desktop Reload remains staged.
            var profile=SourceRifleDefinition.ForId(selectedRifle);if(profile!=null&&profile.SourceManualControls&&!input.Desktop){pendingShot=Rifle.StepSource(dt,input,reload?HandlingAction.Eject:HandlingAction.Sample,suspended);return pendingShot;}if(profile!=null&&profile.VrOpeningOnly&&!input.Desktop&&reload&&Rifle.Snapshot.Phase!=ReloadPhase.Ready)reload=false;
            pendingShot=Rifle.Step(dt,input,reload,suspended);return pendingShot;
        }
        public void EjectActiveMagazineAfterShot(HandlingInput input,bool paused){if(!sourceLoadout||selectedRifle!="rfb")return;Rifle.StepSource(0,input,HandlingAction.Eject,paused||Health<=0);}
        public bool StepMagnum(float dt,R23MagnumInput frame,bool paused) {
            if(!sourceLoadout||selectedRifle!="magnum"||Magnum==null)return false;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            bool suspended=paused||Health<=0;
            if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}
            var input=frame.Mechanics;input.Paused|=suspended;
            pendingShot=Magnum.Step(dt,new R23MagnumInput(input,frame.PrimaryButton,frame.ManualClickAllowed,frame.TriggerBlocksFlick,frame.StickX,frame.StickY,frame.DesktopReload));return pendingShot;
        }
        public bool StepM1014(float dt,HandlingInput input,HandlingAction action,R23M1014Pose pose,bool ejectAfterShot,bool paused){
            if(!sourceLoadout||selectedRifle!="m1014"||M1014==null)return false;if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            bool suspended=paused||Health<=0;if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}
            input.Paused|=suspended;input.DeltaSeconds=dt;var command=Command();input.Sequence=command.Tick;R23M1014Volley volley;M1014.Apply(command,action,input,pose,out volley,ejectAfterShot);LastM1014Volley=volley;pendingShot=volley!=null;return pendingShot;
        }
        // Real accepted native pellet contacts are grouped once per registered target.
        // No fabricated health pool, head multiplier, or second ammunition debit.
        public int ApplyM1014Contacts(R23M1014Volley volley,R23M1014Contact[] contacts,bool muzzleBlocked=false){
            if(!pendingShot||selectedRifle!="m1014"||volley==null||!object.ReferenceEquals(volley,LastM1014Volley)||M1014==null||volley.InstanceId!=M1014.InstanceId)return 0;
            pendingShot=false;if(muzzleBlocked||contacts==null||contacts.Length>8)return 0;
            var groups=new Dictionary<string,float>();var heads=new Dictionary<string,bool>();var seen=new System.Collections.Generic.HashSet<int>();
            foreach(var contact in contacts){if(contact.Pellet<0||contact.Pellet>=8||!seen.Add(contact.Pellet))return 0;if(string.IsNullOrEmpty(contact.TargetId)||!practice.ContainsKey(contact.TargetId)||!contact.Point.IsFinite)continue;float x=contact.Point.X-volley.Origin.X,y=contact.Point.Y-volley.Origin.Y,z=contact.Point.Z-volley.Origin.Z,distance=(float)Math.Sqrt(x*x+y*y+z*z);float damage=R23M1014Definition.DamageAtDistance(distance);if(damage<=0||!query.HasLineOfSight("player",contact.TargetId,volley.Origin,contact.Point,volley.Range))continue;float prior;groups.TryGetValue(contact.TargetId,out prior);groups[contact.TargetId]=prior+damage;bool head;heads.TryGetValue(contact.TargetId,out head);heads[contact.TargetId]=head||contact.Head;}
            int applied=0;foreach(var group in groups){var before=health.Read(group.Key);if(before==null||before.Life!=LifeState.Alive)continue;float shield=Math.Min(before.Shield,group.Value),loss=Math.Min(before.Health,group.Value-shield);var command=Command();var receipt=health.ApplyDamage(command,command.CommandId,group.Key,before.Revision,loss,shield,false);if(!receipt.Result.Accepted)continue;var target=practice[group.Key];target.Last=(before.Shield-receipt.Snapshot.Shield)+(before.Health-receipt.Snapshot.Health);target.Total+=target.Last;target.Head=heads[group.Key];target.ResetLeft=1.5f;Hits++;applied++;}health.DrainTransitions();return applied;
        }
        public HealthSnapshot ReadTarget(string id) { return practice.ContainsKey(id)?health.Read(id):null; }
        public float LastTargetDamage(string id) { Target t;return practice.TryGetValue(id,out t)?t.Last:0; }
        public float TotalTargetDamage(string id) { Target t;return practice.TryGetValue(id,out t)?t.Total:0; }
        public bool LastTargetHeadshot(string id) { Target t;return practice.TryGetValue(id,out t)&&t.Head; }
        public bool ResetTarget(string id) {
            Target t;if(!practice.TryGetValue(id,out t))return false;
            var before=health.Read(id);RuleResult result;
            // Explicit offline fixture reset; never respawns the player or grants ammunition.
            resettingPractice=true;try{result=health.Spawn(Command(),id,before.Revision,100,100);}finally{resettingPractice=false;}
            if(!result.Accepted)return false;t.Last=t.Total=t.ResetLeft=0;t.Head=false;health.DrainTransitions();return true;
        }
        public bool Hit(string id,Float3 origin,Float3 point) { return Hit(id,origin,point,false,sourceLoadout?selectedRifle:"mk18"); }
        public bool Hit(string id,Float3 origin,Float3 point,bool headshot,string weaponId) {
            if(weaponId=="m1014")return false;
            if(!pendingShot)return false;pendingShot=false;
            Target target;if((sourceLoadout&&weaponId!=selectedRifle)||!practice.TryGetValue(id,out target))return false;var before=health.Read(id);
            float dx=point.X-origin.X,dy=point.Y-origin.Y,dz=point.Z-origin.Z;
            var rangeDefinition=SourceRifleDefinition.ForId(weaponId);float range=weaponId=="magnum"?R23MagnumRuntime.MaximumRange:rangeDefinition==null?100:rangeDefinition.MaximumRange;
            if(before.Life!=LifeState.Alive||!origin.IsFinite||!point.IsFinite||dx*dx+dy*dy+dz*dz>range*range||!query.HasLineOfSight("player",id,origin,point,range))return false;
            // one_star0991.json, first rarity: MK18 22/33; AKM 20/30. No invented head multiplier.
            var definition=SourceRifleDefinition.ForId(weaponId);if(definition==null&&weaponId!="magnum")return false;R23WeaponInstanceInfo info;float amount=sourceLoadout&&instances.TryGetValue(selectedAction,out info)?(headshot?info.HeadDamage:info.BodyDamage):(headshot?definition.HeadDamage:definition.BodyDamage);
            float shieldLoss=Math.Min(before.Shield,amount),healthLoss=Math.Min(before.Health,amount-shieldLoss);
            var c=Command();DamageReceipt receipt;
            if(weaponId=="magnum"){if(!Magnum.TryApplyHit(health,c,query,id,origin,point,headshot,out receipt))return false;}else receipt=health.ApplyDamage(c,c.CommandId,id,before.Revision,healthLoss,shieldLoss,false);
            if(!receipt.Result.Accepted)return false;
            target.Last=(before.Health-receipt.Snapshot.Health)+(before.Shield-receipt.Snapshot.Shield);target.Total+=target.Last;target.Head=headshot;target.ResetLeft=1.5f;Hits++;health.DrainTransitions();return true;
        }
        public bool ApplyTargetDamage(string id,float amount){
            Target target;if(retiredRound||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||id==null||!practice.TryGetValue(id,out target))return false;
            var before=health.Read(id);if(before==null||before.Life!=LifeState.Alive)return false;float shield=Math.Min(before.Shield,amount),loss=Math.Min(before.Health,amount-shield);if(shield+loss<=0)return false;
            var command=Command();var receipt=health.ApplyDamage(command,command.CommandId,id,before.Revision,loss,shield,false);if(!receipt.Result.Accepted)return false;target.Last=(before.Shield-receipt.Snapshot.Shield)+(before.Health-receipt.Snapshot.Health);target.Total+=target.Last;target.Head=false;target.ResetLeft=1.5f;Hits++;health.DrainTransitions();return true;
        }
        public void Damage(float amount) {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||Health<=0)return;
            var before=health.Read("player");float shieldLoss=sourceLoadout?Math.Min(before.Shield,amount):0,healthLoss=Math.Min(before.Health,amount-shieldLoss);
            var c=Command();health.ApplyDamage(c,c.CommandId,"player",before.Revision,healthLoss,shieldLoss,false);
            health.DrainTransitions();
        }
        public bool ApplyPlayerGain(float healthGain,float shieldGain) {
            if(float.IsNaN(healthGain)||float.IsInfinity(healthGain)||float.IsNaN(shieldGain)||float.IsInfinity(shieldGain)||healthGain<0||healthGain>100||shieldGain<0||shieldGain>100||(healthGain==0&&shieldGain==0))return false;
            return health.Serialize(delegate {
                var before=health.Read("player");
                if(before==null||before.Life!=LifeState.Alive||before.Health<=0||((healthGain==0||before.Health>=100)&&(shieldGain==0||before.Shield>=100)))return false;
                var command=Command();HealthService.PreparedGain prepared;
                if(!health.PrepareGain(command,command.CommandId,"player",before.Revision,before.LifeGeneration,before.ConnectionGeneration,healthGain,shieldGain,out prepared))return false;
                if(prepared.Proposed.Outcome!=GainOutcome.Applied||!prepared.Validate()){prepared.Abort();return false;}
                var receipt=prepared.CommitValidated();health.DrainTransitions();return receipt.Outcome==GainOutcome.Applied&&(receipt.Health>0||receipt.Shield>0);
            });
        }
    }
}
