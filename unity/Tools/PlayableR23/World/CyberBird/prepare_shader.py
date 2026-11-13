"""Stage the source weathered formula in existing native URP scaffolding; no Assets edits."""
import prepare as p
def main():
    p.bindings()
    source=(p.ROOT/'unity/Assets/Ashfall/Integration/Playable/World/Resources/R23ValleyPbr.shader').read_text(encoding='utf-8')
    source=source.replace('Ashfall/R23ValleyPbr','Ashfall/R23CyberWeathered',1)
    source=source.replace('  _Tint("Source tint",Color)=(1,1,1,1)','  _Tint("Source tint",Color)=(1,1,1,1)\n  _BounceColor("Source bounce_color (sRGB)",Color)=(0,0,0,0)\n  _FloorFinish("Source floor_finish",Float)=0',1)
    source=source.replace('float4 _Tint,_EmissionColor,_UvScale,_UvOffset;','float4 _Tint,_EmissionColor,_UvScale,_UvOffset,_BounceColor;\n   float _FloorFinish;',1)
    original='    half4 base=SourceTexture(TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap),i.uv,position,weights)*_Tint;'
    formula='''    // Exact frozen R6 assets/cyber_bird/weathered.gdshader fragment.
    // Source vertex() uses MODEL_MATRIX world position, including +2400Z.
    float3 p=float3(i.positionWS.x,i.positionWS.y,-i.positionWS.z);
    float grit=sin(p.x*3.0+p.z*0.7)*sin(p.z*4.0+p.y*0.2)*0.5+0.5;
    float seams=step(0.975,frac(p.y*0.25))*0.10;
    float streak=sin(p.x*19.0+p.z*17.0)*sin(p.y*0.2)*0.028;
    float3 sourceAlbedo=SRGBToLinear(_Tint.rgb)*(0.90+grit*0.16-seams+streak);
    if(_FloorFinish>0.5)
    {
     float2 tile=abs(frac(p.xz*0.4)-0.5);
     float grout=smoothstep(0.47,0.49,max(tile.x,tile.y));
     sourceAlbedo*=1.0-grout*0.3;
    }
    float3 sourceBounce=SRGBToLinear(_BounceColor.rgb);
    // Godot source_color uniforms enter the source formula in linear space.
    // Current Unity project is Gamma; adapt once after the source math.
    #if defined(UNITY_COLORSPACE_GAMMA)
    half4 base=half4(LinearToSRGB(sourceAlbedo),_Tint.a);
    sourceBounce=LinearToSRGB(sourceBounce);
    #else
    half4 base=half4(sourceAlbedo,_Tint.a);
    #endif'''
    assert original in source;source=source.replace(original,formula,1)
    source=source.replace('    emission*=_EmissionEnergy;','    emission=sourceBounce;',1)
    original='    s.smoothness=1-saturate(_Roughness*Channel(SourceTexture(TEXTURE2D_ARGS(_RoughnessMap,sampler_RoughnessMap),i.uv,position,weights),_RoughnessChannel));'
    assert original in source;source=source.replace(original,'    s.smoothness=1-clamp(_Roughness+grit*0.09,0.12,1.0);',1)
    target=p.HERE/'R23CyberWeathered.shader';target.write_text(source,encoding='utf-8')
    print('Staged '+str(target)+'; root copies resource and binds _BounceColor/_FloorFinish. Source shader formula checked on CPU; shader compiler/render UNRUN.')
if __name__=='__main__':main()
