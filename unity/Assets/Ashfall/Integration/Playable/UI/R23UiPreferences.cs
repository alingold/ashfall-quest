using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ashfall.Integration.Playable.UI
{
    // One bounded, versioned local preference value. No map/session/inventory state is stored.
    public static class R23UiSettingsCodec
    {
        public const int MaximumLength=512;
        const string Header="r23.ui.v1";
        static readonly string[] numericKeys={"snap_degrees","smooth_turn_speed","walk_speed_multiplier","master_volume","sfx","music","ambience","voice","fling_power","motion_damping","offhand_influence","difficulty"};
        public static string Encode(R23UiSettings value)
        {
            if(value==null)throw new ArgumentNullException("value");
            var fields=new List<string>{Header,"turning_style="+(value.Turning==R23Turning.Snap?"snap":"smooth"),"forward_reference="+(value.Forward==R23Forward.LeftController?"hand":"head")};
            float[] numbers={value.SnapDegrees,value.SmoothDegreesPerSecond,value.WalkMultiplier,value.MasterVolume,value.SfxVolume,value.MusicVolume,value.AmbienceVolume,value.VoiceVolume,value.FlingPower,value.MotionDamping,value.OffhandInfluence,value.BotDifficulty};
            for(int i=0;i<numbers.Length;i++)fields.Add(numericKeys[i]+"="+numbers[i].ToString("R",CultureInfo.InvariantCulture));
            fields.Add("grip_toggle="+(value.GripToggle?"true":"false"));fields.Add("dominant_hand="+(value.DominantHand==0?"left":"right"));
            fields.Add("legacy_maps="+(value.RetiredMaps?"true":"false"));fields.Add("legacy_skins="+(value.RetiredSkins?"true":"false"));fields.Add("legacy_sounds="+(value.RetiredSounds?"true":"false"));
            fields.Add("legacy_defaults=2");
            string result=string.Join(";",fields.ToArray());if(result.Length>MaximumLength)throw new InvalidOperationException("Settings record exceeds bound");return result;
        }
        public static R23UiSettings Decode(string record)
        {bool migrated;return Decode(record,out migrated);}
        public static R23UiSettings Decode(string record,out bool legacyDefaultsMigrated)
        {
            legacyDefaultsMigrated=true;
            var defaults=new R23UiSettings();
            if(string.IsNullOrEmpty(record)||record.Length>MaximumLength)return defaults;
            var fields=record.Split(';');if(fields[0]!=Header)return defaults;
            var values=new Dictionary<string,string>(StringComparer.Ordinal);
            for(int i=1;i<fields.Length;i++)
            {
                int separator=fields[i].IndexOf('=');if(separator<1||separator!=fields[i].LastIndexOf('='))return defaults;
                string key=fields[i].Substring(0,separator),value=fields[i].Substring(separator+1);
                bool known=key=="legacy_defaults"||key=="legacy_maps"||key=="legacy_skins"||key=="legacy_sounds"||key=="turning_style"||key=="forward_reference"||key=="grip_toggle"||key=="dominant_hand"||Array.IndexOf(numericKeys,key)>=0;
                if(!known||values.ContainsKey(key))return defaults;values.Add(key,value);
            }
            string marker;int version;
            legacyDefaultsMigrated=!values.TryGetValue("legacy_defaults",out marker)||!int.TryParse(marker,NumberStyles.Integer,CultureInfo.InvariantCulture,out version)||version<2;
            string turn,forward,grip,dominant;values.TryGetValue("turning_style",out turn);values.TryGetValue("forward_reference",out forward);values.TryGetValue("grip_toggle",out grip);values.TryGetValue("dominant_hand",out dominant);
            return new R23UiSettings(turn=="snap"?R23Turning.Snap:R23Turning.Smooth,forward=="hand"?R23Forward.LeftController:R23Forward.Head,
                Number(values,"snap_degrees",defaults.SnapDegrees),Number(values,"smooth_turn_speed",defaults.SmoothDegreesPerSecond),Number(values,"walk_speed_multiplier",defaults.WalkMultiplier),
                Number(values,"master_volume",defaults.MasterVolume),Number(values,"sfx",defaults.SfxVolume),Number(values,"music",defaults.MusicVolume),Number(values,"ambience",defaults.AmbienceVolume),Number(values,"voice",defaults.VoiceVolume),
                Number(values,"fling_power",defaults.FlingPower),Number(values,"motion_damping",defaults.MotionDamping),Number(values,"offhand_influence",defaults.OffhandInfluence),grip=="true",dominant=="left"?0:1,Number(values,"difficulty",defaults.BotDifficulty),legacyDefaultsMigrated||Flag(values,"legacy_maps"),legacyDefaultsMigrated||Flag(values,"legacy_skins"),legacyDefaultsMigrated||Flag(values,"legacy_sounds"));
        }
        static bool Flag(Dictionary<string,string> fields,string key){string value;return !fields.TryGetValue(key,out value)||value=="true";}
        static float Number(Dictionary<string,string> fields,string key,float fallback)
        {
            string text;float value;
            return fields.TryGetValue(key,out text)&&float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value)?value:fallback;
        }
        public static bool SameValue(R23UiSettings a,R23UiSettings b)
        {
            return a!=null&&b!=null&&a.RetiredMaps==b.RetiredMaps&&a.RetiredSkins==b.RetiredSkins&&a.RetiredSounds==b.RetiredSounds&&a.Turning==b.Turning&&a.Forward==b.Forward&&a.SnapDegrees==b.SnapDegrees&&a.SmoothDegreesPerSecond==b.SmoothDegreesPerSecond&&a.WalkMultiplier==b.WalkMultiplier&&
                a.MasterVolume==b.MasterVolume&&a.SfxVolume==b.SfxVolume&&a.MusicVolume==b.MusicVolume&&a.AmbienceVolume==b.AmbienceVolume&&a.VoiceVolume==b.VoiceVolume&&a.FlingPower==b.FlingPower&&a.MotionDamping==b.MotionDamping&&a.OffhandInfluence==b.OffhandInfluence&&a.GripToggle==b.GripToggle&&a.DominantHand==b.DominantHand&&a.BotDifficulty==b.BotDifficulty;
        }
    }
