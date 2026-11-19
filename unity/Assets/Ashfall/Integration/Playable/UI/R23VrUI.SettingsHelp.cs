using System;
namespace Ashfall.Integration.Playable.UI
{
    // Copy describes current actual consumers. No setting or availability owner here.
    internal static class R24SettingsHelpCopy
    {
        internal static bool PersonalAvailable(int field){return field>=0&&field<=10&&field!=6;}
        internal static string Personal(int field)
        {
            switch(field){
            case 0:return "Degrees per snap turn.\nUsed when TURNING is SNAP.";
            case 1:return "Turning speed in degrees per second.\nUsed when TURNING is SMOOTH.";
            case 2:return "Personal walking-speed multiplier.\nMatch rules can override this value.";
            case 3:return "Volume of all game audio.";
            case 4:return "Weapon, movement, menu and interaction sounds.";
            case 5:return "Volume of the original synchronized music.\nZero mutes music without changing SFX.";
            case 6:return "Unavailable: player voice volume is not connected.\nYour saved preference is preserved.";
            case 7:return "World wind and environmental sounds.\nSeparate from weapons and flying sounds.";
            case 8:return "Launch strength when you release a climb.";
            case 9:return "Smooths carried weapon aim.\nHigher values reduce jitter but add lag.";
            case 10:return "How strongly your support hand steers weapon aim.\nApplies while it is gripping the weapon.";
            default:return "Point and hold the trigger to drag the handle.";}
        }
        internal static string PersonalChoice(string key)
        {switch(key){case "player_name":return "Choose the name other players see.\nName change applies next room.";case "forward":return "Walking follows your head or left-controller heading.";case "turning":return "SNAP turns in steps. SMOOTH turns continuously.";case "grip_toggle":return "ON: tap grip to latch weapon support.\nOFF: hold grip to keep supporting the weapon.";case "dominant_hand":return "Chooses your main hand.\nHeld guns switch when a safe transfer is possible.";default:return "Point and release the trigger to change this setting.";}}
        internal static string PersonalMenu(string icon)
        {switch(icon){case "settings_0":return "Personal movement and turning preferences.";case "settings_1":return "Set full player height from the current headset pose.\nSeated and standing both work.";case "settings_2":return "Grip style, handedness and carried weapon aim tuning.";case "settings_3":return "Master, SFX, music and world ambience.\nPlayer voice is not connected.";default:return "Point at a setting for help.";}}
        internal static string Match(string key,bool available)
        {
            if(!available){switch(key){case "starting_builds":return "Starting-build selection is locked at 999.\nCustom starting amounts are not supported here.";case "score_limit":return "Unavailable: no multiplayer score tracking.\nTime and round limits still work.";case "headshots":return "Weapon headshot damage stays unchanged.\nThis match multiplier is not connected.";case "build_damage":return "Damage to structures keeps its current behavior.\nThis match multiplier is not connected.";case "friendly_fire":return "Unavailable: there are no remote teammates here.";case "run":case "flight":case "stamina":return "This match multiplier is unavailable here.\nPersonal movement behavior is unchanged.";default:return "Unavailable in this local build.\nThis control cannot change gameplay.";}}
            switch(key){
            case "teams":return "0 means no teams.\nFREE-FOR-ALL uses no-team respawn rules.";
            case "mode":return "REVIVE uses downed and bleed-out rules.\nDEATHMATCH allows automatic respawning.";
            case "time_limit":return "Match time cap for Team Deathmatch and Free-for-all.\nTeam Revive has no time cap: it ends when one team is eliminated.";
            case "rounds":return "Number of rounds before local match results.";
            case "damage":return "Incoming damage multiplier.\nZone damage uses its own setting.";
            case "headshots":return "Multiplier on headshot damage from guns.\n0% makes head hits do no damage; body hits are unchanged.";
            case "build_damage":return "Multiplier on damage to build pieces.\nApplies to guns, explosives, melee and vehicle guns.";
            case "starting_health":return "Health when you spawn into the match.";
            case "starting_shield":return "Shield when you spawn into the match.";
            case "walk":return "Walking-speed multiplier for this match.";
            case "fling":return "Climb-release launch strength for this match.";
            case "loadout":return "Weapons provided at the start of this match.";
            case "loot":return "Enable map caches and mounted weapon racks.";
            case "catalog":return "ON: full item catalog in the backpack.\nOFF: owned items only. Applies on START MATCH.";
            case "builds":return "Allow placing structures during the match.";
            case "infinite_builds":return "Build placement does not spend build resources.";
            case "infinite_ammo":return "Reserve ammunition does not run out.\nReloading and physical handling still apply.";
            case "respawn_delay":return "Wait after elimination before respawning.\nUsed by respawn rules.";
            case "protection":return "Spawn/revive protection time against damage.\nFatal falls are still possible.";
            case "downed_seconds":return "Time before a downed player bleeds out.\nUsed by revive rules.";
            case "zone_enabled":return "A shrinking square defines the safe area.\nOutside it, zone damage hits your health.";
            case "zone_damage":return "Health lost per second outside the square.\nShield does not absorb zone damage.";
            case "zone_speed":return "Speed of the shrinking square's edge, in m/s.";
            case "zone_wait":return "Wait in seconds before each zone shrink.";
            default:return "This rule is applied by START MATCH.";}
        }
        internal static string Preset(string id)
        {switch(id){case "free_for_all":return "Free-for-all: respawn rules with no teams.\nThis local build has one player.";case "team_revive":return "Downed/bleed-out rules and team deployment.\nNo remote teammate is connected.";case "team_respawn":return "Team rules with automatic respawning.\nNo remote teammate is connected.";default:return "Select rules for your next match.";}}
        internal static string MatchTab(int tab)
        {switch(tab){case 0:return "Host or join on the same local network.\nThe host selects maps and starts rounds.";case 1:return "Teams, respawn mode, time limit and rounds.";case 2:return "Starting health, shield and damage rules.";case 3:return "Movement settings for your next match.";case 4:return "Starting gear, world loot, catalog and resources.";case 5:return "Respawn wait, protection and downed time.";case 6:return "Safe-area shrinking and outside-zone damage.";default:return "Point at a rule for help.";}}
    }
}
#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.UI
{
    public sealed partial class R23VrUI
    {
        sealed class SettingHelpRegion{internal UnityEngine.Transform Space;internal UnityEngine.Vector3 Center;internal UnityEngine.Vector2 Size;internal string Copy;}
        readonly System.Collections.Generic.List<SettingHelpRegion> settingHelpRegions=new System.Collections.Generic.List<SettingHelpRegion>();
        readonly string[] pointedSettingsHelp=new string[2];UnityEngine.TextMesh settingsHelpText;string settingsHelpDefault;
        public string CurrentSettingsHelp{get{return settingsHelpText==null?"":settingsHelpText.text;}}
        void ClearPointedSettingsHelp(){pointedSettingsHelp[0]=pointedSettingsHelp[1]=null;if(settingsHelpText!=null)settingsHelpText.text=settingsHelpDefault;}
        void ClearSettingsHelp(){settingHelpRegions.Clear();pointedSettingsHelp[0]=pointedSettingsHelp[1]=null;settingsHelpText=null;settingsHelpDefault=null;}
        void RegisterSettingsHelp(UnityEngine.Transform space,UnityEngine.Vector3 center,UnityEngine.Vector2 size,string copy)
        {if(Page!=R23UiPage.Settings&&!String.IsNullOrEmpty(copy))settingHelpRegions.Add(new SettingHelpRegion{Space=space,Center=center,Size=size,Copy=copy});}
        void UpdateSettingsHelpRay(int hand,UnityEngine.Vector3 origin,UnityEngine.Vector3 direction,bool valid)
        {
            pointedSettingsHelp[hand]=null;if(!valid||settingsHelpText==null)return;
            float nearest=float.PositiveInfinity;
            foreach(var region in settingHelpRegions)
            {
                if(region.Space==null||!region.Space.gameObject.activeInHierarchy)continue;
                var local=region.Space.InverseTransformPoint(origin)-region.Center;var forward=region.Space.InverseTransformDirection(direction.normalized);
                if(UnityEngine.Mathf.Abs(forward.z)<.00001f)continue;float distance=-local.z/forward.z;if(distance<0||distance>8||distance>=nearest)continue;var at=local+forward*distance;
                if(UnityEngine.Mathf.Abs(at.x)>region.Size.x*.5f||UnityEngine.Mathf.Abs(at.y)>region.Size.y*.5f)continue;nearest=distance;pointedSettingsHelp[hand]=region.Copy;
            }
        }
        void RefreshSettingsHelp()
        {
            if(settingsHelpText==null)return;
            string copy=sliderDrag[1]!=null?sliderDrag[1].Help:sliderDrag[0]!=null?sliderDrag[0].Help:null;
            if(String.IsNullOrEmpty(copy))copy=pointedSettingsHelp[1]??pointedSettingsHelp[0]??settingsHelpDefault;
            if(settingsHelpText.text!=copy)settingsHelpText.text=copy;
        }
        // Called by BuildSettingsHelp on GENERAL, below AMBIENCE VOLUME.
        void BuildPlayerNameRow()
        {
            var button=AddButton(panel,"NAME    "+R26PlayerName.Current,0,-.205f,.90f,.085f,OpenNameKeyboard,.03f);PreparePersonalChoice(button,"player_name");
            Text(panel,"Other players see this name.\nName change applies next room.",new UnityEngine.Vector3(0,-.31f,-.025f),.019f,1.12f,Hex(R25Theme.TextDim));
        }
        void BuildSettingsHelp(bool match,bool root,string fallback)
        {
            if(!match&&!root&&settingsPage==GeneralSettingsPage)BuildPlayerNameRow();
            if(!match){settingsHelpDefault=fallback;if(root&&!String.IsNullOrEmpty(fallback))settingsHelpText=Text(panel,fallback,new UnityEngine.Vector3(0,-.51f,-.025f),.019f,.90f,Hex(R25Theme.Text));return;}
            settingsHelpDefault=fallback??"Point at a setting for help.\nDrag bright handles with the trigger; dimmed values are locked.";
            UnityEngine.Vector3 at=match?FrontPoint(640,605):new UnityEngine.Vector3(0,root?-.51f:-.39f,-.025f);
            UnityEngine.Vector2 size=match?new UnityEngine.Vector2(2.06f,.108f):new UnityEngine.Vector2(1.16f,root?.10f:.18f);
            Quad(panel,"Settings hover help backing",at,size,Hex(R25Theme.Raised));at.z-=.006f;
            settingsHelpText=Text(panel,settingsHelpDefault,at,match?.022f:root?.019f:.022f,match?1.98f:1.10f,Hex(R25Theme.Text));
        }
        void PreparePersonalChoice(Button button,string key)
        {button.Help=R24SettingsHelpCopy.PersonalChoice(key);SetButtonStyle(button,Hex(R25Theme.RaisedSelected),Hex(R25Theme.Text));RegisterSettingsHelp(button.Surface,UnityEngine.Vector3.zero,button.Size,button.Help);}
        void BuildSettingsSlider(Button button,float width,bool available,out UnityEngine.Transform fill,out UnityEngine.Transform knob)
        {
            bool match=Page==R23UiPage.MatchRules;SetButtonStyle(button,Hex(available?R25Theme.Raised:R25Theme.Raised),Hex(available?R25Theme.Text:R25Theme.TextDisabled));
            var track=Hex(available?R25Theme.StrokeStrong:R25Theme.Stroke);var accent=Hex(available?R25Theme.Teal:R25Theme.TextDisabled);var grip=Hex(available?R25Theme.Text:R25Theme.TextDisabled);
            var trackView=Quad(button.Surface,match?"Rule slider track":"Slider track",UnityEngine.Vector3.zero,new UnityEngine.Vector2(width,match?.011f:.014f),track);trackView.GetComponent<UnityEngine.MeshRenderer>().sortingOrder=12;
            fill=Quad(button.Surface,match?"Rule slider fill":"Slider remaining",new UnityEngine.Vector3(0,0,-.002f),new UnityEngine.Vector2(1,match?.011f:.014f),accent);fill.GetComponent<UnityEngine.MeshRenderer>().sortingOrder=13;
            knob=Quad(button.Surface,match?"Rule slider handle":"Slider knob",new UnityEngine.Vector3(0,0,-.004f),new UnityEngine.Vector2(match?.026f:.028f,match?.038f:.048f),grip);knob.GetComponent<UnityEngine.MeshRenderer>().sortingOrder=14;
            for(int side=-1;side<=1;side+=2){var endpoint=Quad(button.Surface,"Slider endpoint",new UnityEngine.Vector3(side*width*.5f,0,-.001f),new UnityEngine.Vector2(.004f,.024f),accent);endpoint.GetComponent<UnityEngine.MeshRenderer>().sortingOrder=13;var handleGrip=Quad(knob,"Slider handle grip",new UnityEngine.Vector3(side*.005f,0,-.001f),new UnityEngine.Vector2(.003f,.020f),Hex(R25Theme.Raised));handleGrip.GetComponent<UnityEngine.MeshRenderer>().sortingOrder=15;}
        }
    }
}
#endif
