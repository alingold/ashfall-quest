using System;using System.Collections.Generic;using Ashfall.Core;
namespace Ashfall.Integration.Playable.Networking
{
    // Stable map-owned sides, independent of player poses, join order and RNG.
    // Converted marker payloads carry positions but no team labels. Split along
    // the farthest pair's horizontal axis; balanced slices also support >2 teams.
    public static class R26SpawnSides
    {
        static int Compare(Float3 a,Float3 b){int x=a.X.CompareTo(b.X);if(x!=0)return x;int z=a.Z.CompareTo(b.Z);return z!=0?z:a.Y.CompareTo(b.Y);}
        // If there are fewer authored anchors than teams, adjacent teams share
        // an anchor and the spawn owner tests distinct standing positions there.
        public static int AnchorSide(int team,int teams,int sites)
        {
            if(sites<=0||sites>=teams)return team;
            return ((team+1)*sites-1)/teams*teams/sites;
        }
        public static int[] Split(IList<Float3> sites,int teams)
        {
            if(sites==null||teams<1||teams>8)throw new ArgumentException("Map sites and 1 to 8 teams required");
            var result=new int[sites.Count];if(sites.Count==0)return result;
            var order=new List<int>();for(int i=0;i<sites.Count;i++)order.Add(i);
            order.Sort(delegate(int a,int b){return Compare(sites[a],sites[b]);});
            Float3 low=sites[order[0]],high=low;double far=-1;
            for(int i=0;i<order.Count;i++)for(int j=i+1;j<order.Count;j++){
                var a=sites[order[i]];var b=sites[order[j]];double x=(double)b.X-a.X,z=(double)b.Z-a.Z,d=x*x+z*z;if(d>far){far=d;low=a;high=b;}
            }
            double dx=(double)high.X-low.X,dz=(double)high.Z-low.Z;
            order.Sort(delegate(int a,int b){double pa=(sites[a].X-low.X)*dx+(sites[a].Z-low.Z)*dz,pb=(sites[b].X-low.X)*dx+(sites[b].Z-low.Z)*dz;int c=pa.CompareTo(pb);return c!=0?c:Compare(sites[a],sites[b]);});
            for(int i=0;i<order.Count;i++)result[order[i]]=Math.Min(teams-1,i*teams/order.Count);
            return result;
        }
    }
}
