#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.MapStyles
{
    public static class R25StyleGlobals
    {
        public static void Apply(R25StyleProfile p)
        {
            Shader.SetGlobalFloat("_R25StyleEnabled",p==null?0:1);
            var v=p??new R25StyleProfile();
            Shader.SetGlobalColor("_R25Tint",new Color(v.Tint[0],v.Tint[1],v.Tint[2],1));
            Shader.SetGlobalColor("_R25ShadeColor",new Color(v.ShadeColor[0],v.ShadeColor[1],v.ShadeColor[2],1));
            Shader.SetGlobalColor("_R25FogColor",new Color(v.FogColor[0],v.FogColor[1],v.FogColor[2],1));
            Shader.SetGlobalFloat("_R25Saturation",v.Saturation);
            Shader.SetGlobalFloat("_R25Contrast",v.Contrast);
            Shader.SetGlobalFloat("_R25Exposure",v.Exposure);
            Shader.SetGlobalFloat("_R25ShadeStrength",v.ShadeStrength);
            Shader.SetGlobalFloat("_R25FogDensity",v.FogDensity);
        }
    }
}
#endif
