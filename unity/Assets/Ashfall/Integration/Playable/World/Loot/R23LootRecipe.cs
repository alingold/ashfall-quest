using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Activities;

namespace Ashfall.Integration.Playable.World.Loot
{
    public struct R23CacheWeapon
    {
        public readonly int Catalog,Stars,Slot;
        public R23CacheWeapon(int catalog,int stars,int slot){Catalog=catalog;Stars=stars;Slot=slot;}
    }
    // archive_gameplay_r8.activate_near; keep unsupported catalog entries in the
    // recipe, rather than substituting supported guns and changing the draw stream.
    public static class R23LootRecipe
    {
        static readonly int[] active={0,1,2,3,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,23,24,25,26,27,28,29};
        public static int[] ReviewCatalogIds {get{return(int[])active.Clone();}}
        public static string GrenadeKind(int catalog){return catalog==26?"frag":catalog==28?"zone":null;}
        public static uint SourceHash(string text)
        {
            if(text==null)throw new ArgumentNullException("text");uint hash=5381;
            for(int i=0;i<text.Length;i++)
            {uint c=text[i];if(c==0)break;if(Char.IsHighSurrogate(text[i])&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])){c=(uint)Char.ConvertToUtf32(text[i],text[i+1]);i++;}hash=unchecked(hash*33+c);}
            return hash;
        }
        static bool Melee(int id){return id==14||id==15||id==16||id==29;}
        static bool Explosive(int id){return id==26||id==27||id==28;}
        public static R23CacheWeapon[] Cache(string mapId,int siteIndex){return Cache(mapId,siteIndex,R24ArchiveMapPolicy.MagnumOnly(mapId));}
        public static R23CacheWeapon[] Cache(string mapId,int siteIndex,bool magnumOnly)
        {
            if(siteIndex<0||siteIndex>65535)throw new ArgumentOutOfRangeException("siteIndex");
            var rng=new R23BuildLightningRecipe.SourceRandom((ulong)SourceHash(mapId)+(ulong)siteIndex*7907UL);
            var chosen=new HashSet<int>();var result=new R23CacheWeapon[6];bool melee=false,explosive=false;
            for(int slot=0;slot<6;slot++)
            {
                var pool=new List<int>();if(magnumOnly)pool.Add(7);else foreach(int id in active)if(!chosen.Contains(id)&&(!melee||!Melee(id))&&(!explosive||!Explosive(id)))pool.Add(id);
                uint bound=(uint)pool.Count,threshold=unchecked(0u-bound)%bound,draw;do{draw=rng.Next();}while(draw<threshold);
                int catalog=pool[(int)(draw%bound)];chosen.Add(catalog);melee|=Melee(catalog);explosive|=Explosive(catalog);
                int stars=1+(rng.Value()>.45?1:0)+(rng.Value()>.75?1:0)+(rng.Value()>.91?1:0)+(rng.Value()>.98?1:0);
                result[slot]=new R23CacheWeapon(catalog,stars,slot);
            }
            return result;
        }
    }
    public sealed class R23CacheActivation
    {
        readonly Float3[] sites;readonly bool[] active;
        public int ActivatedCount {get;private set;}
        public R23CacheActivation(Float3[] positions)
        {if(positions==null||positions.Length>65536)throw new ArgumentException("bounded cache sites");sites=(Float3[])positions.Clone();active=new bool[sites.Length];}
        public int[] Activate(Float3[] players,bool initial)
        {
            if(players==null)throw new ArgumentNullException("players");var added=new List<int>();double radius=initial?35:65;
            for(int i=0;i<sites.Length;i++)
            {
                if(active[i])continue;bool near=false;
                foreach(var p in players){double x=p.X-sites[i].X,y=p.Y-sites[i].Y,z=p.Z-sites[i].Z;if(x*x+y*y+z*z<radius*radius){near=true;break;}}
                if(!near)continue;active[i]=true;ActivatedCount++;added.Add(i);if(added.Count>=(initial?12:2))break;
            }
            return added.ToArray();
        }
    }
}
