using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
namespace Ashfall.Integration.Playable.World.SourceArt
{
 [Serializable]public sealed class R24SourceArtPartData {public string kind="";public int[] triangles=null;public float[] projected=null,normals=null;}
 [Serializable]public sealed class R24SourceArtChunk {public int chunk=0,vertices=0,indices=0;public string vertexSha256="",indexSha256="";public R24SourceArtPartData[] parts=null;}
 [Serializable]public sealed class R24SourceArtData {public int version=0,chunks=0;public string mapId="",geometrySha256="";public string[] textures=null;public R24SourceArtChunk[] patches=null;}
 public sealed class R24SourceArtPart
 {
  public readonly string Kind;public readonly int[] VertexIndices;public readonly float[] ProjectedUV3,SourceNormals;
  internal R24SourceArtPart(string kind,int[] indices,float[] projected,float[] normals){Kind=kind;VertexIndices=indices;ProjectedUV3=projected;SourceNormals=normals;}
 }
 // Rendering subsets only. Original/collider indices and encoded arrays remain owned by the loader.
 public sealed class R24SourceArtPlan
 {
  readonly R24SourceArtData data;readonly Dictionary<int,R24SourceArtChunk> chunks=new Dictionary<int,R24SourceArtChunk>();
  sealed class Face {internal string Kind;internal float[] UV,Normals;}
  sealed class Group{internal readonly List<int> Indices=new List<int>();internal readonly List<float> UV=new List<float>(),Normals=new List<float>();}
  public bool UsesJunction {get;private set;}public bool UsesClearGlass {get;private set;}public string MapId {get{return data.mapId;}}public int PatchCount {get{return chunks.Count;}}public string[] Textures {get{return (string[])data.textures.Clone();}}
  public static bool KnownKind(string kind){return kind=="container_r10"||kind=="van_r10"||kind=="tire_r10"||kind=="container_end_panels"||kind=="wood_panels"||kind=="van"||kind=="truck"||kind=="tire"||kind=="wood"||kind=="sign"||kind=="banner"||kind=="clear_glass"||kind=="junction";}
  public R24SourceArtPlan(R24SourceArtData source,string mapId,int count)
  {
   if(source==null||source.version!=1||source.mapId!=mapId||source.chunks!=count||source.textures==null||source.patches==null||source.patches.Length>count)throw new InvalidDataException("Source art map binding");data=source;
   foreach(string name in source.textures)if(name!="freight.png"&&name!="northline-portrait.png")throw new InvalidDataException("Source art image binding");
   foreach(var patch in source.patches)
   {
    if(patch==null||patch.chunk<0||patch.chunk>=count||patch.vertices<1||patch.vertices>200000||patch.indices<3||patch.indices%3!=0||patch.parts==null||chunks.ContainsKey(patch.chunk))throw new InvalidDataException("Source art chunk");
    var seen=new HashSet<int>();foreach(var part in patch.parts){if(part==null||!KnownKind(part.kind)||part.triangles==null||part.projected==null||part.projected.Length!=part.triangles.Length*9||(part.normals!=null&&part.normals.Length!=part.projected.Length))throw new InvalidDataException("Source art projected corner count");if(part.kind=="junction")UsesJunction=true;if(part.kind=="clear_glass")UsesClearGlass=true;foreach(int tri in part.triangles)if(tri<0||tri>=patch.indices/3||!seen.Add(tri))throw new InvalidDataException("Source art triangle ownership");foreach(float v in part.projected)if(Single.IsNaN(v)||Single.IsInfinity(v))throw new InvalidDataException("Source art UV");if(part.normals!=null)foreach(float v in part.normals)if(Single.IsNaN(v)||Single.IsInfinity(v))throw new InvalidDataException("Source art normal");}chunks.Add(patch.chunk,patch);
   }
  }
  public void VerifyGeometry(Stream stream){if(HashSpan(stream,0,stream.Length)!=data.geometrySha256)throw new InvalidDataException("Source art original geometry changed");}
  public bool VerifyChunk(int chunk,Stream stream,long vertexStart,long indexStart,long end,int vertices,int indices)
  {R24SourceArtChunk p;if(!chunks.TryGetValue(chunk,out p))return false;if(vertices!=p.vertices||indices!=p.indices||indexStart-vertexStart!=(long)vertices*48||end-indexStart!=(long)indices*4||HashSpan(stream,vertexStart,indexStart-vertexStart)!=p.vertexSha256||HashSpan(stream,indexStart,end-indexStart)!=p.indexSha256)throw new InvalidDataException("Source art original chunk changed");return true;}
  static long Key(int a,int b,int c){return ((long)a<<36)|((long)b<<18)|(uint)c;}
  // Call before compacting vertex indices. Pass the complete original chunk indices,
  // plus any existing tower/destructible render subset. Attach returned details to THAT owner.
  public bool Select(int chunk,int[] originalIndices,int[] renderSubset,out int[] keepIndices,out R24SourceArtPart[] details)
  {
   keepIndices=renderSubset;details=new R24SourceArtPart[0];R24SourceArtChunk patch;if(!chunks.TryGetValue(chunk,out patch))return false;
   if(originalIndices==null||originalIndices.Length!=patch.indices||renderSubset==null||renderSubset.Length%3!=0)throw new InvalidDataException("Source art render subset layout");
   var wanted=new Dictionary<long,Face>();foreach(var part in patch.parts)for(int i=0;i<part.triangles.Length;i++){int at=part.triangles[i]*3;int a=originalIndices[at],b=originalIndices[at+1],c=originalIndices[at+2];if(a<0||b<0||c<0||a>=patch.vertices||b>=patch.vertices||c>=patch.vertices)throw new InvalidDataException("Source art original vertex range");var uv=new float[9];Array.Copy(part.projected,i*9,uv,0,9);long key=Key(a,b,c);float[] normal=null;if(part.normals!=null){normal=new float[9];Array.Copy(part.normals,i*9,normal,0,9);}Face duplicate;if(wanted.TryGetValue(key,out duplicate)){if(duplicate.Kind!=part.kind)throw new InvalidDataException("Source art duplicate face style");for(int k=0;k<9;k++)if(Math.Abs(uv[k]-duplicate.UV[k])>.0003f)throw new InvalidDataException("Source art duplicate face UV");}else wanted.Add(key,new Face{Kind=part.kind,UV=uv,Normals=normal});}
   var keep=new List<int>();var groups=new Dictionary<string,Group>();for(int i=0;i<renderSubset.Length;i+=3){int a=renderSubset[i],b=renderSubset[i+1],c=renderSubset[i+2];if(a<0||b<0||c<0||a>=patch.vertices||b>=patch.vertices||c>=patch.vertices)throw new InvalidDataException("Source art subset must reference original vertices");Face face;if(!wanted.TryGetValue(Key(a,b,c),out face)){keep.Add(a);keep.Add(b);keep.Add(c);continue;}Group g;if(!groups.TryGetValue(face.Kind,out g)){g=new Group();groups.Add(face.Kind,g);}g.Indices.Add(a);g.Indices.Add(b);g.Indices.Add(c);g.UV.AddRange(face.UV);if(face.Normals!=null)g.Normals.AddRange(face.Normals);}
   keepIndices=keep.ToArray();var result=new List<R24SourceArtPart>();foreach(var pair in groups)result.Add(new R24SourceArtPart(pair.Key,pair.Value.Indices.ToArray(),pair.Value.UV.ToArray(),pair.Value.Normals.Count==0?null:pair.Value.Normals.ToArray()));details=result.ToArray();return details.Length>0;
  }
  static string HashSpan(Stream stream,long start,long length)
  {if(start<0||length<0||start>stream.Length-length)throw new InvalidDataException("Source art byte range");long position=stream.Position;try{stream.Position=start;using(var hash=SHA256.Create()){var buffer=new byte[32768];while(length>0){int n=stream.Read(buffer,0,(int)Math.Min(length,buffer.Length));if(n<=0)throw new EndOfStreamException();hash.TransformBlock(buffer,0,n,buffer,0);length-=n;}hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();}}finally{stream.Position=position;}}
 }
}
