using System;
using System.Globalization;
namespace Ashfall.Integration.Playable.UI
{
    // Source match_hud_r3.gd:59 and front_menu1000.gd:214. No local clock.
    public static class R24MatchLifeText
    {
        public const float PanelWidth=.34f,PanelHeight=.013f,PanelX=-.02f,PanelY=-.333f,PanelUnityZ=.803f,TextUnityZ=.798f,TextEmHeight=.0095f;
        public const int SourceFontSize=32;
        public static bool IsGhost(string state,bool results)
        {return !results&&(state=="downed"||state=="eliminated"||state=="respawning"||state=="dead");}
        public static bool UseLegacyDeathModal(bool healthEmpty,string authoritativeMatchLife,bool awaitingAuthoritativeHealth=false)
        {return healthEmpty&&authoritativeMatchLife==null&&!awaitingAuthoritativeHealth;}

        public static string Normalize(string state)
        {
            if(state==null)return null;state=state.ToLowerInvariant();
            if(state=="alive"||state=="downed"||state=="respawning"||state=="eliminated"||state=="dead")return state;
            throw new ArgumentException("Actual local match life state required","state");
        }
        public static string Title(string state,bool results)
        {if(results)return "ROUND COMPLETE";return state=="downed"?"DOWNED":state=="respawning"?"RESPAWNING":state=="eliminated"||state=="dead"?"ELIMINATED":"YOU DIED";}
        public static string Notice(string state,float remainingSeconds,bool results)
        {
            if(float.IsNaN(remainingSeconds)||float.IsInfinity(remainingSeconds))throw new ArgumentOutOfRangeException("remainingSeconds");
            if(results)return "ROUND COMPLETE";
            if(state=="downed")return "YOU ARE A GHOST \u00b7 WAITING FOR REVIVE";
            if(state=="respawning")return "YOU DIED \u00b7 RESPAWN "+Math.Max(0,remainingSeconds).ToString("F1",CultureInfo.InvariantCulture)+"s";
            if(state=="eliminated"||state=="dead")return "ELIMINATED \u00b7 WAITING FOR ROUND";
            return "";
        }
    }
}
