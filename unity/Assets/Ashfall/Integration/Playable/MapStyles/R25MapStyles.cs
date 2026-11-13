using System;
using System.Collections.Generic;
using System.Globalization;
namespace Ashfall.Integration.Playable.MapStyles
{
    // Resolved value object. Null from Resolve means the original archive environment.
    public sealed class R25StyleProfile
    {
        public string Label = "", GroundTexture = "";
        public float[] ClearColor = new float[] {.616f,.714f,.776f};
        public float[] AmbientColor = new float[] {.773f,.835f,.886f};
        public float[] SunColor = new float[] {1,1,1};
        public float[] FogColor = new float[] {.616f,.714f,.776f};
        public float[] Tint = new float[] {1,1,1};
        public float[] ShadeColor = new float[] {0,0,0};
        public float[] GroundTint = new float[] {1,1,1};
        public float[] SunEuler = new float[] {53,28,0};
        public float[] SkyZenith = null;
        public float[] SkyHorizon = null;
        public float[] SkyNadir = null;
        public float AmbientIntensity = 0.8f;
        public float SunIntensity = 1f;
        public float FogDensity = 0f;
        public float Saturation = 1f;
        public float Contrast = 1f;
        public float Exposure = 1f;
        public float ShadeStrength = 0f;
        public float SunGlow = 0f;
    }
    public sealed class R25MapStyles
    {
        readonly Dictionary<string, Dictionary<string,string>> presets = new Dictionary<string, Dictionary<string,string>>(StringComparer.Ordinal);
        readonly Dictionary<string, Dictionary<string,string>> maps = new Dictionary<string, Dictionary<string,string>>(StringComparer.Ordinal);
        public IEnumerable<string> MapIds { get { return maps.Keys; } }
        public static R25MapStyles Parse(string text)
        {
            try
            {
                var root = R25StyleJson.Object(text);
                string version;
                if (!root.TryGetValue("version", out version) || Number(version) != 1) return null;
                var result = new R25MapStyles();
                foreach (var row in R25StyleJson.Object(root["presets"])) result.presets.Add(row.Key, R25StyleJson.Object(row.Value));
                foreach (var row in R25StyleJson.Object(root["maps"])) result.maps.Add(row.Key, R25StyleJson.Object(row.Value));
                // Validate known fields even in unused presets; unknown fields remain harmless.
                foreach (var row in result.presets.Values) Apply(new R25StyleProfile(), row);
                foreach (var row in result.maps.Values) Apply(new R25StyleProfile(), row);
                return result;
            }
            catch (Exception e)
            {
                if (e is FormatException || e is KeyNotFoundException || e is OverflowException) return null;
                throw;
            }
        }
        public R25StyleProfile Resolve(string mapId)
        {
            Dictionary<string,string> entry, preset;
            if (mapId == null || !mapId.StartsWith("archive:", StringComparison.Ordinal) || !maps.TryGetValue(mapId, out entry)) return null;
            var p = new R25StyleProfile(); string token;
            if (entry.TryGetValue("preset", out token))
            {
                // Exactly one lookup. A preset's own preset member never recurses.
                if (!presets.TryGetValue(R25StyleJson.String(token), out preset)) return null;
                Apply(p, preset);
            }
            Apply(p, entry); return p;
        }
        static double Number(string token)
        {
            double n;
            if (!Double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out n) || Double.IsNaN(n) || Double.IsInfinity(n)) throw new FormatException("Style number");
            return n;
        }
        static float Clamp(string token, double lo, double hi) { return (float)Math.Max(lo, Math.Min(hi, Number(token))); }
        static float[] Triple(string token, bool colour)
        {
            if (!token.StartsWith("[", StringComparison.Ordinal) || !token.EndsWith("]", StringComparison.Ordinal)) throw new FormatException("Style triple");
            string[] values = token.Substring(1, token.Length-2).Split(',');
            if (values.Length != 3) throw new FormatException("Style triple length");
            var v = new float[3];
            for (int i=0;i<3;i++) v[i] = Clamp(values[i], colour ? 0 : -Single.MaxValue, colour ? 1 : Single.MaxValue);
            return v;
        }
        static void Apply(R25StyleProfile p, Dictionary<string,string> row)
        {
            string v;
            if (row.TryGetValue("label", out v)) p.Label = R25StyleJson.String(v);
            if (row.TryGetValue("preset", out v)) R25StyleJson.String(v);
            if (row.TryGetValue("skyTexture", out v)) p.GroundTexture = R25StyleJson.String(v);
            if (row.TryGetValue("skyTint", out v)) p.GroundTint = Triple(v, true);
            if (row.TryGetValue("groundTexture", out v)) p.GroundTexture = R25StyleJson.String(v);
            if (row.TryGetValue("clearColor", out v)) p.ClearColor = Triple(v, true);
            if (row.TryGetValue("ambientColor", out v)) p.AmbientColor = Triple(v, true);
            if (row.TryGetValue("sunColor", out v)) p.SunColor = Triple(v, true);
            if (row.TryGetValue("fogColor", out v)) p.FogColor = Triple(v, true);
            if (row.TryGetValue("tint", out v)) p.Tint = Triple(v, true);
            if (row.TryGetValue("shadeColor", out v)) p.ShadeColor = Triple(v, true);
            if (row.TryGetValue("groundTint", out v)) p.GroundTint = Triple(v, true);
            if (row.TryGetValue("sunEuler", out v)) p.SunEuler = Triple(v, false);
            if (row.TryGetValue("skyZenith", out v)) p.SkyZenith = Triple(v, true);
            if (row.TryGetValue("skyHorizon", out v)) p.SkyHorizon = Triple(v, true);
            if (row.TryGetValue("skyNadir", out v)) p.SkyNadir = Triple(v, true);
            if (row.TryGetValue("ambientIntensity", out v)) p.AmbientIntensity = Clamp(v, 0, 2);
            if (row.TryGetValue("sunIntensity", out v)) p.SunIntensity = Clamp(v, 0, 3);
            if (row.TryGetValue("fogDensity", out v)) p.FogDensity = Clamp(v, 0, 0.2);
            if (row.TryGetValue("saturation", out v)) p.Saturation = Clamp(v, 0, 2);
            if (row.TryGetValue("contrast", out v)) p.Contrast = Clamp(v, 0.5, 1.5);
            if (row.TryGetValue("exposure", out v)) p.Exposure = Clamp(v, 0.25, 2);
            if (row.TryGetValue("shadeStrength", out v)) p.ShadeStrength = Clamp(v, 0, 1);
            if (row.TryGetValue("sunGlow", out v)) p.SunGlow = Clamp(v, 0, 1);
        }
    }
}
