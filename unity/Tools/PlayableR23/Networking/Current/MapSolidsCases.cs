using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Integration.Playable.MapSolids;
using Ashfall.Integration.Playable.MapStyles;
partial class LobbyCases
{
    static string SolidHash = new string('a',64);
    static string SolidRow(int id, string volume)
    {
        return "{\"id\":\"p"+id+"\",\"kind\":\"source\",\"shape\":\"convex\",\"positionsSha256\":\""+SolidHash+"\",\"material\":0,\"vertices\":4,\"triangles\":4,\"references\":[0,"+(id*4)+",0,"+(id*4+1)+",0,"+(id*4+2)+",0,"+(id*4+3)+"],\"volume\":"+volume+",\"center\":[0,0,0],\"headFitPossible\":false}";
    }
    static string SolidFile(string rows)
    {
        return "{\"version\":2,\"format\":\"ashfall-solid-volumes-r25\",\"mapId\":\"archive:test\",\"sourceSha256\":\""+SolidHash+"\",\"geometrySha256\":\""+SolidHash+"\",\"chunks\":1,\"definitions\":["+rows+"]}";
    }
    static R25MapSolids SolidParse(string text)
    {
        string reason;return R25MapSolids.Parse(text,"archive:test",SolidHash,SolidHash,1,out reason);
    }
    static void MapSolidsValidation()
    {
        string good=SolidFile(SolidRow(0,"1"));
        Need(SolidParse(good)!=null,"Valid sidecar");
        foreach(string bad in new[]{good.Replace("r25","r26"),good.Replace("\"version\":2","\"version\":1"),good.Replace("archive:test","archive:other"),good.Replace("\"geometrySha256\":\"a","\"geometrySha256\":\"b"),good.Replace("\"sourceSha256\":\"a","\"sourceSha256\":\"b"),good.Replace("\"vertices\":4","\"vertices\":256"),good.Replace("\"triangles\":4","\"triangles\":1025"),SolidFile(SolidRow(0,"NaN")),SolidFile(SolidRow(0,"1e999")),good.Replace("[0,0,0]","[0,1e999,0]"),good.Replace("[0,0,0]","[0,NaN,0]"),good.Replace("[0,0,0]","[0,3.5e38,0]"),"{",good+" trailing",good.Replace("\"chunks\":1","\"chunks\":2147483648"),SolidFile(SolidRow(0,"1")+","+SolidRow(0,"2"))})
            Need(SolidParse(bad)==null,"Invalid sidecar rejected without exception");
    }
    static void MapSolidsCap()
    {
        var rows=new List<string>();for(int i=0;i<1502;i++)rows.Add(SolidRow(i,(i+1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var data=SolidParse(SolidFile(String.Join(",",rows.ToArray())));
        Need(data!=null && data.OriginalCount==1502 && data.Definitions.Length==1500,"Named cap trims over-cap input");
        Need(data.Definitions[0].Volume==1502 && data.Definitions[1499].Volume==3,"Largest volumes retained");
        rows[0]=SolidRow(0,"1e999");Need(SolidParse(SolidFile(String.Join(",",rows.ToArray())))==null,"Validate even definitions that would be trimmed");
    }
    static string SolidPayloadRoot()
    {
        var dir=new DirectoryInfo(Environment.CurrentDirectory);
        while(dir!=null){string candidate=Path.Combine(dir.FullName,"unity/Assets/StreamingAssets/PlayableR23");if(Directory.Exists(candidate))return candidate;dir=dir.Parent;}
        throw new Exception("Find installed payload root");
    }
    static void MapSolidsReal()
    {
        string root=SolidPayloadRoot();int files=0,definitions=0;
        foreach(string path in Directory.GetFiles(root,"solid-volumes-r25.json",SearchOption.AllDirectories))
        {
            string text=File.ReadAllText(path);var header=R25StyleJson.Object(text);
            string map=R25StyleJson.String(header["mapId"]),source=R25StyleJson.String(header["sourceSha256"]);
            string geometryPath=Path.Combine(Path.GetDirectoryName(path),"geometry.bytes");string hash;
            using(var sha=System.Security.Cryptography.SHA256.Create())using(var stream=File.OpenRead(geometryPath))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            string reason;var data=R25MapSolids.Parse(text,map,source,hash,Int32.Parse(header["chunks"]),out reason);
            Need(data!=null,"Real sidecar "+path+": "+reason);files++;definitions+=data.Definitions.Length;
            // A trimmed copy retains one original definition; totals are informational.
            var rows=R25StyleJson.Array(header["definitions"]);header["definitions"]="["+rows[0]+"]";
            Need(R25MapSolids.Parse(R25StyleJson.Encode(header),map,source,hash,Int32.Parse(header["chunks"]),out reason).Definitions.Length==1,"Trimmed real sidecar");
        }
        Need(files==60 && definitions==24494,"Every installed r25 sidecar and geometry binding (Afterlife parsed but unused at runtime)");
    }
}
