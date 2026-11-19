#if UNITY_6000_0_OR_NEWER
using System;using System.Globalization;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        bool HitregEnabled{get{return Arg("-ashfallNetBotHitreg")=="true";}}
        R24LanLobby hitregLobby;
        R24PlayableMatchRules HitregRoundRules()
        {
            string preset=Arg("-ashfallNetBotMatch");if(preset.Length==0)preset="free_for_all";
            float damage=RemoteEvidenceEnabled?4f:0f,value;
            string requested=Arg("-ashfallNetBotDamage");if(requested.Length>0){if(!Single.TryParse(requested,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||Single.IsNaN(value)||Single.IsInfinity(value)||value<0)throw new ArgumentException("Invalid NetBot damage");damage=value;}
            var rules=R24PlayableMatchRules.ForPreset(preset,root.Map.MapId).With("zone_enabled",false).With("damage",damage);
            if(Arg("-ashfallNetBotUnlimitedMatch")=="true")rules=rules.With("time_limit",0).With("score_limit",0);
            if(HitregEnabled)rules=rules.With("protection",0f);
            Write("requested-rules","preset="+preset+" teams="+rules.Teams+" damage="+rules.Damage+" hitreg="+HitregEnabled);return rules;
        }
        void AimHitreg(R24LanPoseSnapshot targetPose,ref Vector2 move,ref float height,ref Vector3 left,ref Vector3 right,ref Quaternion aim,ref float trigger,ref float grip)
        {
            if(!HitregEnabled)return;BindHitreg();
            if(root.LanSession.Lobby!=null&&Arg("-ashfallNetBotFire")=="false"){trigger=0;return;}
            if(targetPose==null||!root.Gameplay.HasWeapon)return;
            Vector3 point=V(targetPose.Pose.Head.Position)-Vector3.up*.45f;
            aim=Quaternion.LookRotation(point-right)*Quaternion.Inverse(Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.LocalRotation);
            trigger=(Time.realtimeSinceStartup-playBegan)%2<.35f?1:0;
            // Keep the normal swept motor, tracked sampling, custody, shot publication and host authority.
        }
        void BindHitreg()
        {
            var lobby=root.LanSession.Lobby;if(hitregLobby==lobby)return;if(hitregLobby!=null)hitregLobby.ShotDiagnostics-=LogShotAdmission;hitregLobby=lobby;
            if(lobby!=null)lobby.ShotDiagnostics+=LogShotAdmission;
        }
        void LogShotAdmission(string detail){Write("shot-admission",detail);}
        void ObserveHitreg()
        {
            if(!HitregEnabled)return;BindHitreg();var lobby=root.LanSession.Lobby;if(lobby==null)return;
            foreach(var member in lobby.Members){R24LanHealthState health;R24LanRawTrackingSample raw;bool fresh=lobby.TryReadRawTracking(member.Actor,lobby.EstimatedHostTime,out raw);
                Write("hitreg-member","actor="+member.Actor+" host="+member.Host+" rosterTeam="+member.Team+" healthTeam="+(lobby.TryReadHealth(member.Actor,out health)?health.Team.ToString():"missing")+" rawFresh="+fresh+" rawTime="+(raw==null?"none":raw.Time.ToString("F3",CultureInfo.InvariantCulture))+" rawRejection="+lobby.LastRawTrackingRejection);
            }
        }
    }
}
#endif
