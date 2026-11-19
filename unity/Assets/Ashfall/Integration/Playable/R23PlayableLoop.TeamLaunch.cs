#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.TeamLaunch;
using Ashfall.Integration.Playable.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public R24TeamLaunch TeamLaunch {get;private set;}
        public R24LaunchMapCatalog LaunchMapCatalog {get;private set;}
        long localLaunchEpoch;
        bool ejectLocalCarrier;
        public string LaunchFailure {get;private set;}
        void InitializeTeamLaunch()
        {
            LaunchMapCatalog=new R24LaunchMapCatalog();StartCoroutine(LaunchMapCatalog.Load());
            TeamLaunch=new GameObject("R6 actual team decks and launch pods").AddComponent<R24TeamLaunch>();
            TeamLaunch.transform.SetParent(transform,false);
            TeamLaunch.Initialize(new R24LaunchMap(),new R24LaunchRules(),Map.transform);
            TeamLaunch.SetWorldEnabled(false);
            Climbing.AdditionalStaticTerrain=TeamLaunch.IsStaticTerrain;
            Activities.AdditionalStaticTerrain=TeamLaunch.IsStaticTerrain;
            Rig.ReadCarrierMotion=ReadLocalCarrierMotion;
            Rig.CarrierMotionAccepted=delegate(Vector3 accepted){if(TeamLaunch!=null)TeamLaunch.AcceptCarrierMotion("player",accepted);};
        }
        public bool PlatformUnarmed{get{return Rig!=null&&(Rig.CarrierActive||TeamLaunch!=null&&TeamLaunch.Eligible&&TeamLaunch.Runtime.Layout.PlatformAt(LaunchF(Rig.transform.position))>=0);}}
        void DisarmPlatform()
        {
            if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();
            if(Consumables!=null&&Consumables.HeldId!=null)Consumables.Stow();
            if(Explosives!=null&&Explosives.HeldId!=null)Explosives.Stow();
            if(PaddleHands)StowPaddles();
            if(WeaponWheel!=null&&WeaponWheel.Active)WeaponWheel.Cancel();
            if(BackpackOpen)UI.SetPlaying();
        }
        void BeginTeamLaunch()
        {
            LaunchFailure=null;ejectLocalCarrier=false;Rig.ClearCarrierMotion();OnLaunchDeck=false;
            R24LaunchMapInfo info;
            if(!MatchActive||ActualMatchRules==null||!LaunchMapCatalog.Ready||!LaunchMapCatalog.TryRead(Map.MapId,out info))
            {TeamLaunch.SetWorldEnabled(false);return;}
            if(Map.transform.rotation!=Quaternion.identity||Map.transform.lossyScale!=Vector3.one)
                throw new InvalidOperationException("Source team launch requires the actual unrotated metre map frame");
            var origin=Map.transform.TransformPoint(LaunchV(info.RepresentedOriginUnity));
            var launchMap=info.ToLaunchMap(LaunchF(Map.transform.position),info.HasDeploymentTop?0:R24LaunchMapCatalog.ScanCollisionTop(Map.transform,origin.y));
            var rules=new R24LaunchRules{Live=true,Mode=ActualMatchRules.Mode,Stage="match",Teams=ActualMatchRules.Teams,Epoch=LanSession!=null&&LanSession.Lobby!=null?LanSession.Lobby.Epoch:++localLaunchEpoch};
            TeamLaunch.Configure(launchMap,rules,Map.transform);TeamLaunch.SetSfxVolume(UI.Settings.SfxVolume);
            TeamLaunch.SetWorldEnabled(true);
            if(!TeamLaunch.Eligible)return;
            if(!TeamLaunch.Ready)throw new InvalidOperationException(TeamLaunch.Failure??"Actual launch hardware is still loading");
            var assignment=TeamLaunch.AssignDeckSpawn("player",LocalMatchTeam,new R24LaunchAssignment[0]);
            if(assignment==null)throw new InvalidOperationException("Actual local-team deck assignment refused");
            OnLaunchDeck=true;
            Vector3 spawn=LaunchV(assignment.Spawn);
            Rig.SetSpawn(spawn,Map.Bounds.center);Calibration.NotifyRootPoseReset();DisarmPlatform();
            if(!info.CandidatesReady)StartCoroutine(LaunchMapCatalog.LoadCandidates(Map.MapId));
        }
        void EndTeamLaunch()
        {
            ejectLocalCarrier=false;OnLaunchDeck=false;
            if(Rig!=null)Rig.ClearCarrierMotion();
            if(TeamLaunch!=null)
            {TeamLaunch.Configure(new R24LaunchMap(),new R24LaunchRules(),Map.transform);TeamLaunch.SetWorldEnabled(false);}
        }
        R24LaunchPlayer ReadLocalLaunchPlayer()
        {
            return new R24LaunchPlayer{Id="player",Team=LocalMatchTeam,Alive=Gameplay!=null&&Gameplay.Health>0,
                OnDeck=OnLaunchDeck,Position=LaunchF(Rig.transform.position)};
        }
        void TickTeamLaunch(float dt,bool hardware)
        {
            if(TeamLaunch==null||!MatchActive||!roundStarted||switchingMap||!TeamLaunch.Eligible)return;
            OnLaunchDeck=TeamLaunch.Runtime.Layout.PlatformAt(LaunchF(Rig.transform.position))>=0;
            if(PlatformUnarmed)DisarmPlatform();
            TeamLaunch.Refresh(MatchClock,Mathf.Max(0,dt));
            var left=Rig.LeftInput;var right=Rig.RightInput;
            bool xr=Rig.DisplayRunning;
#if UNITY_EDITOR
            xr|=ManualSmoke; // Same XR-disabled tracked-input fixture protocol as the existing UI/item route.
#endif
            var input=new R24LaunchInput{HeadTracked=true,Head=LaunchF(Rig.Head.transform.position),HeadForward=LaunchF(Rig.Head.transform.forward),Enabled=hardware,XR=xr,MenuOpen=PageBlocksGameplay||BackpackOpen,
                SessionOverlay=UI.Page!=R23UiPage.Playing,
                DesktopE=Keyboard.current!=null&&Keyboard.current.eKey.isPressed,
                Left=new R24LaunchHand{Tracked=left.Tracked,Position=LaunchF(Rig.Left.position),Forward=LaunchF(Rig.Left.forward),Trigger=left.Trigger,Grip=left.Grip},
                Right=new R24LaunchHand{Tracked=right.Tracked,Position=LaunchF(Rig.Right.position),Forward=LaunchF(Rig.Right.forward),Trigger=right.Trigger,Grip=right.Grip}};
            var player=ReadLocalLaunchPlayer();var intent=TeamLaunch.SampleInput(player,MatchClock,input);
            if(intent.Kind==R24LaunchIntentKind.Launch)TryLaunchLocalPod(intent.PodId);
            else if(intent.Kind==R24LaunchIntentKind.Eject){if(LanSession!=null&&LanSession.Lobby!=null)LanSession.Lobby.RequestPodEject();else ejectLocalCarrier=true;}
        }
        public bool TryLaunchLocalPod(string actualPodId)
        {
            if(!MatchActive||!roundStarted||PageBlocksGameplay||Gameplay.Health<=0||TeamLaunch==null||!TeamLaunch.Eligible)return false;
            if(LanSession!=null&&LanSession.Lobby!=null)return LanSession.Lobby.RequestPodLaunch(actualPodId);
            R24LaunchMapInfo info;var spawns=new List<Float3>{LaunchF(Map.GameSpawn)};var loot=new List<Float3>();
            if(LaunchMapCatalog.TryRead(Map.MapId,out info)&&info.CandidatesReady)
            {foreach(var p in info.CandidateSpawns)spawns.Add(LaunchF(Map.transform.TransformPoint(LaunchV(p))));foreach(var p in info.CandidateLoot)loot.Add(LaunchF(Map.transform.TransformPoint(LaunchV(p))));}
            if(WorldLoot!=null)foreach(var p in WorldLoot.CacheSitePositions)loot.Add(LaunchF(p));
            R24LaunchRide ride;
            bool accepted=TeamLaunch.TryLaunch(ReadLocalLaunchPlayer(),actualPodId,MatchClock,spawns,loot,LaunchF(Map.GameSpawn),out ride);
            LaunchFailure=accepted?null:TeamLaunch.Runtime.Failure;
            if(accepted){OnLaunchDeck=false;if(Climbing!=null)Climbing.ResetMotion();}
            return accepted;
        }
        R23RigCarrierMotion? ReadLocalCarrierMotion()
        {
            if(TeamLaunch==null||!MatchActive||!roundStarted||!TeamLaunch.Eligible)return null;
            if(LanSession!=null&&LanSession.Lobby!=null){var lobby=LanSession.Lobby;Ashfall.Integration.Playable.Networking.R24LanPodState shared;if(!lobby.TryReadPod(lobby.OwnActor,out shared))return null;if(shared.Phase!=Ashfall.Integration.Playable.Networking.R24LanPodPhase.Flying)return null;OnLaunchDeck=false;return new R23RigCarrierMotion{Target=LaunchV(shared.Plan.Position(System.Math.Max(0,lobby.RoundElapsed-shared.Started))),Flying=true};}
            R24LaunchCarrier motion;bool eject=ejectLocalCarrier;ejectLocalCarrier=false;
            if(!TeamLaunch.TryCarrier(ReadLocalLaunchPlayer(),MatchClock,eject,out motion))return null;
            return new R23RigCarrierMotion{Target=LaunchV(motion.Target),Flying=motion.Carried};
        }
        public void ApplyLanPodPresentation(Ashfall.Integration.Playable.Networking.R24LanPodState state){if(LanSession==null||LanSession.Lobby==null||TeamLaunch==null||!MatchActive||state==null)return;var lobby=LanSession.Lobby;Ashfall.Integration.Playable.Networking.R24LanPodState actual;if(!lobby.TryReadPod(state.Actor,out actual)||!System.Object.ReferenceEquals(state,actual))return;TeamLaunch.Runtime.AdoptHostRide(state.Actor==lobby.OwnActor?"player":state.Actor,state.Plan,state.Started,(int)state.Phase,state.Terminal);}
        public bool PlanLanPod(string actor,int team,string pod,Float3 authenticatedRoot,double clock,out Ashfall.Modules.Flight.CapsulePlan plan){
            plan=null;if(TeamLaunch==null||!MatchActive||!TeamLaunch.Eligible)return false;
            var sites=new List<Float3>{LaunchF(Map.GameSpawn)};var loot=new List<Float3>();R24LaunchMapInfo info;
            if(LaunchMapCatalog.TryRead(Map.MapId,out info)&&info.CandidatesReady){foreach(var v in info.CandidateSpawns)sites.Add(LaunchF(Map.transform.TransformPoint(LaunchV(v))));foreach(var v in info.CandidateLoot)loot.Add(LaunchF(Map.transform.TransformPoint(LaunchV(v))));}
            if(WorldLoot!=null)foreach(var v in WorldLoot.CacheSitePositions)loot.Add(LaunchF(v));R24LaunchRide ride;
            if(!TeamLaunch.TryLaunch(new R24LaunchPlayer{Id=actor,Team=team,Alive=true,OnDeck=true,Position=authenticatedRoot},pod,clock,sites,loot,LaunchF(Map.GameSpawn),out ride))return false;plan=ride.Plan;return true;
        }
        static Float3 LaunchF(Vector3 point){return new Float3(point.x,point.y,point.z);}
        static Vector3 LaunchV(Float3 point){return new Vector3(point.X,point.Y,point.Z);}
    }
}
#endif
