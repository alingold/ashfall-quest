using System;

namespace Ashfall.Integration.Playable.UI
{
    public enum R23MenuCue { Hover, Click, Initialize }

    // Source front_menu_091: sound on hover identity changes and first tracked menu boot.
    public sealed class R23MenuAudioEdges
    {
        readonly long[] hovered=new long[2];
        bool initialized;
        public const float SourceGain=0.39810717f; // sound_cues099.gd: -8 dB, SFX bus.
        public static float Gain(float sfx)
        {return float.IsNaN(sfx)||float.IsInfinity(sfx)?0:SourceGain*Math.Max(0,Math.Min(1,sfx));}
        public bool Hover(int hand,int page,int button,bool active)
        {
            if(hand<0||hand>1)throw new ArgumentOutOfRangeException("hand");
            long target=active&&button>=0?((long)(page+1)<<32)|(uint)(button+1):0;
            if(hovered[hand]==target)return false;
            hovered[hand]=target;return target!=0&&hovered[1-hand]!=target;
        }
        public bool Initialize(bool active,bool tracked,bool focused,bool clipAvailable)
        {
            if(initialized||!active||!tracked||!focused||!clipAvailable)return false;
            initialized=true;return true;
        }
        public void ResetHover(){hovered[0]=hovered[1]=0;}
    }
}
