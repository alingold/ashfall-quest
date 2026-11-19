using System;
using System.Globalization;
using System.Text;
namespace Ashfall.Integration.Playable
{
    // R26 (playtest 2026-11-15 PLAYER-NAMES): the name a player types once in Settings.
    // It is the room display name (room roster, tactical map, world scoreboards,
    // nameplates) and the single-player score name. Pure rules without Unity types
    // so CPU checks can cover them.
    public static class R26PlayerNameRules
    {
        public const int MinimumLength=2,MaximumLength=16;
        // ASCII letters, digits, space, '-' and '_'. A strict subset of what the host's
        // Session.CleanName keeps (24 UTF-16 units, no control or []<> characters), so a
        // valid name reaches every roster unchanged, and the UI fonts have every glyph.
        public static bool Allowed(char c){return c>='A'&&c<='Z'||c>='a'&&c<='z'||c>='0'&&c<='9'||c==' '||c=='-'||c=='_';}
        // Trims, drops other characters, reads any whitespace as one space and collapses
        // runs, then cuts to MaximumLength. Returns "" when under MinimumLength remain.
        public static string Clean(string value)
        {
            var result=new StringBuilder(MaximumLength);
            foreach(char raw in value??"")
            {
                char c=Char.IsWhiteSpace(raw)?' ':raw;
                if(!Allowed(c)||c==' '&&(result.Length==0||result[result.Length-1]==' '))continue;
                if(result.Length==MaximumLength)break;
                result.Append(c);
            }
            string name=result.ToString().TrimEnd(' ');
            return name.Length>=MinimumLength?name:"";
        }
        public static bool Valid(string value){return value!=null&&value.Length>0&&Clean(value)==value;}
        // One on-screen keyboard key: an allowed character, "SPACE", "DEL" or "CLEAR".
        // A draft may end in a space or be short while typing; Clean finishes it.
        public static string Edit(string draft,string key)
        {
            draft=draft??"";
            if(key=="CLEAR")return "";
            if(key=="DEL")return draft.Length==0?"":draft.Substring(0,draft.Length-1);
            if(key=="SPACE")key=" ";
            if(key==null||key.Length!=1||!Allowed(key[0])||draft.Length>=MaximumLength)return draft;
            if(key==" "&&(draft.Length==0||draft[draft.Length-1]==' '))return draft;
            return draft+key;
        }
        // The first name until the player types one: "Player-1000" to "Player-9999".
        public static string Generated(int seed){return "Player-"+(1000+(int)((uint)seed%9000u)).ToString(CultureInfo.InvariantCulture);}
    }
#if UNITY_6000_0_OR_NEWER
    // Separate PlayerPrefs record, like the outfit choice: R23UiSettingsCodec rejects
    // unknown keys, so the name is not added to the settings record.
    public static class R26PlayerName
    {
        public const string PreferenceKey="ashfall.r26.player-name.v1";
        static string current;
        // Raised after Set saves a different name. Rooms read Current when they are made
        // or joined; the lobby has no rename message, so a room keeps the joining name.
        public static event Action<string> Changed;
        // Main thread only. The first read on a fresh install generates a name and saves
        // it, so the device keeps one name across restarts until the player types one.
        public static string Current{get{if(current==null)current=Load();return current;}}
        static string Load()
        {
            string saved="";
            try{saved=UnityEngine.PlayerPrefs.GetString(PreferenceKey,"");}
            catch(UnityEngine.PlayerPrefsException error){UnityEngine.Debug.LogWarning("Could not load saved player name: "+error.Message);}
            // Guid, not UnityEngine.Random: gameplay may seed Unity's generator identically on every headset.
            string name=R26PlayerNameRules.Clean(saved);if(name.Length==0)name=R26PlayerNameRules.Generated(Guid.NewGuid().GetHashCode());
            if(name!=saved)Save(name);
            return name;
        }
        // False, keeping the current name, when the value cleans to nothing.
        public static bool Set(string value)
        {
            string name=R26PlayerNameRules.Clean(value);if(name.Length==0)return false;
            if(name==Current)return true;
            current=name;Save(name);if(Changed!=null)Changed(name);return true;
        }
        static void Save(string name)
        {
            try{UnityEngine.PlayerPrefs.SetString(PreferenceKey,name);UnityEngine.PlayerPrefs.Save();}
            catch(UnityEngine.PlayerPrefsException error){UnityEngine.Debug.LogWarning("Player name applied for this session but could not be saved: "+error.Message);}
        }
    }
#endif
}
