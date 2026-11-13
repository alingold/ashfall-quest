using System;
namespace Ashfall.Integration.Playable.Calibration
{
    // Exact vertical_combat_initialization.gd::_completion_chime, not vr_initialize WAV.
    public static class R23CalibrationCompletionCue
    {
        public const int SampleRate=22050;public const double Seconds=.42;
        public const float SourceGain=.19952623f; // AudioStreamPlayer volume_db=-14, SFX bus.
        public static short[] BuildPcm16()
        {
            var data=new short[(int)(Seconds*SampleRate)];
            for(int i=0;i<data.Length;i++){double t=(double)i/SampleRate,envelope=Math.Min(1,t/.008)*Math.Pow(Math.Max(0,1-t/.42),2),frequency=t<.13?660:t<.26?880:1100;data[i]=(short)(int)(Math.Sin(Math.PI*2*frequency*t)*envelope*10000);}
            return data;
        }
    }
}
