using System;
using System.Collections.Generic;
using System.Globalization;
using Ashfall.Integration.Playable.MapStyles;

namespace Ashfall.Integration.Playable.MapSolids
{
    public sealed class R25SolidDefinition
    {
        public string Id, PositionsHash;
        public int Material, Vertices, Triangles;
        public int[] References;
        public double Volume;
    }

    // Optional local exclusion data. Validate every definition before selecting the largest.
    public sealed class R25MapSolids
    {
        public const int MaxSolidsPerMap = 1500;
        public const int MaxDefinitions = 65536, MaxTrianglesPerHull = 1024;
        public const int MaxTotalTriangles = 1000000;
        public R25SolidDefinition[] Definitions { get; private set; }
        public int OriginalCount { get; private set; }
        static void Need(bool ok, string reason) { if (!ok) throw new FormatException(reason); }
        static string S(Dictionary<string,string> row, string key) { return R25StyleJson.String(row[key]); }
        static int I(string token) { return Int32.Parse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture); }
        static double F(string token)
        {
            double n = Double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
            Need(!Double.IsNaN(n) && !Double.IsInfinity(n) && Math.Abs(n) <= Single.MaxValue, "nonfinite/overflow value");
            return n;
        }
        static bool Hash(string s)
        {
            if (s == null || s.Length != 64) return false;
            foreach (char c in s) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
            return true;
        }
        public static R25MapSolids Parse(string json, string map, string source, string geometry, int chunks, out string reason)
        {
            reason = null;
            try
            {
                var root = R25StyleJson.Object(json);
                Need(I(root["version"]) == 2 && S(root,"format") == "ashfall-solid-volumes-r25", "format/version");
                Need(S(root,"mapId") == map, "map id");
                Need(Hash(source) && S(root,"sourceSha256") == source, "source hash");
                Need(Hash(geometry) && S(root,"geometrySha256") == geometry, "geometry hash");
                Need(chunks > 0 && I(root["chunks"]) == chunks, "chunk count");
                var rows = R25StyleJson.Array(root["definitions"]);
                Need(rows.Count <= MaxDefinitions, "definition count");
                var definitions = new List<R25SolidDefinition>();
                var ids = new HashSet<string>(StringComparer.Ordinal); var refs = new HashSet<long>(); int total = 0;
                foreach (string token in rows)
                {
                    var r = R25StyleJson.Object(token); var d = new R25SolidDefinition();
                    d.Id = S(r,"id"); d.PositionsHash = S(r,"positionsSha256");
                    Need(d.Id.Length > 0 && d.Id.Length <= 256 && ids.Add(d.Id), "unique bounded id");
                    Need(S(r,"kind") == "source" && Hash(d.PositionsHash), "kind/positions hash");
                    string shape = S(r,"shape"); Need(shape == "aabb" || shape == "obb" || shape == "convex", "shape");
                    d.Material = I(r["material"]); d.Vertices = I(r["vertices"]); d.Triangles = I(r["triangles"]);
                    Need(d.Material >= -1 && d.Vertices >= 4 && d.Vertices <= 255 && d.Triangles >= 4 && d.Triangles <= MaxTrianglesPerHull, "hull limits");
                    d.Volume = F(r["volume"]); Need(d.Volume > 0, "volume");
                    var center = R25StyleJson.Array(r["center"]); Need(center.Count == 3, "center"); foreach (string v in center) F(v);
                    Need(r["headFitPossible"] == "true" || r["headFitPossible"] == "false", "head fit flag");
                    var rr = R25StyleJson.Array(r["references"]); Need(rr.Count == d.Triangles * 2, "reference count");
                    total += d.Triangles; Need(total <= MaxTotalTriangles, "total triangle limit");
                    d.References = new int[rr.Count]; long previous = -1;
                    for (int i = 0; i < rr.Count; i += 2)
                    {
                        int c = I(rr[i]), t = I(rr[i+1]); long key = ((long)c << 32) | (uint)t;
                        Need(c >= 0 && c < chunks && t >= 0 && key > previous && refs.Add(key), "disjoint ascending references");
                        previous = key; d.References[i] = c; d.References[i+1] = t;
                    }
                    definitions.Add(d);
                }
                int original = definitions.Count;
                if (original > MaxSolidsPerMap)
                {
                    definitions.Sort(delegate(R25SolidDefinition a, R25SolidDefinition b) {
                        int order = b.Volume.CompareTo(a.Volume); return order != 0 ? order : StringComparer.Ordinal.Compare(a.Id,b.Id); });
                    definitions.RemoveRange(MaxSolidsPerMap, original - MaxSolidsPerMap);
                }
                return new R25MapSolids { Definitions = definitions.ToArray(), OriginalCount = original };
            }
            catch (Exception error) { reason = error.Message.Replace('\r',' ').Replace('\n',' '); return null; }
        }
    }
}
