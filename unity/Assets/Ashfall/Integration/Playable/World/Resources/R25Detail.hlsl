// One world-space sample. Wall projections keep world height on their V axis.
float3 R25DetailAlbedo(float3 albedo,float3 positionWS,float3 normalWS)
{
    float fade=saturate((30-distance(GetCameraPositionWS(),positionWS))/24);
    if(fade<=0)return albedo;
    float3 axis=abs(normalWS);
    float2 uv=axis.y>=axis.x && axis.y>=axis.z?positionWS.xz:
        (axis.x>=axis.z?positionWS.zy:positionWS.xy);
    float detail=SAMPLE_TEXTURE2D(_R25DetailMap,sampler_R25DetailMap,uv/_R25DetailMetres).r;
    return albedo*(1+(_R25DetailStrength*fade)*(2*detail-1));
}
