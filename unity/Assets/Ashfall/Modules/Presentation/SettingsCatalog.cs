using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ashfall.Modules.Presentation
{
    public sealed class SettingSpec
    {
        public readonly string Key, Label, Page, Default;
        public readonly double Min, Max, Step;
        public readonly string[] Choices;
        public bool Numeric { get { return Choices == null; } }
        public SettingSpec(string page, string key, string label, double min, double max, double step, double value)
        { Page=page; Key=key; Label=label; Min=min; Max=max; Step=step; Default=value.ToString("0.########",CultureInfo.InvariantCulture); }
        public SettingSpec(string page, string key, string label, string value, params string[] choices)
        { Page=page; Key=key; Label=label; Default=value; Choices=choices; }
        public string Normalize(string value)
        {
            if (!Numeric) return Array.IndexOf(Choices,value)>=0 ? value : Default;
            double number;
            if (!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out number) || double.IsNaN(number) || double.IsInfinity(number)) return Default;
            return Math.Max(Min,Math.Min(Max,number)).ToString("0.########",CultureInfo.InvariantCulture);
        }
    }

    public static class SettingsCatalog
    {
        private static SettingSpec N(string p,string k,string l,double a,double b,double s,double d) { return new SettingSpec(p,k,l,a,b,s,d); }
        private static SettingSpec C(string p,string k,string l,string d,params string[] c) { return new SettingSpec(p,k,l,d,c); }
        public static IList<SettingSpec> Create() { return Create(false); }
        public static IList<SettingSpec> Create(bool skinRuntime)
        {
            return new List<SettingSpec> {
                N("general","master_volume","Master volume",0,1,.01,1),
                N("general","audio_volumes.SFX","SFX volume",0,1,.01,1),
                N("general","audio_volumes.Music","Music volume",0,1,.01,0),
                N("general","audio_volumes.Voice","Player voice volume",0,1,.01,1),
                N("general","audio_volumes.Ambience","Ambience volume",0,1,.01,0),
                C("general","vignette_enabled","Comfort vignette","false","false","true"),
                C("movement","forward_reference","Forward motion","head","head","hand"),
                C("movement","turning_style","Turning","smooth","snap","auto_snap","smooth"),
                N("movement","snap_degrees","Snap angle (degrees)",15,180,5,30),
                N("movement","smooth_turn_speed","Smooth turn (degrees/s)",30,720,5,360),
                N("movement","fling_power","Fling power",0,5,.05,1),
                N("movement","walk_speed_multiplier","Walk speed multiplier",.25,skinRuntime?3:5,.05,1),
                C("turning","smooth_ease_in","Ease in","false","false","true"),
                C("turning","smooth_analog","Analog control","true","false","true"),
                C("turning","comfort_speed","Comfort walk base","false","false","true"),
                C("turning","sprint_binding","Sprint binding","left_stick_click","left_stick_click","left_grip"),
                N("hidden","sprint_double_press_window","Double press window",.1,.6,.01,.32),
                N("flight","sprint_slow_gain","Slow sprint multiplier",1,2,.05,7.5/6.5),
                N("flight","sprint_fast_gain","Fast sprint multiplier",1,2.5,.05,9.5/6.5),
                N("flight","glide_steering_gain","Glide steering",.25,2.5,.05,.875),
                N("flight","sprint_stamina_seconds","Stamina (seconds)",2,20,.5,6),
                N("flight","sprint_recovery_seconds","Recovery (seconds)",1,20,.5,4),
                N("weapons","weapon_settings.motion_damping","Motion damping",0,2,.01,1),
                N("weapons","weapon_settings.offhand_influence","Offhand influence",0,1,.01,0),
                C("weapons","weapon_settings.grip_toggle","Grip toggle","false","false","true"),
                C("weapons","dominant_hand","Dominant hand","right","right","left"),
                N("game","difficulty","Difficulty",0,1,.01,.5),
                C("game","graphics_quality","Graphics","high","high","balanced"),
                N("game","hud_size","HUD size",.65,1.75,.05,1),
                N("game","enemy_damage_multiplier","Enemy damage",.25,4,.25,1),
                N("game","player_damage_multiplier","Player damage",.25,4,.25,1),
                C("game","hud_style","HUD style","reference","reference","simple")
            }.AsReadOnly();
        }
    }
}
