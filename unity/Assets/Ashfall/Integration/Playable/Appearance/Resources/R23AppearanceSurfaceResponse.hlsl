#ifndef ASHFALL_APPEARANCE_SURFACE_RESPONSE
#define ASHFALL_APPEARANCE_SURFACE_RESPONSE
// Bound dry-surface response with a separate gloss limit for metal and visors.
// Authored atlas roughness preserves material differences within each named part.
void R23ApplyAppearanceResponse(half3 sourceAlbedo,half metallic,half roughness,half sourceDielectric,half2 response,
    out half3 diffuse,out half3 specular,out half smoothness)
{
    metallic=saturate(metallic);roughness=saturate(roughness);
    // Atlas roughness distinguishes fabric from glossy visor and metal zones.
    // Bound every glossy lobe; dry parts retain a much broader, weaker highlight.
    half weight=(1-smoothstep(.08h,.45h,metallic))*smoothstep(.25h,.65h,roughness);
    half dielectricScale=abs(response.y);
    half dielectric=sourceDielectric*lerp(.8h,dielectricScale,weight);
    metallic=min(metallic,.55h);
    roughness=max(roughness,lerp(.38h,response.x,weight));
    specular=lerp(dielectric.xxx,sourceAlbedo*.60h,metallic);
    half remaining=max(1-Max3(specular.r,specular.g,specular.b),.0001h);
    // Keep the source diffuse energy while reducing dry dielectric highlights.
    diffuse=sourceAlbedo*(1-metallic)*(1-sourceDielectric)/remaining;
    smoothness=1-roughness;
}
// Active only for explicit named part finishes. Disabled is an exact no-op.
half R23AppearanceFinishSmoothness(half smoothness,half2 minimumAndEnabled)
{
    if(minimumAndEnabled.y>.5h)return max(smoothness,saturate(minimumAndEnabled.x));
    return smoothness;
}
#endif
