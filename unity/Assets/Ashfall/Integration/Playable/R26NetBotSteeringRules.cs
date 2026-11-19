using System;
namespace Ashfall.Integration.Playable
{
    internal static class R26NetBotSteeringRules
    {
        internal enum Intent { Existing, Zone, Ping, Gunshot }
        internal static bool SafeWaypoint(double x,double z,double minX,double minZ,double size,out double safeX,out double safeZ)
        {
            double margin=Math.Min(4,size*.25);
            safeX=Math.Max(minX+margin,Math.Min(x,minX+size-margin));
            safeZ=Math.Max(minZ+margin,Math.Min(z,minZ+size-margin));
            return safeX!=x||safeZ!=z;
        }
        internal static bool Heard(double distanceSquared,double age){return distanceSquared<=45*45&&age>=-.1&&age<6;}
        internal static Intent Choose(bool retreat,bool rescuing,bool teamPing,bool visibleEnemy,bool heard)
        {
            if(retreat)return Intent.Zone;
            if(rescuing)return Intent.Existing;
            if(teamPing)return Intent.Ping;
            return !visibleEnemy&&heard?Intent.Gunshot:Intent.Existing;
        }
    }
}
