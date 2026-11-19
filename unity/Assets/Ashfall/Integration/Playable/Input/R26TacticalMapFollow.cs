using System;
namespace Ashfall.Integration.Playable.Input
{
    // HUD19 TACMAP-ORBIT. Unity-free follow math for the left-trigger tactical map board, so the turn behaviour can be unit-tested.
    // The rig turns about the measured head (R23VRRig.ApplyMotorTurn: RotateAround(Head.position)). The board must therefore
    // turn about the head too: pivoting it about the locomotion anchor (which sits a seated lean/offset away from the head)
    // swings it on an oval relative to the player. Walking still moves the board with the anchor; tracked head sway moves nothing.
    public static class R26TacticalMapFollow
    {
        // Moves the board position by the locomotion step, then rotates it about the vertical axis through (headX, headZ).
        // yawDegrees uses Unity's Quaternion.AngleAxis(yaw, up) sense: +yaw turns forward (0,0,1) toward +X.
        public static void Step(ref float x, ref float y, ref float z, float stepX, float stepY, float stepZ, float headX, float headZ, float yawDegrees)
        {
            x += stepX; y += stepY; z += stepZ;
            if (yawDegrees == 0) return;
            double r = yawDegrees * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r), dx = x - headX, dz = z - headZ;
            x = (float)(headX + dx * c + dz * s); z = (float)(headZ - dx * s + dz * c);
        }
    }
}
