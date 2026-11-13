using System;
using Ashfall.Core;
namespace Ashfall.Modules.Avatar
{
    public static class RenderMounts
    {
        public static float[] Identity() { return new float[] {1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1}; }
        public static float[] StandingHeadToEye()
        { var m=Identity(); m[7]=-1.68f; return m; }
        // grip_hand_086.setup: basis around Godot BACK (+Z), then S R S.
        // Inputs are source GLTF wrist/knuckle in Godot coordinates, never tracked poses.
        public static float[] Glove(bool left, Float3 wrist, Float3 knuckle)
        {
            if(!wrist.IsFinite || !knuckle.IsFinite) throw new ArgumentException("Nonfinite glove landmarks");
            float s=left ? -1 : 1;
            float x=(float)(((double)wrist.X+knuckle.X)*.5), y=(float)(((double)wrist.Y+knuckle.Y)*.5), z=(float)(((double)wrist.Z+knuckle.Z)*.5);
            var m=Identity(); m[0]=0; m[1]=-s; m[4]=s; m[5]=0;
            m[3]=s*y; m[7]=.032f-s*x; m[11]=z;
            if(!AvatarMath.ValidTrs(m)) throw new ArgumentException("Glove landmarks exceed render bounds");
            return m;
        }
    }
}
