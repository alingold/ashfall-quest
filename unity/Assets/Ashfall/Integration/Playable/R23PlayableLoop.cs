#if UNITY_6000_0_OR_NEWER

using System;

using Ashfall.Core;

using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.InputSystem;

using Ashfall.Integration.Playable.World;

using Ashfall.Integration.Playable.World.Loot;

using Ashfall.Integration.Playable.HandsWeapons;

using Ashfall.Integration.Playable.Activities;

using Ashfall.Integration.Playable.UI;

using Ashfall.Integration.Playable.Appearance;

using Ashfall.Integration.Playable.Climbing;

using Ashfall.Integration.Playable.Calibration;

using Ashfall.Integration.Playable.Consumables;

using Ashfall.Integration.Playable.Explosives;

using Ashfall.Integration.Playable.JumpPads;

using Ashfall.Integration.Playable.Lobby;

using Ashfall.Integration.Playable.Gallery;

using Ashfall.Integration.Playable.AudioTraversal;

using Ashfall.Integration.Playable.Input;

using Ashfall.Integration.Playable.Networking;

using Ashfall.Integration.Playable.PickupHints;

using Ashfall.Modules.Player;

namespace Ashfall.Integration.Playable

{

    [DefaultExecutionOrder(-100)]

    public sealed partial class R23PlayableLoop:MonoBehaviour,IR23ConsumableEffects,IR23ExplosiveEffects

    {

        public R23PlayableMap Map {get;private set;}

        public R23WorldLoot WorldLoot {get;private set;}

        public R23VRRig Rig {get;private set;}

        public R23HandsWeapons Gameplay {get;private set;}

        public R23VRActivities Activities {get;private set;}

        public R23VrUI UI {get;private set;}

        public R23PlayerAppearance Appearance {get;private set;}

        public R23VRClimbing Climbing {get;private set;}

        public R23VerticalCalibration Calibration {get;private set;}

        public R23AppearancePreview Preview {get;private set;}

        public R23VRConsumables Consumables {get;private set;}

        public R23VRExplosives Explosives {get;private set;}

        public R24VRSupplies Supplies {get;private set;}

        int inventoryCapacity=3;public int InventoryCapacity {get{return inventoryCapacity;}}

        readonly List<string> activeSupplyIds=new List<string>();string sourcePickupMap;Vector3 sourcePickupOrigin;bool sourcePickupLayoutPrepared;

        public R23JumpPads JumpPads {get;private set;}

        public R23HealthStations HealthStations {get;private set;}

        public R23Lobby Lobby {get;private set;}

        public R23LobbyWeaponGallery Gallery {get;private set;}

        public R23WeaponWheel WeaponWheel {get;private set;}

        public R23TraversalAudio TraversalAudio {get;private set;}

        public R24LanSession LanSession {get;private set;}

        public R23WorldPing WorldPing {get;private set;}

        public R23TeamMarks TeamMarks {get;private set;}

        public R23PickupHints PickupHints {get;private set;}

        bool BackpackOpen {get{return roundStarted&&UI!=null&&UI.Page==R23UiPage.Loadout;}}

        bool PageBlocksGameplay {get{return LanSession!=null&&LanSession.MapEntryBlocked||UI!=null&&UI.BlocksGameplay&&!BackpackOpen;}}

        bool PlayerMotionBlocked {get{return !roundStarted||PageBlocksGameplay||Rig!=null&&Rig.CarrierActive||(ActualMatchRules==null?Gameplay!=null&&Gameplay.Health<=0:!MatchLocalMotionAllowed);}}

        public bool Ready {get;private set;}

        public bool ExternalProbePump;

        public string Failure {get;private set;}

        bool reloadRequested,switchingMap,roundStarted;string selectedWeapon,queuedEquip,queuedDrop;int queuedHand,requestedDominantHand=-1;Vector3 activeSpawn;

        readonly R23ObjectiveGripArbiter objectiveGrips=new R23ObjectiveGripArbiter();

        R23GripObjective selectedObjective;bool routeObjectiveInput;

        public R23GripObjective SelectedGripObjective {get{return selectedObjective;}}

#if UNITY_EDITOR

        public bool ManualSmoke;

        bool smokeUiPump;

        public void SmokeUiUpdate()

        {

            if(UnityEngine.XR.XRSettings.isDeviceActive)throw new InvalidOperationException("UI smoke requires XR disabled");

            bool previous=smokeUiPump;try{smokeUiPump=true;Update();}finally{smokeUiPump=previous;}

        }

#endif

        IEnumerator Start()

