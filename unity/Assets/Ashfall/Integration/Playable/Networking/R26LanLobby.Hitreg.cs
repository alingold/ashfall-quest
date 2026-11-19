using System;using System.Text;using System.Collections.Generic;using System.Globalization;using Ashfall.Core;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        // Explicit rig subscription only: ordinary clients/hosts do not collect diagnostics.
        public Action<string> ShotDiagnostics;
        StringBuilder hitregTargets;
        partial void HitregTarget(string actor,bool head,float distance)
        {if(hitregTargets!=null)hitregTargets.Append(" target=").Append(actor??"none").Append(" head=").Append(head).Append(" distance=").Append(distance.ToString("F3",CultureInfo.InvariantCulture));}
        StringBuilder hitregDetail;Dictionary<string,float> hitregBefore;
        partial void BeforeHitregShot(string actor,R24LanShot shot)
        {
            if(ShotDiagnostics==null)return;var detail=new StringBuilder();var before=new Dictionary<string,float>();
            detail.Append("shooter=").Append(actor).Append(" profile=").Append(shot==null?"null":shot.Profile).Append(" stamp=").Append(shot==null?"null":shot.Time.ToString("F3",CultureInfo.InvariantCulture)).Append(" now=").Append(clock.Now.ToString("F3",CultureInfo.InvariantCulture)).Append(" combatLive=").Append(CombatLive).Append(" countdown=").Append(RoundCountdown);
            foreach(var member in members){R24LanHealthState h;R24LanRawTrackingSample raw;bool fresh=Fresh(member.Actor,clock.Now,out raw);if(TryReadHealth(member.Actor,out h))before[member.Actor]=h.Health+h.Shield;
                detail.Append(" candidate=").Append(member.Actor).Append(" team=").Append(member.Team).Append(" fresh=").Append(fresh).Append(" rawAge=").Append(raw==null?"none":(clock.Now-raw.Time).ToString("F3",CultureInfo.InvariantCulture));}
            hitregDetail=detail;hitregBefore=before;hitregTargets=new StringBuilder();
        }
        partial void AfterHitregShot(string actor,R24LanShot shot,bool accepted)
        {
            if(hitregDetail==null)return;var detail=hitregDetail;var before=hitregBefore;var targets=hitregTargets;hitregDetail=null;hitregBefore=null;hitregTargets=null;
            detail.Append(" accepted=").Append(accepted).Append(" rejected=").Append(!accepted).Append(" reason=").Append(LastShotRejection??"none").Append(targets);
            foreach(var entry in before){R24LanHealthState after;if(TryReadHealth(entry.Key,out after))detail.Append(" damageTo=").Append(entry.Key).Append(':').Append((entry.Value-after.Health-after.Shield).ToString("F3",CultureInfo.InvariantCulture));}
            ShotDiagnostics(detail.ToString());
        }
    }
}
