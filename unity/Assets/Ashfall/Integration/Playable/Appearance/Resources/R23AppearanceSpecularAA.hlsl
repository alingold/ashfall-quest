#ifndef R23_APPEARANCE_SPECULAR_AA_INCLUDED
#define R23_APPEARANCE_SPECULAR_AA_INCLUDED
// Appearance-only specular anti-aliasing. Broaden unresolved normal-map highlights,
// preserving authored scalar/maps, metallic/F0, colors and clean-surface gloss.
// This approximates the source renderer roughness limiter; it is not its exact implementation.
half R23NormalDetailSmoothnessAA(half smoothness, half3 normalTS)
{
    float3 n=(float3)normalTS;
    n*=rsqrt(max(dot(n,n),1e-8));
    float3 dx=ddx(n),dy=ddy(n);
    float variance=0.25*max(dot(dx,dx),dot(dy,dy));
    float kernel=min(2.0*variance,0.18);
    float perceptualRoughness=1.0-saturate((float)smoothness);
    float roughness=perceptualRoughness*perceptualRoughness;
    return (half)(1.0-sqrt(sqrt(saturate(roughness*roughness+kernel))));
}
#endif