        {

            try

            {

                gameObject.AddComponent<R23XrStartupRecovery>();

                Rig=new GameObject("R23 VR player").AddComponent<R23VRRig>();Rig.transform.SetParent(transform,false);

                Rig.Initialize(Vector3.zero,Vector3.forward);Rig.GameplayBlocked=true;

                UI=new GameObject("R23 loading menus and HUD").AddComponent<R23VrUI>();UI.transform.SetParent(transform,false);UI.Initialize(Rig.Head);

                UI.SetLocomotionAnchorProvider(delegate{return Rig.LocomotionAnchorPosition;});

                UI.BeginLoading("BATTLEFIELD PAINTBALL");

                var maps=new List<R23MapOption>();foreach(var choice in R23PlayableMap.AvailableMaps)maps.Add(new R23MapOption(choice.Id,choice.Label));UI.SetMapOptions(maps);UI.MapSelected+=EnterMap;UI.MapChoiceChanged+=MapChoiceChanged;

                Rig.MenuPressed+=ToggleMenu;

                UI.StartRequested+=StartPlaying;UI.RestartRequested+=RestartRound;UI.ReloadRequested+=QueueReload;

                UI.MainMenuRequested+=ReturnToLobby;UI.MapLobbyRequested+=ReturnToMapLobby;

                UI.WeaponSelected+=SelectWeapon;UI.SettingsChanged+=ApplySettings;Rig.ApplySettings(UI.Settings);

                UI.EquipRequested+=QueueEquip;UI.DropRequested+=QueueDrop;UI.CreativeItemRequested+=QueueCreativeItem;

                Lobby=new GameObject("Actual R23 menu map").AddComponent<R23Lobby>();Lobby.transform.SetParent(transform,false);Lobby.Initialize(Rig.transform,Rig.Head);Lobby.Load();Lobby.Show();Rig.SetSpawn(Lobby.Spawn,Lobby.Spawn+Vector3.forward);UI.SetMenuAnchor(Lobby.MenuAnchor);

            }

            catch(Exception e){Fail(e);}

            if(Failure!=null)yield break;

            // Present the loading screen before synchronous desktop mesh cooking.

            yield return null;

            try{UI.SetLoadingProgress("LOADING MAP",.1f);Map=new GameObject("R23 map").AddComponent<R23PlayableMap>();Map.transform.SetParent(transform,false);Map.BindViewCamera(Rig.Head);Map.SetWorldVisible(false);Map.Load();}

            catch(Exception e){Fail(e);}

            if(Failure!=null)yield break;

            while(Map.IsLoading)yield return null;

            if(!Map.IsLoaded){Fail(new Exception(Map.LoadError));yield break;}

            try

            {

                activeSpawn=Map.WarmupSpawn;Rig.Head.farClipPlane=Map.CameraFarDistance;UI.Recenter();UI.SetLoadingProgress("PREPARING EQUIPMENT",.65f);

                Calibration=new GameObject("R23 standing-height calibration").AddComponent<R23VerticalCalibration>();Calibration.transform.SetParent(transform,false);

                Calibration.Initialize(Rig.TrackingOrigin,Rig.Head,Rig.Body);Rig.Calibration=Calibration;Calibration.CalibrationChanged+=Calibrated;Calibration.SetSfxVolume(UI.Settings.SfxVolume);UI.RecalibrationRequested+=delegate{UI.ToggleMenu();Calibration.RequestRecalibration();};

                Gameplay=new GameObject("R23 hands and gameplay").AddComponent<R23HandsWeapons>();Gameplay.transform.SetParent(transform,false);Gameplay.ViewObstructed=Rig.ViewFullyObstructed;

                Gameplay.ExternalInputPump=true;Gameplay.Initialize(Rig.transform,Rig.Head,Rig.Left,Rig.Right);Gameplay.Restarted+=ResetRig;

                Gallery=new GameObject("R23 independent lobby weapon gallery").AddComponent<R23LobbyWeaponGallery>();Gallery.transform.SetParent(transform,false);Gallery.Initialize(Rig.Head,Rig.Left,Rig.Right,Rig.transform);Gallery.SetDockAnchor(Lobby.MenuAnchor);Gameplay.SetExternalHandArtReader(ReadExternalItemHandArt);UI.WeaponViewed+=ViewGalleryWeapon;

                Appearance=new GameObject("R23 selected player skin").AddComponent<R23PlayerAppearance>();Appearance.transform.SetParent(transform,false);

                Appearance.GloveMaterialChanged+=Gameplay.SetGloveMaterial;Appearance.SetHandArtPointReader(Gameplay.TryReadHandArtPoint);

                Appearance.Initialize(Rig.transform,Rig.Head.transform,Rig.Left,Rig.Right);

                var styles=new List<R23AppearanceOption>();foreach(var style in Appearance.AvailableStyles)styles.Add(new R23AppearanceOption(style.Id,style.Label,sourceIndex:style.SourceIndex));

                UI.SetAppearanceOptions(styles);UI.AppearanceSelected+=SelectAppearance;

                Preview=new GameObject("R23 single skin selector preview").AddComponent<R23AppearancePreview>();Preview.transform.SetParent(transform,false);Preview.Initialize(Rig.Head.transform);

                Preview.SetWorldAnchor(Lobby.PreviewAnchor,true);UI.AppearanceViewed+=ViewAppearance;

                Activities=new GameObject("R23 flight and construction").AddComponent<R23VRActivities>();Activities.transform.SetParent(transform,false);

                Activities.Initialize(Rig.transform,Rig.Head.transform,Rig.Left,Rig.Right,Rig.Body,Map.transform);Rig.Activities=Activities;
                // MATCH RULES HEADSHOTS / BUILD DAMAGE (offline): read the live match, else the pending warm-up selection.
                Activities.ReadBuildDamageMultiplier=delegate{return OfflineMatchRules.BuildDamage;};
                Gameplay.ReadHeadshotMultiplier=delegate{return OfflineMatchRules.Headshots;};
                Gameplay.WorldBulletHeadshot=delegate(Collider collider){return ValleyBots!=null&&ValleyBots.IsHeadCollider(collider);};

                Gameplay.WorldHit+=DamageConstructedCover;

                Gameplay.MeleeWorldTargetId=MeleeWorldTargetId;

                Gameplay.MeleeWorldInputAllowed=delegate{return Ready&&roundStarted&&!PageBlocksGameplay&&Rig.HeadCollision!=null&&Rig.HeadCollision.Clear(Rig.Head.transform.position,Vector3.zero);};

                Rig.ReadItemMovementMultiplier=delegate{return Gameplay==null?1:Gameplay.ItemMovementMultiplier;};

                ApplySettings(UI.Settings);

                Climbing=new GameObject("R23 free-hand wall climbing").AddComponent<R23VRClimbing>();Climbing.transform.SetParent(transform,false);

                Climbing.Initialize(Rig.transform,Rig.Head.transform,Rig.Left,Rig.Right,Rig.Body,Map.transform);Rig.Climbing=Climbing;

                Climbing.FlingPower=UI.Settings.FlingPower;

                Consumables=new GameObject("R23 source consumables").AddComponent<R23VRConsumables>();Consumables.transform.SetParent(transform,false);

                Consumables.Initialize(Rig.transform,Rig.Head.transform,Rig.Left,Rig.Right,this);Consumables.SetSfxVolume(UI.Settings.SfxVolume);

                Explosives=new GameObject("R23 finite frag and zone grenades").AddComponent<R23VRExplosives>();Explosives.transform.SetParent(transform,false);

                Explosives.Initialize(Rig.transform,Rig.Head.transform,Rig.Left,Rig.Right,this);Explosives.FlashSmoke.WorldAudioGain=SetFlashWorldAudio;Explosives.FlashSmoke.SfxGain=delegate{return UI.Settings.SfxVolume;};Explosives.KindAllowed=delegate(string k){var r=ActualMatchRules;return r==null||Ashfall.Integration.Playable.FlashSmoke.R25Throwables.Allowed(k,r.Flashbangs,r.SmokeGrenades);};Explosives.SetSfxVolume(UI.Settings.SfxVolume);

                Explosives.Haptic+=GrenadeHaptic;Gameplay.ConfigureSmawEffects(this);Gameplay.SmawDetonated+=delegate(R23GrenadeBlast blast){Explosives.PlaySourceVisual("smaw",new Vector3(blast.Center.X,blast.Center.Y,blast.Center.Z));};

                Supplies=new GameObject("R24 source ammo, build supplies and backpacks").AddComponent<R24VRSupplies>();Supplies.transform.SetParent(transform,false);

                Supplies.Initialize(Rig.transform,Rig.Head.transform,Rig.Left,Rig.Right,GrantWorldSupply);Supplies.WithdrawSupply=WithdrawWorldSupply;Supplies.SetSfxVolume(UI.Settings.SfxVolume);InitializePaddles();

                Consumables.MakeRoomForWorldItem=delegate(string kind,int hand){return MakeRoomForPickup(kind,false);};

                Consumables.EquipCollectedItem=delegate(string id,int hand){return TryEquipOwnedItem(id,hand-1);};

                Gameplay.CanCollectWorldWeapon=CanCollectWeapon;

                Gameplay.MakeRoomForWorldWeapon=PrepareWorldWeaponPickup;

                Gameplay.CanClaimDualCustody=CanClaimDualCustody;

                Gameplay.DualLocomotionAnchor=delegate{return Rig.LocomotionAnchorPosition;};

                Gameplay.PreferredPickupHand=UI.Settings.DominantHand;Gameplay.CanSelectWorldGrip=delegate(string id,int hand){return (WorldLoot==null||WorldLoot.AllowsWorldClaim(id))&&(MountedRacks==null||MountedRacks.AllowsWorldClaim(id))&&(!routeObjectiveInput||selectedObjective.Kind==R23GripObjectiveKind.Weapon&&selectedObjective.Id==id&&selectedObjective.Hand==hand);};Gameplay.CanClaimWorldGrip=delegate(string id,int hand){return Gameplay.CanSelectWorldGrip(id,hand)&&(LanSession==null||LanSession.AllowWeaponClaim(id,hand));};

                Consumables.CanClaimObjectiveGrip=delegate(string id,int hand){return (LanSession==null||LanSession.AllowFoodClaim(id,hand))&&(!routeObjectiveInput||selectedObjective.Hand==hand-1&&(selectedObjective.Kind==R23GripObjectiveKind.Open&&id=="open"||selectedObjective.Kind==R23GripObjectiveKind.Consumable&&id=="pickup:"+selectedObjective.Id));};

                Consumables.Equipped=delegate(int hand){StowPaddles();if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();if(Explosives!=null)Explosives.Stow();};

                Rig.BeforeHandInteractions=PrepareTrackedObjectiveGrips;

                WeaponWheel=new GameObject("R23 source held-A inventory wheel").AddComponent<R23WeaponWheel>();WeaponWheel.transform.SetParent(transform,false);WeaponWheel.Initialize(Rig.Head.transform,TryEquipOwnedItem,OpenBackpack,ToggleHeldItem,EquipPaddles);ConfigureWheelDrop();if(!WeaponWheel.Ready)throw new Exception(WeaponWheel.Failure);

                JumpPads=new GameObject("R23 authored map jump pads").AddComponent<R23JumpPads>();JumpPads.transform.SetParent(transform,false);JumpPads.Initialize(Map.MapId,Rig.transform,Map.transform,Rig.TryLaunchPad);JumpPads.SetSfxVolume(UI.Settings.SfxVolume);

                HealthStations=new GameObject("R23 source map healing contacts").AddComponent<R23HealthStations>();HealthStations.transform.SetParent(transform,false);HealthStations.Initialize(Map.MapId,Rig.transform,Map.transform,ApplyStationHealth);HealthStations.SetSfxVolume(UI.Settings.SfxVolume);

                TraversalAudio=new GameObject("R23 source traversal audio").AddComponent<R23TraversalAudio>();TraversalAudio.transform.SetParent(transform,false);TraversalAudio.Initialize(Rig);TraversalAudio.SetSfxVolume(UI.Settings.SfxVolume);

                Gameplay.SurfaceImpact+=TraversalAudio.PlayImpact;

                LanSession=new GameObject("Unity LAN connection lobby").AddComponent<R24LanSession>();LanSession.transform.SetParent(transform,false);LanSession.Initialize(this);gameObject.AddComponent<R25NetDiagnostics>();if(R25NetBot.Requested)gameObject.AddComponent<R25NetBot>();

                WorldPing=new GameObject("R23 source local ping owner").AddComponent<R23WorldPing>();WorldPing.transform.SetParent(transform,false);WorldPing.Initialize(Rig.Head.transform,Rig.transform);Rig.PingPressed+=PingWorld;

                TeamMarks=new GameObject("R23 source primary B mark wheel").AddComponent<R23TeamMarks>();TeamMarks.transform.SetParent(transform,false);TeamMarks.Initialize(Rig.Head.transform,Rig.transform);TeamMarks.SetSfxVolume(UI.Settings.SfxVolume);

                PickupHints=new GameObject("R23 source aimed pickup rings").AddComponent<R23PickupHints>();PickupHints.transform.SetParent(transform,false);PickupHints.Initialize(Rig.Head.transform);

                WorldLoot=new GameObject("R23 source map weapon caches").AddComponent<R23WorldLoot>();WorldLoot.transform.SetParent(transform,false);WorldLoot.Initialize(Map,Gameplay,Explosives,Rig.transform);

                InitializeTeamLaunch();InitializeMatch();InitializeArchiveItems();InitializeSourceTraining();InitializeWorldScores();InitializeTacticalMap();InitializeValleyBots();InitializeSoundscape();

            }

            catch(Exception e){Fail(e);}

            if(Failure!=null)yield break;

            float until=Time.realtimeSinceStartup+90;

            while((!Gameplay.Ready||!Consumables.Ready||!Paddles.Ready||!Supplies.Ready||!JumpPads.Ready||!Lobby.Ready||!WorldLoot.Ready||!Explosives.Ready||!MountedRacks.Ready)&&Time.realtimeSinceStartup<until&&Consumables.Failure==null&&Paddles.Failure==null&&Supplies.Failure==null&&JumpPads.Failure==null&&Lobby.Failure==null&&WorldLoot.Failure==null&&Explosives.Failure==null&&MountedRacks.Failure==null)yield return null;

            if(!Gameplay.Ready){Fail(new Exception("Source hand and weapon loading timed out"));yield break;}

            if(!Consumables.Ready){Fail(new Exception(Consumables.Failure??"Source consumable loading timed out"));yield break;}

            if(!Paddles.Ready){Fail(new Exception(Paddles.Failure??"Source paddles loading timed out"));yield break;}

            if(!Supplies.Ready){Fail(new Exception(Supplies.Failure??"Source supply loading timed out"));yield break;}

            if(!JumpPads.Ready){Fail(new Exception(JumpPads.Failure??"Source authored pad loading timed out"));yield break;}

            if(!Lobby.Ready){Fail(new Exception(Lobby.Failure??"Source lobby loading timed out"));yield break;}

            if(!WorldLoot.Ready){Fail(new Exception(WorldLoot.Failure??"Source cache loading timed out"));yield break;}

            if(!MountedRacks.Ready){Fail(new Exception(MountedRacks.Failure??"Source mounted rack loading timed out"));yield break;}

            if(!Explosives.Ready){Fail(new Exception(Explosives.Failure??"Source grenade assets loading timed out"));yield break;}

            Gameplay.SetPracticeTargetsVisible(false);Gameplay.SetPickupHome(activeSpawn+Vector3.up*1.3f,Quaternion.identity);

            SpawnConsumables();Consumables.SetWorldVisible(false);if(Explosives!=null)Explosives.SetWorldVisible(false);if(Supplies!=null)Supplies.SetWorldVisible(false);RefreshLoadout();

            var catalog=new List<string>(Gallery.AvailableWeaponIds);catalog.Sort(delegate(string a,string b){return R23WeaponPickupRules.CatalogIndex(a).CompareTo(R23WeaponPickupRules.CatalogIndex(b));});

            var options=new List<R23WeaponOption>();foreach(string id in catalog)options.Add(new R23WeaponOption(id,R23HandsWeapons.WeaponDisplayName(id)));UI.SetWeaponOptions(options);

            UI.SetLoadingProgress("READY",1);Ready=true;UI.ShowReady();Debug.Log("R23 VR map loop ready; select START in headset menu");

        }

        void Fail(Exception error){Failure=error.ToString();Debug.LogException(error);if(UI!=null)UI.ShowFailure(error.Message);}

        void QueueReload(){reloadRequested=true;}

        bool ApplyStationHealth(){if(Gameplay==null||Gameplay.Health<=0)return false;float health=Gameplay.MaxHealth-Gameplay.Health,shield=Gameplay.MaxShield-Gameplay.Shield;if(health<=0&&shield<=0)return false;return Gameplay.ApplyPlayerGain(Mathf.Max(0,health),Mathf.Max(0,shield));}

        int ConsumableSlotCount {get{var slots=new HashSet<int>();if(Consumables!=null)foreach(var item in Consumables.OwnedItems)slots.Add(item.Slot);return slots.Count;}}

        int ExplosiveSlotCount {get{var slots=new HashSet<int>();if(Explosives!=null)foreach(var item in Explosives.OwnedItems)slots.Add(item.Slot);return slots.Count;}}

        void RefreshSlotBudget(){Gameplay.SlotCapacity=InventoryCapacity;Consumables.SlotCapacity=InventoryCapacity;Consumables.SetOtherSlotsUsed(Gameplay.OwnedWeaponActionIds.Length+ExplosiveSlotCount);if(Explosives!=null){Explosives.SlotCapacity=InventoryCapacity;Explosives.SetOtherSlotsUsed(Gameplay.OwnedWeaponActionIds.Length+ConsumableSlotCount);}}

        bool CanCollectWeapon(){return Networking.R26InventorySlots.HasRoom(InventoryCapacity,Gameplay.OwnedWeaponActionIds.Length,ConsumableSlotCount,ExplosiveSlotCount);}

        int GrantWorldSupply(string receipt,string family,int amount)

        {

            if(!Ready||!roundStarted||Gameplay.Health<=0)return 0;if(LanSession!=null&&!LanSession.AuthorizeSupply(receipt,family,amount))return 0;int accepted;

            // Host receipt retires the local box; SyncResources alone projects its balance.
            if(LanSession!=null&&LanSession.Lobby!=null){accepted=amount;}else if(family=="backpack"){if(InventoryCapacity>=6||amount!=1)return 0;inventoryCapacity++;RefreshSlotBudget();accepted=1;}

            else accepted=family=="builds"?Activities.GrantSupply(receipt,amount):Gameplay.ApplyReserveSupply(receipt,family,amount);

            if(accepted>0)RefreshLoadout();return accepted;

        }

        int WithdrawWorldSupply(string family,int amount){if(LanSession!=null&&LanSession.Lobby!=null)return 0;return family=="builds"?Activities.WithdrawSupply(amount):Gameplay.WithdrawReserveSupply(family,amount);}

