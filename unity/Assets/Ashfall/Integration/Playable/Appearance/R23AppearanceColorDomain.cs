using System;
namespace Ashfall.Integration.Playable.Appearance
{
    // glTF baseColorFactor/emissiveFactor are scene-linear. URP Gamma shaders
    // sample sRGB color images without decoding; their scalar tint must use the
    // same domain. Linear rendering keeps factors linear. Alpha/data maps never
    // pass through this conversion. No exposure, tone or artistic tint change.
    public static class R23AppearanceColorDomain
    {
        public static float ShaderRgb(float sourceLinear,bool gammaRendering)
        {
            if(Single.IsNaN(sourceLinear)||Single.IsInfinity(sourceLinear)||sourceLinear<0)
                throw new ArgumentOutOfRangeException("sourceLinear");
            if(!gammaRendering)return sourceLinear;
            return sourceLinear<=.0031308f?sourceLinear*12.92f:(float)(1.055*Math.Pow(sourceLinear,1/2.4)-.055);
        }
        public static float LinearRgb(float encoded)
        {
            if(Single.IsNaN(encoded)||Single.IsInfinity(encoded)||encoded<0)
                throw new ArgumentOutOfRangeException("encoded");
            return encoded<=.04045f?encoded/12.92f:(float)Math.Pow((encoded+.055)/1.055,2.4);
        }
    }
}
