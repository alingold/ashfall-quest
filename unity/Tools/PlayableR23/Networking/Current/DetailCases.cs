using System;
using System.IO;
using Ashfall.Integration.Playable.World;
partial class LobbyCases
{
    static void DetailSelection()
    {
        string row="{\"detail_map\":\"rock-grain.png\",\"world_tiling_metres\":0.01,\"strength\":4}";
        var data=R25DetailRules.Parse("{\"version\":1,\"aliases\":{\"t108_c.jpg\":"+row+",\"skip.jpg\":{\"detail_map\":null,\"world_tiling_metres\":99,\"strength\":1}},\"grounds\":{\"basalt.jpg\":"+row+"}}");
        Need(data!=null,"Detail schema parses");
        var a=data.Select("textures/T108_C.JPG");
        Need(a!=null&&a.Map=="rock-grain.png"&&a.Metres==.25f&&a.Strength==1,"Alias basename case and low/high clamps");
        Need(data.Select("missing")==null&&data.Select(null)==null,"No implicit fallback on unknown albedo");
        Need(data.Select("skip.jpg").Map==null&&data.Select("skip.jpg").Strength==0&&data.Select("skip.jpg").Metres==4,"Null explicitly excludes; upper tiling clamp");
        Need(data.Ground("Styles/ground/basalt.jpg").Map==a.Map&&data.Ground("unknown")==null,"Data-selected ground theme");
        string zero=row.Replace("0.01","2").Replace("strength\":4","strength\":-2");
        var low=R25DetailRules.Parse("{\"version\":1,\"aliases\":{\"a\":"+zero+"},\"grounds\":{}}");
        Need(low!=null&&low.Select("a").Strength==0,"Lower strength clamp");
        foreach(string bad in new[]{null,"","{}","{","{\"version\":2}","{\"version\":1,\"aliases\":{},\"grounds\":{},\"version\":1}","{\"version\":1,\"aliases\":{\"a\":"+row.Replace("rock-grain.png","../bad.png")+"},\"grounds\":{}}","{\"version\":1,\"aliases\":{\"a\":"+row.Replace("0.01","1e999")+"},\"grounds\":{}}"})
            Need(R25DetailRules.Parse(bad)==null,"Malformed detail data must disable layer");
    }
    static void DetailShipped()
    {
        var dir=new DirectoryInfo(Environment.CurrentDirectory);string path=null;
        while(dir!=null){string candidate=Path.Combine(dir.FullName,"unity/Assets/StreamingAssets/PlayableR23/Styles/detail/detail.json");if(File.Exists(candidate)){path=candidate;break;}dir=dir.Parent;}
        Need(path!=null,"Real installed detail JSON exists");
        var rules=R25DetailRules.Parse(File.ReadAllText(path));Need(rules!=null&&rules.AliasCount==402,"All installed albedo aliases parsed");
        // Rock faces use rock-grain since revision C: the strata map showed a visible repeat at 2 m.
        Need(rules.Select("t108_c.jpg").Map=="rock-grain.png","Real rock slot selected");
        Need(rules.Select("t100_c.jpg").Strength==0,"Uncertain slot100 remains excluded");
        Need(rules.Ground("pale-dune.jpg").Map=="sand-grain.png"&&rules.Ground("basalt-ember.jpg").Map=="rock-grain.png"&&rules.Ground("moon-regolith.jpg").Strength<=.2f,"Ground themes select sand rock and low grain");
        foreach(string map in R25DetailRules.Maps)Need(File.Exists(Path.Combine(Path.GetDirectoryName(path),map)),"Installed detail texture exists: "+map);
    }
}