        bool IncomingFits(string incoming,bool weapon,bool explosive){return weapon?CanCollectWeapon():explosive?Explosives.CanStore(incoming):Consumables.CanStore(incoming);}

        bool catalogPickup;
        bool MakeRoomForPickup(string incoming,bool weapon,bool explosive=false)

        {

            if(!Ready||!roundStarted||switchingMap||Gameplay.Health<=0||PageBlocksGameplay||BackpackOpen&&!catalogPickup)return false;

            RefreshSlotBudget();if(IncomingFits(incoming,weapon,explosive))return true;

            if(Explosives!=null&&Explosives.HeldId!=null)

            {

                int slot=-1;foreach(var item in Explosives.OwnedItems)if(item.Id==Explosives.HeldId)slot=item.Slot;if(slot<0)return false;

                var members=new List<R23OwnedGrenade>();foreach(var item in Explosives.OwnedItems)if(item.Slot==slot)members.Add(item);

                var positions=new Vector3[members.Count];var rotations=new Quaternion[members.Count];

                for(int i=0;i<members.Count;i++){Bounds bounds;if(!Explosives.GetDropBounds(members[i].Id,out bounds)||!R23WorldItemPlacement.TryDrop(Rig.Head.transform,Rig.transform,bounds,out positions[i],out rotations[i]))return false;}

                if(!Explosives.DropSlot(slot,positions,rotations))return false;

            }

            else if(Consumables.HeldId!=null)

            {

                int slot=-1;foreach(var item in Consumables.OwnedItems)if(item.Id==Consumables.HeldId)slot=item.Slot;if(slot<0)return false;

                var members=new List<R23OwnedConsumable>();foreach(var item in Consumables.OwnedItems)if(item.Slot==slot)members.Add(item);

                var positions=new Vector3[members.Count];var rotations=new Quaternion[members.Count];

                for(int i=0;i<members.Count;i++)if(!R23WorldItemPlacement.TryDrop(Rig.Head.transform,Rig.transform,Consumables.GetDropBounds(members[i].Id),out positions[i],out rotations[i]))return false;

                if(!Consumables.DropSlot(slot,positions,rotations))return false;

            }

            else

            {

                string id=Gameplay.ActiveWeaponActionId;Bounds bounds;Vector3 position;Quaternion rotation;

                if(!Gameplay.OwnsWeapon(id)||!Gameplay.TryGetWeaponBounds(id,out bounds)||!R23WorldItemPlacement.TryDrop(Rig.Head.transform,Rig.transform,bounds,out position,out rotation)||!Gameplay.DropOwned(id,position,rotation))return false;

            }

            RefreshSlotBudget();RefreshLoadout();return IncomingFits(incoming,weapon,explosive);

        }

        void QueueEquip(string id,int hand){queuedEquip=id;queuedHand=hand;if(roundStarted)UI.SetPlaying();else UI.SetLoadoutStatus("SELECTED / RETURN AND PRESS START");}

        void QueueDrop(string id){if(!roundStarted){UI.SetLoadoutStatus("PRESS START BEFORE DROPPING INTO THE MAP");return;}queuedDrop=id;if(!id.StartsWith("reserve/",StringComparison.Ordinal))UI.SetPlaying();}

        // The rules offline damage multipliers read: the running match, else what the Match Rules page shows.
        internal Ashfall.Integration.Playable.Match.R24PlayableMatchRules OfflineMatchRules{get{return ActualMatchRules??UI.SelectedMatchRules??Ashfall.Integration.Playable.Match.R24PlayableMatchRules.ForPreset("team_revive",Map==null?"range":Map.MapId);}}
        public R23ConsumableVitals ReadVitals(){return new R23ConsumableVitals{Health=Gameplay==null?0:Gameplay.Health,Shield=Gameplay==null?0:Gameplay.Shield,Stamina=Rig==null?0:Rig.Stamina,MaxStamina=Rig==null?6:Rig.StaminaMaximum,Alive=Gameplay!=null&&Gameplay.Health>0};}

        public bool TryApplyGain(float health,float shield,bool refillStamina)

        {

            if(Gameplay==null||Gameplay.Health<=0||float.IsNaN(health)||float.IsInfinity(health)||float.IsNaN(shield)||float.IsInfinity(shield)||health<0||shield<0)return false;

            if(LanSession!=null&&LanSession.Lobby!=null)return false;
            bool gained=(health>0||shield>0)&&Gameplay.ApplyPlayerGain(health,shield);

            if(refillStamina&&Rig!=null&&Rig.Stamina<Rig.StaminaMaximum)gained|=Rig.RefillStamina()>0;

            return gained;

        }

        public R23ExplosiveTarget[] ReadDamageTargets()

        {

            var targets=new List<R23ExplosiveTarget>();if(Gameplay==null||!roundStarted)return targets.ToArray();

            for(int i=0;i<Gameplay.PracticeTargetCount;i++){string id;Vector3 at;if(Gameplay.TryReadPracticeDamageTarget(i,out id,out at))targets.Add(new R23ExplosiveTarget(id,new Ashfall.Core.Float3(at.x,at.y,at.z)));}

            if(ValleyBots!=null)ValleyBots.AppendTargets(targets);if(Activities!=null)foreach(var panel in Activities.PanelSnapshot)targets.Add(new R23ExplosiveTarget("r23-build/"+panel.Id,panel.Shape.Center));return targets.ToArray();

        }

        public bool ApplyTargetDamage(string id,float amount,Ashfall.Core.Float3 origin)

        {

            if(!roundStarted||Gameplay==null||!origin.IsFinite)return false;

            if(ValleyBots!=null&&ValleyBots.ApplyTargetDamage(id,amount))return true;if(id!=null&&id.StartsWith("r23-build/",StringComparison.Ordinal))return Activities!=null&&Activities.TryDamagePanel(id.Substring(10),amount);

            return Gameplay.ApplyTargetDamage(id,amount,new Vector3(origin.X,origin.Y,origin.Z));

        }

        public bool ApplyPlayerDamage(float amount,Ashfall.Core.Float3 origin)

        {

            if(!roundStarted||Gameplay==null||!origin.IsFinite||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0)return false;

            float before=Gameplay.Health+Gameplay.Shield;Gameplay.ApplyPlayerDamage(amount,new Vector3(origin.X,origin.Y,origin.Z));return Gameplay.Health+Gameplay.Shield<before;

        }

        void GrenadeHaptic(int hand,float amplitude,float seconds)

        {

            if(Rig==null||!Rig.Tracking||!Rig.SessionFocused||hand<1||hand>2)return;

            var device=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(hand==1?UnityEngine.XR.XRNode.LeftHand:UnityEngine.XR.XRNode.RightHand);UnityEngine.XR.HapticCapabilities capabilities;

            if(device.isValid&&device.TryGetHapticCapabilities(out capabilities)&&capabilities.supportsImpulse&&capabilities.numChannels>0)device.SendHapticImpulse(0,amplitude,seconds);

        }

        void SpawnConsumables()

        {

            // A true checkpoint reset/map transition retires old dropped supply

            // displays. Ordinary return-to-menu/resume preserves those records.

            if(Supplies!=null){foreach(string id in activeSupplyIds)Supplies.SetWorldItemVisible(id,false);activeSupplyIds.Clear();}

            if(Gameplay!=null)Gameplay.ResetPracticeTargets();ResetSourceTrainingCheckpoint();

            EnsureCheckpointPickups(true);

        }

        void EnsureCheckpointPickups(bool force)

        {

            if(Map==null||!Map.IsLoaded||Consumables==null||Supplies==null)return;

            if(!force&&sourcePickupLayoutPrepared&&sourcePickupMap==Map.MapId&&sourcePickupOrigin==activeSpawn)return;

            if(ArchivePickups!=null)ArchivePickups.LeaveArchive();archivePickupReset=true;

            var capturedMap=Map;string mapId=Map.MapId;

            Func<bool> collisionReady=delegate{return this!=null&&capturedMap!=null&&Map==capturedMap&&Ready&&roundStarted&&!switchingMap&&capturedMap.IsLoaded&&capturedMap.MapId==mapId;};

            Consumables.EnsureSourceCheckpointLayout(activeSpawn,collisionReady);

            Supplies.EnsureSourceCheckpointLayout(activeSpawn,collisionReady);

            sourcePickupMap=mapId;sourcePickupOrigin=activeSpawn;sourcePickupLayoutPrepared=true;

        }

        void RefreshLoadout()

        {

            var owned=new List<string>(Gameplay.OwnedWeaponActionIds);owned.Sort(CompareSourceOwnedWeapons);

            var options=new List<R23LoadoutOption>();foreach(string id in owned){var state=Gameplay.ReadWeaponState(id);bool equipped=Gameplay.HasWeapon&&Gameplay.ActiveWeaponActionId==id;

                string profile=Gameplay.ResolveWeaponProfile(id);R23WeaponInstanceInfo instance;Gameplay.TryReadWeaponInstance(id,out instance);int capacity=instance==null?R23HandsWeapons.WeaponCapacity(profile):instance.Capacity;string rarity=instance!=null&&instance.Stars>1?" "+new string('\u2605',instance.Stars):"";

                options.Add(new R23LoadoutOption(id,R23HandsWeapons.WeaponDisplayName(profile)+rarity,state.Loaded+" / "+capacity+" | "+R23HandsWeapons.WeaponAmmoFamily(profile)+" reserve "+Gameplay.ReadReserve(id),1,equipped,Gameplay.PrimaryHand,profileId:profile,stars:instance==null?1:instance.Stars));}

            if(Consumables!=null){var slots=new HashSet<int>();foreach(var item in Consumables.OwnedItems){if(!slots.Add(item.Slot))continue;int count=0;foreach(var other in Consumables.OwnedItems)if(other.Slot==item.Slot)count++;

                bool equipped=Consumables.HeldId==item.Id;options.Add(new R23LoadoutOption(item.Id,item.Kind.ToUpperInvariant(),equipped?Consumables.Status:"GRIP: EQUIP / TRIGGER: DROP",count,equipped,equipped?Consumables.HeldHand-1:1,profileId:item.Kind));}}

            if(Explosives!=null){var slots=new HashSet<int>();foreach(var item in Explosives.OwnedItems){if(!slots.Add(item.Slot))continue;int count=0;foreach(var other in Explosives.OwnedItems)if(other.Slot==item.Slot)count++;bool equipped=Explosives.HeldId==item.Id;options.Add(new R23LoadoutOption(item.Id,item.Label,"",count,equipped,equipped?Explosives.HeldHand-1:1,profileId:item.Kind));}}

            UI.SetCreativeCatalog(CreativeCatalogAllowed);UI.SetOwnedLoadout(options,InventoryCapacity);

            UI.SetOwnedBackpacks(InventoryCapacity-3,CanCollectWeapon());

            var resources=new List<R23BackpackResource>();foreach(string family in new[]{"pistol","smg","rifle","shells","sniper"})resources.Add(new R23BackpackResource(family,Gameplay.ReadAmmoReserveFamily(family),true));

            resources.Add(new R23BackpackResource("builds",Activities==null?0:Activities.Balance,true));UI.SetBackpackResources(resources);

            if(WeaponWheel!=null){var slots=new List<R23WeaponWheelSlot>();foreach(var option in options)slots.Add(new R23WeaponWheelSlot(option.Id,option.Label,option.ProfileId,option.Detail,option.CanEquip,option.Stars,option.Count,WheelItemDroppable(option.Id)));WeaponWheel.SetInventorySlots(InventoryCapacity,slots.ToArray());}

        }

        static int SourceWeaponCategory(string id)

        {

            switch(R23WeaponPickupRules.CatalogIndex(id)){case 14:case 15:case 16:case 29:return 0;case 1:case 4:case 9:case 12:return 1;case 2:case 7:case 19:case 20:return 2;case 6:case 10:case 11:case 17:case 18:return 3;case 0:case 3:case 8:case 21:case 23:case 24:return 4;case 5:case 13:case 25:return 5;default:return 6;}

        }

