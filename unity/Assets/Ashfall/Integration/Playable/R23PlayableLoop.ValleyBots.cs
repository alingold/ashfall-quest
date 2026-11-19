#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Bots;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Match;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public R24ValleyBots ValleyBots {get;private set;}
        public R24DamageNumbers CombatDamageNumbers {get;private set;}
        void PracticeTargetDamaged(string id,bool headshot,float shieldLoss,float healthLoss)
        {
            if(CombatDamageNumbers==null||!Ready||!roundStarted||switchingMap||Lobby==null||Lobby.Visible||shieldLoss<0||healthLoss<0||float.IsNaN(shieldLoss)||float.IsInfinity(shieldLoss)||float.IsNaN(healthLoss)||float.IsInfinity(healthLoss))return;
            var blade=Gameplay.ActiveBladeDamagePoint;Vector3 anchor;
            if(blade.HasValue)CombatDamageNumbers.EmitBlade(shieldLoss+healthLoss,blade.Value,shieldLoss>0);
            else if(Gameplay.TryReadPracticeDamageAnchor(id,out anchor))CombatDamageNumbers.Emit(shieldLoss+healthLoss,anchor,shieldLoss>0);
        }
        void ClearCombatFeedback(){valleyRecovery.Reset();if(IncomingDamageDirection!=null)IncomingDamageDirection.Clear();if(CombatDamageNumbers!=null)CombatDamageNumbers.Clear();}
        void DisposeCombatFeedback(){if(Gameplay!=null){Gameplay.TargetDamaged-=PracticeTargetDamaged;Gameplay.PlayerPoolLost-=ValleyPlayerPoolLost;}if(CombatDamageNumbers!=null){CombatDamageNumbers.Dispose();CombatDamageNumbers=null;}}
        public R24ValleyMissionWorld ValleyMission {get;private set;}
        R24PlayableMatchRules valleyCampaignMatch;
        long valleyBotEpoch;int valleyBotRound;
        void InitializeValleyBots()
        {InitializeCombatFx();CombatDamageNumbers=new R24DamageNumbers(Rig.Head.transform);Gameplay.TargetDamaged+=PracticeTargetDamaged;Gameplay.PlayerPoolLost+=ValleyPlayerPoolLost;ValleyBots=new GameObject("Relay Valley KESTREL encounter owner").AddComponent<R24ValleyBots>();ValleyBots.transform.SetParent(transform,false);ValleyBots.Initialize(this);ValleyMission=new GameObject("Original Relay Valley objective owner").AddComponent<R24ValleyMissionWorld>();ValleyMission.transform.SetParent(transform,false);ValleyMission.Initialize(this);UI.ValleyCampaignAvailable=ValleyCampaignContext;UI.ValleyCheckpointAvailable=delegate{return ValleyMission.HasCheckpoint;};UI.NewValleyCampaignRequested+=StartNewValleyCampaign;UI.ContinueValleyCampaignRequested+=ContinueValleyCampaign;}
        public void StepValleyBots(float dt,bool hardware)
        {
            if(CombatDamageNumbers!=null)CombatDamageNumbers.Advance(Mathf.Clamp(dt,0,.25f));
            if(ValleyBots==null)return;
            bool visible=Ready&&roundStarted&&!switchingMap&&Lobby!=null&&!Lobby.Visible&&MatchActive&&Map.MapId=="valley"&&(LanSession==null||LanSession.Lobby==null);
            bool campaign=visible&&ReferenceEquals(ActualMatchRules,valleyCampaignMatch);
            if(visible&&(valleyBotEpoch!=MatchEpoch||valleyBotRound!=MatchRound)){
                if(!campaign)ValleyBots.Mission.Restore(0,0,0);
                valleyBotEpoch=MatchEpoch;valleyBotRound=MatchRound;
            }
            bool simulate=visible&&hardware&&!PageBlocksGameplay&&!Rig.Paused&&!PlatformUnarmed&&Gameplay.Health>0;
            ValleyBots.Step(Mathf.Clamp(dt,0,.25f),visible,simulate);
            if(ValleyMission!=null)ValleyMission.Step(Mathf.Clamp(dt,0,.25f),campaign,simulate&&ValleyBots.Ready);
        }
        Vector3 LocalValleyCheckpoint()
        {var p=ValleyBots.Mission.Checkpoint;return Map.transform.TransformPoint(new Vector3(p.X,p.Y,p.Z));}
        Vector3 WarmupOrValleyCheckpoint()
        {return Map.WarmupSpawn;}
        void BeginValleyChapter()
        {
            // Source _begin_chapter preserves the acquired weapon inventory and
            // player position, resets builds/projectiles and relocates supplies
            // to the new checkpoint. Do not ResetRound/regrant carried weapons.
            StowPaddles();activeSpawn=LocalValleyCheckpoint();Activities.ResetRound();Gameplay.ClearImpactMarks();Gameplay.ClearSmawProjectiles();if(Explosives!=null)Explosives.ClearProjectiles();if(WorldPing!=null)WorldPing.ClearAll();PositionPracticeTargets();SpawnConsumables();RefreshLoadout();
        }
        public void SpawnValleyEnemySupply(string defeatedId,Vector3 position)
        {string id="valley-drop/"+defeatedId;if(Supplies!=null&&Supplies.Spawn(id,"builds",position,6))activeSupplyIds.Add(id);}
        public bool LoadValleyCheckpoint()
        {if(!ValleyCampaignContext()||ValleyMission==null||!ValleyMission.LoadCheckpoint())return false;BeginValleyCampaignMatch();return true;}
        bool ValleyCampaignContext(){return Ready&&Map!=null&&Map.MapId=="valley"&&ActualMatchRules==null&&pendingMatch==null&&(LanSession==null||LanSession.Lobby==null);}
        void ContinueValleyCampaign(){LoadValleyCheckpoint();}
        public void StartNewValleyCampaign()
        {if(!ValleyCampaignContext())return;ValleyBots.Mission.Restore(0,0,0);ValleyMission.SaveCheckpoint();BeginValleyCampaignMatch();}
        void BeginValleyCampaignMatch()
        {valleyCampaignMatch=R24PlayableMatchRules.ForPreset("team_respawn","valley").With("time_limit",0).With("score_limit",0);RequestLocalMatch(valleyCampaignMatch);}
        void NotifyValleyGunshot()
        {if(ValleyBots==null||!ValleyBots.Ready)return;R23ShotPose pose;if(Gameplay.TryReadLastAcceptedShotPose(out pose))ValleyBots.NotifyGunshot(new Vector3(pose.Origin.X,pose.Origin.Y,pose.Origin.Z));}
    }
}
#endif
