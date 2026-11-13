using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ashfall.Integration.Playable.World
{
    /// <summary>Source R6 material selectors; never changes positions, indices or collision.</summary>
    public sealed class R24ArchitectureMaterials
    {
        [Serializable] public sealed class SourceMaterial
        { public string key=""; public float[] tint=null; public float roughness=1; public bool vertexSrgb=false; }
        [Serializable] public sealed class Patch
        { public int chunk=0,vertices=0; public int[] triangles=null; public string triangleKinds="";public Fragment[] fragments=null; [NonSerialized]public byte[] decoded; [NonSerialized]public Dictionary<long,byte> lookup;[NonSerialized]public Dictionary<long,Fragment> fragmentLookup; }
        [Serializable] public sealed class Fragment
        {public int triangle=0;public float[] barycentric=null;public string triangleKinds="";[NonSerialized]public byte[] decoded;}
        [Serializable] public sealed class Junction
        { public float[] center=null,scale=null,min=null,max=null;public float height=0; }
        [Serializable] public sealed class Trailer
        { public float[] origin=null,long_axis=null,across_axis=null,dimensions=null; }
        [Serializable] public sealed class Wheel
        { public float[] center=null,axle=null,up=null,across=null;public float radius=0; }
        [Serializable] public sealed class Family
        {public Junction[] junction_projections=null;public Trailer[] trailers=null;public Wheel[] wheels=null;}
        [Serializable] public sealed class Data
        {public int version=0,chunks=0;public string mapId="",geometrySha256="";public SourceMaterial[] materials=null;public Patch[] patches=null;public string[] textures=null;public Family family=null;}
        readonly Data data;
        readonly Dictionary<int,Patch> patches=new Dictionary<int,Patch>();
        public string[] Textures {get{return data.textures;}}
        public void VerifyGeometry(Stream stream)
        {
            long position=stream.Position;stream.Position=0;byte[] hash;
            using(var algorithm=System.Security.Cryptography.SHA256.Create())hash=algorithm.ComputeHash(stream);
            stream.Position=position;
            if(BitConverter.ToString(hash).Replace("-","").ToLowerInvariant()!=data.geometrySha256)throw new InvalidDataException("Architecture source geometry changed");
        }
        public R24ArchitectureMaterials(string json,string mapId,int chunks)
        {
            data=JsonUtility.FromJson<Data>(json);
            if(data==null||data.version!=1||data.mapId!=mapId||data.chunks!=chunks||data.materials==null||data.patches==null||data.textures==null)throw new InvalidDataException("Invalid R6 architecture binding");
            foreach(var p in data.patches)
            {
                if(p==null||p.chunk<0||p.chunk>=chunks||p.vertices<1||p.vertices>200000||p.triangles==null||patches.ContainsKey(p.chunk))throw new InvalidDataException("Invalid architecture chunk");
                p.decoded=Convert.FromBase64String(p.triangleKinds);if(p.decoded.Length!=p.triangles.Length)throw new InvalidDataException("Architecture selector count changed");
                var seen=new HashSet<int>();for(int i=0;i<p.triangles.Length;i++)
                {int k=p.decoded[i];if(p.triangles[i]<0||!seen.Add(p.triangles[i])||!((k>=1&&k<=7)||(k>=32&&k<40)||(k>=48&&k<64)))throw new InvalidDataException("Invalid architecture face selector");}
                if(p.fragments!=null)foreach(var f in p.fragments)
                {
                    if(f==null||f.triangle<0||!seen.Add(f.triangle)||f.barycentric==null)throw new InvalidDataException("Invalid source fragment owner");
                    f.decoded=Convert.FromBase64String(f.triangleKinds);if(f.decoded.Length<1||f.decoded.Length>256||f.barycentric.Length!=f.decoded.Length*9)throw new InvalidDataException("Invalid source fragment layout");
                    foreach(byte kind in f.decoded)if(kind!=255&&!((kind>=1&&kind<=7)||(kind>=32&&kind<40)||(kind>=48&&kind<64)))throw new InvalidDataException("Invalid source fragment kind");
                    for(int i=0;i<f.barycentric.Length;i+=3){float sum=0;for(int j=0;j<3;j++){float w=f.barycentric[i+j];if(Single.IsNaN(w)||Single.IsInfinity(w)||w<0||w>1)throw new InvalidDataException("Source fragment outside original triangle");sum+=w;}if(Math.Abs(sum-1)>.00001f)throw new InvalidDataException("Source fragment interpolation");}
                }
                patches.Add(p.chunk,p);
            }
            foreach(var name in data.textures)if(String.IsNullOrEmpty(name)||name.IndexOfAny(new[]{'/','\\',':'})>=0||name.Contains(".."))throw new InvalidDataException("Invalid architecture texture");
        }
        public bool UsesMaterial(string key){foreach(var m in data.materials)if(m.key==key)return true;return false;}
        static long Key(int a,int b,int c){return ((long)a<<36)|((long)b<<18)|(uint)c;}
        public byte[] Kinds(int chunk,int[] original,int[] part)
        {
            Patch patch;if(!patches.TryGetValue(chunk,out patch))return null;
            if(patch.lookup==null)
            {
                patch.lookup=new Dictionary<long,byte>();for(int i=0;i<patch.triangles.Length;i++){int at=patch.triangles[i]*3;if(at<0||at+2>=original.Length)throw new InvalidDataException("Architecture face range changed");patch.lookup[Key(original[at],original[at+1],original[at+2])]=patch.decoded[i];}
            }
            var result=new byte[part.Length/3];for(int i=0;i<result.Length;i++){byte value;result[i]=patch.lookup.TryGetValue(Key(part[i*3],part[i*3+1],part[i*3+2]),out value)?value:(byte)255;}return result;
        }
        public Fragment[] RenderFragments(int chunk,int[] original,int[] part)
        {
            Patch patch;if(!patches.TryGetValue(chunk,out patch)||patch.fragments==null||patch.fragments.Length==0)return null;
            if(patch.fragmentLookup==null){patch.fragmentLookup=new Dictionary<long,Fragment>();foreach(var f in patch.fragments){int at=f.triangle*3;if(at<0||at+2>=original.Length)throw new InvalidDataException("Source fragment original face changed");patch.fragmentLookup.Add(Key(original[at],original[at+1],original[at+2]),f);}}
            var result=new Fragment[part.Length/3];bool any=false;for(int i=0;i<result.Length;i++){Fragment f;if(patch.fragmentLookup.TryGetValue(Key(part[i*3],part[i*3+1],part[i*3+2]),out f)){result[i]=f;any=true;}}return any?result:null;
        }
        public static void ExpandRender(byte[] kinds,ref Vector3[] positions,ref Vector3[] normals,ref Vector2[] uv,ref Color[] colors,ref int[] indices,Fragment[] fragments=null)
        {
            if(kinds==null&&fragments==null)return;if(kinds!=null&&kinds.Length*3!=indices.Length||fragments!=null&&fragments.Length*3!=indices.Length)throw new InvalidDataException("Architecture face membership changed");
            var p=new List<Vector3>(positions);var n=new List<Vector3>(normals);var u=new List<Vector2>(uv);var c=new List<Color>(colors);var ix=new List<int>(indices.Length);
            for(int i=0;i<c.Count;i++){var color=c[i];color.a=1;c[i]=color;}
            for(int face=0;face<indices.Length/3;face++)
            {
                Fragment f=fragments==null?null:fragments[face];byte kind=kinds==null?(byte)255:kinds[face];
                if(f!=null)
                {
                    int a=indices[face*3],b=indices[face*3+1],d=indices[face*3+2];
                    for(int t=0;t<f.decoded.Length;t++)for(int corner=0;corner<3;corner++){int at=t*9+corner*3;float x=f.barycentric[at],y=f.barycentric[at+1],z=f.barycentric[at+2];ix.Add(p.Count);p.Add(positions[a]*x+positions[b]*y+positions[d]*z);n.Add((normals[a]*x+normals[b]*y+normals[d]*z).normalized);u.Add(uv[a]*x+uv[b]*y+uv[d]*z);var color=colors[a]*x+colors[b]*y+colors[d]*z;color.a=f.decoded[t]/255f;c.Add(color);}
                }
                else for(int corner=0;corner<3;corner++){int index=indices[face*3+corner];if(kind==255){ix.Add(index);continue;}ix.Add(p.Count);p.Add(positions[index]);n.Add(normals[index]);u.Add(uv[index]);var color=colors[index];color.a=kind/255f;c.Add(color);}
            }
            positions=p.ToArray();normals=n.ToArray();uv=u.ToArray();colors=c.ToArray();indices=ix.ToArray();
        }
        static Vector4 V(float[] a,float w=0){if(a==null||a.Length!=3)throw new InvalidDataException("Invalid architecture vector");return new Vector4(a[0],a[1],a[2],w);}
        public void Configure(Material material,string key,Dictionary<string,Texture2D> textures)
        {
            SourceMaterial source=null;foreach(var row in data.materials)if(row.key==key)source=row;
            if(source==null)throw new InvalidDataException("Missing source architecture material");
            if(source.tint==null||source.tint.Length!=4)throw new InvalidDataException("Invalid source architecture tint");
            material.SetColor("_SourceTint",new Color(source.tint[0],source.tint[1],source.tint[2],source.tint[3]));material.SetFloat("_VertexSrgb",source.vertexSrgb?1:0);material.SetFloat("_Roughness",source.roughness);
            string[] files={"brick.png","concrete.png","corrugated.png","planks.png","statue-stone.png","freight.png"};string[] props={"_Brick","_Concrete","_Metal","_Wood","_Stone","_Freight"};
            for(int i=0;i<files.Length;i++)material.SetTexture(props[i],textures["architecture/"+files[i]]);
            var zones=new Vector4[16];var boxes=new Vector4[16];var heights=new float[16];var family=data.family;
            var junctions=family==null||family.junction_projections==null?new Junction[0]:family.junction_projections;
            if(junctions.Length>16)throw new InvalidDataException("Too many source junctions");
            for(int i=0;i<junctions.Length;i++){var p=junctions[i];zones[i]=new Vector4(p.center[0],p.center[1],p.scale[0],p.scale[1]);boxes[i]=new Vector4(p.min[0]-.035f,p.max[0]+.035f,p.min[1]-.035f,p.max[1]+.035f);heights[i]=p.height;}
            material.SetInt("_JunctionCount",junctions.Length);material.SetVectorArray("_JunctionZones",zones);material.SetVectorArray("_JunctionBoxes",boxes);material.SetFloatArray("_JunctionHeights",heights);
            var trailers=family==null||family.trailers==null?new Trailer[0]:family.trailers;
            if(trailers.Length>8)throw new InvalidDataException("Too many source trailers");
            var origins=new Vector4[8];var longs=new Vector4[8];var across=new Vector4[8];var sizes=new Vector4[8];
            for(int i=0;i<trailers.Length;i++){var p=trailers[i];origins[i]=V(p.origin);longs[i]=V(p.long_axis);across[i]=V(p.across_axis);sizes[i]=V(p.dimensions);}
            material.SetInt("_TrailerCount",trailers.Length);material.SetVectorArray("_TrailerOrigins",origins);material.SetVectorArray("_TrailerLongs",longs);material.SetVectorArray("_TrailerAcross",across);material.SetVectorArray("_TrailerSizes",sizes);
            var wheels=family==null||family.wheels==null?new Wheel[0]:family.wheels;
            if(wheels.Length>16)throw new InvalidDataException("Too many source wheels");
            var centers=new Vector4[16];var axles=new Vector4[16];var ups=new Vector4[16];var sides=new Vector4[16];
            for(int i=0;i<wheels.Length;i++){var p=wheels[i];centers[i]=V(p.center,p.radius);axles[i]=V(p.axle);ups[i]=V(p.up);sides[i]=V(p.across);}
            material.SetInt("_WheelCount",wheels.Length);material.SetVectorArray("_WheelCenters",centers);material.SetVectorArray("_WheelAxles",axles);material.SetVectorArray("_WheelUps",ups);material.SetVectorArray("_WheelSides",sides);
        }
    }
}