        static float SourceWeaponSortDamage(string id)

        {

            if(id=="m1014")return R23M1014Definition.PelletDamage;

            if(id=="dt11")return R24Dt11Definition.PelletDamage;

            if(id=="magnum")return R23MagnumRuntime.BodyDamage;

            var profile=Ashfall.Modules.WeaponHandling.SourceRifleDefinition.ForId(id);return profile==null?0:profile.BodyDamage;

        }

        int CompareSourceOwnedWeapons(string a,string b)

        {

            string pa=Gameplay.ResolveWeaponProfile(a)??a,pb=Gameplay.ResolveWeaponProfile(b)??b;

            int category=SourceWeaponCategory(pa).CompareTo(SourceWeaponCategory(pb));if(category!=0)return category;

            R23WeaponInstanceInfo ia,ib;Gameplay.TryReadWeaponInstance(a,out ia);Gameplay.TryReadWeaponInstance(b,out ib);

            float da=ia==null?SourceWeaponSortDamage(pa):ia.BodyDamage/(pa=="m1014"||pa=="dt11"?8:1),db=ib==null?SourceWeaponSortDamage(pb):ib.BodyDamage/(pb=="m1014"||pb=="dt11"?8:1);

            if(Mathf.Approximately(da,db))return R23WeaponPickupRules.CatalogIndex(pa).CompareTo(R23WeaponPickupRules.CatalogIndex(pb));return db.CompareTo(da);

        }

        void ApplyLoadoutRequests()

        {

            ApplyBackpackDrop();ApplyCreativeItem();PumpWheelItemDrop();

            if(queuedEquip!=null){string id=queuedEquip;queuedEquip=null;bool okay=false;

                okay=TryEquipOwnedItem(id,Gameplay.PrimaryHand);

                UI.SetLoadoutStatus(okay?"EQUIPPED":"EQUIP REFUSED / FREE THE REQUESTED HAND");}

            if(queuedDrop!=null){string id=queuedDrop;queuedDrop=null;if(id.StartsWith("reserve/",StringComparison.Ordinal)){DropWorldReserve(id.Substring(8));return;}Bounds bounds=new Bounds();bool weapon=Gameplay.OwnsWeapon(id),explosive=OwnsExplosive(id);bool found=weapon?Gameplay.TryGetWeaponBounds(id,out bounds):explosive?Explosives.GetDropBounds(id,out bounds):true;if(!weapon&&!explosive)bounds=Consumables.GetDropBounds(id);

                Vector3 position;Quaternion rotation;bool okay=found&&R23WorldItemPlacement.TryDrop(Rig.Head.transform,Rig.transform,bounds,out position,out rotation)&&(weapon?Gameplay.DropOwned(id,position,rotation):explosive?Explosives.Drop(id,position,rotation):Consumables.Drop(id,position,rotation));

                UI.SetLoadoutStatus(okay?"DROPPED WITH AMMO PRESERVED":"DROP REFUSED / NO REACHABLE CLEAR GROUND");}

        }

        void DropWorldReserve(string family)

        {

            Vector3 position=Vector3.zero;Quaternion rotation=Quaternion.identity;string id;bool okay=Supplies!=null&&R23WorldItemPlacement.TryDrop(Rig.Head.transform,Rig.transform,Supplies.GetDropBounds(family),out position,out rotation);

            if(okay&&LanSession!=null&&LanSession.Lobby!=null){bool queued=LanSession.QueueReserveDrop(family,position);UI.SetLoadoutStatus(queued?"DROP REQUESTED / WAITING FOR HOST":"DROP REFUSED");return;}
            if(okay){Supplies.UpdateInputAvailability(SupplyInput(true,Rig.LeftInput,Rig.RightInput));okay=Supplies.TryDropReserve(family,position,rotation,out id);if(okay)activeSupplyIds.Add(id);}

            UI.SetLoadoutStatus(okay?"DROPPED":"DROP REFUSED / NO REACHABLE CLEAR GROUND");RefreshLoadout();

        }

        bool OwnsExplosive(string id){if(Explosives!=null)foreach(var item in Explosives.OwnedItems)if(item.Id==id)return true;return false;}

        bool PairedWeapon(string id){R23WeaponInstanceInfo info;return R23HandsWeapons.WeaponClaimsBothHands(Gameplay.TryReadWeaponInstance(id,out info)?info.ProfileId:id);}

        bool CanClaimDualCustody(){return Ready&&!PlatformUnarmed&&roundStarted&&!switchingMap&&!PageBlocksGameplay&&Gameplay.Health>0&&!PaddleHands&&!Climbing.LeftAnchored&&!Climbing.RightAnchored;}

        bool PrepareWorldWeaponPickup(string id,int hand)

        {

            // Source make_room precedes _clear_consumable_selection. A refused

            // pickup must not stow another item; accepted paired guns need both

            // hands before their real owner is equipped.

            if(!MakeRoomForPickup(id,true))return false;

            if(PairedWeapon(id)){Consumables.Stow();if(Explosives!=null)Explosives.Stow();Gameplay.UpdateInputAvailability(AvailabilityFrame());}

            return true;

        }

        R23HandsInputFrame AvailabilityFrame(){return new R23HandsInputFrame{LeftTracked=Rig.LeftInput.Tracked,RightTracked=Rig.RightInput.Tracked,LeftHandReserved=PaddleHands||Climbing.LeftAnchored||Consumables.LeftOccupied||Explosives!=null&&Explosives.LeftOccupied,RightHandReserved=PaddleHands||Climbing.RightAnchored||Consumables.RightOccupied||Explosives!=null&&Explosives.RightOccupied};}

        void SampleConsumableAvailability(){Consumables.Sample(new R23ConsumableSample{DeltaSeconds=.00001f,Enabled=true,LeftReady=Rig.LeftInput.Tracked,RightReady=Rig.RightInput.Tracked,LeftFree=!Gameplay.LeftHandOccupied&&!Climbing.LeftAnchored,RightFree=!Gameplay.RightHandOccupied&&!Climbing.RightAnchored});}

        bool TryEquipOwnedItem(string id,int hand)

        {

            if(!Ready||!roundStarted||PlatformUnarmed||switchingMap||Gameplay.Health<=0||hand<0||hand>1||!(hand==0?Rig.LeftInput.Tracked:Rig.RightInput.Tracked)||(hand==0?Climbing.LeftAnchored:Climbing.RightAnchored))return false;

            bool weapon=Gameplay.OwnsWeapon(id),explosive=OwnsExplosive(id),owned=weapon||explosive;foreach(var item in Consumables.OwnedItems)owned|=item.Id==id;if(!owned)return false;

            if(weapon&&PairedWeapon(id)&&(!Rig.LeftInput.Tracked||!Rig.RightInput.Tracked||Climbing.LeftAnchored||Climbing.RightAnchored))return false;

            bool previousPaddles=PaddleHands;StowPaddles();string previousSupply=Consumables.HeldId,previousGun=Gameplay.HasWeapon?Gameplay.ActiveWeaponActionId:null;int previousSupplyHand=Consumables.HeldHand,previousGunHand=Gameplay.PrimaryHand;

            string previousGrenade=Explosives==null?null:Explosives.HeldId;int previousGrenadeHand=Explosives==null?0:Explosives.HeldHand;

            Consumables.Stow();if(Explosives!=null)Explosives.Stow();if(!weapon&&Gameplay.HasWeapon)Gameplay.HolsterCurrent();Gameplay.UpdateInputAvailability(AvailabilityFrame());

            bool okay;if(weapon)okay=Gameplay.EquipOwned(id,hand);else if(explosive)okay=Explosives.TryEquip(id,hand+1,true);else{SampleConsumableAvailability();okay=Consumables.Equip(id,hand+1);}

            if(!okay){if(previousPaddles)Paddles.Equip(true);if(previousGun!=null){Gameplay.UpdateInputAvailability(AvailabilityFrame());Gameplay.EquipOwned(previousGun,previousGunHand);}if(previousSupply!=null){SampleConsumableAvailability();Consumables.Equip(previousSupply,previousSupplyHand);}if(previousGrenade!=null)Explosives.TryEquip(previousGrenade,previousGrenadeHand,true);}

            return okay;

        }

        void OpenBackpack(){if(Ready&&roundStarted&&!PlatformUnarmed){UI.OpenLoadout();Rig.GameplayBlocked=false;}}

        void ToggleHeldItem(){if(!Ready||!roundStarted||PlatformUnarmed)return;if(PaddleHands)StowPaddles();else if(Explosives!=null&&Explosives.HeldId!=null)Explosives.Stow();else if(Consumables.HeldId!=null)Consumables.Stow();else if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();else TryEquipOwnedItem(Gameplay.ActiveWeaponActionId,Gameplay.PrimaryHand);}

        void ViewAppearance(string id){if(Preview!=null&&Lobby!=null&&Lobby.Visible&&UI.Page==R23UiPage.Appearance&&id!=null)Preview.Show(id);}

        void PingWorld(){if(WorldPing==null||!Ready||!roundStarted||PageBlocksGameplay||BackpackOpen||WeaponWheel!=null&&WeaponWheel.Active||!Rig.SessionFocused||!Rig.Tracking||Gameplay.Health<=0)return;var hand=Gameplay.PrimaryHand==0?Rig.Right:Rig.Left;var ray=new Ray(hand.position,R23TrackedGripPose.Forward(hand));if(TeamMarks!=null&&TeamMarks.Ready)TeamMarks.TryPlace(ray);else{WorldPing.Tick(0,true);WorldPing.TryPlace(ray);}}

        void ClearLocalEffects(){ClearCombatFeedback();if(TacticalMap!=null)TacticalMap.Close();queuedBackpackDrop=false;StowPaddles();objectiveGrips.Reset();selectedObjective=default(R23GripObjective);if(Rig!=null){Rig.LeftObjectiveGripReserved=Rig.RightObjectiveGripReserved=false;}if(PickupHints!=null)PickupHints.ClearAll();if(TeamMarks!=null)TeamMarks.ClearAll();if(Rig!=null)Rig.ReservePrimaryMarkTurn=false;if(WorldPing!=null)WorldPing.ClearAll();if(TraversalAudio!=null)TraversalAudio.ResetRound();if(Supplies!=null)Supplies.Suspend();if(Explosives!=null){Explosives.Suspend();Explosives.ClearProjectiles();}if(Gameplay!=null){Gameplay.ClearImpactMarks();Gameplay.ClearSmawProjectiles();Gameplay.SetFreeHandContactOffsets(Vector3.zero,Vector3.zero);}if(Appearance!=null)Appearance.SetFreeHandPresentationOffsets(Vector3.zero,Vector3.zero);}

        void PrepareTrackedObjectiveGrips()

        {

            bool enabled=Ready&&roundStarted&&!switchingMap&&!PageBlocksGameplay&&!BackpackOpen&&Gameplay.Health>0&&Rig.Tracking&&Rig.SessionFocused&&!Rig.Paused;

            if(WeaponWheel!=null)enabled&=!WeaponWheel.Active&&(Activities.BuildActive||!R23InputProjection.WheelHeld(Rig.LeftInput,Rig.RightInput,Gameplay.PrimaryHand));

            if(TeamMarks!=null)enabled&=!TeamMarks.InputHeld;

            PrepareObjectiveGrips(Rig.LeftInput,Rig.RightInput,enabled,Rig.LeftInputReady,Rig.RightInputReady);

            Rig.GrappleHands=Gameplay;Rig.ReadTraversalRules=()=>ActualMatchRules;Rig.TraversalInputAllowed=enabled;
            Rig.ConstructionInputAllowed=R23BuildInputRules.Eligible(enabled&&!TacticalMapOpen,Gameplay.IsHolstered,

                Explosives!=null&&Explosives.HeldHand!=0,

                !Gameplay.ReloadHandOccupied,

                Climbing.Mantling,true);

        }

