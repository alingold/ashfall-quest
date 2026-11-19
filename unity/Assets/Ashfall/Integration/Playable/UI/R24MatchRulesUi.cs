#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.Match;
using UnityEngine;

namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        R24PlayableMatchRules selectedMatchRules;
        int matchTab,matchRulePage;
        string localMatchStatus="";
        // Each enabled control has an actual local lifecycle/owner consumer. Root may
        // enable additional source controls only after wiring their production owners.
        readonly HashSet<string> availableMatchRules=new HashSet<string>(StringComparer.Ordinal){
            "teams","mode","time_limit","rounds","damage","headshots","build_damage","friendly_fire","enemy_revive","starting_health","starting_shield",
            "fists","parry","walk","fling","starting_builds","loadout","loot",
            "tanks","helicopters","vehicles","jetpack","grapple_gun","grapple_pull","grapple_pull_friends","flashbangs","smoke_grenades","catalog","respawn_delay","protection","downed_seconds","zone_enabled","zone_damage","zone_speed","zone_wait"
        };
        TextMesh matchStatusText,matchExplanationText;
        Button matchStartButton;
        readonly Button[] matchPresetButtons=new Button[MatchPresetIds.Length];
        static readonly string[] MatchTabs={"HOSTING","SESSION","COMBAT","MOVEMENT","EQUIPMENT","RESPAWN","ZONE"};
        static readonly string[][] MatchKeys={
            new string[0], // Truthful local hosting information, no fake transport controls.
            // Source network-only MAX PLAYERS / LATE JOIN / SPECTATORS / LOCKED and
            // PLAYERS roster are deliberately absent from this offline local page.
            new[]{"teams","mode","time_limit","score_limit","rounds"},
            new[]{"damage","build_damage","headshots","friendly_fire","starting_health","starting_shield"},
            new[]{"tanks","helicopters","vehicles","jetpack","grapple_gun","grapple_pull","grapple_pull_friends","walk","run","flight","fling","stamina"},
            new[]{"flashbangs","smoke_grenades","fists","parry","loadout","loot","catalog","builds","build_pieces","starting_builds","infinite_builds","infinite_ammo"},
            new[]{"respawn_delay","protection","downed_seconds","enemy_revive"},
            new[]{"zone_enabled","zone_damage","zone_speed","zone_wait"}
        };
        static readonly string[] MatchPresetIds={"team_revive","team_respawn","free_for_all"};
        static readonly string[] MatchPresetLabels={"TEAM REVIVE","TEAM DEATHMATCH","FREE-FOR-ALL"};
        public R24PlayableMatchRules SelectedMatchRules {get{return selectedMatchRules;}}
        public string LocalMatchStatus {get{return localMatchStatus;}}
        // Pending host data is not an actual Alive/Dead life state.
        bool pauseRoundActive;
        public void SetPauseRoundActive(bool active){if(pauseRoundActive==active)return;pauseRoundActive=active;if(Page==R23UiPage.Pause||Page==R23UiPage.MatchRules)Build();}
        bool authoritativeHealthPending;
        public bool AuthoritativeHealthPending{get{return authoritativeHealthPending;}}
        public void SetAuthoritativeHealthPending(bool pending){authoritativeHealthPending=pending;}
        string localMatchLife;float localMatchLifeRemaining;bool localMatchResults;TextMesh matchLifeNotice;
        public string LocalMatchLifeState {get{return localMatchLife;}}
        public string LocalMatchLifeCaption {get{return R24MatchLifeText.Notice(localMatchLife,localMatchLifeRemaining,localMatchResults);}}
        public void SetLocalMatchLifeState(string lifeName,float remainingSeconds,bool results=false)
        {
            Require();string state=R24MatchLifeText.Normalize(lifeName);
            if(float.IsNaN(remainingSeconds)||float.IsInfinity(remainingSeconds))throw new ArgumentOutOfRangeException("remainingSeconds");
            bool changed=state!=localMatchLife||results!=localMatchResults;
            localMatchLife=state;localMatchLifeRemaining=Mathf.Max(0,remainingSeconds);localMatchResults=state!=null&&results;
            // Source match_hud_r3 is a notice, not a field-menu page. Actual open
            // menus stay open; only an obsolete death modal is returned to HUD.
            if(state!=null&&(!localMatchResults||state=="alive")&&Page==R23UiPage.Dead){SetPlaying();return;}
            if(localMatchResults&&state!="alive"&&Page==R23UiPage.Playing){SetPage(R23UiPage.Dead);return;}
            if(Page==R23UiPage.Dead&&changed)Build();
            else if(Page==R23UiPage.Dead&&matchLifeNotice!=null)matchLifeNotice.text=LocalMatchLifeCaption;
            RefreshLocalMatchGhostHud();
        }
        void BuildMatchLifePage()
        {
            title.text=R24MatchLifeText.Title(localMatchLife,localMatchResults);
            matchLifeNotice=Text(panel,LocalMatchLifeCaption,new Vector3(0,.22f,-.01f),.026f,1.16f,Hex(R25Theme.Text));
            if(localMatchResults)BuildSurvivalResults();
            // Results: the host (or offline player) restarts; guests wait for the host. HOME follows the room rules.
            if(localMatchResults&&!RoomGuest)MenuCard(RestartMatchLabel,-.07f,"pause0993_0",delegate{if(RestartRequested!=null)RestartRequested();});
            else if(localMatchResults)Text(panel,"THE HOST STARTS THE NEXT MATCH",new Vector3(0,-.07f,-.01f),.024f,1.16f,Hex(R25Theme.TextDim));
            AddButton(panel,HomeCaption(),0,-.51f,1.2f,.11f,RequestHome,.033f);
        }
        public event Action<R24PlayableMatchRules> MatchRulesChanged;
        public event Action<R24PlayableMatchRules> MatchStartRequested;
        public void OpenMatchRules(){matchTab=0;OpenSubpage(R23UiPage.MatchRules);}
        public void SetLocalMatchStatus(string value)
        {
            localMatchStatus=Clean(value,140);
            if(Page==R23UiPage.MatchRules&&matchStatusText!=null)matchStatusText.text=MatchStatusCaption();
        }
        string MatchMapId(){return SelectedMapId??"range";}
        R24PlayableMatchRules DisplayMatchRules {get{return selectedMatchRules??R24PlayableMatchRules.ForPreset("team_revive",MatchMapId());}}
        public void SelectMatchPreset(string presetId)
        {
            if(LanMapHostOnly)return;
            Require();PublishMatchRules(R24PlayableMatchRules.ForPreset(presetId,MatchMapId()));
            if(Page==R23UiPage.MatchRules)Build();
        }
        public void SetMatchRuleAvailable(string sourceKey,bool available)
        {
            Require();DisplayMatchRules.ReadSourceValue(sourceKey);
            if(available)availableMatchRules.Add(sourceKey);else availableMatchRules.Remove(sourceKey);
            if(Page==R23UiPage.MatchRules)Build();
        }
        public bool IsMatchRuleAvailable(string sourceKey){return !LanMapHostOnly&&!(sourceKey=="rounds"&&R24ArchiveRoundPolicy.FiveLevels(MatchMapId()))&&availableMatchRules.Contains(sourceKey)&&!(DisplayMatchRules.Mode!="revive"&&sourceKey.StartsWith("zone_",StringComparison.Ordinal))&&!(sourceKey=="teams"&&DisplayMatchRules.Teams==0)&&!(sourceKey=="time_limit"&&!DisplayMatchRules.HasTimeCap);}
        public bool SetMatchRule(string sourceKey,object value)
        {
            Require();if(!IsMatchRuleAvailable(sourceKey))return false;
            string mode=DisplayMatchRules.Mode;
            PublishMatchRules(DisplayMatchRules.With(sourceKey,value).WithMap(MatchMapId()));
            if(Page==R23UiPage.MatchRules){if(mode!=DisplayMatchRules.Mode)Build();else{RefreshSettingsRows();RefreshMatchPresetButtons();}}
            return true;
        }
        public void ClearMatchSelection()
        {
            if(LanMapHostOnly)return;
            Require();selectedMatchRules=null;if(MatchRulesChanged!=null)MatchRulesChanged(null);
            if(Page==R23UiPage.MatchRules)Build();
        }
        public bool CanStartSelectedMatch {get{return !LanMapHostOnly&&SelectedMapId!=null&&MatchStartRequested!=null;}}
        public void ApplyLanMatchRules(R24PlayableMatchRules rules){if(rules==null)throw new ArgumentNullException("rules");selectedMatchRules=runningMatchRules=rules;if(Page==R23UiPage.MatchRules||Page==R23UiPage.Ready)Build();}
        public bool RequestMatchStart()
        {
            Require();
            if(!CanStartSelectedMatch||Page==R23UiPage.Loading||Page==R23UiPage.Error||Page==R23UiPage.Dead)return false;
            // A room that is loading, counting down or entering a world takes no second start.
            if(!MayControlMatch){SetRoomNotice("Wait for the room to finish loading");return false;}
            var request=DisplayMatchRules.WithMap(SelectedMapId);PublishMatchRules(request);runningMatchRules=request;
            // Actual Root owns map loading, Health, countdown and match launch.
            // No SetPlaying, legacy StartRequested or exploration MapSelected here.
            if(MatchStartRequested==null)return false;
            MatchStartRequested(request);return true;
        }
        void PublishMatchRules(R24PlayableMatchRules value)
        {
            selectedMatchRules=value;
            if(MatchRulesChanged!=null)MatchRulesChanged(value);
        }
        string NextMatchCaption()
        {var rules=DisplayMatchRules;return "NEXT MATCH: "+MatchPresetLabels[Array.IndexOf(MatchPresetIds,rules.PresetId)]+" / CATALOG "+(rules.Catalog?"ON":"OFF");}
        string MatchStatusCaption()
        {
            // Line 1: what is running now, else what START MATCH would start. Line 2: the real session.
            string running=room.Preset.Length>0?PresetLabel(room.Preset):runningMatchRules!=null?PresetLabel(runningMatchRules.PresetId):"";
            string selection=pauseRoundActive?"MATCH RUNNING"+(running.Length>0?" / "+running:""):selectedMatchRules==null?"WARM-UP / START USES TEAM REVIVE":"NEXT MATCH / "+PresetLabel(selectedMatchRules.PresetId);
            // In a room the session line is read from the room itself, never from a local "offline" label.
            string detail=InRoomUi?Networking.R26RoomStatus.MatchDetail(localMatchStatus):localMatchStatus;
            string session=InRoomUi?RoomLine()+(detail.Length>0?"   |   "+detail:""):detail;
            return selection+(String.IsNullOrEmpty(session)?"":"\n"+session);
        }
        void BuildMatchRules()
        {
            title.text="";
            // Actual front_menu1000 host-rule board coordinates, now honest local use.
            Quad(panel,"Source match rules backing",new Vector3(0,0,.002f),new Vector2(2.2f,1.375f),Hex(R25Theme.Panel));
            var heading=FrontText("MATCH RULES",40,60,32,1180);heading.anchor=TextAnchor.MiddleLeft;heading.alignment=TextAlignment.Left;
            matchStatusText=FrontText(MatchStatusCaption(),40,122,19,1180);matchStatusText.anchor=TextAnchor.MiddleLeft;matchStatusText.alignment=TextAlignment.Left;matchStatusText.color=Hex(R25Theme.Teal);
            for(int i=0;i<MatchTabs.Length;i++)
            {
                int tab=i;var button=FrontButton(MatchTabs[i],35+i*176,160,166,54,delegate{matchTab=tab;matchRulePage=0;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),20);
                button.Help=R24SettingsHelpCopy.MatchTab(i);RegisterSettingsHelp(button.Surface,Vector3.zero,button.Size,button.Help);
                if(i==matchTab)SetButtonStyle(button,Hex(R25Theme.RaisedSelected),Hex(R25Theme.Teal));
            }
            if(matchTab==0)BuildLocalHosting();
            else{matchExplanationText=FrontText(MatchModeExplanation(),640,228,17,1180);matchExplanationText.color=Hex(R25Theme.TextDim);}
            bool unavailable=false;
            int count=MatchKeys[matchTab].Length;int pages=Mathf.Max(1,(count+7)/8);matchRulePage=Mathf.Clamp(matchRulePage,0,pages-1);
            for(int i=matchRulePage*8;i<Mathf.Min(count,(matchRulePage+1)*8);i++){string key=MatchKeys[matchTab][i];BuildMatchRuleRow(key,i%8);if(!availableMatchRules.Contains(key))unavailable=true;}
            if(pages>1){FrontButton("RULES <",40,596,170,32,delegate{matchRulePage=(matchRulePage+pages-1)%pages;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),17);FrontButton("RULES >",1065,596,170,32,delegate{matchRulePage=(matchRulePage+1)%pages;Build();},Hex(R25Theme.Raised),Hex(R25Theme.Text),17);}
            if(matchTab==2)BuildBotDifficultyRow(MatchKeys[matchTab].Length);
            if(matchTab!=0)BuildSettingsHelp(true,false,unavailable?"Dimmed settings are locked in this local build.\nPoint at a setting to see why.":null);
            float[] x={260,520,790},width={245,255,235};
            for(int i=0;i<MatchPresetIds.Length;i++)
            {
                string preset=MatchPresetIds[i];matchPresetButtons[i]=FrontButton(MatchPresetLabels[i],x[i],655,width[i],50,delegate{SelectMatchPreset(preset);},Hex(R25Theme.Raised),Hex(R25Theme.Text),20);
                matchPresetButtons[i].Help=R24SettingsHelpCopy.Preset(preset);RegisterSettingsHelp(matchPresetButtons[i].Surface,Vector3.zero,matchPresetButtons[i].Size,matchPresetButtons[i].Help);
            }
            FrontButton("BACK",40,710,170,58,delegate{SetPage(returnPage);},Hex(R25Theme.Raised),Hex(R25Theme.Text),22);
            var bots=FrontText("BOTS: SINGLE-PLAYER MATCHES ON RELAY VALLEY",640,641,15,1180);bots.color=Hex(R25Theme.TextDim);
            // The same match actions, labels and handlers as the pause menu (R26VrUI.Rooms.cs). Guests get none.
            matchStartButton=null;
            if(RoomGuest){var wait=FrontText("THE HOST CONTROLS THE MATCH",985,745,19,500);wait.color=Hex(R25Theme.TextDim);}
            else if(pauseRoundActive)
            {
                var restart=FrontButton(RestartMatchLabel,730,720,240,50,RequestRestartMatch,Hex(R25Theme.Raised),Hex(R25Theme.Text),21);StyleAction(restart,false,MayControlMatch);restart.Help="Start this match again from round 1.";
                var end=FrontButton(EndMatchLabel,985,720,240,50,RequestEndMatch,Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber),21);StyleAction(end,true,MayControlMatch);end.Help="End the match. Everyone stays on this map for warm-up.";
            }
            else{matchStartButton=FrontButton(StartActionLabel,985,720,240,50,delegate{RequestMatchStart();},Hex(R25Theme.Primary),Hex(R25Theme.TextOnEmber),21);StyleAction(matchStartButton,true,MayControlMatch&&CanStartSelectedMatch);}
            RefreshSettingsRows();RefreshMatchPresetButtons();
        }
        void BuildLocalHosting()
        {
            if(InRoomUi){BuildRoomSummary();return;}
            BuildLanHosting();
        }
        string MatchModeExplanation()
        {
            if(matchTab==4)return "The backpack catalog is open in warm-up. During a match, this control toggles catalog access.";
            if(lanState!=Networking.R24LanLobbyState.Offline)return DisplayMatchRules.Mode=="revive"?"The host controls shared round rules. Team Revive has no time cap: it ends when one team is eliminated.":"The host controls shared round rules, damage, rescues and timing.";
            var rules=DisplayMatchRules;
            if(rules.Teams==0)return "FREE-FOR-ALL: each player fights for themselves and respawns after death.";
            return rules.Mode=="revive"?"TEAM REVIVE: rescue downed teammates. No time cap: the match ends only when one team is eliminated.":"TEAM DEATHMATCH: teams score kills and players respawn after death.";
        }
        string UnavailableMatchExplanation()
        {
            if(matchTab==1)return "KILL CAP is disabled: multiplayer scoring is unavailable. Dimmed controls do not change gameplay.";
            if(matchTab==2)return "Dimmed damage and friendly-fire controls are unavailable in this local build.";
            if(matchTab==3)return "Dimmed run, flight and stamina multipliers are unavailable; personal controls still apply.";
            if(matchTab==4)return "Starting builds use 999.";
            return "Dimmed controls are unavailable in this local build and do not change gameplay.";
        }
        string MatchRowCaption(string key)
        {
            string label=key=="jetpack"?"JET PACK":key=="grapple_gun"?"GRAPPLE GUN":key=="flashbangs"?"FLASHBANGS":key=="smoke_grenades"?"SMOKE GRENADES":key=="catalog"?"BACKPACK CATALOG":key=="score_limit"?"KILL CAP":key=="time_limit"?"TIME CAP":key.Replace('_',' ').ToUpperInvariant();
            return label+"     "+DisplayMatchRules.Pretty(key);
        }
        void BuildMatchRuleRow(string sourceKey,int rowIndex)
        {
            if(sourceKey=="score_limit"&&DisplayMatchRules.Mode=="revive")return;
            string key=sourceKey;bool available=IsMatchRuleAvailable(key);string help=LanMapHostOnly?"The LAN host edits shared match rules.":key=="time_limit"&&!DisplayMatchRules.HasTimeCap?"Team Revive has no time cap.\nThe match ends only when one team is eliminated.\nTime cap applies to Team Deathmatch and Free-for-all.":DisplayMatchRules.Mode!="revive"&&key.StartsWith("zone_",StringComparison.Ordinal)?"The zone is available in Team Revive.":key=="teams"&&DisplayMatchRules.Teams==0?"Select Team Revive or Team Deathmatch to set a team count.":key=="friendly_fire"?"Allow teammates to damage each other.":key=="enemy_revive"?"Allow rescue of downed players on another team.":R24SettingsHelpCopy.Match(key,available);
            RegisterSettingsHelp(panel,FrontPoint(640,258+rowIndex*43),new Vector2(1120*2.2f/1280,40*1.375f/800),help);
            var text=FrontText(MatchRowCaption(key),130,258+rowIndex*43,21,800);text.anchor=TextAnchor.MiddleLeft;text.alignment=TextAlignment.Left;if(!available)text.color=Hex(R25Theme.TextDisabled);
            Quad(panel,"Source rule guide "+key,FrontPoint(835,251+rowIndex*43),new Vector2(210*2.2f/1280,1.375f/800),Hex(R25Theme.Stroke));
            float min,max;
            if(!(key=="teams"&&DisplayMatchRules.Teams==0)&&R24PlayableMatchRules.TryGetRange(key,out min,out max))
            {
                float lower=min,upper=max,step=DisplayMatchRules.ReadSourceValue(key) is int?1:.05f;if(key=="time_limit")step=60;
                float width=345*2.2f/1280;
                var button=FrontButton("",810,235+rowIndex*43,345,32,null,Color.clear,Hex(R25Theme.Text),21);button.Enabled=available;button.Help=help;RegisterSettingsHelp(button.Surface,Vector3.zero,button.Size,button.Help);
                Transform fill,knob;BuildSettingsSlider(button,width,available,out fill,out knob);
                button.SliderChanged=delegate(float fraction){float next=Mathf.Clamp(Mathf.Round(Mathf.Lerp(lower,upper,fraction)/step)*step,lower,upper);SetMatchRule(key,next);};
                numericRows.Add(new NumericRow{Caption=delegate{return MatchRowCaption(key);},Value=delegate{return Convert.ToSingle(DisplayMatchRules.ReadSourceValue(key));},Min=min,Max=max,Text=text,Fill=fill,Knob=knob,Width=width});
            }
            else
            {
                var button=FrontButton(DisplayMatchRules.Pretty(key),810,232+rowIndex*43,345,39,delegate{CycleMatchRule(key);},Hex(R25Theme.Raised),Hex(R25Theme.Text),21);
                button.Enabled=available;button.Help=help;RegisterSettingsHelp(button.Surface,Vector3.zero,button.Size,button.Help);
                numericRows.Add(new NumericRow{Caption=delegate{return MatchRowCaption(key);},Text=text});
                numericRows.Add(new NumericRow{Caption=delegate{return DisplayMatchRules.Pretty(key);},Text=button.Caption});
            }
        }
        void CycleMatchRule(string key)
        {
            object current=DisplayMatchRules.ReadSourceValue(key);
            if(current is bool){SetMatchRule(key,!(bool)current);return;}
            var choices=R24PlayableMatchRules.Choices(key);if(choices.Length==0)return;
            SetMatchRule(key,choices[(Array.IndexOf(choices,(string)current)+1)%choices.Length]);
        }
        void RefreshMatchPresetButtons()
        {
            for(int i=0;i<matchPresetButtons.Length;i++)
            {
                var button=matchPresetButtons[i];if(button==null||button.Surface==null)continue;
                bool selected=selectedMatchRules!=null&&selectedMatchRules.PresetId==MatchPresetIds[i];
                button.Caption.text=(selected?"\u25cf ":"")+MatchPresetLabels[i];button.Enabled=!LanMapHostOnly;SetButtonStyle(button,Hex(LanMapHostOnly?R25Theme.Disabled:selected?R25Theme.RaisedSelected:R25Theme.Raised),LanMapHostOnly?Hex(R25Theme.TextDisabled):selected?Hex(R25Theme.EmberHot):Hex(R25Theme.Text));
            }
            if(matchStatusText!=null)matchStatusText.text=MatchStatusCaption();
            if(matchExplanationText!=null)matchExplanationText.text=MatchModeExplanation();
            if(matchStartButton!=null)matchStartButton.Enabled=MayControlMatch&&CanStartSelectedMatch;
        }
    }
}
#endif
