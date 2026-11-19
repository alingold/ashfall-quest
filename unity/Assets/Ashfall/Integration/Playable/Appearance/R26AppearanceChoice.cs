using System;
namespace Ashfall.Integration.Playable.Appearance
{
    // R26 (playtest 2026-11-15 ROOM-APPEARANCE): the local player's own outfit choice.
    // Pure policy without Unity types so CPU checks can cover it.
    public static class R26AppearanceChoicePolicy
    {
        public const int MaximumIdLength=64;
        // Stored values are catalog IDs only: lowercase ASCII letters, digits and '_'.
        public static bool WellFormed(string id)
        {
            if(string.IsNullOrEmpty(id)||id.Length>MaximumIdLength)return false;
            foreach(char c in id)if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'))return false;
            return true;
        }
        // The body the local player wears: the saved choice when it is installed and,
        // for a retired skin, Retired Skins is on; otherwise the fallback (default) kit.
        public static string Effective(string preferred,bool shipped,bool retired,bool retiredEnabled,string fallback)
        {return WellFormed(preferred)&&(shipped||retired&&retiredEnabled)?preferred:fallback;}
    }
#if UNITY_6000_0_OR_NEWER
    // Separate PlayerPrefs record: R23UiSettingsCodec rejects unknown keys, so the
    // outfit is not added to the settings record (older records stay valid).
    public static class R26AppearanceChoice
    {
        public const string PreferenceKey="ashfall.r26.appearance-style.v1";
        public static string Load()
        {
            try{string value=UnityEngine.PlayerPrefs.GetString(PreferenceKey,"");return R26AppearanceChoicePolicy.WellFormed(value)?value:null;}
            catch(UnityEngine.PlayerPrefsException error){UnityEngine.Debug.LogWarning("Could not load saved outfit: "+error.Message);return null;}
        }
        public static bool Save(string id)
        {
            if(!R26AppearanceChoicePolicy.WellFormed(id))return false;
            try{if(UnityEngine.PlayerPrefs.GetString(PreferenceKey,"")==id)return true;UnityEngine.PlayerPrefs.SetString(PreferenceKey,id);UnityEngine.PlayerPrefs.Save();return true;}
            catch(UnityEngine.PlayerPrefsException error){UnityEngine.Debug.LogWarning("Outfit applied for this session but could not be saved: "+error.Message);return false;}
        }
    }
    public sealed partial class R23PlayerAppearance
    {
        // Local body only. The chosen outfit survives app restarts, room create/join,
        // map changes and rounds. Remote and podium instances never store a choice.
        public string PreferredStyleId {get;private set;}
        string keptStyle;int pendingSourceIndex=-1;
        void BeginPreferredStyle()
        {
            PreferredStyleId=R26AppearanceChoice.Load()??DefaultStyleId;
            keptStyle=EffectivePreferredStyle();RequestStyle(keptStyle);
        }
        void RememberChoice(string id){PreferredStyleId=id;R26AppearanceChoice.Save(id);}
        string EffectivePreferredStyle()
        {
            string preferred=PreferredStyleId;bool shipped=ShippedStyle(preferred);
            return R26AppearanceChoicePolicy.Effective(preferred,shipped,!shipped&&UI.R25LegacyPack.IsSkin(preferred),!shipped&&UI.R25LegacyPack.SkinsEnabled,DefaultStyleId);
        }
        // Replaces the one-way retired-skin reset (r11 forced the default and forgot the
        // choice). The choice is kept and re-applied when Retired Skins is turned back on
        // or the optional pack manifest finishes loading after startup.
        void KeepPreferredStyle()
        {
            string wanted=EffectivePreferredStyle();
            bool blocked=RequestedStyleId!=null&&RequestedStyleId!=wanted&&!ShippedStyle(RequestedStyleId)&&UI.R25LegacyPack.IsSkin(RequestedStyleId)&&!UI.R25LegacyPack.SkinsEnabled;
            if(wanted!=keptStyle||blocked){keptStyle=wanted;RequestStyle(wanted);}
            // A saved kit that fails to load must not leave the player without a body.
            else if(current==null&&!Loading&&Failure!=null&&RequestedStyleId!=DefaultStyleId)RequestStyle(DefaultStyleId);
        }
        // Presence carries the stable source index (byte, < 139). A retired index seen
        // before the optional pack manifest has loaded is retried, not latched to the default.
        public bool SelectSourceIndex(int sourceIndex)
        {
            string id=StyleIdForSourceIndex(sourceIndex);
            pendingSourceIndex=id==null&&!UI.R25LegacyPack.Ready?sourceIndex:-1;
            return SelectStyle(id??DefaultStyleId);
        }
        public void InitializeRemote(UnityEngine.Transform playerRoot,UnityEngine.Transform headPose,UnityEngine.Transform leftArtPose,UnityEngine.Transform rightArtPose,int sourceIndex)
        {InitializeBindings(playerRoot,headPose,leftArtPose,rightArtPose);RemoteMode=true;SelectSourceIndex(sourceIndex);}
        void RetryRemoteStyle(){if(pendingSourceIndex<0||!UI.R25LegacyPack.Ready)return;int index=pendingSourceIndex;pendingSourceIndex=-1;SelectSourceIndex(index);}
        // Non-allocating catalog lookups (AvailableStyles allocates a fresh array).
        static bool ShippedStyle(string id)
        {
            if(id==null)return false;
            foreach(var style in styles)if(style.Id==id)return true;
            foreach(var style in r5Styles)if(style.Id==id)return true;
            return false;
        }
        static bool HasStyle(string id){return ShippedStyle(id)||id!=null&&UI.R25LegacyPack.IsSkin(id);}
        public static int SourceIndexOf(string id)
        {
            if(id==null)return -1;
            foreach(var style in styles)if(style.Id==id)return style.SourceIndex;
            foreach(var style in r5Styles)if(style.Id==id)return style.SourceIndex;
            foreach(var row in UI.R25LegacyPack.Skins)if(row.id==id)return row.sourceIndex;
            return -1;
        }
        public static string StyleIdForSourceIndex(int sourceIndex)
        {
            foreach(var style in styles)if(style.SourceIndex==sourceIndex)return style.Id;
            foreach(var style in r5Styles)if(style.SourceIndex==sourceIndex)return style.Id;
            foreach(var row in UI.R25LegacyPack.Skins)if(row.sourceIndex==sourceIndex)return row.id;
            return null;
        }
    }
#endif
}