        public void PrepareObjectiveGrips(HandSample left,HandSample right,bool enabled,bool leftReady,bool rightReady)

        {

            var targets=new List<R23GripObjective>();

            if(enabled&&!PaddleHands&&Gameplay!=null&&Consumables!=null)

            {

                // The source objective transfer can replace the item in its own

                // holding hand. Keep real climb/reload ownership; ordinary held

                // items reserve firing/support only at the later dispatch.

                Gameplay.UpdateInputAvailability(new R23HandsInputFrame{LeftTracked=leftReady&&left.Tracked,RightTracked=rightReady&&right.Tracked,LeftGrip=left.Grip,RightGrip=right.Grip,LeftHandReserved=Climbing.LeftAnchored,RightHandReserved=Climbing.RightAnchored});

                Consumables.UpdateInputAvailability(ConsumableInput(.00001f,true,left,right,false,false));

                if(Explosives!=null)Explosives.UpdateInputAvailability(ExplosiveInput(enabled,left,right));

                if(Supplies!=null)Supplies.UpdateInputAvailability(SupplyInput(enabled,left,right));

                for(int hand=0;hand<2;hand++)

                {

                    string id;Vector3 center;float score;

                    // World pickups belong to the configured weapon hand. The

                    // support hand may still open a held cap/peel, reload or climb.

                    if(Gameplay.TryReadAimedWorldWeapon(hand,out id,out center,out score))targets.Add(new R23GripObjective(R23GripObjectiveKind.Weapon,id,hand,Vector3.Distance((hand==0?Rig.Left:Rig.Right).position,center)));

                    if(Consumables.TryReadObjectiveGrip(hand+1,out id,out center,out score))targets.Add(new R23GripObjective(id=="open"?R23GripObjectiveKind.Open:R23GripObjectiveKind.Consumable,id,hand,score));

                    if(Explosives!=null&&Explosives.TryReadAimedWorldItem(hand+1,out id,out center,out score))targets.Add(new R23GripObjective(R23GripObjectiveKind.Explosive,id,hand,score));

                    if(Supplies!=null&&Supplies.TryReadObjectiveGrip(hand+1,out id,out center,out score))targets.Add(new R23GripObjective(R23GripObjectiveKind.Supply,id,hand,score));

                    if(ValleyMission!=null&&!Gameplay.ReloadClaimsHand(hand)&&!(hand==0?Climbing.LeftAnchored:Climbing.RightAnchored)&&ValleyMission.TryTarget((hand==0?Rig.Left:Rig.Right).position,out id,out center,out score))targets.Add(new R23GripObjective(R23GripObjectiveKind.Station,id,hand,score));

                }

            }

            selectedObjective=objectiveGrips.Step(enabled,leftReady&&left.Tracked,rightReady&&right.Tracked,left.Grip,right.Grip,targets.ToArray());

            if(Rig!=null){Rig.LeftObjectiveGripReserved=objectiveGrips.Reserved(0)||enabled&&Gameplay.ReloadClaimsHand(0);Rig.RightObjectiveGripReserved=objectiveGrips.Reserved(1)||enabled&&Gameplay.ReloadClaimsHand(1);}

            if(enabled&&selectedObjective.Kind==R23GripObjectiveKind.Station&&ValleyMission!=null&&ValleyMission.Activate(selectedObjective.Id,(selectedObjective.Hand==0?Rig.Left:Rig.Right).position))BeginValleyChapter();

        }

        R23ConsumableSample ConsumableInput(float seconds,bool enabled,HandSample left,HandSample right,bool leftUi,bool rightUi)

        {

            return new R23ConsumableSample{DeltaSeconds=seconds,Enabled=enabled,LeftReady=left.Tracked,RightReady=right.Tracked,LeftFree=!leftUi&&Gameplay.WorldPickupHandEligible(0)&&!Climbing.LeftAnchored,RightFree=!rightUi&&Gameplay.WorldPickupHandEligible(1)&&!Climbing.RightAnchored,LeftGrip=left.Grip,RightGrip=right.Grip};

        }

        R24SupplyInputFrame SupplyInput(bool enabled,HandSample left,HandSample right)

        {return new R24SupplyInputFrame{Enabled=enabled&&Ready&&roundStarted,LeftEligible=left.Tracked&&!Climbing.LeftAnchored&&Gameplay.WorldPickupHandEligible(0),RightEligible=right.Tracked&&!Climbing.RightAnchored&&Gameplay.WorldPickupHandEligible(1)};}

        R23ExplosiveInputFrame ExplosiveInput(bool enabled,HandSample left,HandSample right,bool itemUi=false,bool leftUi=false,bool rightUi=false)

        {

            // The gun consumer reserves an equipped grenade's hand. That reservation

            // must not feed back and disable the grenade which owns the same hand.

            bool ownLeft=Explosives!=null&&Explosives.LeftOccupied,ownRight=Explosives!=null&&Explosives.RightOccupied;

            return new R23ExplosiveInputFrame{Enabled=enabled,LeftReady=left.Tracked&&!itemUi&&!leftUi&&!Climbing.LeftAnchored&&(ownLeft||Gameplay.WorldPickupHandEligible(0)),RightReady=right.Tracked&&!itemUi&&!rightUi&&!Climbing.RightAnchored&&(ownRight||Gameplay.WorldPickupHandEligible(1)),LeftTrigger=left.Trigger,RightTrigger=right.Trigger};

        }

        void ClaimSelectedGrenade()

        {

            if(Explosives==null||selectedObjective.Kind!=R23GripObjectiveKind.Explosive)return;

            string id;Vector3 center;float distance;R23GrenadeItemState item;

            if(!Explosives.TryReadAimedWorldItem(selectedObjective.Hand+1,out id,out center,out distance)||id!=selectedObjective.Id||!Explosives.TryReadItem(id,out item)||!Explosives.CanReceivePickup(UI.Settings.DominantHand+1)||!MakeRoomForPickup(item.Kind,false,true))return;

            if(Explosives.TryPickupToHand(id,selectedObjective.Hand+1,UI.Settings.DominantHand+1,true)){Consumables.Stow();if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();RefreshSlotBudget();}

        }

        public void RefreshWorldPickupHints(bool enabled)

        {

            if(PickupHints==null)return;

            if(!enabled||!Ready){PickupHints.ClearAll();return;}

            PickupHints.Present(true,PickupHint(0),PickupHint(1));

        }

        R23PickupHintTarget PickupHint(int hand)

        {

            var target=default(R23PickupHintTarget);if(hand!=Gameplay.PrimaryHand)return target;Vector3 center;string id;float score;

            Transform pose=hand==0?Rig.Left:Rig.Right;float distance=float.PositiveInfinity;

            if(Gameplay.TryReadAimedWorldWeapon(hand,out id,out center,out score)){string profile=Gameplay.ResolveWeaponProfile(id)??id;R23WeaponInstanceInfo instance;Gameplay.TryReadWeaponInstance(id,out instance);target=new R23PickupHintTarget("weapon/"+id,R23InventoryPresentation.Caption(profile,R23HandsWeapons.WeaponDisplayName(profile),instance==null?1:instance.Stars),center);distance=Vector3.Distance(pose.position,center);}

            if(Consumables.TryReadAimedWorldItem(hand+1,out id,out center,out score)&&score<distance)

            {var item=Consumables.InspectItem(id);if(item!=null){target=new R23PickupHintTarget("consumable/"+id,item.Kind=="soda"?"SHIELD SODA":item.Kind=="shaker"?"SHIELD SHAKER":"BANANA",center);distance=score;}}

            if(Explosives!=null&&Explosives.TryReadAimedWorldItem(hand+1,out id,out center,out score)&&score<distance){R23GrenadeItemState item;if(Explosives.TryReadItem(id,out item)){target=new R23PickupHintTarget("grenade/"+id,R23GrenadeState.KindLabel(item.Kind),center);distance=score;}}

            if(Supplies!=null&&Supplies.TryReadObjectiveGrip(hand+1,out id,out center,out score)&&score<distance){R24SupplyItem item;if(Supplies.TryReadItem(id,out item))target=new R23PickupHintTarget("supply/"+id,R24SupplyState.Label(item.Family),center);}

            return target;

        }

        void Calibrated(R23CalibrationReceipt receipt){Rig.AcceptCalibratedTrackingOrigin();}

        void PositionPracticeTargets()

        {

            // main._begin_chapter creates ONE reusable dummy at this authored

            // checkpoint offset. No camera yaw, floor projection or extra row.

            Gameplay.RepositionTrainingTargets(new[]{activeSpawn+Map.transform.TransformVector(new Vector3(-3,-1,6))},Map.transform.rotation);

        }

        void ApplySettings(R23UiSettings value)

        {

            Rig.ApplySettings(value);if(Soundscape!=null)Soundscape.SetVolumes(value.MusicVolume,value.AmbienceVolume);if(ValleyBots!=null){ValleyBots.SetSfxVolume(value.SfxVolume);ValleyBots.SetDifficulty(value.BotDifficulty);}if(Climbing!=null)Climbing.FlingPower=value.FlingPower;

            if(Calibration!=null)Calibration.SetSfxVolume(value.SfxVolume);if(Map!=null)Map.SetSfxVolume(value.SfxVolume);if(TeamLaunch!=null)TeamLaunch.SetSfxVolume(value.SfxVolume);

            if(Activities!=null){Activities.SetWalkMultiplier(value.WalkMultiplier);Activities.SetSfxVolume(value.SfxVolume);}

            if(Gameplay!=null){Gameplay.SetSfxVolume(value.SfxVolume);Gameplay.SetGripToggle(value.GripToggle);Gameplay.PreferredPickupHand=value.DominantHand;Gameplay.SetWeaponTuning(value.MotionDamping,value.OffhandInfluence);requestedDominantHand=value.DominantHand;}

            if(Explosives!=null)Explosives.SetSfxVolume(value.SfxVolume);if(JumpPads!=null)JumpPads.SetSfxVolume(value.SfxVolume);

            if(Paddles!=null)Paddles.SetSfxVolume(value.SfxVolume);if(Supplies!=null)Supplies.SetSfxVolume(value.SfxVolume);if(Consumables!=null){Consumables.SetSfxVolume(value.SfxVolume);Consumables.PreferredHand=value.DominantHand+1;}if(Gallery!=null)Gallery.SetSfxVolume(value.SfxVolume);if(TraversalAudio!=null)TraversalAudio.SetSfxVolume(value.SfxVolume);if(TeamMarks!=null)TeamMarks.SetSfxVolume(value.SfxVolume);if(HealthStations!=null)HealthStations.SetSfxVolume(value.SfxVolume);ApplyMatchSettings(value);if(flashWorldGain<1){float duck=flashWorldGain;flashWorldGain=-1;SetFlashWorldAudio(duck);}

        }

        void ViewGalleryWeapon(string id){if(Gallery==null)return;if(id==null||!Lobby.Visible)Gallery.Hide();else if(!Gallery.Show(id))Gallery.Hide();}

        void DamageConstructedCover(Collider collider,float amount){if(!roundStarted)return;if(ValleyBots!=null)ValleyBots.Hit(collider,amount);if(Map!=null)Map.TryDamageCollider(collider,amount);if(Activities!=null)Activities.TryDamageCollider(collider,amount);}

        string MeleeWorldTargetId(Collider collider)

        {

            string bot=ValleyBots==null?null:ValleyBots.TargetId(collider);if(bot!=null)return bot;string id;bool broken;if(Map!=null&&Map.TryReadDestructible(collider,out id,out broken)&&!broken)return "map/"+Map.MapId+"/"+id;

            Ashfall.Modules.Construction.PlacedPanel panel;if(Activities!=null&&Activities.TryReadPanel(collider,out panel))return "build/"+panel.Id;

            return null;

        }

