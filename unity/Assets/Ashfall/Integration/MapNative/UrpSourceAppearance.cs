#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Modules.MapStreaming;
namespace Ashfall.Integration.MapNative
{
    // Explicit supported subset: opaque, single-sided glTF PBR constants and TEXCOORD_0
    // base color PNG/baseline JPEG, identity UV transform, exactly representable sampler. Unsupported source
    // features reject, including normal maps and vertex colors in real pilots.
    public sealed class UrpSourceAppearance : ISourceMaterialResolver,IDisposable
    {
        sealed class Entry {internal Material Material;internal Texture2D Texture;internal Color Color;internal float Metal,Smooth;internal string Pixels;internal int Width,Height,Queue;internal SourceSamplerR4 Sampler;internal long Bytes;internal MaterialIntegrityR3 Integrity;}
        readonly Dictionary<int,Entry> entries=new Dictionary<int,Entry>();
        readonly Dictionary<int,VerifiedImageSlice> images;readonly Dictionary<string,object> refs;
        readonly Dictionary<string,object> document;readonly string manifestHash;readonly Shader shader;readonly int lane;
        readonly int maxMaterials;readonly long maxDecoded;long decoded;bool closed;
        public int MaterialCount {get{return entries.Count;}} public long DecodedBytes {get{return decoded;}}
        public int TextureCount {get{int n=0;foreach(var e in entries.Values)if(e.Texture)n++;return n;}}
        static Dictionary<string,object> D(object o){return (Dictionary<string,object>)o;}
        static int I(object o){return checked((int)((MapJson.Number)o).AsInteger());}
        static float F(object o){double d=((MapJson.Number)o).AsDouble();if(Double.IsNaN(d)||Double.IsInfinity(d)||d<0||d>1)throw new InvalidDataException("PBR scalar");return (float)d;}
        static void Keys(Dictionary<string,object> d,params string[] allowed){foreach(string k in d.Keys)if(Array.IndexOf(allowed,k)<0)throw new InvalidDataException("unsupported appearance feature: "+k);}
        public UrpSourceAppearance(byte[] manifest,Shader urp176Lit,Dictionary<int,VerifiedImageSlice> verifiedImages,int maxMaterials,long maxDecodedBytes)
        {
            if(manifest==null||manifest.Length>16777216||!urp176Lit||urp176Lit.name!="Universal Render Pipeline/Lit"||verifiedImages==null||maxMaterials<1||maxMaterials>4096||maxDecodedBytes<1||maxDecodedBytes>268435456)throw new ArgumentException("URP17.6 explicit bounded inputs");
            shader=urp176Lit;images=new Dictionary<int,VerifiedImageSlice>(verifiedImages);manifestHash=MapStream.Hash(manifest);document=D(MapJson.Parse(manifest,16777216,128,1000000));refs=D(document["references"]);this.maxMaterials=maxMaterials;maxDecoded=maxDecodedBytes;lane=System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        void Lane(){if(System.Threading.Thread.CurrentThread.ManagedThreadId!=lane)throw new InvalidOperationException("Unity main control lane required");if(closed)throw new ObjectDisposedException("UrpSourceAppearance");}
        public Material Resolve(PrimitiveBinding binding)
        {
            Lane();if(binding==null||binding.ManifestHash!=manifestHash)throw new InvalidDataException("appearance manifest binding");
            if(document.ContainsKey("instances"))foreach(object row in (object[])document["instances"]){var instance=D(row);if((string)instance["id"]==binding.Id&&D(instance["attributes"]).ContainsKey("COLOR_0"))throw new InvalidDataException("source vertex color requires explicit conversion; pilot remains quarantined");}
            Entry e;if(entries.TryGetValue(binding.MaterialIndex,out e))return e.Material;
            if(entries.Count>=maxMaterials)throw new InvalidDataException("material budget");e=new Entry();
            try {
                var materials=(object[])refs["materials"];if(binding.MaterialIndex<0||binding.MaterialIndex>=materials.Length)throw new InvalidDataException("explicit source material required");
                var m=D(materials[binding.MaterialIndex]);Keys(m,"name","pbrMetallicRoughness","alphaMode","doubleSided");if(m.ContainsKey("alphaMode")&&(string)m["alphaMode"]!="OPAQUE"||m.ContainsKey("doubleSided")&&(bool)m["doubleSided"])throw new InvalidDataException("opaque single sided required");var p=m.ContainsKey("pbrMetallicRoughness")?D(m["pbrMetallicRoughness"]):new Dictionary<string,object>();Keys(p,"baseColorFactor","baseColorTexture","metallicFactor","roughnessFactor");
                e.Color=Color.white;e.Metal=p.ContainsKey("metallicFactor")?F(p["metallicFactor"]):1;e.Smooth=1-(p.ContainsKey("roughnessFactor")?F(p["roughnessFactor"]):1);
                if(p.ContainsKey("baseColorFactor")){var c=(object[])p["baseColorFactor"];if(c.Length!=4||F(c[3])!=1)throw new InvalidDataException("opaque color required");e.Color=new Color(F(c[0]),F(c[1]),F(c[2]),1);}
                if(p.ContainsKey("baseColorTexture")) {
                    var t=D(p["baseColorTexture"]);Keys(t,"index","texCoord");if(t.ContainsKey("texCoord")&&I(t["texCoord"])!=0)throw new InvalidDataException("UV set unsupported");
                    var td=D(((object[])refs["textures"])[I(t["index"])]);Keys(td,"source","sampler");var sampler=D(((object[])refs["samplers"])[I(td["sampler"])]);Keys(sampler,"magFilter","minFilter","wrapS","wrapT");
                    e.Sampler=new SourceSamplerR4(I(sampler["magFilter"]),I(sampler["minFilter"]),sampler.ContainsKey("wrapS")?I(sampler["wrapS"]):10497,sampler.ContainsKey("wrapT")?I(sampler["wrapT"]):10497);
                    int image=I(td["source"]);var im=D(((object[])refs["images"])[image]);Keys(im,"uri","mimeType","bufferView");VerifiedImageSlice receipt;if(!images.TryGetValue(image,out receipt))throw new InvalidDataException("missing verified source image receipt");
                    if(im.ContainsKey("uri")){string uri=(string)im["uri"];if(uri.Contains(":")||uri.Contains("..")||Path.IsPathRooted(uri))throw new InvalidDataException("external image URI");if(document.ContainsKey("source")){string sourcePath=(string)D(document["source"])["path"];string expected=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath),uri.Replace('/',Path.DirectorySeparatorChar)));if(receipt.SourcePath!=expected||receipt.Offset!=0||receipt.Length!=receipt.FileLength)throw new InvalidDataException("source image path receipt mismatch");}}else {if(!im.ContainsKey("bufferView")||!im.ContainsKey("mimeType")||((string)im["mimeType"]!="image/png"&&(string)im["mimeType"]!="image/jpeg")||!document.ContainsKey("source"))throw new InvalidDataException("embedded PNG source mapping required");var src=D(document["source"]);var view=D(((object[])refs["bufferViews"])[I(im["bufferView"])]);long offset=((MapJson.Number)src["binOffset"]).AsInteger()+(view.ContainsKey("byteOffset")?((MapJson.Number)view["byteOffset"]).AsInteger():0);if(receipt.SourcePath!=(string)src["path"]||receipt.FileSha256!=((string)src["sha256"]).ToLowerInvariant()||receipt.FileLength!=((MapJson.Number)src["length"]).AsInteger()||I(view["buffer"])!=0||receipt.Offset!=offset||receipt.Length!=I(view["byteLength"]))throw new InvalidDataException("GLB image slice mismatch");}
                    byte[] png=receipt.Read(16777216);int w,h;bool jpeg=png.Length>=2&&png[0]==255&&png[1]==216;if(im.ContainsKey("mimeType")&&(string)im["mimeType"]!=(jpeg?"image/jpeg":"image/png"))throw new InvalidDataException("image MIME mismatch");if(jpeg)BoundedJpeg.Inspect(png,4096,out w,out h);else BoundedPng.Inspect(png,4096,out w,out h);long bytes=e.Sampler.DecodedBytes(w,h);e.Bytes=bytes;if(bytes>maxDecoded-decoded)throw new InvalidDataException("decoded dependency budget");
                    e.Texture=new Texture2D(2,2,TextureFormat.RGBA32,e.Sampler.Mipmaps,false);if(!ImageConversion.LoadImage(e.Texture,png,false)||e.Texture.width!=w||e.Texture.height!=h)throw new InvalidDataException("PNG decode shape");
                    e.Texture.wrapModeU=Wrap(e.Sampler.WrapS);e.Texture.wrapModeV=Wrap(e.Sampler.WrapT);e.Texture.filterMode=e.Sampler.Point?FilterMode.Point:e.Sampler.Trilinear?FilterMode.Trilinear:FilterMode.Bilinear;e.Texture.anisoLevel=0;e.Width=w;e.Height=h;e.Pixels=MapStream.Hash(e.Texture.GetRawTextureData());
                }
                e.Material=new Material(shader);e.Material.SetColor("_BaseColor",e.Color);e.Material.SetFloat("_Metallic",e.Metal);e.Material.SetFloat("_Smoothness",e.Smooth);e.Material.SetTexture("_BaseMap",e.Texture);e.Material.SetTextureScale("_BaseMap",Vector2.one);e.Material.SetTextureOffset("_BaseMap",Vector2.zero);
                e.Material.SetFloat("_SrcBlend",1);e.Material.SetFloat("_DstBlend",0);e.Material.SetFloat("_SrcBlendAlpha",1);e.Material.SetFloat("_DstBlendAlpha",0);
                e.Queue=e.Material.renderQueue;e.Integrity=new MaterialIntegrityR3(e.Material);entries.Add(binding.MaterialIndex,e);if(e.Texture)decoded+=e.Bytes;return e.Material;
            }catch{Release(e.Material);Release(e.Texture);throw;}
        }
        public bool Matches(PrimitiveBinding binding,Material original,Material clone)
        {
            Lane();Entry e;if(binding.ManifestHash!=manifestHash||!entries.TryGetValue(binding.MaterialIndex,out e)||original!=e.Material||!clone)return false;
            if(!e.Integrity.Matches(original,true)||!e.Integrity.Matches(clone,false))return false;
            foreach(var m in new[]{original,clone})if(!m||m.shader!=shader||m.GetColor("_BaseColor")!=e.Color||m.GetFloat("_Metallic")!=e.Metal||m.GetFloat("_Smoothness")!=e.Smooth||m.GetTexture("_BaseMap")!=e.Texture||m.GetTextureScale("_BaseMap")!=Vector2.one||m.GetTextureOffset("_BaseMap")!=Vector2.zero||m.shaderKeywords.Length!=0||m.renderQueue!=e.Queue||m.GetFloat("_Surface")!=0||m.GetFloat("_Cull")!=2||m.GetFloat("_AlphaClip")!=0||m.GetFloat("_ZWrite")!=1||m.GetFloat("_WorkflowMode")!=1)return false;
            return !e.Texture||(e.Texture.wrapModeU==Wrap(e.Sampler.WrapS)&&e.Texture.wrapModeV==Wrap(e.Sampler.WrapT)&&e.Texture.filterMode==(e.Sampler.Point?FilterMode.Point:e.Sampler.Trilinear?FilterMode.Trilinear:FilterMode.Bilinear)&&e.Texture.mipmapCount==MipCount(e.Width,e.Height,e.Sampler.Mipmaps)&&e.Texture.anisoLevel==0&&e.Texture.width==e.Width&&e.Texture.height==e.Height&&MapStream.Hash(e.Texture.GetRawTextureData())==e.Pixels);
        }
        static int MipCount(int w,int h,bool mip){int n=1;while(mip&&(w>1||h>1)){w=Math.Max(1,w/2);h=Math.Max(1,h/2);n++;}return n;}
        static TextureWrapMode Wrap(int v){return v==10497?TextureWrapMode.Repeat:v==33071?TextureWrapMode.Clamp:TextureWrapMode.Mirror;}
        static void Release(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        public void Dispose(){if(closed)return;Lane();closed=true;foreach(var e in entries.Values){Release(e.Material);Release(e.Texture);}entries.Clear();decoded=0;}
    }
}
#endif
