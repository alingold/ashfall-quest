using System;
using Ashfall.Integration.Playable.UI;
class SourceDefaults
{
    static void Main()
    {
        var source=new R23UiSettings();
        if(source.Turning!=R23Turning.Smooth||source.Forward!=R23Forward.Head||source.SnapDegrees!=30||source.SmoothDegreesPerSecond!=360||source.WalkMultiplier!=2.5f)throw new Exception("player.gd defaults differ");
        if(source.MasterVolume!=1||source.SfxVolume!=1||source.MusicVolume!=0||source.AmbienceVolume!=0||source.VoiceVolume!=1)throw new Exception("menu.gd audio defaults differ");
        var changed=new R23UiSettings(masterVolume:2,walkMultiplier:9,snapDegrees:-1);
        if(changed.MasterVolume!=1||changed.WalkMultiplier!=5||changed.SnapDegrees!=15||source.WalkMultiplier!=2.5f)throw new Exception("Immutable clamps failed");
        foreach(var field in typeof(R23UiSettings).GetFields())if(!field.IsInitOnly)throw new Exception("Mutable output "+field.Name);
        bool rejected=false;try{new R23UiSettings(masterVolume:float.NaN);}catch(ArgumentOutOfRangeException){rejected=true;}
        if(!rejected)throw new Exception("Nonfinite setting accepted");
        rejected=false;try{new R23WeaponOption("", "Bad");}catch(ArgumentException){rejected=true;}
        if(!rejected)throw new Exception("Empty weapon intent ID accepted");
        Console.WriteLine("PASS source movement/audio defaults; immutable setting outputs; bounded and nonfinite values; stable weapon intent IDs");
    }
}
