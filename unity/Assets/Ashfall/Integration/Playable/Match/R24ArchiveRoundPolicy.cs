using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Match
{
    // Explicit reviewed maps. Ranking targets are not spawn grants: the map's
    // existing source floor/headroom owner must validate the selected point.
    public static class R24ArchiveRoundPolicy
    {
        public const string FiveLevelsId="archive:5-levels-zyro";
        public static bool FiveLevels(string id){return id==FiveLevelsId;}
        // Audited cyan lattice boundary blocks (pb8a19331); central inset stays
        // clear of the authored perimeter, including chamfered corners.
        public static bool ForcefieldInterior(string id,Float3 p)
        {
            switch(id){
            case "archive:beast-mode-gods-pitdevil":return p.Y<174.675f&&p.X>=-126.815f&&p.X<=133.342f&&p.Z>=-113.078f&&p.Z<=98.871f;
            case "archive:blue-gods-quietowl":return p.Y<355.500f&&p.X>=-188.268f&&p.X<=167.733f&&p.Z>=-259.127f&&p.Z<=141.685f;
            case "archive:candle-magic-gods-goodwitch":return p.Y<483.097f&&p.X>=-144.567f&&p.X<=55.757f&&p.Z>=-111.424f&&p.Z<=88.634f;
            case "archive:chaotic-gods-alamo-hank":return p.Y<140.000f&&p.X>=-161.375f&&p.X<=176.875f&&p.Z>=-117.910f&&p.Z<=168.006f;
            case "archive:ereptile-disfunction-gods-pitdevil":return p.Y<232.097f&&p.X>=-119.250f&&p.X<=171.250f&&p.Z>=-131.125f&&p.Z<=115.625f;
            case "archive:flash-gods-pitdevil":return p.Y<204.215f&&p.X>=-96.915f&&p.X<=95.420f&&p.Z>=-102.199f&&p.Z<=92.620f;
            case "archive:g-spot-gods-pitdevil":return p.Y<164.675f&&p.X>=-88.916f&&p.X<=102.361f&&p.Z>=-111.761f&&p.Z<=102.326f;
            case "archive:g0dtham-gods-poplightning":return p.Y<170.161f&&p.X>=-116.341f&&p.X<=144.975f&&p.Z>=-114.787f&&p.Z<=142.251f;
            case "archive:high-ground-gods-thor":return p.Y<137.000f&&p.X>=-102.430f&&p.X<=97.327f&&p.Z>=-79.381f&&p.Z<=110.876f;
            case "archive:hildrs-echo-gods-pitdevil":return p.Y<174.675f&&p.X>=-126.815f&&p.X<=133.342f&&p.Z>=-113.078f&&p.Z<=98.871f;
            case "archive:melipop-gods-poplightning":return p.Y<240.439f&&p.X>=-155.288f&&p.X<=48.868f&&p.Z>=-28.815f&&p.Z<=162.571f;
            case "archive:mushy-gods-putangfreyja":return p.Y<181.500f&&p.X>=-63.625f&&p.X<=123.125f&&p.Z>=-129.125f&&p.Z<=59.625f;
            case "archive:oppression-gods-thor":return p.Y<81.052f&&p.X>=-119.044f&&p.X<=75.652f&&p.Z>=-95.534f&&p.Z<=99.089f;
            case "archive:plan-b-gods-pitdevil":return p.Y<423.390f&&p.X>=-230.524f&&p.X<=259.505f&&p.Z>=-272.042f&&p.Z<=217.987f;
            case "archive:poppy-gods-poplightning":return p.Y<118.551f&&p.X>=-116.341f&&p.X<=144.975f&&p.Z>=-114.787f&&p.Z<=142.251f;
            case "archive:quietowl-gods-pitdevil":return p.Y<81.052f&&p.X>=-119.044f&&p.X<=75.652f&&p.Z>=-95.534f&&p.Z<=99.089f;
            case "archive:realtree-gods-poplightning":return p.Y<179.000f&&p.X>=-93.868f&&p.X<=119.395f&&p.Z>=-126.281f&&p.Z<=78.281f;
            case "archive:robotics-2-gods-pitdevil":return p.Y<144.052f&&p.X>=-37.359f&&p.X<=66.285f&&p.Z>=-82.812f&&p.Z<=44.503f;
            case "archive:romulus-gods-skblong":return p.Y<146.747f&&p.X>=-109.994f&&p.X<=102.950f&&p.Z>=-126.814f&&p.Z<=94.437f;
            case "archive:suffering-gods-thor":return p.Y<173.500f&&p.X>=-171.298f&&p.X<=24.076f&&p.Z>=-52.242f&&p.Z<=144.242f;
            case "archive:the-powerpuff-g0ds-gods-meli":return p.Y<196.905f&&p.X>=-83.937f&&p.X<=101.124f&&p.Z>=-122.954f&&p.Z<=60.139f;
            case "archive:the-white-house-gods-osprey-kat":return p.Y<198.711f&&p.X>=-85.902f&&p.X<=57.984f&&p.Z>=-39.588f&&p.Z<=147.702f;
            case "archive:thor-city-gods-thor":return p.Y<139.352f&&p.X>=7.142f&&p.X<=310.397f&&p.Z>=-35.932f&&p.Z<=347.634f;
            default:return false;}
        }
        public static bool Forcefield(string id)
        {return id=="archive:beast-mode-gods-pitdevil"||id=="archive:blue-gods-quietowl"||id=="archive:candle-magic-gods-goodwitch"||id=="archive:chaotic-gods-alamo-hank"||id=="archive:ereptile-disfunction-gods-pitdevil"||id=="archive:flash-gods-pitdevil"||id=="archive:g-spot-gods-pitdevil"||id=="archive:g0dtham-gods-poplightning"||id=="archive:high-ground-gods-thor"||id=="archive:hildrs-echo-gods-pitdevil"||id=="archive:melipop-gods-poplightning"||id=="archive:mushy-gods-putangfreyja"||id=="archive:oppression-gods-thor"||id=="archive:plan-b-gods-pitdevil"||id=="archive:poppy-gods-poplightning"||id=="archive:quietowl-gods-pitdevil"||id=="archive:realtree-gods-poplightning"||id=="archive:robotics-2-gods-pitdevil"||id=="archive:romulus-gods-skblong"||id=="archive:suffering-gods-thor"||id=="archive:the-powerpuff-g0ds-gods-meli"||id=="archive:the-white-house-gods-osprey-kat"||id=="archive:thor-city-gods-thor";}
        public static bool GroundTeams(string id)
        {return Forcefield(id)||id=="archive:battlefield-paintball-gods-pitdevil"||id=="archive:skb-paintball-gods-skblong"||id=="archive:10000-days-gods-crazytrain"||id=="archive:afterlife-gods-alamo-hank"||FiveLevels(id);}
        public static bool GroupedTeams(string id)
        {return id=="archive:10000-days-gods-crazytrain"||id=="archive:afterlife-gods-alamo-hank"||FiveLevels(id);}
        public static Float3 TeamTarget(R24ArchiveRecoveryInfo info,float y,int team,int teams,int round=1)
        {
            if(info==null||team<0||team>=teams||teams>8)throw new ArgumentException("Actual authored bounds and assigned team");
            float lowX=info.BoundsMin.X,highX=info.BoundsMax.X,lowZ=info.BoundsMin.Z,highZ=info.BoundsMax.Z;
            if(FiveLevels(info.Id)){
                var hints=RoundSites(info,info.Deathmatch,round);if(hints.Length<2)throw new InvalidOperationException("Authored opposing floor markers required");
                lowX=highX=hints[0].X;lowZ=highZ=hints[0].Z;
                foreach(var p in hints){lowX=Math.Min(lowX,p.X);highX=Math.Max(highX,p.X);lowZ=Math.Min(lowZ,p.Z);highZ=Math.Max(highZ,p.Z);}y=LevelHeights(info)[round-1];
            }
            float x=(lowX+highX)*.5f,z=(lowZ+highZ)*.5f;
            if(teams==2){bool alongX=highX-lowX>=highZ-lowZ;return alongX?new Float3(team==0?lowX:highX,y,z):new Float3(x,y,team==0?lowZ:highZ);}
            double angle=2*Math.PI*team/teams;return new Float3(x+(float)Math.Cos(angle)*(highX-lowX)*.5f,y,z+(float)Math.Sin(angle)*(highZ-lowZ)*.5f);
        }
        public static float[] LevelHeights(R24ArchiveRecoveryInfo info)
        {
            if(info==null||!FiveLevels(info.Id)||info.SourceSha256!="2a5f30b621ed16c6e44526a2874e64406e12061ebaf4ad9856cc258772297b54"||info.Deathmatch.Length!=14)throw new ArgumentException("Exact authored Five Levels markers required");
            var ordered=new List<float>();foreach(var p in info.Deathmatch)ordered.Add(p.Y);ordered.Sort();var result=new List<float>();
            foreach(float y in ordered)if(result.Count==0||y-result[result.Count-1]>1)result.Add(y);
            if(result.Count!=5)throw new InvalidOperationException("Five authored source floor groups required");return result.ToArray();
        }
        public static bool OnRoundLevel(R24ArchiveRecoveryInfo info,Float3 point,int round)
        {
            if(!FiveLevels(info.Id))return true;if(round<1||round>5)return false;var h=LevelHeights(info);int level=round-1;
            float low=level==0?h[0]-8:(h[level-1]+h[level])*.5f,high=level==4?h[4]+8:(h[level]+h[level+1])*.5f;
            return point.IsFinite&&point.Y>low&&point.Y<high;
        }
        public static Float3[] RoundSites(R24ArchiveRecoveryInfo info,IEnumerable<Float3> actualSites,int round)
        {if(info==null||actualSites==null)throw new ArgumentNullException();var result=new List<Float3>();foreach(var p in actualSites)if(OnRoundLevel(info,p,round))result.Add(p);return result.ToArray();}
        public static double GridTop(R24ArchiveRecoveryInfo info,int round)
        {if(!FiveLevels(info.Id))return (double)info.SolidMax.Y+5;var h=LevelHeights(info);if(round<1||round>5)throw new ArgumentOutOfRangeException("round");return (double)h[round-1]+5;}
        public static double GridBottom(R24ArchiveRecoveryInfo info,int round)
        {if(!FiveLevels(info.Id))return (double)info.SolidMin.Y-5;var h=LevelHeights(info);if(round<1||round>5)throw new ArgumentOutOfRangeException("round");return (double)h[round-1]-8;}
    }
}