        void SelectWeapon(string id){selectedWeapon=id;if(roundStarted)UI.SetPlaying();else UI.SetLoadoutStatus("SELECTED / RETURN AND PRESS START");}

        void SelectAppearance(string id){if(Appearance!=null)Appearance.SelectStyle(id);}

        public void SelectMap(string id,bool training)

        {if(Ready&&!switchingMap&&R23MapCatalog.Find(id)!=null)StartCoroutine(ChangeMap(id,training));}

        void MapChoiceChanged(string id){if(roundStarted)ReturnToLobby();else EndMatch();}

        bool mapHealthResetPending=true;

        void EnterMap(string id,bool training)

        {

            if(LanSession!=null&&LanSession.RouteMapEntry(id))return;

            if(!Ready||switchingMap||R23MapCatalog.Find(id)==null)return;

            EndMatch();mapHealthResetPending=true;

            if(Map.MapId==id){activeSpawn=Map.WarmupSpawn;StartPlaying();}

            else StartCoroutine(ChangeMap(id,false,true));

        }

        public bool EnterLanWarmup(string id,string manifestHash)

        {if(!Ready||switchingMap||Map.MapId!=id||Map.ManifestSha256!=manifestHash||!Map.IsLoaded)return false;if(LanSession!=null&&LanSession.Lobby!=null&&!LanSession.PrepareLocalInventory(LanSession.Lobby.SelectedMap.SourceRules.StartingCatalog,LanSession.Lobby.SelectedMap.SourceRules.RetainInventory))return false;EndMatch();mapHealthResetPending=true;activeSpawn=Map.WarmupSpawn;StartPlaying();return roundStarted&&Lobby!=null&&!Lobby.Visible;}

        IEnumerator ChangeMap(string id,bool training,bool enterOnReady=false)

        {

            CancelSourceArchiveEntry();EndTeamLaunch();EndMatch();if(Soundscape!=null)Soundscape.SuspendEnvironment();ClearLocalEffects();if(Explosives!=null)Explosives.ResetRound();if(WorldLoot!=null)WorldLoot.WithdrawMap();WithdrawArchiveItems();

            if(WeaponWheel!=null)WeaponWheel.Cancel();switchingMap=true;roundStarted=false;Ready=false;if(Gallery!=null)Gallery.Hide();Rig.GameplayBlocked=true;Gameplay.SetGameplayPaused(true);Consumables.CancelGestures();Consumables.SetWorldVisible(false);if(Explosives!=null){Explosives.Stow();Explosives.SetWorldVisible(false);}if(Supplies!=null)Supplies.SetWorldVisible(false);if(Preview!=null)Preview.Hide();UI.BeginLoading(R23MapCatalog.Find(id).Label);

            Activities.ResetRound();Climbing.ResetMotion();yield return null;Map.Load(id);while(Map.IsLoading)yield return null;

            if(!Map.IsLoaded){switchingMap=false;Fail(new Exception(Map.LoadError));yield break;}

            mapHealthResetPending=true;activeSpawn=training?Map.GameSpawn:Map.WarmupSpawn;Rig.Head.farClipPlane=Map.CameraFarDistance;Map.SetWorldVisible(false);Lobby.Show();Rig.SetSpawn(Lobby.Spawn,Lobby.Spawn+Vector3.forward);Calibration.NotifyRootPoseReset();UI.SetMenuAnchor(Lobby.MenuAnchor);UI.Recenter();

            Gameplay.SetPracticeTargetsVisible(false);Gameplay.SetPickupHome(activeSpawn+Vector3.up*1.3f,Quaternion.identity);

            switchingMap=false;Ready=true;UI.SetSelectedMap(id);UI.ShowReady();SpawnConsumables();Consumables.SetWorldVisible(false);if(Explosives!=null)Explosives.SetWorldVisible(false);if(Supplies!=null)Supplies.SetWorldVisible(false);JumpPads.SetMap(id);if(HealthStations!=null)HealthStations.SetMap(id,true);if(enterOnReady)StartPlaying();

        }

        void ToggleMenu(){if(UI!=null){if(WeaponWheel!=null)WeaponWheel.Cancel();UI.ToggleMenu();if(Rig!=null)Rig.GameplayBlocked=PlayerMotionBlocked;}}

        public void StartPlaying(){if(BeginSourceArchiveEntry())return;StartPlayingCore();}
        void StartPlayingCore(){if(LanSession!=null&&LanSession.MapEntryBlocked)return;if(Ready){if(mapHealthResetPending&&ActualMatchRules==null){if(!Gameplay.ResetMapVitals())throw new InvalidOperationException("Fresh map Health reset refused");mapHealthResetPending=false;}if(pendingMatch==null&&ActualMatchRules==null&&!sourceArchiveCommitting)activeSpawn=WarmupOrValleyCheckpoint();ClearLocalEffects();if(Gallery!=null)Gallery.Hide();if(!Gameplay.PlayerHealthViewBound&&Gameplay.Health<=0){Gameplay.ResetRound();Consumables.ResetRound();}Gameplay.SetWeaponPresentationSuppressed(false);Lobby.Hide();Map.SetWorldVisible(true);UI.ClearMenuAnchor();if(Preview!=null)Preview.Hide();Rig.SetSpawn(activeSpawn,Map.Bounds.center);Calibration.NotifyRootPoseReset();PositionPracticeTargets();Gameplay.SetPracticeTargetsVisible(false);Consumables.SetWorldVisible(true);if(Explosives!=null)Explosives.SetWorldVisible(true);if(Supplies!=null)Supplies.SetWorldVisible(true);Vector3 forward=Vector3.ProjectOnPlane(Rig.Head.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=Rig.transform.forward;Gameplay.SetPickupHome(Rig.Head.transform.position+forward*.5f+Rig.Head.transform.right*.2f-Vector3.up*.42f,Quaternion.LookRotation(forward,Vector3.up));roundStarted=true;EnsureCheckpointPickups(false);if(WorldLoot!=null)WorldLoot.SetWorldEnabled(true);UI.SetPlaying();BeginMatch();Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,this,"R23 START {0} root={1} rawHead={2} origin={3}",Map.MapId,Rig.transform.position,Rig.Head.transform.localPosition,Rig.TrackingOrigin.localPosition);}}

        public void ReturnToMapLobby(){if(!Ready||switchingMap)return;if(LanSession!=null&&LanSession.RouteMapEntry(Map.MapId))return;EndMatch();activeSpawn=Map.WarmupSpawn;StartPlaying();}

        public void ReturnToLobby(){if(!Ready)return;lanHome.Home(delegate{return LanSession!=null&&LanSession.LeaveForHome();},ReturnToLobbyCore);}
        void ReturnToLobbyCore(){CancelSourceArchiveEntry();if(ValleyMission!=null)ValleyMission.Withdraw();if(Soundscape!=null)Soundscape.SuspendEnvironment();if(ValleyBots!=null)ValleyBots.Clear();EndTeamLaunch();EndMatch();activeSpawn=Map.WarmupSpawn;ClearLocalEffects();if(WeaponWheel!=null)WeaponWheel.Cancel();if(Gallery!=null)Gallery.Hide();roundStarted=false;PrepareArchiveWorld(false);if(MountedRacks!=null)MountedRacks.SetWorldEnabled(false,false);if(WorldLoot!=null)WorldLoot.SetWorldEnabled(false);Rig.GameplayBlocked=true;Gameplay.SetGameplayPaused(true);Gameplay.HolsterCurrent();Gameplay.SetPracticeTargetsVisible(false);Consumables.CancelGestures();Consumables.SetWorldVisible(false);if(Explosives!=null){Explosives.Stow();Explosives.SetWorldVisible(false);}if(Supplies!=null)Supplies.SetWorldVisible(false);Activities.ResetRound();Climbing.ResetMotion();Map.SetWorldVisible(false);Lobby.Show();Rig.SetSpawn(Lobby.Spawn,Lobby.Spawn+Vector3.forward);Calibration.NotifyRootPoseReset();UI.SetMenuAnchor(Lobby.MenuAnchor);UI.BeginLoading("MENU");UI.ShowReady();}

        // Same dispatch for live tracked input and focused native probes. Objective

        // selection occurs before root motion; both consumers revalidate its ID.

        public bool StepItemInput(float seconds,HandSample left,HandSample right,bool hardware,bool blocked,bool itemUi=false,bool inspecting=false,bool leftUi=false,bool rightUi=false)

        {

            RefreshSlotBudget();haveItemAvailability=true;itemHardware=hardware;itemLeftTracked=left.Tracked;itemRightTracked=right.Tracked;

            routeObjectiveInput=true;

            try{

            if(Explosives!=null)Explosives.UpdateInputAvailability(ExplosiveInput(hardware&&!blocked&&!inspecting,left,right,itemUi,leftUi,rightUi));

            if(Supplies!=null)Supplies.UpdateInputAvailability(SupplyInput(hardware&&!blocked&&!itemUi&&!inspecting,left,right));

            if(hardware&&!blocked&&!itemUi&&!inspecting){PrepareSameHandItemReplacement();ClaimSelectedGrenade();if(Supplies!=null&&selectedObjective.Kind==R23GripObjectiveKind.Supply)Supplies.TryCollect(selectedObjective.Id,selectedObjective.Hand+1);}

            var supplies=ConsumableInput(Mathf.Clamp(seconds,.00001f,.1f),hardware&&!blocked&&!itemUi&&!inspecting,left,right,leftUi,rightUi);Consumables.Sample(supplies);

            var handsInput=new R23HandsInputFrame{

                LeftTracked=hardware&&left.Tracked,RightTracked=hardware&&right.Tracked,

                RightHandReserved=PaddleHands||rightUi||Consumables.RightOccupied||Climbing.RightAnchored||Explosives!=null&&Explosives.RightOccupied,

                LeftHandReserved=PaddleHands||leftUi||Consumables.LeftOccupied||Climbing.LeftAnchored||Explosives!=null&&Explosives.LeftOccupied,

                LeftGrip=leftUi||itemUi?0:left.Grip,LeftTrigger=leftUi||itemUi?0:left.Trigger,LeftPrimary=!itemUi&&left.Primary,LeftSecondary=left.Secondary,LeftStickClick=left.Click,LeftStickX=itemUi?0:left.StickX,LeftStickY=itemUi?0:left.StickY,

                RightGrip=rightUi||itemUi?0:right.Grip,RightTrigger=rightUi||itemUi?0:right.Trigger,RightPrimary=!itemUi&&right.Primary,RightSecondary=right.Secondary,RightStickClick=!itemUi&&right.Click,RightStickX=itemUi?0:right.StickX,RightStickY=itemUi?0:right.StickY,ReloadRequested=!itemUi&&reloadRequested,

                NextWeaponPressed=false,RestartPressed=false};

            if(inspecting){var galleryInput=handsInput;galleryInput.LeftPrimary=galleryInput.RightPrimary=false;galleryInput.ReloadRequested=reloadRequested||right.Click;Gallery.Sample(Mathf.Clamp(seconds,0,.1f),galleryInput,hardware,Gameplay.PrimaryHand);}

            bool shot=Gameplay.TickInput(Mathf.Clamp(seconds,0,.1f),handsInput);StepPaddles(Mathf.Clamp(seconds,0,.1f),hardware,blocked,itemUi,inspecting,left,right);if(Explosives!=null)Explosives.Tick(Mathf.Clamp(seconds,0,.1f),ExplosiveInput(hardware&&!blocked&&!inspecting,left,right,itemUi,leftUi,rightUi));reloadRequested=false;if(shot)NotifyValleyGunshot();return shot;

            }finally{routeObjectiveInput=false;}

        }

