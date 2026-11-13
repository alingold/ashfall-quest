using System;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // grip_hand_086.gd: authored pose choice and per-finger closure only.
    public static class R23GlovePoseRules
    {
        public static int Pose(string id,bool sword)
        {
            if(sword)return 2;
            return id=="px4"||id=="1911"||id=="tec9"||id=="mp9"||id=="magnum"||id=="matadors"?1:0;
        }
        public static float Closure(string id,bool sword,bool index,float requested,bool holding)
        {
            if(float.IsNaN(requested)||float.IsInfinity(requested))throw new ArgumentOutOfRangeException("requested");
            if(!holding)return Math.Max(0,Math.Min(1,requested));
            if(id=="matadors")return index?.80f:1.05f;
            if(sword||id=="magnum"||id=="tec9")return index?.86f:1.12f;
            if(id=="knife")return index?.80f:1.05f;
            if(id=="px4"||id=="1911"||id=="mp9")return index?.62f:.85f;
            return index?.54f:.74f;
        }
        public static float Blend(float dt)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            return 1-(float)Math.Exp(-dt*22);
        }
        public static bool Settled(float x,float y,float z,float w){return x*x+y*y+z*z+w*w<=1e-12f;}
    }
}