#if UNITY_6000_0_OR_NEWER
    public static class R23UiPreferences
    {
        const string Key="ashfall.r23.personal-ui-settings.v1";
        public static R23UiSettings Load()
        {
            try
            {
                bool legacyDefaultsMigrated;
                var value=R23UiSettingsCodec.Decode(UnityEngine.PlayerPrefs.GetString(Key,""),out legacyDefaultsMigrated);
                // R7 (playtest 6 "the sound still isn't working"): the playtest-3 hum
                // test asked for master volume at zero, and that was saved. Restore a
                // silent master volume to the default once; a later choice is kept.
                const string AudioRestored="ashfall.r27.master-volume-restored.v1";
                if(UnityEngine.PlayerPrefs.GetInt(AudioRestored,0)==0)
                {
                    UnityEngine.PlayerPrefs.SetInt(AudioRestored,1);
                    var defaults=new R23UiSettings();
                    if(value.MasterVolume<.05f||value.SfxVolume<.05f)
                    {
                        UnityEngine.Debug.Log("R27 audio restored silent saved volume master="+value.MasterVolume.ToString("F2")+" sfx="+value.SfxVolume.ToString("F2"));
                        value=value.With(master:value.MasterVolume<.05f?defaults.MasterVolume:value.MasterVolume,sfx:value.SfxVolume<.05f?defaults.SfxVolume:value.SfxVolume);
                        Save(value);
                    }
                    else UnityEngine.PlayerPrefs.Save();
                }
                // Persist the one-time legacy opt-in even when no settings UI is opened.
                if(legacyDefaultsMigrated)Save(value);
                return value;
            }
            catch(UnityEngine.PlayerPrefsException error){UnityEngine.Debug.LogWarning("Could not load personal VR settings: "+error.Message);return new R23UiSettings();}
        }
        public static bool Save(R23UiSettings value)
        {
            string record=R23UiSettingsCodec.Encode(value);
            try{UnityEngine.PlayerPrefs.SetString(Key,record);UnityEngine.PlayerPrefs.Save();return true;}
            catch(UnityEngine.PlayerPrefsException error){UnityEngine.Debug.LogWarning("Personal VR settings applied for this session but could not be saved: "+error.Message);return false;}
        }
    }
#endif
}