        void PrepareSameHandItemReplacement()

        {

            // A handedness change between objective sampling and dispatch must

            // not consume the previous hand's stale world-pickup reservation.

            int receivingHand=UI.Settings.DominantHand+1;if(!(receivingHand==1?Rig.LeftInput.Tracked:Rig.RightInput.Tracked))return;

            bool grenadeHeld=Explosives!=null&&Explosives.HeldHand==receivingHand;

            if(!selectedObjective.Valid||Consumables.HeldHand!=receivingHand&&!grenadeHeld||selectedObjective.Kind==R23GripObjectiveKind.Open||selectedObjective.Kind==R23GripObjectiveKind.Explosive||selectedObjective.Kind==R23GripObjectiveKind.Supply)return;

            string id;Vector3 point;float score;bool valid;

            if(selectedObjective.Kind==R23GripObjectiveKind.Weapon)valid=Gameplay.TryReadAimedWorldWeapon(selectedObjective.Hand,out id,out point,out score)&&id==selectedObjective.Id&&MakeRoomForPickup(id,true);

            else

            {

                valid=Consumables.TryReadAimedWorldItem(selectedObjective.Hand+1,out id,out point,out score)&&id==selectedObjective.Id;

                var item=valid?Consumables.InspectItem(id):null;valid=valid&&item!=null&&MakeRoomForPickup(item.Kind,false);

            }

            if(!valid){selectedObjective=default(R23GripObjective);return;}

            if(Consumables.HeldHand==receivingHand)Consumables.Stow();

            if(grenadeHeld)Explosives.Stow();

        }

        void Update()

        {

#if UNITY_EDITOR

            if(ExternalProbePump&&Ready&&!smokeUiPump)return;

            if(ManualSmoke&&Ready&&!smokeUiPump)return;

#else

            if(ExternalProbePump&&Ready)return;

#endif

            PrepareArchiveWorld(Ready&&(roundStarted||SourceArchiveEntryPending)&&!switchingMap&&Lobby!=null&&!Lobby.Visible);

            if(Rig==null||UI==null)return;

            Rig.MenuLocomotion=Ready&&!switchingMap&&Lobby!=null&&Lobby.Visible&&UI.Page!=R23UiPage.Loading&&!Rig.CarrierActive;

            TickSourceTraining();

            var left=Rig.LeftInput;var right=Rig.RightInput;bool hardware=Rig.DisplayRunning&&Rig.Tracking&&Rig.SessionFocused;

            UI.ScrollOutfits(right.StickY,Time.unscaledDeltaTime,hardware&&right.Tracked);UI.ScrollBackpack(right.StickY,Time.unscaledDeltaTime,hardware&&right.Tracked);

#if UNITY_EDITOR

            if(ManualSmoke)hardware=true;

#endif

            // Dominant A/X closes the open backpack; consume through release so this

            // same press cannot reopen the owned wheel or reach an item action.

            int backpackHand=Gameplay==null?UI.Settings.DominantHand:Gameplay.PrimaryHand;var backpackInput=backpackHand==0?left:right;

            bool backpackCloseA=UI.SampleBackpackClose(backpackInput.Primary,hardware&&backpackInput.Tracked);

            if(backpackCloseA){if(backpackHand==0)left.Primary=false;else right.Primary=false;if(WeaponWheel!=null)WeaponWheel.Cancel();}

            bool galleryHeld=Gallery!=null&&Lobby!=null&&Lobby.Visible&&Gallery.Active&&Gallery.HoldingHand>=0;

            bool galleryLeft=galleryHeld&&(Gallery.ClaimsBothHands||Gallery.HoldingHand==0),galleryRight=galleryHeld&&(Gallery.ClaimsBothHands||Gallery.HoldingHand==1);

            // A held preview gun owns its trigger. The other hand can still use

            // the board; shooting toward it must not click a menu or release custody.

            menuPointerLeftEligible=hardware&&Rig.LeftAimTracked&&!galleryLeft;menuPointerRightEligible=hardware&&Rig.RightAimTracked&&!galleryRight;

            bool leftUi=UI.UpdateLoadoutPointer(0,Rig.LeftAimPosition,R23TrackedGripPose.Forward(Rig.Left),left.Trigger>.65f,left.Grip>.65f,menuPointerLeftEligible);

            bool rightUi=UI.UpdateLoadoutPointer(1,Rig.RightAimPosition,R23TrackedGripPose.Forward(Rig.Right),right.Trigger>.65f,right.Grip>.65f,menuPointerRightEligible);

            if(Appearance!=null){Appearance.SetPresentationState(hardware,PageBlocksGameplay);Appearance.SetCrouchFloor(Rig.CrouchFloorValid,Rig.CrouchFloorY);Appearance.SetFlightTrailMode(Activities!=null&&!Rig.CarrierActive&&!Climbing.LeftAnchored&&!Climbing.RightAnchored&&!Climbing.Mantling?Activities.Mode:Ashfall.Modules.Flight.FlightMode.Air);Appearance.SetMotionState(Rig.Supported,

                Activities!=null&&Activities.Mode==Ashfall.Modules.Flight.FlightMode.Glide&&!Rig.CarrierActive&&!Climbing.LeftAnchored&&!Climbing.RightAnchored&&!Climbing.Mantling,

                Rig.MotionVelocity,Ready&&roundStarted&&Lobby!=null&&!Lobby.Visible);UI.SetAppearanceStatus(Appearance.SelectedStyleId,Appearance.Loading,Appearance.Failure);}

            if(Calibration!=null)UI.SetCalibrationStatus(Calibration.Status);

            if(Preview!=null)

            {

                if(Lobby.Visible&&UI.Page!=R23UiPage.Loading){Preview.Show(UI.CurrentViewedAppearanceId??Appearance.RequestedStyleId??"cinder_scout");}

                else if(Preview.Visible)Preview.Hide();

            }

            StepSoundscape(Mathf.Min(Time.unscaledDeltaTime,.25f),hardware);

            if(!Ready){if(TacticalMap!=null)TacticalMap.Close();RefreshWorldPickupHints(false);Rig.GameplayBlocked=true;Rig.ReservePrimaryMarkTurn=false;if(TraversalAudio!=null)TraversalAudio.Suspend();if(WorldPing!=null)WorldPing.ClearAll();if(TeamMarks!=null){TeamMarks.CancelInput();TeamMarks.Tick(Mathf.Max(0,Time.unscaledDeltaTime));}return;}

            TickMatch(Mathf.Max(0,Time.unscaledDeltaTime),hardware);TickTeamLaunch(Mathf.Max(0,Time.unscaledDeltaTime),hardware);

            StepTacticalMap(left,right,hardware);

            bool inspecting=Gallery!=null&&Gallery.Active&&Lobby.Visible;

            bool blocked=!roundStarted||PageBlocksGameplay||Gameplay.Health<=0||Rig.CarrierActive||!MatchLocalInputAllowed;

            TickArchiveItems();

            if(WorldLoot!=null)WorldLoot.SetWorldEnabled((!blocked||MatchLocalGhost)&&MatchLootAllowed);

            if(MountedRacks!=null)MountedRacks.SetWorldEnabled(roundStarted&&!switchingMap,(!blocked||MatchLocalGhost)&&MatchLootAllowed);

            Rig.ReservePrimaryMarkTurn=TeamMarks!=null&&TeamMarks.Ready&&hardware&&!blocked&&!BackpackOpen&&!inspecting;

            if(TeamMarks!=null){TeamMarks.Tick(Mathf.Max(0,Time.unscaledDeltaTime),TacticalMapOpen);if(Gameplay.Health<=0)TeamMarks.ClearAll();}

            if(WorldPing!=null)WorldPing.Tick(Mathf.Clamp(Time.unscaledDeltaTime,0,.1f),hardware&&!blocked);

            Rig.GameplayBlocked=PlayerMotionBlocked;Rig.PrimaryHand=Gameplay.PrimaryHand;

            Rig.RightHandOccupied=PaddleHands||Gameplay.RightHandOccupied||Consumables.RightOccupied||Explosives!=null&&Explosives.RightOccupied||rightUi||(inspecting&&(Gallery.ClaimsBothHands||Gallery.HoldingHand==1));Rig.LeftHandOccupied=PaddleHands||leftUi||Gameplay.LeftHandOccupied||Consumables.LeftOccupied||Explosives!=null&&Explosives.LeftOccupied||(inspecting&&(Gallery.ClaimsBothHands||Gallery.HoldingHand==0));

            Gameplay.NotifyFocus(hardware);Gameplay.SetGameplayPaused(!hardware||blocked);

            if(hardware&&!blocked&&requestedDominantHand>=0){if(Gameplay.PrimaryHand==requestedDominantHand||(Gameplay.HasWeapon?TryEquipOwnedItem(Gameplay.ActiveWeaponActionId,requestedDominantHand):Gameplay.SetPreferredHandWhenHolstered(requestedDominantHand)))requestedDominantHand=-1;}

            Consumables.PreferredHand=UI.Settings.DominantHand+1;

            RefreshLoadout();int wheelHand=Gameplay.PrimaryHand;var wheelTransform=wheelHand==0?Rig.Left:Rig.Right;

            var aim=new Ray(wheelTransform.position,R23TrackedGripPose.Forward(wheelTransform));

            if(WeaponWheel!=null)WeaponWheel.Step(Mathf.Clamp(Time.unscaledDeltaTime,0,.1f),R23InputProjection.WheelHeld(left,right,wheelHand),!Activities.BuildActive&&hardware&&!blocked&&!TacticalMapOpen&&!BackpackOpen&&!inspecting&&(TeamMarks==null||!TeamMarks.InputHeld)&&(wheelHand==0?left.Tracked:right.Tracked),aim,Rig.LocomotionAnchorPosition,wheelHand);

            if(TeamMarks!=null){var primary=wheelHand==0?left:right;TeamMarks.Sample(Mathf.Clamp(Time.unscaledDeltaTime,0,.1f),primary.Secondary,new Vector2(primary.StickX,primary.StickY),aim,!Activities.BuildActive&&hardware&&!blocked&&!TacticalMapOpen&&!BackpackOpen&&!inspecting&&primary.Tracked&&(WeaponWheel==null||!WeaponWheel.Active),Rig.LocomotionAnchorPosition);}

            bool bothHandsUi=BackpackOpen||TeamMarks!=null&&TeamMarks.InputHeld;
            bool wheelOpen=WeaponWheel!=null&&WeaponWheel.Active,itemUi=bothHandsUi||wheelOpen;

            Rig.LeftHandOccupied|=bothHandsUi||wheelOpen&&wheelHand==0;Rig.RightHandOccupied|=bothHandsUi||wheelOpen&&wheelHand==1;

            Vector3 leftArt=hardware&&!PlayerMotionBlocked&&!Rig.LeftHandOccupied?Climbing.LeftContactProxy-Rig.Left.position:Vector3.zero,rightArt=hardware&&!PlayerMotionBlocked&&!Rig.RightHandOccupied?Climbing.RightContactProxy-Rig.Right.position:Vector3.zero;

            Gameplay.SetFreeHandContactOffsets(leftArt,rightArt);if(Appearance!=null)Appearance.SetFreeHandPresentationOffsets(leftArt,rightArt);

            Gameplay.SetWeaponPresentationSuppressed(inspecting||!roundStarted||itemUi||PlatformUnarmed);

            Activities.TickStructure(Mathf.Min(Time.unscaledDeltaTime,.1f),!hardware||blocked);

            if(!blocked&&selectedWeapon!=null){Consumables.Stow();if(Gameplay.SelectWeapon(selectedWeapon))selectedWeapon=null;}

            RefreshSlotBudget();

            StepItemInput(Mathf.Min(Time.unscaledDeltaTime,.1f),left,right,hardware,blocked,itemUi,inspecting,leftUi,rightUi);StepValleyBots(Mathf.Min(Time.unscaledDeltaTime,.25f),hardware);

            RefreshWorldPickupHints(hardware&&!blocked&&!itemUi&&!inspecting);

            if(hardware&&!blocked)ApplyLoadoutRequests();

            RefreshLoadout();

            JumpPads.Step(new Vector3(Rig.Head.transform.position.x,Rig.transform.position.y,Rig.Head.transform.position.z),Mathf.Clamp(Time.unscaledDeltaTime,.00001f,.1f),hardware&&!blocked&&Rig.LastFrame.Tracked,Climbing.LeftAnchored||Climbing.RightAnchored,Climbing.Mantling);

            if(hardware&&!blocked)Map.ProcessPlayerTouch(Rig.transform.position,Rig.Head.transform.position);

            if(HealthStations!=null)HealthStations.Step(new Vector3(Rig.Head.transform.position.x,Rig.transform.position.y,Rig.Head.transform.position.z),Mathf.Clamp(Time.unscaledDeltaTime,.00001f,.1f),hardware&&!blocked&&Rig.LastFrame.Tracked);

            if(TraversalAudio!=null)TraversalAudio.Tick(Mathf.Clamp(Time.unscaledDeltaTime,.00001f,.1f),Rig.Supported&&Rig.SprintTier>0,Calibration.Calibrated&&Rig.Head.transform.localPosition.y<Calibration.Receipt.TrackedStandingEye*.75f,blocked);

            if(roundStarted)UI.UpdateHud(new R23HudSnapshot(Gameplay.Health,Gameplay.MaxHealth,Gameplay.Ammo,Gameplay.Reserve,Gameplay.HasWeapon,Gameplay.Reloading,Gameplay.ActiveWeaponName,Gameplay.Hits,Gameplay.Shield,Gameplay.MaxShield,Rig.Stamina,Rig.StaminaMaximum,Activities.Balance,Climbing.LeftAnchored||Climbing.RightAnchored?"CLIMBING":Activities.Mode.ToString(),Gameplay.Reloading||Gameplay.Ammo==0?Gameplay.ReloadInstruction:""));

            if(Keyboard.current!=null&&Keyboard.current.enterKey.wasPressedThisFrame)RestartRound();

            TickLocalWarmupFallFailsafe();

        }

