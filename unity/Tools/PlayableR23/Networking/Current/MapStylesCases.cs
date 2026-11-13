using System;
using System.IO;
using Ashfall.Integration.Playable.MapStyles;
partial class LobbyCases
{
    static R25StyleProfile Style(string fields)
    {
        var data=R25MapStyles.Parse("{\"version\":1,\"presets\":{},\"maps\":{\"archive:test\":{"+fields+"}}}");
        Need(data!=null,"Valid style fields");return data.Resolve("archive:test");
    }
    static void MapStylesDefaults()
    {
        Need(R25MapStyles.Parse(null)==null && R25MapStyles.Parse("")==null,"Missing file is no profile");
        var data=R25MapStyles.Parse("{\"version\":1,\"presets\":{},\"maps\":{}}");
        Need(data!=null && data.Resolve("archive:missing")==null && data.Resolve("range")==null,"Missing map is no profile");
        var p=Style("");
        Need(p.ClearColor[0]==.616f && p.AmbientColor[0]==.773f && p.AmbientIntensity==.8f && p.SunIntensity==1 && p.SunEuler[0]==53 && p.SunEuler[1]==28,"Environment defaults");
        Need(p.FogDensity==0 && p.Tint[0]==1 && p.Saturation==1 && p.Contrast==1 && p.Exposure==1 && p.ShadeStrength==0 && p.GroundTint[0]==1 && p.GroundTexture=="" && p.SkyZenith==null && p.SunGlow==0,"Neutral world/ground/sky defaults");
    }
    static void MapStylesMerge()
    {
        var d=R25MapStyles.Parse("{\"version\":1,\"presets\":{\"a\":{\"preset\":\"b\",\"tint\":[0.2,0.3,0.4],\"exposure\":1.2},\"b\":{\"sunIntensity\":2}},\"maps\":{\"archive:test\":{\"preset\":\"a\",\"exposure\":1.4,\"unknown\":{\"anything\":[null,true,42]}}}}");
        Need(d!=null,"Preset data");var p=d.Resolve("archive:test");
        Need(p.Tint[0]==.2f && p.Exposure==1.4f && p.SunIntensity==1,"Override wins; preset references do not recurse; unknown ignored");
        p.Tint[0]=1;Need(d.Resolve("archive:test").Tint[0]==.2f,"Resolve does not mutate preset");
        p=Style("\"skyTexture\":\"old.jpg\",\"skyTint\":[0.2,0.3,0.4]");Need(p.GroundTexture=="old.jpg" && p.GroundTint[1]==.3f,"Deprecated aliases");
        p=Style("\"skyTexture\":\"old.jpg\",\"groundTexture\":\"new.jpg\",\"skyTint\":[0,0,0],\"groundTint\":[1,1,1]");Need(p.GroundTexture=="new.jpg" && p.GroundTint[0]==1,"Canonical fields win within layer");
        p=Style("\"skyZenith\":[1,0,0],\"skyHorizon\":[0,1,0],\"skyNadir\":[0,0,1]");Need(p.SkyZenith[0]==1 && p.SkyHorizon[1]==1 && p.SkyNadir[2]==1,"Three sky colours retained");
    }
    static void MapStylesClampAndInvalid()
    {
        var p=Style("\"ambientIntensity\":9,\"sunIntensity\":-1,\"fogDensity\":1,\"saturation\":-2,\"contrast\":9,\"exposure\":0,\"shadeStrength\":2,\"sunGlow\":-1,\"tint\":[-1,0.5,3],\"sunEuler\":[-90,720,0]");
        Need(p.AmbientIntensity==2 && p.SunIntensity==0 && p.FogDensity==.2f && p.Saturation==0 && p.Contrast==1.5f && p.Exposure==.25f && p.ShadeStrength==1 && p.SunGlow==0 && p.Tint[0]==0 && p.Tint[2]==1,"Upper and lower clamps");
        Need(p.SunEuler[0]==-90 && p.SunEuler[1]==720,"Euler has no schema range");
        p=Style("\"ambientIntensity\":-1,\"sunIntensity\":8,\"fogDensity\":-1,\"saturation\":8,\"contrast\":0,\"exposure\":8,\"shadeStrength\":-1,\"sunGlow\":8");
        Need(p.AmbientIntensity==0 && p.SunIntensity==3 && p.FogDensity==0 && p.Saturation==2 && p.Contrast==.5f && p.Exposure==2 && p.ShadeStrength==0 && p.SunGlow==1,"Opposite clamps");
        foreach(string bad in new[]{"{", "{} trailing", "{\"version\":1,}", "{\"version\":01}", "{\"version\":1,\"version\":1}", "{\"version\":2}", "{\"version\":1,\"presets\":{},\"maps\":{\"archive:test\":{\"tint\":[1,2]}}}", "{\"version\":1,\"presets\":{},\"maps\":{\"archive:test\":{\"exposure\":\"x\"}}}"})
            Need(R25MapStyles.Parse(bad)==null,"Malformed/invalid styles return null without exception: "+bad);
    }
    static void MapStylesShipped()
    {
        var dir=new DirectoryInfo(Environment.CurrentDirectory);string path=null;
        while(dir!=null){string candidate=Path.Combine(dir.FullName,"unity/Assets/StreamingAssets/PlayableR23/Styles/styles.json");if(File.Exists(candidate)){path=candidate;break;}dir=dir.Parent;}
        Need(path!=null,"Find real shipped styles.json from harness working directory");
        var data=R25MapStyles.Parse(File.ReadAllText(path));Need(data!=null,"Real shipped file parses");int count=0;
        foreach(string id in data.MapIds){var p=data.Resolve(id);Need(p!=null && p.SkyZenith!=null && p.SkyHorizon!=null && p.SkyNadir!=null,"Resolve real map "+id);count++;}
        Need(count==64,"All 64 retained style metadata IDs resolve (61 active, three retired)");
    }
}
