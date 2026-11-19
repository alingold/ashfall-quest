#if UNITY_6000_0_OR_NEWER
using System;using System.Globalization;using UnityEngine;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        // RULES19 evidence (-ashfallNetBotRulesEvidence true, with the warm-up options): the host changes HEADSHOTS and BUILD DAMAGE
        // through the Match Rules UI during warm-up; the guest logs what its room rules read.
        bool RulesEvidenceEnabled{get{return Arg("-ashfallNetBotRulesEvidence")=="true";}}
        float rulesEvidenceBegan=-1,rulesEvidenceNext;bool rulesEvidenceSet;string rulesEvidenceLast="";
        static string RulesText(R24PlayableMatchRules r){return r==null?"none":"mode="+r.Mode+" headshots="+r.Headshots.ToString("F2",CultureInfo.InvariantCulture)+" build_damage="+r.BuildDamage.ToString("F2",CultureInfo.InvariantCulture)+" time_limit="+r.TimeLimit+" has_time_cap="+r.HasTimeCap;}
        void CaptureRulesEvidence()
        {
            if(!RulesEvidenceEnabled||root==null||!root.Ready||log==null||closed)return;var lobby=root.LanSession.Lobby;if(lobby==null)return;var map=lobby.SelectedMap;
            float now=Time.realtimeSinceStartup;
            if(mode=="host"){
                if(map==null||!map.Active||map.Cancelled||map.Rules!=null||lobby.MemberCount<2)return;
                if(rulesEvidenceBegan<0){rulesEvidenceBegan=now;Write("rules-evidence","host warm-up open "+RulesText(map.SourceRules));}
                if(!rulesEvidenceSet&&now-rulesEvidenceBegan>=14){
                    rulesEvidenceSet=true;var ui=root.UI;
                    bool avail=ui.IsMatchRuleAvailable("headshots")&&ui.IsMatchRuleAvailable("build_damage");bool head=ui.SetMatchRule("headshots",3f),build=ui.SetMatchRule("build_damage",0f);
                    Write("rules-evidence","host UI set headshots=300% accepted="+head+" build_damage=0% accepted="+build+" available="+avail+" time_cap_editable_in_team_revive="+ui.IsMatchRuleAvailable("time_limit")+" ui="+RulesText(ui.SelectedMatchRules));
                }
                if(rulesEvidenceSet&&now>=rulesEvidenceNext){rulesEvidenceNext=now+2;string row=RulesText(lobby.SelectedMap==null?null:lobby.SelectedMap.SourceRules);if(row!=rulesEvidenceLast){rulesEvidenceLast=row;Write("rules-evidence","host room rules "+row);}}
            }else if(mode=="join"){
                if(now<rulesEvidenceNext)return;rulesEvidenceNext=now+1;string row=RulesText(map==null?null:map.SourceRules)+" mapActive="+(map!=null&&map.Active)+" round="+(map!=null&&map.Rules!=null);
                if(row!=rulesEvidenceLast){rulesEvidenceLast=row;Write("rules-evidence","guest room rules "+row);}
            }
        }
    }
}
#endif
