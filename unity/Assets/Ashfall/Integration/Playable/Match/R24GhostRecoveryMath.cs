using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.MatchZone;
namespace Ashfall.Integration.Playable.Match
{
    // net_bridge1000._ghost_recovery_r13 / _arena_spawn_r12. Raw source Z is
    // reflected once here; inputs/outputs are already represented Unity world.
    public static class R24GhostRecoveryMath
    {
        public static bool Supported(string map){return map=="range"||map=="valley"||map=="cyber_bird";}
        public static bool Outside(string map,float sourceWorldY)
        {return Supported(map)&&!Single.IsNaN(sourceWorldY)&&sourceWorldY<(map=="cyber_bird"?-12:-25);}
        public static Float3[] Candidates(string map,Float3 sourceBaseUnity,Float3 sourceOriginUnity,R24ZoneSquare? next)
        {
            if(!Supported(map)||!sourceBaseUnity.IsFinite||!sourceOriginUnity.IsFinite)throw new ArgumentException("Actual supported source map/base/origin required");
            if(next.HasValue)
            {
                var rect=next.Value;var points=new Float3[49];double size=rect.Size-2;
                for(int z=0;z<7;z++)for(int x=0;x<7;x++)points[z*7+x]=new Float3(sourceOriginUnity.X+(float)(rect.X+1+size*(x+.5)/7),sourceOriginUnity.Y+100,sourceOriginUnity.Z-(float)(rect.Z+1+size*(z+.5)/7));
                return points;
            }
            var result=new Float3[8];
            for(int i=0;i<8;i++)result[i]=map=="cyber_bird"?
                new Float3(sourceOriginUnity.X-55+(i%4)*35,sourceOriginUnity.Y+1,sourceOriginUnity.Z-330+(i/4)*660):
                new Float3(sourceBaseUnity.X-15+(i%4)*10,sourceBaseUnity.Y+2,sourceBaseUnity.Z+6+(i/4)*30);
            return result;
        }
        public static int SourceOrder(int index,int count,int randomOffset,int sourceActorId)
        {if(index<0||index>=count||count<1||randomOffset<0||randomOffset>=count||sourceActorId<0)throw new ArgumentOutOfRangeException();return(int)(((long)index+sourceActorId+randomOffset)%count);}
    }
}
