// Godot FILMIC equations adapted under the MIT license; see GODOT-LICENSE.txt.
using System;

namespace Ashfall.Integration.Playable.Rendering
{
    public enum R23FilmicInputDomain
    {
        LinearSceneReferred = 0,
        // Only for an actual sRGB encoding of scene-linear radiance, not Gamma-space lighting.
        SrgbEncodedSceneReferred = 1
    }

    public static class R23GodotFilmicMath
    {
        public static bool ValidParameters(float exposure, float white)
        {
            return Finite(exposure) && exposure >= 0 && Finite(white) && white > 0 && Raw(white) > 1e-10;
        }
        public static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
        static double Raw(double color)
        {
            // exposure_bias=2, baked into A and B exactly as Godot's Compatibility shader.
            const double a = .22 * 2 * 2, b = .30 * 2, c = .10, d = .20, e = .01, f = .30;
            return ((color * (a * color + c * b) + d * e) / (color * (a * color + b) + d * f)) - e / f;
        }
        public static float Linear(float input, float exposure = 1, float white = 1)
        {
            if (!Finite(input) || !ValidParameters(exposure, white)) throw new ArgumentOutOfRangeException("FILMIC requires finite radiance/exposure and positive white");
            // Source apply_tonemapping clamps negative radiance, not highlights.
            return (float)(Raw(Math.Max(0, (double)input * exposure)) / Raw(white));
        }
        public static float GodotDisplayEncode(float linear)
        {
            if (!Finite(linear)) throw new ArgumentOutOfRangeException("linear");
            return (float)Math.Max(1.055 * Math.Pow(Math.Max(0, linear), .416666667) - .055, 0);
        }
        public static float SrgbDecode(float encoded)
        {
            if (!Finite(encoded)) throw new ArgumentOutOfRangeException("encoded");
            encoded = Math.Max(0, encoded);
            return encoded <= .04045f ? encoded / 12.92f : (float)Math.Pow((encoded + .055) / 1.055, 2.4);
        }
        public static float SrgbEncode(float linear)
        {
            if (!Finite(linear)) throw new ArgumentOutOfRangeException("linear");
            linear = Math.Max(0, linear);
            return linear <= .0031308f ? linear * 12.92f : (float)(1.055 * Math.Pow(linear, 1 / 2.4) - .055);
        }
        public static float Display(float input, R23FilmicInputDomain domain, float exposure = 1, float white = 1)
        {
            return GodotDisplayEncode(Linear(domain == R23FilmicInputDomain.SrgbEncodedSceneReferred ? SrgbDecode(input) : input, exposure, white));
        }
    }
}
