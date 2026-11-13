using System;
using System.IO;
using System.Collections.Generic;
namespace Ashfall.Modules.MapStreaming
{
    internal static class MapSchema
    {
        static void Need(bool b,string m) {if(!b)throw new InvalidDataException("MAPREF schema: "+m);}
        static Dictionary<string,object> Obj(object o,string fields,string optional="")
        {
            var d=o as Dictionary<string,object>;Need(d!=null,"object required");
            var keys=new HashSet<string>(fields.Split(' '),StringComparer.Ordinal);
            foreach(string k in d.Keys)Need(keys.Contains(k)||Array.IndexOf(optional.Split(' '),k)>=0,"unknown member "+k);
            foreach(string k in keys)Need(d.ContainsKey(k),"missing member "+k);return d;
        }
        static object[] Arr(object o) {var a=o as object[];Need(a!=null,"array required");return a;}
        static void Text(Dictionary<string,object> d,string keys) {foreach(string k in keys.Split(' '))Need(d[k] is string,"string "+k);}
        static void Ints(Dictionary<string,object> d,string keys) {foreach(string k in keys.Split(' ')){var n=d[k] as MapJson.Number;Need(n!=null,"integer "+k);n.AsInteger();}}
        internal static void Validate(Dictionary<string,object> d)
        {
            Obj(d,"format schema mapId source policy instances accessorRanges references");Text(d,"format mapId");Ints(d,"schema");
            var s=Obj(d["source"],"path sha256 revision approvalDisposition length jsonSha256 binOffset binLength");Text(s,"path sha256 revision approvalDisposition jsonSha256");Ints(s,"length binOffset binLength");
            var p=Obj(d["policy"],"indoor capsules collisionRole playableApproval","spawnValidation nativeDeathmatchMarkers nativeWarmupMarkers deathmatchMissing semanticSourceSha256");Text(p,"collisionRole");foreach(string k in new[]{"spawnValidation","semanticSourceSha256"})if(p.ContainsKey(k))Need(p[k] is string,"policy text");
            foreach(string k in new[]{"indoor","playableApproval"})Need(p[k] is bool,"boolean "+k);
            Need(p["capsules"] is bool || (p["capsules"] is string && (string)p["capsules"]=="UNRESOLVED"),"capsules type");
            foreach(string k in new[]{"nativeDeathmatchMarkers","nativeWarmupMarkers"})if(p.ContainsKey(k))Arr(p[k]);if(p.ContainsKey("deathmatchMissing"))Need(p["deathmatchMissing"] is bool,"deathmatchMissing");
            var r=Obj(d["references"],"nodes scenes meshes accessors bufferViews buffers materials textures images samplers");foreach(object v in r.Values)Arr(v);
            foreach(object v in Arr(d["instances"])) {var row=Obj(v,"id node mesh primitive matrix attributes indices material collisionRole");Text(row,"id collisionRole");Ints(row,"node mesh primitive indices material");foreach(object n in Arr(row["matrix"]))Need(n is MapJson.Number,"matrix number");var attrs=row["attributes"] as Dictionary<string,object>;Need(attrs!=null,"attributes");foreach(string k in attrs.Keys)Ints(attrs,k);}
            foreach(object v in Arr(d["accessorRanges"])) {var row=Obj(v,"id offset viewOffset viewLength componentType type count normalized byteStride");Text(row,"type");Ints(row,"id offset viewOffset viewLength componentType count byteStride");Need(row["normalized"] is bool,"normalized boolean");}
            // glTF allows application extras/extensions; consumed integer and boolean fields still have exact types.
            foreach(object v in Arr(r["accessors"])) {var a=(Dictionary<string,object>)v;if(a.ContainsKey("normalized"))Need(a["normalized"] is bool,"accessor normalized");}
        }
    }
}
