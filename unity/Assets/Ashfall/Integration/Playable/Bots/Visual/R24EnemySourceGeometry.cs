#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using System.Globalization;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Bots
{
 // Original Godot leaf arrays, not reconstructed primitives. Shared by living
 // render buckets and bounded corpses so death cannot swap to different art.
 internal sealed class R24EnemySourceGeometry
 {
  [Serializable]internal sealed class Manifest {public int version=0,maximumCorpses=0,maximumBodies=0;public float lifetime=0;public string anatomySha256=null,geometrySha256=null;public VariantData[] variants=null;}
  [Serializable]internal sealed class VariantData {public int id=0,triangles=0;public PartData[] parts=null;public BodyData[] bodies=null;public JointData[] joints=null;public BoneData[] bones=null;}
  [Serializable]internal sealed class PartData {public int parent=0,material=0,shadows=0;public float end=0,margin=0;public float[] local=null,vertices=null,normals=null,tangents=null,uv=null,uv2=null,colours=null;public int[] indices=null;}
  [Serializable]internal sealed class BodyData {public string name=null;public float[] local=null,size=null;public float mass=0,linearDamp=0,angularDamp=0,friction=0,bounce=0;}
  [Serializable]internal sealed class JointData {public int a=0,b=0;public float[] local=null;public float swing=0,twist=0,bias=0,softness=0;}
  [Serializable]internal sealed class BoneData {public string name=null;public int parent=0,body=0;}
  internal sealed class Part {internal PartData Source;internal Matrix4x4 Local;internal Mesh Mesh;internal string GroupKey;}
  internal sealed class Group {internal int Parent,Material,Shadows;internal float End,Margin;internal Mesh Mesh;internal string Key;internal readonly List<CombineInstance> Pieces=new List<CombineInstance>();}
  internal sealed class Variant {internal VariantData Source;internal readonly List<Part> Parts=new List<Part>();internal readonly List<Group> Groups=new List<Group>();}
  static R24EnemySourceGeometry shared;readonly Variant[] variants=new Variant[4];readonly List<Mesh> owned=new List<Mesh>();int owners;bool disposed;
  internal static R24EnemySourceGeometry Acquire(){if(shared==null){var next=new R24EnemySourceGeometry();try{next.Build();shared=next;}catch{next.Clear();throw;}}shared.owners++;return shared;}
  internal void Release(){if(disposed)return;if(--owners<=0){if(shared==this)shared=null;Clear();}}
  internal Variant ForVariant(int id){if(disposed||id<0||id>3)throw new ArgumentOutOfRangeException("source variant");return variants[id];}
  internal static Vector3 Reflect(Vector3 value){return new Vector3(value.x,value.y,-value.z);}
  internal static Vector3 Vector(float[] value){if(value==null||value.Length!=3)throw new InvalidOperationException("Source vector schema");return Reflect(new Vector3(value[0],value[1],value[2]));}
  internal static Matrix4x4 Matrix(float[] data){if(data==null||data.Length!=16)throw new InvalidOperationException("Source matrix schema");var m=new Matrix4x4();for(int row=0;row<4;row++)for(int column=0;column<4;column++){float value=data[row*4+column];if(!Finite(value))throw new InvalidOperationException("Finite source matrix");m[row,column]=value*(row==2?-1:1)*(column==2?-1:1);}return m;}
  static bool Finite(float value){return !Single.IsNaN(value)&&!Single.IsInfinity(value);}
  static string Key(PartData p){return p.parent+"/"+p.material+"/"+p.shadows+"/"+p.end.ToString("R",CultureInfo.InvariantCulture)+"/"+p.margin.ToString("R",CultureInfo.InvariantCulture);}
  Mesh Own(Mesh mesh){owned.Add(mesh);return mesh;}
  void Build()
  {
   var text=Resources.Load<TextAsset>("R24EnemySourceGeometry");if(text==null)throw new InvalidOperationException("Original enemy geometry source resource missing");Manifest data;
   try{data=JsonUtility.FromJson<Manifest>(text.text);}finally{Resources.UnloadAsset(text);}
   if(data==null||data.version!=1||data.maximumCorpses!=6||data.maximumBodies!=48||data.lifetime!=12||data.variants==null||data.variants.Length!=4||data.anatomySha256!="334a7db08accf647b45cb00d1e1dbc583cc7fc2591288b9a7720d3fceeb2355c"||data.geometrySha256!="c1f1c8f7736eec284b9ad090a05ad0bace353d5c26f0fe7cd9f673cfd279769f")throw new InvalidOperationException("Original enemy geometry provenance/schema");
   int[] census={540,4452,540,6148},bodyCounts={8,6,8,6};
   for(int id=0;id<4;id++){
    var source=data.variants[id];if(source==null||source.id!=id||source.triangles!=census[id]||source.parts==null||source.parts.Length<1||source.parts.Length>128||source.bodies==null||source.bodies.Length!=bodyCounts[id]||source.joints==null||source.joints.Length!=bodyCounts[id]-1||source.bones==null||source.bones.Length!=(id==0||id==2?64:0))throw new InvalidOperationException("Original variant census");
    var variant=new Variant{Source=source};variants[id]=variant;var groups=new Dictionary<string,Group>();int triangles=0;
    for(int index=0;index<source.parts.Length;index++){
     var p=source.parts[index];if(p==null||p.parent<0||p.parent>5||p.material<0||p.material>11||p.shadows<0||p.shadows>3||!Finite(p.end)||!Finite(p.margin)||p.end<0||p.margin<0)throw new InvalidOperationException("Source leaf binding");
     var mesh=Own(ReadMesh(p,"Original R6 variant"+id+" leaf"+index));string key=Key(p);var part=new Part{Source=p,Mesh=mesh,Local=Matrix(p.local),GroupKey=key};variant.Parts.Add(part);Group group;
     if(!groups.TryGetValue(key,out group)){group=new Group{Key=key,Parent=p.parent,Material=p.material,Shadows=p.shadows,End=p.end,Margin=p.margin};groups.Add(key,group);variant.Groups.Add(group);}
     group.Pieces.Add(new CombineInstance{mesh=mesh,transform=part.Local});triangles+=p.indices.Length/3;
     // Geometry is in the shared native meshes now. Keep only original node
     // identity/frame/material metadata for death ownership; no resident JSON copy.
     p.vertices=p.normals=p.tangents=p.uv=p.uv2=p.colours=null;p.indices=null;
    }
    if(triangles!=source.triangles)throw new InvalidOperationException("Source triangle conservation");
    foreach(var group in variant.Groups){var mesh=Own(new Mesh{name="Original R6 variant"+id+" bucket "+group.Key,indexFormat=IndexFormat.UInt32});mesh.CombineMeshes(group.Pieces.ToArray(),true,true,false);mesh.RecalculateBounds();group.Mesh=mesh;group.Pieces.Clear();}
   }
  }
  static Mesh ReadMesh(PartData p,string name)
  {
   if(p.vertices==null||p.vertices.Length<9||p.vertices.Length%3!=0||p.vertices.Length>300000||p.normals==null||p.normals.Length!=p.vertices.Length||p.indices==null||p.indices.Length<3||p.indices.Length%3!=0||p.indices.Length>300000)throw new InvalidOperationException("Source leaf mesh schema");
   int count=p.vertices.Length/3;var v=new Vector3[count];var n=new Vector3[count];for(int i=0;i<count;i++){v[i]=new Vector3(p.vertices[i*3],p.vertices[i*3+1],-p.vertices[i*3+2]);n[i]=new Vector3(p.normals[i*3],p.normals[i*3+1],-p.normals[i*3+2]);if(!Finite(v[i].x+v[i].y+v[i].z+n[i].x+n[i].y+n[i].z))throw new InvalidOperationException("Finite source mesh");}
   foreach(int i in p.indices)if(i<0||i>=count)throw new InvalidOperationException("Source index range");
   var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};try{mesh.vertices=v;mesh.normals=n;mesh.triangles=p.indices;
    SetUv(mesh,p.uv,0,count);SetUv(mesh,p.uv2,1,count);
    if(p.tangents!=null&&p.tangents.Length>0){if(p.tangents.Length!=count*4)throw new InvalidOperationException("Source tangent count");var tangents=new Vector4[count];for(int i=0;i<count;i++)tangents[i]=new Vector4(p.tangents[i*4],p.tangents[i*4+1],-p.tangents[i*4+2],-p.tangents[i*4+3]);mesh.tangents=tangents;}
    if(p.colours!=null&&p.colours.Length>0){if(p.colours.Length!=count*4)throw new InvalidOperationException("Source colour count");var colours=new Color[count];for(int i=0;i<count;i++)colours[i]=new Color(p.colours[i*4],p.colours[i*4+1],p.colours[i*4+2],p.colours[i*4+3]);mesh.colors=colours;}
    mesh.RecalculateBounds();return mesh;
   }catch{UnityEngine.Object.Destroy(mesh);throw;}
  }
  static void SetUv(Mesh mesh,float[] data,int channel,int count){if(data==null||data.Length==0)return;if(data.Length!=count*2)throw new InvalidOperationException("Source UV count");var values=new List<Vector2>(count);for(int i=0;i<count;i++)values.Add(new Vector2(data[i*2],data[i*2+1]));mesh.SetUVs(channel,values);}
  void Clear(){if(disposed)return;disposed=true;foreach(var mesh in owned)if(mesh!=null)UnityEngine.Object.Destroy(mesh);owned.Clear();for(int i=0;i<variants.Length;i++)variants[i]=null;}
 }
}
#endif
