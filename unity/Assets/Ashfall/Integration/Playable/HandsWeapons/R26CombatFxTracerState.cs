using System;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public static class R26CombatFxTracerState
    {
        // 0.143 degrees, with the existing near-bore stroke as a minimum.
        public const float WidthPerMetre=.0025f,MinimumWidth=.003f;
        public static float Width(float distance){return Single.IsNaN(distance)||Single.IsInfinity(distance)?MinimumWidth:Math.Max(MinimumWidth,Math.Max(0,distance)*WidthPerMetre);}
    }
}
