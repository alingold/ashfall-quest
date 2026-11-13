#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.MapNative
{
    // Captures actual installed shader property and pass state, including borrowed resources.
    // Identity/integrity only: this does not attest source fidelity or content approval.
    internal sealed class MaterialIntegrityR3
    {
        readonly Material expected;readonly Shader shader;readonly string[] names,passes,keywords;
        readonly ShaderPropertyType[] types;readonly object[] values;readonly bool[] enabled;
        readonly Vector2[] scales,offsets;readonly int queue;readonly bool instancing,twoSided;
        readonly MaterialGlobalIlluminationFlags gi;readonly string renderType;
        public MaterialIntegrityR3(Material material)
        {
            expected=material;shader=material.shader;queue=material.renderQueue;keywords=material.shaderKeywords;
            instancing=material.enableInstancing;twoSided=material.doubleSidedGI;gi=material.globalIlluminationFlags;renderType=material.GetTag("RenderType",false,"");
            int n=shader.GetPropertyCount();names=new string[n];types=new ShaderPropertyType[n];values=new object[n];scales=new Vector2[n];offsets=new Vector2[n];
            for(int i=0;i<n;i++){names[i]=shader.GetPropertyName(i);types[i]=shader.GetPropertyType(i);values[i]=Read(material,i);if(types[i]==ShaderPropertyType.Texture){scales[i]=material.GetTextureScale(names[i]);offsets[i]=material.GetTextureOffset(names[i]);}}
            passes=new string[material.passCount];enabled=new bool[passes.Length];for(int i=0;i<passes.Length;i++){passes[i]=material.GetPassName(i);enabled[i]=material.GetShaderPassEnabled(passes[i]);}
        }
        object Read(Material m,int i)
        {
            switch(types[i]){
                case ShaderPropertyType.Color:return m.GetColor(names[i]);
                case ShaderPropertyType.Vector:return m.GetVector(names[i]);
                case ShaderPropertyType.Texture:return m.GetTexture(names[i]);
                case ShaderPropertyType.Int:return m.GetInteger(names[i]);
                default:return m.GetFloat(names[i]);
            }
        }
        public bool Matches(Material m,bool requireIdentity)
        {
            if(!expected||!shader||!m||(requireIdentity&&m!=expected)||m.shader!=shader||m.renderQueue!=queue||m.enableInstancing!=instancing||m.doubleSidedGI!=twoSided||m.globalIlluminationFlags!=gi||m.GetTag("RenderType",false,"")!=renderType||m.passCount!=passes.Length)return false;
            var k=m.shaderKeywords;if(k.Length!=keywords.Length)return false;for(int i=0;i<k.Length;i++)if(k[i]!=keywords[i])return false;
            for(int i=0;i<names.Length;i++){
                if(types[i]==ShaderPropertyType.Texture){var texture=values[i] as Texture;if(!System.Object.ReferenceEquals(texture,null)&&!texture)return false;if(m.GetTexture(names[i])!=texture||m.GetTextureScale(names[i])!=scales[i]||m.GetTextureOffset(names[i])!=offsets[i])return false;}
                else if(!Read(m,i).Equals(values[i]))return false;
            }
            for(int i=0;i<passes.Length;i++)if(m.GetPassName(i)!=passes[i]||m.GetShaderPassEnabled(passes[i])!=enabled[i])return false;
            return true;
        }
    }
}
#endif
