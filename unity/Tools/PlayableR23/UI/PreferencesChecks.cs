using System;
using System.Globalization;
using System.Threading;
using Ashfall.Integration.Playable.UI;
class PreferencesChecks
{
    static int checks;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;}
    static void Defaults(R23UiSettings settings,string label){Check(R23UiSettingsCodec.SameValue(settings,new R23UiSettings()),label);}
    static R23UiSettings Read(string fields){return R23UiSettingsCodec.Decode("r23.ui.v1;"+fields);}
    static void Main()
    {
        Defaults(R23UiSettingsCodec.Decode(null),"Missing save uses actual player/audio defaults");
        Defaults(R23UiSettingsCodec.Decode(""),"Empty save defaults");
        Defaults(R23UiSettingsCodec.Decode("not settings"),"Malformed save defaults");
        Defaults(R23UiSettingsCodec.Decode("r23.ui.v9;turning_style=snap"),"Unsupported version defaults");
        Defaults(R23UiSettingsCodec.Decode(new string('x',513)),"Oversized record rejected before parsing");
        Defaults(Read("turning_style=snap;turning_style=smooth"),"Duplicate field rejects ambiguous record");
        Defaults(Read("turning_style=snap;unexpected=1"),"Unexpected field rejects malformed local record");
        Defaults(Read("turning_style=snap;walk_speed_multiplier=1=2"),"Malformed pair defaults");
        var partial=Read("turning_style=snap;forward_reference=hand");
        Check(partial.Turning==R23Turning.Snap&&partial.Forward==R23Forward.LeftController&&partial.WalkMultiplier==2.5f&&partial.MasterVolume==1,"Partial save uses defaults for missing values");
        var invalid=Read("turning_style=auto_snap;forward_reference=banana;snap_degrees=NaN;smooth_turn_speed=Infinity;walk_speed_multiplier=1,5;master_volume=oops");
        Defaults(invalid,"Unknown unsupported enums and malformed/nonfinite numbers use field defaults");
        var clamped=Read("snap_degrees=500;smooth_turn_speed=-1;walk_speed_multiplier=9;master_volume=-2;sfx=2;music=-1;ambience=5;voice=-4");
        Check(clamped.SnapDegrees==180&&clamped.SmoothDegreesPerSecond==30&&clamped.WalkMultiplier==5,"Source motion bounds include180deg snap");
        Check(clamped.MasterVolume==0&&clamped.SfxVolume==1&&clamped.MusicVolume==0&&clamped.AmbienceVolume==1&&clamped.VoiceVolume==0,"Source audio bounds clamp0to1");
        Check(new R23UiSettings((R23Turning)99,(R23Forward)99).Turning==R23Turning.Smooth,"Public settings normalize invalid turning enum");
        Check(new R23UiSettings((R23Turning)99,(R23Forward)99).Forward==R23Forward.Head,"Public settings normalize invalid heading enum");
        var changed=new R23UiSettings(R23Turning.Snap,R23Forward.LeftController,180,715,.25f,.34f,.67f,.13f,.21f,.98f);
        string encoded=R23UiSettingsCodec.Encode(changed);
        Check(encoded.Length<=R23UiSettingsCodec.MaximumLength&&encoded.Contains("forward_reference=hand"),"Bounded save uses source heading spelling");
        Check(R23UiSettingsCodec.SameValue(changed,R23UiSettingsCodec.Decode(encoded)),"Every current personal field roundtrips");
        Check(R23UiSettingsCodec.SameValue(changed,R23UiSettingsCodec.Decode(R23UiSettingsCodec.Encode(R23UiSettingsCodec.Decode(encoded)))),"Repeated restart roundtrip stable");
        var culture=Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture=new CultureInfo("fr-FR");
            Check(encoded==R23UiSettingsCodec.Encode(changed),"Save invariant under comma-decimal locale");
            Check(R23UiSettingsCodec.SameValue(changed,R23UiSettingsCodec.Decode(encoded)),"Load invariant under comma-decimal locale");
        }
        finally{Thread.CurrentThread.CurrentCulture=culture;}
        Defaults(R23UiSettingsCodec.Decode(R23UiSettingsCodec.Encode(new R23UiSettings())),"Default record roundtrip");
        foreach(var master in new[]{0f,.0005f,.01f,.5f,1f})
        {
            var setting=new R23UiSettings(masterVolume:master);
            Check(R23UiSettingsCodec.Decode(R23UiSettingsCodec.Encode(setting)).MasterVolume==master,"No zero/quiet-volume loss at restart");
        }
        var tuned=new R23UiSettings(flingPower:4.25f,motionDamping:1.75f,offhandInfluence:.65f,gripToggle:true,dominantHand:0);
        Check(R23UiSettingsCodec.SameValue(tuned,R23UiSettingsCodec.Decode(R23UiSettingsCodec.Encode(tuned))),"Fling/weapon tuning restart roundtrip");
        Check(Read("fling_power=99;motion_damping=-3;offhand_influence=4").FlingPower==5,"Source fling range bounds");
        var bounded=Read("fling_power=NaN;motion_damping=-3;offhand_influence=4;grip_toggle=YES;dominant_hand=banana");
        Check(bounded.FlingPower==3&&bounded.MotionDamping==0&&bounded.OffhandInfluence==1&&!bounded.GripToggle&&bounded.DominantHand==1,"Malformed tuning fallback and bounds");
        Check(tuned.With(sfx:.2f).FlingPower==4.25f&&tuned.With(sfx:.2f).MotionDamping==1.75f&&tuned.With(walk:2).GripToggle&&tuned.With(turning:R23Turning.Snap).DominantHand==0,"Audio/motion edits preserve weapon/fling");
        Check(new R23UiSettings().FlingPower==3,"Fresh source fling3; menu fallback1 only with saved file");
        Console.WriteLine("PASS "+checks+" preferences checks: source defaults/ranges, malformed/oversized/versioned saves, per-field fallback, full/culture/quiet-volume restart roundtrips");
    }
}
