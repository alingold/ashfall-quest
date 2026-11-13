#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Bots
{
 // Authored Warden3 geometry only. Parent owns accepted motion/yaw, source
 // state/Health/core rules, floor-validated impact point and all colliders.
 public sealed class R24WardenVisual:MonoBehaviour,IDisposable
 {
  R24WardenGeometry geometry;readonly R24WardenPose pose=new R24WardenPose();readonly Dictionary<R24WardenRecipe.Attachment,Transform> attachments=new Dictionary<R24WardenRecipe.Attachment,Transform>();readonly List<Renderer> core=new List<Renderer>(),optic=new List<Renderer>();readonly List<Ranged> ranged=new List<Ranged>();readonly Dictionary<string,Renderer> sourceRenderers=new Dictionary<string,Renderer>();MaterialPropertyBlock coreBlock,opticBlock;Transform visual,turret,flash,marker;bool initialized,disposed,visible=true,markerShown;
  sealed class Ranged {internal Renderer Renderer;internal float End;}
  public bool Ready{get;private set;}public string Failure{get;private set;}public Transform Muzzle{get;private set;}public Transform HeadPose{get{return null;}}public bool CoreOpen{get;private set;}public float LeftShutterRadians{get{return pose.LeftShutter;}}public float RightShutterRadians{get{return pose.RightShutter;}}
  public void Initialize(){if(initialized)throw new InvalidOperationException("One authored Warden visual owner");initialized=true;try{geometry=R24WardenGeometry.Acquire();Build();Ready=true;UpdatePose(0,Vector3.zero,0,0,0,0,false,1);}catch(Exception e){Failure=e.ToString();Dispose();}}
  static Transform Node(string name,Transform parent){var go=new GameObject(name);go.layer=18;go.transform.SetParent(parent,false);return go.transform;}
  static Vector3 G(Vector3 point){return new Vector3(point.x,point.y,-point.z);}
  void Build(){visual=Node("Warden source _visual SCALE1.3 once",transform);visual.localScale=Vector3.one*1.3f;attachments.Add(R24WardenRecipe.Attachment.Body,visual);turret=Node("Source Warden turret",visual);turret.localPosition=Vector3.up*1.5f;attachments.Add(R24WardenRecipe.Attachment.Turret,turret);
   foreach(float side in new[]{-1f,1f}){var shutter=Node("Actual core shutter hinge "+side,visual);shutter.localPosition=G(new Vector3(side*.34f,1.02f,-.39f));attachments.Add(side<0?R24WardenRecipe.Attachment.LeftShutter:R24WardenRecipe.Attachment.RightShutter,shutter);var rotor=Node("Actual Warden rotor "+side,visual);rotor.localPosition=G(new Vector3(side*1.35f,1.17f,.1f));attachments.Add(side<0?R24WardenRecipe.Attachment.LeftRotor:R24WardenRecipe.Attachment.RightRotor,rotor);}
   Muzzle=Node("VisibleWeaponMuzzle source collar front",turret);Muzzle.localPosition=new Vector3(.408f,-.3f,.61f);flash=Node("Source Warden burst tail flash",turret);flash.localPosition=Muzzle.localPosition+Vector3.forward*.075f;attachments.Add(R24WardenRecipe.Attachment.Flash,flash);flash.gameObject.SetActive(false);
   // Source top_level marker is not scaled1.3 and never follows the actor.
   marker=Node("Source Warden world-space impact marker",null);marker.gameObject.SetActive(false);attachments.Add(R24WardenRecipe.Attachment.Impact,marker);
   foreach(var bucket in geometry.Buckets){var parent=attachments[bucket.Parent];var go=Node("Source "+bucket.Parent+" material"+bucket.Material+" range"+bucket.End,parent);go.gameObject.AddComponent<MeshFilter>().sharedMesh=bucket.Mesh;var r=go.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=geometry.Materials[bucket.Material];if(bucket.SourceKey!=null)sourceRenderers.Add(bucket.SourceKey,r);r.shadowCastingMode=bucket.Shadows?ShadowCastingMode.On:ShadowCastingMode.Off;if(bucket.Material==5)core.Add(r);if(bucket.Material==4)optic.Add(r);if(bucket.End>0)ranged.Add(new Ranged{Renderer=r,End=bucket.End});}coreBlock=new MaterialPropertyBlock();opticBlock=new MaterialPropertyBlock();visual.gameObject.SetActive(visible);
  }
  public void UpdatePose(float delta,Vector3 actualWorldDisplacement,float sourceAge,float sourcePhase,float aimPitch,float recoil,bool coreOpen,int bossPhase){if(!Ready||disposed)return;if(!Finite(actualWorldDisplacement)||!Finite(sourceAge)||!Finite(sourcePhase)||!Finite(aimPitch)||!Finite(recoil))throw new ArgumentOutOfRangeException("finite source Warden frame");pose.Step(delta,coreOpen);CoreOpen=coreOpen;turret.localPosition=new Vector3(0,1.5f,-recoil*.12f);turret.localRotation=R24WardenGeometry.Rotation(Vector3.right,aimPitch);attachments[R24WardenRecipe.Attachment.LeftRotor].localRotation=R24WardenGeometry.Rotation(Vector3.up,pose.LeftRotor);attachments[R24WardenRecipe.Attachment.RightRotor].localRotation=R24WardenGeometry.Rotation(Vector3.up,pose.RightRotor);attachments[R24WardenRecipe.Attachment.LeftShutter].localRotation=R24WardenGeometry.Rotation(Vector3.up,pose.LeftShutter);attachments[R24WardenRecipe.Attachment.RightShutter].localRotation=R24WardenGeometry.Rotation(Vector3.up,pose.RightShutter);Color color=coreOpen?new Color(1,.32f,.035f):new Color(.08f,.45f,1);coreBlock.SetColor("_BaseColor",R24KestrelAssets.ShaderColor(color));coreBlock.SetColor("_EmissionColor",R24KestrelAssets.ShaderColor(color*2.5f));foreach(var r in core)r.SetPropertyBlock(coreBlock);
   // Source bossPhase affects AI flight/attacks, not rotorRPM or body geometry.
   // Source age/phase bobbing is already in the accepted actor motion; no second
   // visual displacement or hidden target query is introduced here.
  }
  public void SetThreatEmission(Color sourceLinear){if(!Ready||disposed)return;opticBlock.SetColor("_EmissionColor",R24KestrelAssets.ShaderColor(sourceLinear*2.5f));foreach(var r in optic)r.SetPropertyBlock(opticBlock);}
  public void SetViewerPosition(Vector3 actualHead){foreach(var entry in ranged){float distance=Vector3.Distance(entry.Renderer.bounds.center,actualHead);entry.Renderer.enabled=distance<(entry.Renderer.enabled?entry.End+2:entry.End-2);}}
  // The source top-level ground marker is outside the actor hierarchy.
  // Publish its rendering role with the actor without inheriting actor motion/scale.
  public void PublishRenderLayer(int layer){if(layer<0||layer>31)throw new ArgumentOutOfRangeException("layer");SetLayer(visual,layer);SetLayer(marker,layer);}
  static void SetLayer(Transform owner,int layer){if(owner!=null)foreach(var node in owner.GetComponentsInChildren<Transform>(true))node.gameObject.layer=layer;}
  public void SetVisible(bool show){visible=show;if(visual!=null)visual.gameObject.SetActive(show);if(marker!=null)marker.gameObject.SetActive(show&&markerShown);}
  public void SetFlash(bool sourceBurstTailFlash){if(flash!=null)flash.gameObject.SetActive(visible&&sourceBurstTailFlash);}
  // worldPoint is the actual source floor hit +.06, already validated by Root.
  // Root supplies windup1 or impact 1+(.35-remaining)*2. No ray/fake ground.
  public bool SetImpactMarker(Vector3 worldPoint,bool show,float sourceScale=1){if(!Ready||disposed||!Finite(worldPoint)||!Finite(sourceScale)||sourceScale<=0)return false;marker.SetPositionAndRotation(worldPoint,Quaternion.identity);marker.localScale=Vector3.one*sourceScale;markerShown=show;marker.gameObject.SetActive(show&&visible);return true;}
  static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}static bool Finite(Vector3 value){return Finite(value.x)&&Finite(value.y)&&Finite(value.z);}
  internal R24RagdollSource CaptureCorpseSource(){if(!Ready||disposed)throw new InvalidOperationException("Ready source Warden art required for corpse");var lease=R24WardenGeometry.Acquire();if(lease!=geometry){lease.Release();throw new InvalidOperationException("One Warden source material/geometry lifetime");}var frames=new Transform[6];for(int i=0;i<6;i++)frames[i]=attachments[(R24WardenRecipe.Attachment)i];return new R24RagdollSource{Geometry=geometry.Original,Variant=3,Frames=frames,Materials=geometry.Materials,Renderers=new Dictionary<string,Renderer>(sourceRenderers),Release=lease.Release};}
  public void Dispose(){if(disposed)return;disposed=true;Ready=false;if(visual!=null){visual.gameObject.SetActive(false);Destroy(visual.gameObject);}if(marker!=null){marker.gameObject.SetActive(false);Destroy(marker.gameObject);}if(geometry!=null){geometry.Release();geometry=null;}attachments.Clear();core.Clear();optic.Clear();ranged.Clear();}
  void OnDestroy(){Dispose();}
 }
}
#endif
