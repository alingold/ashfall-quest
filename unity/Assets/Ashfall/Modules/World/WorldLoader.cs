using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Ashfall.Core;

namespace Ashfall.Modules.World
{
    [DataContract] public sealed class MapDocument
    {
        [DataMember] public int schema;
        [DataMember] public string id, version, sourcePath, sourceSha256, sourceRevision, approval;
        [DataMember] public bool indoor, capsules;
        [DataMember] public Surface[] surfaces;
        [DataMember] public Spawn[] spawns;
        [DataMember] public Pad[] pads;
        [DataMember] public SemanticRecord[] semantics;
        [DataMember] public string semanticsVersion;
    }
    [DataContract] public sealed class Surface
    {
        [DataMember] public string id, semanticOwner, colliderRole, materialPath, texturePath;
        [DataMember] public float[] matrix, vertices, normals, uv, color;
        [DataMember] public float roughness, metallic;
        [DataMember] public int[] indices;
        [DataMember] public bool doubleSided;
    }
    [DataContract] public sealed class Spawn
    {
        [DataMember] public string id, mode, team;
        [DataMember] public float[] position, forward;
        [DataMember] public bool nativeMarker;
    }
    [DataContract] public sealed class Pad
    {
        [DataMember] public string id, semanticOwner;
        [DataMember] public float[] position, normal;
        [DataMember] public float radius, speed;
    }
    public sealed class LoadedSurface
    {
        public string Id, SemanticOwner, ColliderRole, MaterialPath, TexturePath;
        public Float3[] Vertices, Normals;
        public float[] Uv, Color;
        public int[] Indices;
        public float Roughness, Metallic;
        public bool DoubleSided;
    }
    public sealed class LoadedSpawn { public string Id, Mode, Team; public bool NativeMarker; public Float3 Position, Forward; }
    public sealed class LoadedPad { public string Id, SemanticOwner; public Float3 Position, Normal; public float Radius, Speed; }
    public sealed class LoadedMap
    {
        public string Id; public bool Indoor, Capsules;
        public LoadedSurface[] Surfaces; public LoadedSpawn[] Spawns; public LoadedPad[] Pads;
    }
    public static class WorldLoader
    {
        public static string Hash(byte[] bytes) { using(var s=SHA256.Create()) return BitConverter.ToString(s.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void Require(bool ok, string message) { if(!ok) throw new InvalidDataException(message); }
        static bool Finite(float x) { return !Single.IsNaN(x) && !Single.IsInfinity(x); }
        static void Numbers(float[] a, int count) { Require(a!=null && a.Length==count && a.All(Finite), "Invalid numeric array"); }
        static Float3 Normalize(double x,double y,double z)
        {
            double n=Math.Sqrt(x*x+y*y+z*z);
            Require(n>0 && !Double.IsNaN(n) && !Double.IsInfinity(n),"Zero/nonfinite direction");
            return new Float3((float)(x/n),(float)(y/n),(float)(z/n));
        }
        public static Float3 Unit(Float3 a) { return Normalize(a.X,a.Y,a.Z); }
        static bool AbsoluteSourcePath(string path)
        {
            // This schema uses canonical local drive paths, never current-drive-relative names.
            if(String.IsNullOrEmpty(path)||path.Length<4||!Char.IsLetter(path[0])||path[1]!=':'||(path[2]!='/'&&path[2]!='\\'))return false;
            try { return String.Equals(Path.GetFullPath(path).Replace('/','\\'),path.Replace('/','\\'),StringComparison.OrdinalIgnoreCase); }
            catch(ArgumentException){return false;} catch(NotSupportedException){return false;} catch(PathTooLongException){return false;}
        }
        static Float3 Reflect(float[] a) { Numbers(a,3); return new Float3(a[0],a[1],-a[2]); }
        // Input column-major affine local-to-map matrix. Baking preserves shear/nonuniform scale.
        static Float3 Point(float[] m, float[] a, int i) { var p=new Float3((float)((double)m[0]*a[i]+(double)m[4]*a[i+1]+(double)m[8]*a[i+2]+m[12]),(float)((double)m[1]*a[i]+(double)m[5]*a[i+1]+(double)m[9]*a[i+2]+m[13]),(float)(-((double)m[2]*a[i]+(double)m[6]*a[i+1]+(double)m[10]*a[i+2]+m[14]))); Require(p.IsFinite,"Transform overflow"); return p; }
        public static LoadedMap Load(byte[] json, string expectedSha256, bool pilot, IContentApproval approval)
        {
            Require(json!=null && json.Length<=16*1024*1024,"Map payload limit");
            Require(String.Equals(Hash(json),expectedSha256,StringComparison.OrdinalIgnoreCase),"Content hash mismatch");
            MapDocument d;
            d=WorldBoundary.Document(json);
            Require(d!=null && d.schema==1 && !String.IsNullOrEmpty(d.id) && !String.IsNullOrEmpty(d.version),"Map schema/identity");
            Require(AbsoluteSourcePath(d.sourcePath) && d.sourceSha256!=null && d.sourceSha256.Length==64 && d.sourceSha256.All(Uri.IsHexDigit) && !String.IsNullOrEmpty(d.sourceRevision),"Source provenance missing");
            Require(pilot || (d.approval=="accepted" && approval!=null && approval.IsApproved(d.id,d.version,expectedSha256)),"Unapproved content");
            Require(!(d.indoor && d.capsules),"Indoor maps prohibit capsule platforms");
            Require(d.surfaces!=null && d.surfaces.Length>0 && d.surfaces.Length<=4096 && d.spawns!=null && d.pads!=null,"Missing map collections");
            var ids=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            Action<string> unique=id=>Require(!String.IsNullOrEmpty(id)&&ids.Add(id),"Duplicate/empty source ID");
            var result=new LoadedMap {Id=d.id,Indoor=d.indoor,Capsules=d.capsules};
            result.Surfaces=d.surfaces.Select(s=>{
                Require(s!=null,"Null surface"); unique(s.id); Require(!String.IsNullOrEmpty(s.semanticOwner),"Missing semantic owner");
                Require(s.colliderRole=="structure" || s.colliderRole=="decoration" || s.colliderRole=="trigger","Unknown collider role");
                Numbers(s.matrix,16); Require(s.matrix[3]==0 && s.matrix[7]==0 && s.matrix[11]==0 && s.matrix[15]==1,"Non-affine matrix");
                var m=s.matrix;
                double c00=(double)m[5]*m[10]-(double)m[9]*m[6],c01=(double)m[9]*m[2]-(double)m[1]*m[10],c02=(double)m[1]*m[6]-(double)m[5]*m[2];
                double c10=(double)m[8]*m[6]-(double)m[4]*m[10],c11=(double)m[0]*m[10]-(double)m[8]*m[2],c12=(double)m[4]*m[2]-(double)m[0]*m[6];
                double c20=(double)m[4]*m[9]-(double)m[8]*m[5],c21=(double)m[8]*m[1]-(double)m[0]*m[9],c22=(double)m[0]*m[5]-(double)m[4]*m[1];
                double det=m[0]*c00+m[4]*c01+m[8]*c02;
                double volume=Math.Sqrt(((double)m[0]*m[0]+(double)m[1]*m[1]+(double)m[2]*m[2])*((double)m[4]*m[4]+(double)m[5]*m[5]+(double)m[6]*m[6])*((double)m[8]*m[8]+(double)m[9]*m[9]+(double)m[10]*m[10]));
                Require(volume>0 && Math.Abs(det)/volume>1e-12 && !Double.IsInfinity(det),"Singular/ill-conditioned transform");
                Require(s.vertices!=null && s.vertices.Length>=9 && s.vertices.Length%3==0 && s.vertices.Length<=600000 && s.vertices.All(Finite),"Invalid vertices");
                int n=s.vertices.Length/3; Numbers(s.normals,n*3); Require(s.uv!=null && (s.uv.Length==0 || s.uv.Length==n*2) && s.uv.All(Finite),"Invalid UV");
                Require(s.indices!=null && s.indices.Length>0 && s.indices.Length%3==0 && s.indices.Length<=600000 && s.indices.All(x=>x>=0&&x<n),"Invalid indices");
                Numbers(s.color,4); Require(!String.IsNullOrWhiteSpace(s.materialPath)&&s.color.All(x=>x>=0&&x<=1)&&Finite(s.roughness)&&s.roughness>=0&&s.roughness<=1&&Finite(s.metallic)&&s.metallic>=0&&s.metallic<=1,"Invalid material");
                var v=new Float3[n]; var normals=new Float3[n];
                for(int i=0;i<n;i++) {
                    v[i]=Point(m,s.vertices,i*3); double x=s.normals[3*i],y=s.normals[3*i+1],z=s.normals[3*i+2];
                    // Cofactor matrix / determinant = inverse transpose, followed by Z reflection.
                    normals[i]=Normalize((c00*x+c01*y+c02*z)/det,(c10*x+c11*y+c12*z)/det,-(c20*x+c21*y+c22*z)/det);
                }
                var idx=(int[])s.indices.Clone();
                // Reflection reverses winding once; a mirrored source basis reverses it back.
                if(det>0) for(int i=0;i<idx.Length;i+=3) {int t=idx[i+1];idx[i+1]=idx[i+2];idx[i+2]=t;}
                return new LoadedSurface {Id=s.id,SemanticOwner=s.semanticOwner,ColliderRole=s.colliderRole,Vertices=v,Normals=normals,Indices=idx,Uv=(float[])s.uv.Clone(),Color=(float[])s.color.Clone(),MaterialPath=s.materialPath,TexturePath=s.texturePath,Roughness=s.roughness,Metallic=s.metallic,DoubleSided=s.doubleSided};
            }).ToArray();
            result.Spawns=d.spawns.Select(s=>{ Require(s!=null,"Null spawn"); unique(s.id); Require(!String.IsNullOrEmpty(s.mode)&&s.team!=null,"Spawn policy missing"); return new LoadedSpawn {Id=s.id,Mode=s.mode,Team=s.team,NativeMarker=s.nativeMarker,Position=Reflect(s.position),Forward=Unit(Reflect(s.forward))}; }).ToArray();
            result.Pads=d.pads.Select(p=>{Require(p!=null,"Null pad");unique(p.id); Require(!String.IsNullOrEmpty(p.semanticOwner)&&Finite(p.radius)&&p.radius>0&&Finite(p.speed)&&p.speed>0,"Invalid pad"); return new LoadedPad {Id=p.id,SemanticOwner=p.semanticOwner,Position=Reflect(p.position),Normal=Unit(Reflect(p.normal)),Radius=p.radius,Speed=p.speed};}).ToArray();
            return result;
        }
        public static LoadedSpawn[] SafeSpawns(LoadedMap map, string mode, string team, float height, ISpawnQuery query)
        {
            if(map==null||map.Spawns==null||query==null || !Finite(height)||height<=0) throw new ArgumentException("Spawn map/physics/height required");
            return map.Spawns.Where(s=>s.Mode==mode&&(s.Team==team||s.Team=="shared")).Select(s=>{
                Float3 floor,normal;
                if(!query.TryFloor(s.Position,s.NativeMarker?1.5f:2,s.NativeMarker?8:5,out floor,out normal)||!floor.IsFinite||!normal.IsFinite||(normal.X==0&&normal.Y==0&&normal.Z==0)||Unit(normal).Y<=.7f) return null;
                if(s.NativeMarker && (floor.Y<s.Position.Y-6 || floor.Y>s.Position.Y+1))return null;
                var feet=new Float3(floor.X,(float)((double)floor.Y+.12),floor.Z);
                if(!feet.IsFinite||!query.HasStandingRoom(feet,.3f,height)||query.IsHazard(feet))return null;
                return new LoadedSpawn {Id=s.Id,Mode=s.Mode,Team=s.Team,NativeMarker=s.NativeMarker,Forward=s.Forward,Position=feet};
            }).Where(s=>s!=null).ToArray();
        }
    }
}
