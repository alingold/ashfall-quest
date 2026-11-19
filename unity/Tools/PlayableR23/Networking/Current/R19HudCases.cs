using System;
using Ashfall.Integration.Playable.Input;

// HUD19 TACMAP-ORBIT: the left-trigger tactical map board turns about the measured head, not the locomotion anchor.
partial class LobbyCases
{
    internal static void RunHudChecks()
    {
        float x=1,y=2,z=3;R26TacticalMapFollow.Step(ref x,ref y,ref z,.5f,0,-.25f,9,9,0);
        Need(x==1.5f&&y==2&&z==2.75f,"Zero yaw only applies the locomotion step (walking moves the board with the anchor)");
        // +yaw turns forward (0,0,1) toward +X, about the head.
        x=0;y=0;z=1;R26TacticalMapFollow.Step(ref x,ref y,ref z,0,0,0,0,0,90);
        Need(Math.Abs(x-1)<1e-5&&Math.Abs(z)<1e-5,"+90 degrees turns the board from forward to +X about the head");
        // Distance and bearing to the head stay constant through a 360 degree smooth turn and through 8 x 45 degree snaps, with the head far from the anchor.
        foreach(int turns in new[]{360,8}){
            float hx=3.36f,hz=4.1f,bx=hx+.1f,bz=hz+.8f,by=0;float d0=(float)Math.Sqrt(Math.Pow(bx-hx,2)+Math.Pow(bz-hz,2));float worst=0;
            for(int i=0;i<turns;i++){R26TacticalMapFollow.Step(ref bx,ref by,ref bz,0,0,0,hx,hz,360f/turns);worst=Math.Max(worst,Math.Abs((float)Math.Sqrt(Math.Pow(bx-hx,2)+Math.Pow(bz-hz,2))-d0));}
            Need(worst<.0005f,"Board keeps its distance from the head through a full turn ("+turns+" steps)");
            Need(Math.Abs(bx-(hx+.1f))<.001f&&Math.Abs(bz-(hz+.8f))<.001f,"Board returns to its original bearing after 360 degrees ("+turns+" steps)");
        }
        Console.WriteLine("PASS HUD19 tactical map follows the head pivot; checks="+checks);
    }
}
