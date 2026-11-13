#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Modules.Avatar
{
    internal static class ReceiptMaterialFactory
    {
        static Texture2D Load(byte[] bytes,bool linear,List<UnityEngine.Object> owned) {
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false,linear);owned.Add(t);
            if(!ImageConversion.LoadImage(t,bytes,false))throw new AvatarValidationException("Texture decode failed");return t;
        }
        internal static Material Create(Shader shader,HandMaterialReceipt receipt,List<UnityEngine.Object> owned) {
            var albedo=Load(receipt.CopyAlbedo(),false,owned);var orm=Load(receipt.CopyOrm(),true,owned);var normal=Load(receipt.CopyNormal(),true,owned);
            var metallic=new Texture2D(orm.width,orm.height,TextureFormat.RGBA32,false,true);owned.Add(metallic);
            var occlusion=new Texture2D(orm.width,orm.height,TextureFormat.RGBA32,false,true);owned.Add(occlusion);
            var pixels=orm.GetPixels32();var ao=new Color32[pixels.Length];for(int i=0;i<pixels.Length;i++){var p=pixels[i];pixels[i]=new Color32(p.b,0,0,(byte)(255-p.g));ao[i]=new Color32(0,p.r,0,255);}metallic.SetPixels32(pixels);metallic.Apply();occlusion.SetPixels32(ao);occlusion.Apply();
            var m=new Material(shader);owned.Add(m);m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",albedo);m.SetTexture("_MetallicGlossMap",metallic);m.SetTexture("_OcclusionMap",occlusion);m.SetTexture("_BumpMap",normal);m.SetFloat("_Smoothness",1);m.SetFloat("_BumpScale",1);m.EnableKeyword("_METALLICSPECGLOSSMAP");m.EnableKeyword("_OCCLUSIONMAP");m.EnableKeyword("_NORMALMAP");return m;
        }
    }
}
#endif
