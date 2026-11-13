#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Reflection;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.Bots;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  sealed class WardenBucketProof {internal string Key;internal int Triangles;internal Bounds Bounds;internal bool HasBounds;}
  static string WardenBucketKey(object part){return Read(part,"Parent")+"/"+Read(part,"Material")+"/"+Read(part,"End")+"/"+Read(part,"Shadows");}
  static Vector3 WardenReflect(Vector3 point){return new Vector3(point.x,point.y,-point.z);}
  static void WardenBoundPoint(WardenBucketProof proof,Vector3 point){if(!proof.HasBounds){proof.Bounds=new Bounds(point,Vector3.zero);proof.HasBounds=true;}else proof.Bounds.Encapsulate(point);}
  // Independent sampled bounds of the pinned Godot primitive definitions,
  // prior to Mesh.CombineMeshes. Not a call to the runtime geometry generator.
  static Dictionary<string,WardenBucketProof> WardenRecipeProof(Action<string> note)
  {
   Type type=typeof(R24WardenVisual).Assembly.GetType("Ashfall.Integration.Playable.Bots.R24WardenRecipe",true);var build=type.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic);Need(build!=null,"Actual source recipe entry");var result=new Dictionary<string,WardenBucketProof>();int parts=0,total=0;var shapeCounts=new Dictionary<string,int>();
   foreach(var part in (IEnumerable)build.Invoke(null,null)){
    string kind=Read(part,"Kind").ToString(),key=WardenBucketKey(part);WardenBucketProof proof;if(!result.TryGetValue(key,out proof)){proof=new WardenBucketProof{Key=key};result.Add(key,proof);}parts++;shapeCounts[kind]=shapeCounts.ContainsKey(kind)?shapeCounts[kind]+1:1;
    int segments=(int)Read(part,"Segments"),rings=(int)Read(part,"Rings");int tris=kind=="Box"?12:kind=="Cylinder"?segments*(2*(rings+1)+2):kind=="Torus"?2*rings*segments:28;proof.Triangles+=tris;total+=tris;
    Vector3 at=(Vector3)Read(part,"Position"),size=(Vector3)Read(part,"Size"),axis=(Vector3)Read(part,"Axis"),align=(Vector3)Read(part,"AlignY");float angle=(float)Read(part,"Angle");
    Quaternion rotation=align.sqrMagnitude>0?Quaternion.FromToRotation(Vector3.up,WardenReflect(align.normalized)):axis.sqrMagnitude==0?Quaternion.identity:Quaternion.AngleAxis(-angle*Mathf.Rad2Deg,WardenReflect(axis));
    Action<Vector3> point=p=>WardenBoundPoint(proof,WardenReflect(at)+rotation*WardenReflect(p));
    if(kind=="Box"||kind=="Plate")foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})point(Vector3.Scale(size,new Vector3(x,y,z))*.5f);
    else if(kind=="Cylinder")for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;foreach(float y in new[]{-.5f,.5f})point(new Vector3(Mathf.Cos(a)*size.x,y*size.y,Mathf.Sin(a)*size.z));}
    else {float inner=(float)Read(part,"Inner"),outer=(float)Read(part,"Outer"),major=(inner+outer)*.5f,minor=(outer-inner)*.5f;for(int i=0;i<rings;i++)for(int j=0;j<segments;j++){float a=i*Mathf.PI*2/rings,b=j*Mathf.PI*2/segments;point(new Vector3(Mathf.Cos(a)*(major+Mathf.Cos(b)*minor),Mathf.Sin(b)*minor,Mathf.Sin(a)*(major+Mathf.Cos(b)*minor)));}}
   }
   Need(parts==112&&total==6568&&shapeCounts["Box"]==50&&shapeCounts["Cylinder"]==49&&shapeCounts["Torus"]==7&&shapeCounts["Plate"]==6,"Frozen source112 primitives/6568 triangles/shape census");note("Pinned source recipe112 parts/6568 triangles; independent native bucket-count/bounds expectations="+result.Count);return result;
  }
  static void WardenActualMeshes(R24WardenVisual art,Action<string> note)
  {
   Need(art.Ready&&String.IsNullOrEmpty(art.Failure),"Actual Warden visual ready");var expected=WardenRecipeProof(note);object geometry=Read(art,"geometry");var materials=(Material[])Read(geometry,"Materials");Need(materials.Length==12,"Source twelve shared material bindings");
   float[] metal={.75f,.8f,.55f,.35f,.25f,.35f,0,0,.65f,.65f,.65f,.65f};int triangles=0,buckets=0,markerTriangles=0;double area=0,positive=0;
   foreach(var bucket in (IEnumerable)Read(geometry,"Buckets")){
    string key=WardenBucketKey(bucket);Need(expected.ContainsKey(key),"No unowned/extra bucket "+key);var proof=expected[key];var mesh=(Mesh)Read(bucket,"Mesh");int material=(int)Read(bucket,"Material");var v=mesh.vertices;var n=mesh.normals;var ix=mesh.triangles;Need(ix.Length==proof.Triangles*3&&n.Length==v.Length,"Exact source triangle/normal ownership "+key);
    Need(Vector3.Distance(mesh.bounds.min,proof.Bounds.min)<.0001f&&Vector3.Distance(mesh.bounds.max,proof.Bounds.max)<.0001f,"Combined source primitive bounds within .1mm "+key);
    for(int i=0;i<n.Length;i++)Need(!Single.IsNaN(n[i].x+n[i].y+n[i].z)&&Mathf.Abs(n[i].magnitude-1)<.001f,"Finite normalized source outward normal");
    for(int i=0;i<ix.Length;i+=3){int a=ix[i],b=ix[i+1],c=ix[i+2];Need(a>=0&&a<v.Length&&b>=0&&b<v.Length&&c>=0&&c<v.Length,"Valid native source indices");Vector3 cross=Vector3.Cross(v[b]-v[a],v[c]-v[a]);double value=cross.magnitude;area+=value;if(Vector3.Dot(cross,n[a]+n[b]+n[c])>0)positive+=value;}
    triangles+=ix.Length/3;if(Read(bucket,"Parent").ToString()=="Impact")markerTriangles+=ix.Length/3;buckets++;note("Actual mesh "+key+" vertices="+v.Length+" triangles="+ix.Length/3+" bounds="+mesh.bounds);expected.Remove(key);
    var m=materials[material];Need(m!=null&&m.shader.name=="Universal Render Pipeline/Lit"&&m.GetFloat("_Surface")==0&&m.GetFloat("_Cull")==2,"Opaque native front-face URP material");Need(Mathf.Abs(m.GetFloat("_Metallic")-metal[material])<.00001f&&Mathf.Abs(m.GetFloat("_Smoothness")-(material<6?.62f:material<8?.2f:.38f))<.00001f,"Original source metal/roughness retained");
   }
   Need(expected.Count==0&&triangles==6568&&markerTriangles==408&&positive/area>.99999,"All source triangles retained with outward winding/normals");
   Transform visual=(Transform)Read(art,"visual"),marker=(Transform)Read(art,"marker");Need(visual.localScale==Vector3.one*1.3f&&marker.parent==null,"Source actor scale once and independent world marker");Need(art.Muzzle.localPosition==new Vector3(.408f,-.3f,.61f)&&art.Muzzle.parent==(Transform)Read(art,"turret"),"Source muzzle follows real turret frame");
   int renderers=0;foreach(var r in art.GetComponentsInChildren<MeshRenderer>(true)){Need(r.gameObject.layer==R24ValleyBots.CombatLayer&&r.GetComponent<MeshCollider>()==null&&r.sharedMaterial!=null,"Actual actor render-only ownership/layer/material");renderers++;}foreach(var r in marker.GetComponentsInChildren<MeshRenderer>(true)){Need(r.gameObject.layer==R24ValleyBots.CombatLayer&&r.GetComponent<Collider>()==null,"Detached marker render-only layer");renderers++;}Need(renderers==buckets,"One native renderer per source material/attachment/range bucket");note("Actual all6568 triangles incl408 marker/outwardNormalArea="+(positive/area)+" scale1.3/muzzle/source-materials/native renderers="+renderers);
  }
  static void WardenCapture(R23PlayableLoop loop,R24WardenVisual art,string label,bool rear,bool ground,Action<string> note)
  {
   Camera camera=loop.Gameplay.BotProbeAwpCamera;Need(camera!=null&&!camera.GetUniversalAdditionalCameraData().allowXRRendering,"Actual XR-off optical capture camera");Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;var target=camera.targetTexture;var clear=camera.clearFlags;Color background=camera.backgroundColor;float fov=camera.fieldOfView,aspect=camera.aspect;int mask=camera.cullingMask;bool enabled=camera.enabled;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(640,800,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var enabledBefore=new Dictionary<Renderer,bool>();
   try{
    foreach(var r in loop.ValleyBots.EncounterTransform.GetComponentsInChildren<Renderer>(true)){enabledBefore.Add(r,r.enabled);r.enabled=r.enabled&&r.transform.IsChildOf(art.transform);}Transform marker=(Transform)Read(art,"marker");Vector3 center=ground?marker.position:art.transform.position+Vector3.up*1.4f;Vector3 eye=ground?center+Vector3.up*5+art.transform.forward*3.8f:center+art.transform.forward*(rear?-7:7)+art.transform.right*1.0f+Vector3.up*.6f;art.SetViewerPosition(eye);
    camera.enabled=false;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(center-eye,Vector3.up));camera.targetTexture=rt;camera.aspect=.8f;camera.fieldOfView=45;camera.cullingMask=1<<R24ValleyBots.CombatLayer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.04f,.055f,1);camera.Render();RenderTexture.active=rt;pixels=new Texture2D(640,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,640,800),0,0);pixels.Apply();
    string directory=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"warden-visual");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,label+".png"),pixels.EncodeToPNG());var data=pixels.GetPixels32();Color32 corner=data[0];int nonuniform=0;foreach(var p in data)if(Mathf.Abs(p.r-corner.r)+Mathf.Abs(p.g-corner.g)+Mathf.Abs(p.b-corner.b)>15)nonuniform++;note("UNMATCHED Unity-only "+label+" eye="+eye+" target="+center+" FOV45/640x800 nonuniformPixels="+nonuniform+" core="+art.CoreOpen+" markerActive="+marker.gameObject.activeInHierarchy);Need(nonuniform>500,"Actual nonuniform Warden surface pixels; PNG saved before assertion");
   }finally{foreach(var r in enabledBefore)if(r.Key!=null)r.Key.enabled=r.Value;camera.enabled=enabled;camera.transform.SetPositionAndRotation(position,rotation);camera.targetTexture=target;camera.clearFlags=clear;camera.backgroundColor=background;camera.fieldOfView=fov;camera.aspect=aspect;camera.cullingMask=mask;RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(rt);}
  }
  static IEnumerator WardenVisualNative(R23PlayableLoop loop,Action<string> note)
  {
   var mission=loop.ValleyBots.Mission;int chapter=mission.Chapter,kills=mission.Kills;double seconds=mission.PlaySeconds;float difficulty=loop.ValleyBots.Difficulty;
   try{
    yield return LoadWardenEncounter(loop,note);var squad=loop.ValleyBots;var art=squad.EncounterTransform.GetComponentInChildren<R24WardenVisual>(true);Need(art!=null,"Actual chapter3 Warden visual owner");WardenActualMeshes(art,note);WardenShaderOracle(squad);
    WardenCapture(loop,art,"closed-front",false,false,note);WardenCapture(loop,art,"closed-back",true,false,note);R24KestrelBrain boss;Vector3 at;bool floor;Need(squad.TryRead(5,out boss,out at,out floor),"Actual source Warden brain");
    squad.NotifyGunshot(loop.Rig.transform.position);yield return WaitWardenWindup(loop,boss,false,1.2f,note);Need(art.CoreOpen&&art.LeftShutterRadians<-.9f&&art.RightShutterRadians>.9f,"Production core-open shutters before capture");WardenCapture(loop,art,"open-front",false,false,note);
    yield return WaitWardenWindup(loop,boss,true,1.0f,note);Transform marker=(Transform)Read(art,"marker");Need(marker.gameObject.activeInHierarchy&&marker.parent==null&&Vector3.Distance(marker.position,V3(boss.ImpactPoint))<.0001f,"Actual production world marker binding");Vector3 mark=marker.position;Quaternion yaw=art.transform.rotation;art.transform.rotation=Quaternion.Euler(0,37,0)*yaw;Need(marker.position==mark&&marker.rotation==Quaternion.identity,"Source top-level marker does not inherit actor yaw");art.transform.rotation=yaw;WardenCapture(loop,art,"area-marker",false,true,note);WardenShaderOracle(squad);
    art.SetVisible(false);Need(!marker.gameObject.activeInHierarchy&&!((Transform)Read(art,"visual")).gameObject.activeInHierarchy,"Actor/independent marker hide together");art.SetVisible(true);Need(marker.gameObject.activeInHierarchy,"Existing active marker visibility restores");note("Source mesh/ownership and production shutter/marker controls only; four UNMATCHED Unity images require personal inspection, no Godot image/HMD/parity/performance claim.");
   }finally{loop.ReturnToLobby();mission.Restore(chapter,kills,seconds);loop.ValleyBots.SetDifficulty(difficulty);}yield return null;
  }
 }
}
#endif
