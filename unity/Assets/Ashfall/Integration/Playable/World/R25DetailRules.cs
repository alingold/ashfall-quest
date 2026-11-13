using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Integration.Playable.MapStyles;
namespace Ashfall.Integration.Playable.World
{
    public sealed class R25DetailChoice
    {
        public readonly string Map;
        public readonly float Metres, Strength;
        internal R25DetailChoice(string map, float metres, float strength)
        { Map=map; Metres=metres; Strength=map==null?0:strength; }
    }
    // Pure, bounded schema reader. A bad entry invalidates the whole optional layer.
    public sealed class R25DetailRules
    {
        public static readonly string[] Maps={"rock-grain.png","rock-strata.png","sand-grain.png","concrete-grain.png","metal-brushed.png","wood-grain.png","generic-grain.png"};
        readonly Dictionary<string,R25DetailChoice> aliases, grounds;
        R25DetailRules(Dictionary<string,R25DetailChoice> a, Dictionary<string,R25DetailChoice> g) { aliases=a; grounds=g; }
        static string FileName(string name)
        { if(String.IsNullOrEmpty(name))return "";name=name.Replace('\\','/');return name.Substring(name.LastIndexOf('/')+1); }
        public R25DetailChoice Select(string albedo)
        { R25DetailChoice value;return aliases.TryGetValue(FileName(albedo),out value)?value:null; }
        public R25DetailChoice Ground(string theme)
        { R25DetailChoice value;return grounds.TryGetValue(FileName(theme),out value)?value:null; }
        public int AliasCount { get { return aliases.Count; } }
        public static R25DetailRules Parse(string json)
        {
            try
            {
                var root=R25StyleJson.Object(json);
                if(root["version"]!="1")return null;
                return new R25DetailRules(Read(root["aliases"]),Read(root["grounds"]));
            }
            catch(FormatException) { return null; }
            catch(KeyNotFoundException) { return null; }
            catch(ArgumentException) { return null; }
        }
        static Dictionary<string,R25DetailChoice> Read(string json)
        {
            var result=new Dictionary<string,R25DetailChoice>(StringComparer.OrdinalIgnoreCase);
            var rows=R25StyleJson.Object(json);
            if(rows.Count>4096)throw new FormatException("Too many detail assignments");
            foreach(var row in rows)
            {
                if(row.Key!=FileName(row.Key)||row.Key.Length==0)throw new FormatException("Expected filename");
                var fields=R25StyleJson.Object(row.Value);
                string map=fields["detail_map"]=="null"?null:R25StyleJson.String(fields["detail_map"]);
                if(map!=null && Array.IndexOf(Maps,map)<0)throw new FormatException("Unknown detail map");
                result.Add(row.Key,new R25DetailChoice(map,Number(fields["world_tiling_metres"],.25f,4),Number(fields["strength"],0,1)));
            }
            return result;
        }
        static float Number(string token,float min,float max)
        {
            float value;
            if(!Single.TryParse(token,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||Single.IsNaN(value)||Single.IsInfinity(value))throw new FormatException("Detail number");
            return Math.Max(min,Math.Min(max,value));
        }
    }
}
