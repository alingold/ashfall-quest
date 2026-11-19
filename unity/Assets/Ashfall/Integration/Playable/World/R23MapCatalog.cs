using System;
using System.Collections.Generic;
namespace Ashfall.Integration.Playable.World
{
    public sealed class R23MapChoice
    {
        public readonly string Id,Label,PayloadFolder;
        internal R23MapChoice(string id,string label,string folder){Id=id;Label=label;PayloadFolder=folder;}
    }
    public static class R23MapCatalog
    {
        public const string PaintballId="archive:battlefield-paintball-gods-pitdevil";
        public const string AlphaId="archive:alfa-aglae-souza";
        public const string RangeId="range";
        public const string ValleyId="valley";
        public const string ClassicId="archive:classic-bigbox-vr";
        public const string CyberBirdId="cyber_bird";
        static readonly List<R23MapChoice> maps=new List<R23MapChoice>{new R23MapChoice(PaintballId,"BATTLEFIELD PAINTBALL","World"),new R23MapChoice(RangeId,"SANDBOX RANGE","Maps/range"),new R23MapChoice(ValleyId,"RELAY VALLEY","Maps/valley"),new R23MapChoice(CyberBirdId,"CYBER BIRD","Maps/cyber_bird")};
        // 2026-11-09: ALFA, CLASSIC and the other non-GODS community maps were retired to ashfall/retired/20261109-non-gods-maps.
        // AlphaId/ClassicId stay as constants because inert policy code and editor smokes still name them.
        static void Install()
        {
#if UNITY_6000_0_OR_NEWER
            R24ArchiveCatalog.Install();
#endif
        }
        // Broken imports (two source maps glitched together) stay loadable by id but are never offered.
        public static readonly HashSet<string> Excluded=new HashSet<string>(StringComparer.Ordinal);
        public static R23MapChoice[] AvailableMaps {get{Install();var result=new List<R23MapChoice>();foreach(var map in maps)if(!Excluded.Contains(map.Id))result.Add(map);
#if UNITY_6000_0_OR_NEWER
            if(UI.R25LegacyPack.MapsEnabled)foreach(var row in UI.R25LegacyPack.Maps)if(!Excluded.Contains(row.id))result.Add(new R23MapChoice(row.id,row.title+" [RETIRED]",row.relativeFolder));
#endif
            return result.ToArray();}}
        internal static void Add(string id,string label,string folder)
        {
            if(String.IsNullOrEmpty(id)||!id.StartsWith("archive:",StringComparison.Ordinal)||String.IsNullOrEmpty(label)||String.IsNullOrEmpty(folder)||!folder.StartsWith("Maps/",StringComparison.Ordinal)||folder.Contains("..")||folder.Contains(":"))throw new ArgumentException("Source archive catalog row");
            foreach(var map in maps)if(map.Id==id)return;
            maps.Add(new R23MapChoice(id,label,folder));
        }
        public static R23MapChoice Find(string id)
        {
            Install();
            if(id=="paintball")id=PaintballId;if(id=="alpha"||id=="alfa")id=AlphaId;if(id=="classic")id=ClassicId;
            foreach(var map in maps)if(map.Id==id)return map;
#if UNITY_6000_0_OR_NEWER
            if(UI.R25LegacyPack.Present)foreach(var row in UI.R25LegacyPack.Maps)if(row.id==id)return new R23MapChoice(row.id,row.title,row.relativeFolder);
#endif
            return null;
        }
    }
}