        public void RestartRound(){if(!Ready||!roundStarted)return;if(ActualMatchRules==null||!MatchActive){EndMatch();activeSpawn=WarmupOrValleyCheckpoint();}if(WeaponWheel!=null)WeaponWheel.Cancel();queuedEquip=queuedDrop=selectedWeapon=null;Gameplay.ResetRound();Consumables.ResetRound();ResetRig();Rig.SetSpawn(activeSpawn,Map.Bounds.center);Calibration.NotifyRootPoseReset();SpawnConsumables();RefreshLoadout();roundStarted=true;UI.SetPlaying();RestartMatch();}

        void ResetRig(){if(ValleyMission!=null)ValleyMission.Withdraw();if(ValleyBots!=null)ValleyBots.Clear();ClearLocalEffects();ResetArchiveItems();if(Paddles!=null)Paddles.Restart();inventoryCapacity=3;RefreshSlotBudget();if(Supplies!=null)Supplies.ResetRound();if(Explosives!=null)Explosives.ResetRound();if(WorldLoot!=null)WorldLoot.ResetMapLoot();if(Rig!=null)Rig.ResetPose();if(Map!=null)Map.ResetDestructibles();if(Activities!=null)Activities.ResetRound();if(Climbing!=null)Climbing.ResetMotion();if(JumpPads!=null)JumpPads.ResetContacts();if(Calibration!=null)Calibration.NotifyRootPoseReset();}

        void OnGUI(){if(Failure!=null)GUI.Label(new Rect(12,12,1000,160),"Ashfall could not load:\n"+Failure);else if(Ready&&!Rig.DisplayRunning)GUI.Label(new Rect(12,12,1000,70),"Connect Quest Link / OpenXR. VR menu: point and release trigger. Left Menu opens settings. Hold A/X: owned wheel; B/Y: mark wheel; support B/Y: quick ping.");}

        bool menuPointerLeftEligible,menuPointerRightEligible;

        [BeforeRenderOrder(1300)]

        void RefreshMenuPointerPresentation()

        {

            if(!isActiveAndEnabled||UI==null||Rig==null)return;

            bool focused=Rig.Tracking&&Rig.SessionFocused;

            UI.RefreshPointerPresentation(0,Rig.LeftAimPosition,R23TrackedGripPose.Forward(Rig.Left),focused&&menuPointerLeftEligible);

            UI.RefreshPointerPresentation(1,Rig.RightAimPosition,R23TrackedGripPose.Forward(Rig.Right),focused&&menuPointerRightEligible);

        }

        void OnEnable(){Application.onBeforeRender+=RefreshMenuPointerPresentation;}

        void OnDisable()

        {

            Application.onBeforeRender-=RefreshMenuPointerPresentation;ClearCombatFeedback();

            if(UI!=null){UI.RefreshPointerPresentation(0,Vector3.zero,Vector3.forward,false);UI.RefreshPointerPresentation(1,Vector3.zero,Vector3.forward,false);}

        }

        void OnDestroy(){CancelSourceArchiveEntry();Application.onBeforeRender-=RefreshMenuPointerPresentation;DisposeCombatFeedback();if(LanSession!=null)LanSession.Shutdown();DisposeMatch();if(TeamLaunch!=null)TeamLaunch.Dispose();if(UI!=null){UI.MapLobbyRequested-=ReturnToMapLobby;UI.PaddlesRequested-=TogglePaddles;UI.BackpackDropRequested-=QueueBackpackDrop;UI.WeaponViewed-=ViewGalleryWeapon;UI.AppearanceViewed-=ViewAppearance;}if(WeaponWheel!=null)WeaponWheel.Cancel();if(Gameplay!=null){Gameplay.WorldHit-=DamageConstructedCover;if(TraversalAudio!=null)Gameplay.SurfaceImpact-=TraversalAudio.PlayImpact;Gameplay.Restarted-=ResetRig;if(Appearance!=null)Appearance.GloveMaterialChanged-=Gameplay.SetGloveMaterial;}if(Rig!=null){Rig.MenuPressed-=ToggleMenu;Rig.PingPressed-=PingWorld;}if(ValleyMission!=null)ValleyMission.Withdraw();if(UI!=null){UI.NewValleyCampaignRequested-=StartNewValleyCampaign;UI.ContinueValleyCampaignRequested-=ContinueValleyCampaign;UI.ValleyCampaignAvailable=null;UI.ValleyCheckpointAvailable=null;}if(Soundscape!=null)Soundscape.Shutdown();if(ValleyBots!=null)ValleyBots.Clear();if(Map!=null)Map.DisposeMap();}

        public R24VRRevivePaddles Paddles{get;private set;}

        // Mode/network owners can supply actual eligible allies and completed

        // authoritative revival. The local FFA default never fabricates a target.

        public Func<R24ReviveTarget[]> ReviveTargetProvider;

        public Func<string,bool> CompleteRevive;

        bool haveItemAvailability,itemHardware,itemLeftTracked,itemRightTracked;

        bool PaddleHands{get{return Paddles!=null&&Paddles.State.Active;}}

        R24ReviveTarget[] ActualReviveTargets(){return ReadActualTrainingAndAllies();}

        bool CompleteActualRevive(string id){return CompleteTrainingOrAllyRevive(id);}

        bool ReviveLineClear(Float3 from,Float3 to){var a=new Vector3(from.X,from.Y,from.Z);var b=new Vector3(to.X,to.Y,to.Z);return Rig.HeadCollision!=null&&!Rig.HeadCollision.Ray(a,b,out reviveRay);}

        RaycastHit reviveRay;

        void InitializePaddles(){Paddles=new GameObject("R24 actual reusable revive gear").AddComponent<R24VRRevivePaddles>();Paddles.transform.SetParent(transform,false);Paddles.Initialize(ActualReviveTargets,ReviveLineClear,CompleteActualRevive);Paddles.SetSfxVolume(UI.Settings.SfxVolume);UI.PaddlesRequested+=TogglePaddles;UI.BackpackDropRequested+=QueueBackpackDrop;}

        bool PaddleAllowed(bool tracked){return Ready&&!PlatformUnarmed&&roundStarted&&!switchingMap&&tracked&&Gameplay.Health>0&&!PageBlocksGameplay&&!BackpackOpen&&!Climbing.Mantling&&!Climbing.LeftAnchored&&!Climbing.RightAnchored&&Rig.HeadCollision!=null&&Rig.HeadCollision.Clear(Rig.Head.transform.position,Vector3.zero);}

        public bool EquipPaddles(){bool tracked=haveItemAvailability?itemHardware&&itemLeftTracked&&itemRightTracked:Rig!=null&&Rig.SessionFocused&&Rig.Tracking&&Rig.LeftInput.Tracked&&Rig.RightInput.Tracked;if(Paddles==null||!Paddles.Ready||!PaddleAllowed(tracked))return false;Consumables.Stow();if(Explosives!=null)Explosives.Stow();if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();bool accepted=Paddles.Equip(true);if(accepted)UI.SetPaddlesStatus("REVIVE PADDLES");return accepted;}

        public void StowPaddles(){if(Paddles!=null)Paddles.Stow();if(Gameplay!=null)Gameplay.SamplePaddleAuthority(0,false,false,false,false,new Float3(),new Float3(),new Float3(),new Float3());if(UI!=null)UI.SetPaddlesStatus("");}

        void TogglePaddles(){if(PaddleHands)StowPaddles();else if(!EquipPaddles())UI.SetLoadoutStatus("FREE BOTH TRACKED HANDS TO EQUIP PADDLES");}

        static Float3 PF(Vector3 p){return new Float3(p.x,p.y,p.z);}

        static R23ShotPose Pose(Transform t,bool corrected=false){var q=corrected?t.rotation*R23TrackedGripPose.LocalRotation:t.rotation;return new R23ShotPose(PF(t.position),PF(q*Vector3.right),PF(q*Vector3.up),PF(q*Vector3.forward));}

        void StepPaddles(float dt,bool hardware,bool blocked,bool itemUi,bool inspecting,HandSample leftInput,HandSample rightInput){if(Paddles==null||!Paddles.Ready)return;bool active=PaddleHands;bool tracked=hardware&&leftInput.Tracked&&rightInput.Tracked;bool allowed=PaddleAllowed(tracked)&&!itemUi&&!inspecting&&!blocked;bool paused=!hardware||blocked;var leftPose=R24VRRevivePaddles.FromCorrectedGrip(Pose(Rig.Left,true));var rightPose=R24VRRevivePaddles.FromCorrectedGrip(Pose(Rig.Right,true));Paddles.SharedTouch=LanRoundBound&&ActualMatchRules!=null&&ActualMatchRules.Mode=="revive";Paddles.Step(dt,allowed,paused,Pose(Rig.transform),leftPose,rightPose,Rig.Head);if(active){Gameplay.SamplePaddleAuthority(dt,true,allowed,paused,Paddles.State.Facing,leftPose.Origin,rightPose.Origin,PF(Rig.transform.InverseTransformPoint(new Vector3(leftPose.Origin.X,leftPose.Origin.Y,leftPose.Origin.Z))),PF(Rig.transform.InverseTransformPoint(new Vector3(rightPose.Origin.X,rightPose.Origin.Y,rightPose.Origin.Z))));UI.SetPaddlesStatus(Paddles.PresentedRemaining>0?"PADDLES READY "+Paddles.PresentedRemaining.ToString("F1")+"s":"PADDLES CHARGE "+Mathf.RoundToInt(Paddles.PresentedCharge*100)+"%");}}

    }

}

#endif

