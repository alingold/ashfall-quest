#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Integration.MapNative;
using Ashfall.Modules.MapStreaming;
namespace Ashfall.Tools.MapAppearanceFormat
{
    public static class FormatNativeFixture
    {
        public static string RunResolvedSource(byte[] manifest,string primitiveId,Shader lit,Dictionary<int,VerifiedImageSlice> receipts)
        {
            var doc=(Dictionary<string,object>)MapJson.Parse(manifest,16777216,128,1000000);PrimitiveBinding binding=null;
            foreach(object row in (object[])doc["instances"]){var instance=(Dictionary<string,object>)row;if((string)instance["id"]==primitiveId)binding=new PrimitiveBinding(primitiveId,checked((int)((MapJson.Number)instance["material"]).AsInteger()),manifest,MapStream.Hash(manifest));}
            if(binding==null)throw new InvalidDataException("exact source primitive required");
            using(var resolver=new UrpSourceAppearance(manifest,lit,receipts,4096,268435456)){
                Material clone=null;try{var original=resolver.Resolve(binding);clone=new Material(original);if(!resolver.Matches(binding,original,clone))throw new InvalidDataException("initial source material");
                    clone.SetFloat("_Smoothness",clone.GetFloat("_Smoothness")==0?1:0);if(resolver.Matches(binding,original,clone))throw new InvalidDataException("material tamper accepted");
                    return "exact source primitive "+primitiveId+" manifest="+MapStream.Hash(manifest)+" decoded="+resolver.DecodedBytes;
                }finally{if(clone){if(Application.isPlaying)UnityEngine.Object.Destroy(clone);else UnityEngine.Object.DestroyImmediate(clone);}}
            }
        }
        // Root-only real decode/upload/mipmap observation. Receipt must be externally
        // supplied from the sealed source trace, never generated here as approval.
        public static string Run(VerifiedImageSlice receipt,bool jpeg)
        {
            var raw=receipt.Read(16777216);int w,h;if(jpeg)BoundedJpeg.Inspect(raw,4096,out w,out h);else BoundedPng.Inspect(raw,4096,out w,out h);
            Texture2D t=null;
            try{t=new Texture2D(2,2,TextureFormat.RGBA32,true,false);if(!ImageConversion.LoadImage(t,raw,false)||t.width!=w||t.height!=h)throw new InvalidDataException("native decode mismatch");
                int levels=1,a=w,b=h;while(a>1||b>1){a=Math.Max(1,a/2);b=Math.Max(1,b/2);levels++;}if(t.mipmapCount!=levels)throw new InvalidDataException("native mip chain");
                return "source-bound decode "+w+"x"+h+" levels="+levels+" encoded="+MapStream.Hash(raw)+" pixels="+MapStream.Hash(t.GetRawTextureData());
            }finally{if(t){if(Application.isPlaying)UnityEngine.Object.Destroy(t);else UnityEngine.Object.DestroyImmediate(t);}}
        }
    }
}
#endif
