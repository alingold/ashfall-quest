#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Integration.Playable.World;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.WorldScores {
 // Offline, source-bound geometry only. Reuses R24's material and single 1 Hz canvas.
 public static class R26WorldScoreboardFrames {
  [Serializable]public sealed class Rectangle{public string id="";public float[] center=null,normal=null,right=null,frameCenter=null;public float width=0,height=0,frameWidth=0,frameHeight=0,frontOffset=0,confidence=0;public bool approved=false,fitAspect=false;}
  [Serializable]sealed class SourceFace{public string id="";public float[] vertices=null,uv=null;public int[] indices=null;}
  [Serializable]sealed class MapRow{public string id="",sourceSha256="";public Rectangle[] rectangles=null,solidPanels=null;public SourceFace[] retainedFaces=null;}
  [Serializable]sealed class Data{public int version=0;public MapRow[] maps=null;}
  static Data data;static bool attempted;
  public static string Failure{get;private set;}
  static MapRow Find(R23PlayableMap map){
   if(!attempted){attempted=true;try{var text=Resources.Load<TextAsset>("R26MapScoreboards");if(text==null)throw new InvalidOperationException("Frame manifest missing");data=JsonUtility.FromJson<Data>(text.text);if(data==null||data.version!=1||data.maps==null)throw new InvalidOperationException("Frame manifest invalid");}catch(Exception e){Failure=e.Message;data=null;}}
   if(data!=null)foreach(var row in data.maps)if(row.id==map.MapId&&row.sourceSha256==map.SourceSha256)return row;return null;
  }
  static bool Finite(float v){return !Single.IsNaN(v)&&!Single.IsInfinity(v);}
  static bool Vector(float[] a){return a!=null&&a.Length==3&&Finite(a[0])&&Finite(a[1])&&Finite(a[2]);}
  static Vector3 V(float[] a){return new Vector3(a[0],a[1],a[2]);}
  public static Rectangle[] Read(R23PlayableMap map){var row=Find(map);if(row==null||row.rectangles==null)return new Rectangle[0];var valid=new List<Rectangle>();foreach(var r in row.rectangles)if(r!=null&&r.approved&&r.confidence>=.9f&&Vector(r.center)&&Vector(r.normal)&&Vector(r.right)&&Finite(r.width)&&Finite(r.height)&&Finite(r.frontOffset)&&r.width>.00001f&&r.height>.00001f&&r.frontOffset>0&&Mathf.Abs(V(r.normal).sqrMagnitude-1)<.001f&&Mathf.Abs(V(r.right).sqrMagnitude-1)<.001f&&Mathf.Abs(Vector3.Dot(V(r.normal),V(r.right)))<.001f)valid.Add(r);return valid.ToArray();}
  // Bind to the map geometry, whose lifetime and visibility also govern collision.
  public static void BuildCollision(R23PlayableMap map,Transform geometry){
   var rows=Read(map);
   foreach(var r in rows)AddSolid(geometry,r);
   var source=Find(map);
   if(source!=null&&source.solidPanels!=null)foreach(var r in source.solidPanels)AddSolid(geometry,r);
   BuildRetainedCollision(geometry,source,rows);
  }
  static void AddSolid(Transform geometry,Rectangle r){
    var node=new GameObject(r.id+" solid");node.transform.SetParent(geometry,false);bool full=Vector(r.frameCenter)&&r.frameWidth>0&&r.frameHeight>0;node.transform.localPosition=V(full?r.frameCenter:r.center);
    node.transform.localRotation=Quaternion.LookRotation(V(r.normal),Vector3.Cross(V(r.right),V(r.normal)));
    node.AddComponent<BoxCollider>().size=new Vector3(full?r.frameWidth:r.width,full?r.frameHeight:r.height,2*r.frontOffset);
  }
  static void BuildRetainedCollision(Transform geometry,MapRow source,Rectangle[] rows){
   if(source==null||source.retainedFaces==null)return;
   foreach(var f in source.retainedFaces){
    var v=f.vertices;if(v==null||v.Length<9||v.Length%3!=0)continue;
    Vector3 first=new Vector3(v[0],v[1],v[2]),center=Vector3.zero;
    for(int i=0;i<v.Length;i+=3)center+=new Vector3(v[i],v[i+1],v[i+2]);
    center/=v.Length/3;
    bool replaced=false;foreach(var r in rows)if(Vector3.Distance(center,V(r.center))<Mathf.Min(r.width,r.height)*.15f){replaced=true;break;}
    if(replaced)continue;
    Vector3 normal=Vector3.Cross(new Vector3(v[3],v[4],v[5])-first,new Vector3(v[6],v[7],v[8])-first).normalized;
    if(normal.sqrMagnitude<.9f)continue;
    var rotation=Quaternion.LookRotation(normal,Vector3.up);var inverse=Quaternion.Inverse(rotation);var bounds=new Bounds(Vector3.zero,Vector3.zero);
    for(int i=0;i<v.Length;i+=3)bounds.Encapsulate(inverse*(new Vector3(v[i],v[i+1],v[i+2])-center));
    var node=new GameObject(f.id+" solid");node.transform.SetParent(geometry,false);node.transform.localPosition=center;node.transform.localRotation=rotation;
    var box=node.AddComponent<BoxCollider>();box.center=bounds.center;box.size=new Vector3(bounds.size.x,bounds.size.y,Mathf.Max(.06f,bounds.size.z));
   }
  }
  public static bool TryBind(R23PlayableMap map,Material display,List<Mesh> meshes,out Transform root){
   root=null;var rows=Read(map);if(rows.Length==0)return false;
   root=new GameObject("R26 detected world scoreboard frames").transform;root.SetParent(map.transform,false);
   foreach(var r in rows){Vector3 center=V(r.center)+V(r.normal)*r.frontOffset,right=V(r.right)*r.width*.5f,up=Vector3.Cross(V(r.right),V(r.normal))*r.height*.5f;Add(root,display,meshes,r.id,new[]{center-right+up,center-right-up,center+right-up,center+right+up},new[]{new Vector2(0,1),new Vector2(0,0),new Vector2(1,0),new Vector2(1,1)},new[]{0,2,1,0,3,2},r.fitAspect?r.width/r.height:0);}
   // Retain calibrated legacy faces not replaced by a detected aperture. This is
   // needed for fragmented Alfa frames and Melipop's original six-vertex faces.
   var mapRow=Find(map);if(mapRow.retainedFaces!=null)foreach(var f in mapRow.retainedFaces){
    if(f.vertices==null||f.uv==null||f.indices==null||f.vertices.Length%3!=0||f.uv.Length*3!=f.vertices.Length*2)continue;
    var v=new Vector3[f.vertices.Length/3];var uv=new Vector2[v.Length];Vector3 center=Vector3.zero;for(int i=0;i<v.Length;i++){v[i]=new Vector3(f.vertices[i*3],f.vertices[i*3+1],f.vertices[i*3+2]);center+=v[i];uv[i]=new Vector2(f.uv[i*2],1-f.uv[i*2+1]);}center/=v.Length;
    bool replaced=false;foreach(var r in rows)if(Vector3.Distance(center,V(r.center))<Mathf.Min(r.width,r.height)*.15f){replaced=true;break;}if(!replaced)Add(root,display,meshes,f.id,v,uv,f.indices);
   }
   return true;
  }
  static void Add(Transform root,Material display,List<Mesh> meshes,string id,Vector3[] vertices,Vector2[] uv,int[] indices,float aspect=0){var mesh=new Mesh();mesh.name=id;mesh.vertices=vertices;mesh.uv=uv;var fit=new Vector2[vertices.Length];for(int i=0;i<fit.Length;i++)fit[i]=new Vector2(aspect,0);mesh.uv2=fit;mesh.triangles=indices;mesh.RecalculateBounds();meshes.Add(mesh);var go=new GameObject(id);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=display;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
 }
}
#endif
