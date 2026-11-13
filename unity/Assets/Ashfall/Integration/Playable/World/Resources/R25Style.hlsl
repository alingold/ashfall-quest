#ifndef ASHFALL_R25_STYLE
#define ASHFALL_R25_STYLE
// Globals deliberately outside UnityPerMaterial. Disabled is neutral before first activation.
float _R25StyleEnabled;
float3 _R25Tint, _R25ShadeColor, _R25FogColor;
float _R25Saturation, _R25Contrast, _R25Exposure, _R25ShadeStrength, _R25FogDensity;
float3 R25StyleAlbedo(float3 albedo)
{
    return _R25StyleEnabled > .5 ? albedo * _R25Tint : albedo;
}
float3 R25StyleFinish(float3 radiance, float diffuse, float3 positionWS)
{
    if (_R25StyleEnabled < .5) return radiance;
    radiance += _R25ShadeColor * _R25ShadeStrength * (1 - diffuse);
    float lum = dot(radiance, float3(.2126,.7152,.0722));
    radiance = lerp(lum.xxx, radiance, _R25Saturation);
    radiance = ((radiance - .5) * _R25Contrast + .5) * _R25Exposure;
    float transmittance = exp(-_R25FogDensity * distance(GetCameraPositionWS(), positionWS));
    return lerp(_R25FogColor, radiance, transmittance);
}
#endif
