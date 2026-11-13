#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Bots
{
 // Authored enemy_visual.gd only. Parent owns bot visual yaw, motion, Health,
 // sight/aim/state clocks, source standing-height authority and all colliders.
 public sealed class R24KestrelVisual:MonoBehaviour,IDisposable
 {
  R24KestrelAssets assets;Transform scaled,model,skeleton,turret,flash;Transform[] bones;readonly Dictionary<string,int> ids=new Dictionary<string,int>();readonly List<Renderer> renderers=new List<Renderer>();readonly List<Renderer> optics=new List<Renderer>();readonly List<Ranged> ranged=new List<Ranged>();MaterialPropertyBlock threat;readonly Dictionary<string,Renderer> sourceRenderers=new Dictionary<string,Renderer>();
  sealed class Ranged {internal Renderer Renderer;internal float End,Margin;}bool initialized,disposed,visible=true;int variant;float phase,locomotion,standing;readonly List<int> controlled=new List<int>();
  struct RestPose
  {
   internal Vector3 Position;
   internal Quaternion Rotation;
   internal float Length;
  }
  struct Limb
  {
   internal int Thigh, Shin, Foot, UpperArm, Forearm, Hand, Middle;
   internal float Phase;
  }
  RestPose[] restPoses;
  readonly Limb[] limbs = new Limb[2];
  public bool Ready{get;private set;}public string Failure{get;private set;}public Transform Muzzle{get;private set;}// Normalized head-bone world frame BEFORE the source +.065*standingRatio offset.
  public Transform HeadPose{get;private set;}public Vector3 HeadSize{get{return variant==1?Vector3.zero:new Vector3(.28f,.32f,.28f)*ScaleRatio;}}public float ScaleRatio{get;private set;}=1;
  // All8source influences are retained. The caller sets source-correct global
  // skinWeights Unlimited once; this visual never changes project quality.
  public bool FullWeightSkinning{get{return QualitySettings.skinWeights==SkinWeights.Unlimited;}}
  public void Initialize(int sourceVariant){if(initialized)throw new InvalidOperationException("One Kestrel visual instance");if(sourceVariant!=0&&sourceVariant!=1&&sourceVariant!=2)throw new ArgumentOutOfRangeException("sourceVariant","Authored source variants0/1/2; Warden3 has separate source geometry");initialized=true;variant=sourceVariant;assets=R24KestrelAssets.Acquire();StartCoroutine(Load());}
  IEnumerator Load(){while(!assets.Ready&&assets.Failure==null&&!disposed)yield return null;if(disposed)yield break;if(assets.Failure!=null){Failure=assets.Failure;yield break;}try{Build();Ready=true;UpdatePose(0,Vector3.zero,true,false,0,0);}catch(Exception e){Failure=e.ToString();}}
  static Transform Node(string name,Transform parent){var go=new GameObject(name);go.layer=18;go.transform.SetParent(parent,false);return go.transform;}
  void Build()
  {
   scaled=Node("Original _visual standing-height scale",transform);if(variant==1){BuildScout();return;}model=Node("Irondust DetailedEnemy authored MODEL",scaled);model.localPosition=Vector3.up*.055f;model.localRotation=Quaternion.Euler(0,180,0);model.localScale=new Vector3(variant==2?94:90,90,90);skeleton=Node("Skeleton3D source64bones",model);bones=new Transform[assets.Skeleton.bones.Length];
   for(int i=0;i<bones.Length;i++){var b=assets.Skeleton.bones[i];if(b.parent>=i)throw new InvalidOperationException("Non-topological source skeleton");bones[i]=Node(b.name,b.parent>=0?bones[b.parent]:skeleton);bones[i].localPosition=V(b.position);bones[i].localRotation=Q(b.rotation);bones[i].localScale=V(b.scale);ids.Add(b.name,i);}
   foreach(string name in new[]{"hips","chest","head","upper_arm_L","forearm_L","hand_L","upper_arm_R","forearm_R","hand_R","thigh_L","shin_L","foot_L","thigh_R","shin_R","foot_R"})controlled.Add(ids[name]);
   // Source rest data is immutable for this visual's asset lease. Decode it once.
   restPoses = new RestPose[bones.Length];
   for (int i = 0; i < bones.Length; i++)
   {
    Matrix4x4 rest = Matrix(assets.Skeleton.bones[i].rest);
    Vector3 position = rest.GetColumn(3);
    restPoses[i] = new RestPose { Position = position, Rotation = rest.rotation, Length = position.magnitude };
   }
   limbs[0] = BuildLimb("L", 0);
   limbs[1] = BuildLimb("R", Mathf.PI);

   foreach(var p in assets.Parts){var t=Node(p.Name,model);var r=t.gameObject.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=p.Mesh;r.sharedMaterial=assets.Skin;r.bones=bones;r.rootBone=bones[0];r.quality=SkinQuality.Auto;r.updateWhenOffscreen=true;r.localBounds=new Bounds(new Vector3(0,.01f,0),new Vector3(.06f,.06f,.06f));renderers.Add(r);}
   turret=Node("Source retained _turret",scaled);turret.localPosition=Vector3.up*1.5f;
   var sourceMaterials=new[]{assets.Dark,assets.Edge,assets.Gun,assets.Threat};foreach(var group in assets.Original.ForVariant(variant).Groups){var renderer=Shape("Original source accessory "+group.Key,group.Parent==1?turret:scaled,group.Mesh,Vector3.zero,Vector3.one,Quaternion.identity,sourceMaterials[group.Material],group.Shadows!=0);sourceRenderers.Add(group.Key,renderer);if(group.Material==3)optics.Add(renderer);}threat=new MaterialPropertyBlock();
   Muzzle=Node("VisibleWeaponMuzzle source marker",turret);Muzzle.localPosition=new Vector3(.408f,-.3f,.61f);flash=Shape("Source burst-tail flash",turret,assets.Box,Muzzle.localPosition+Vector3.forward*.075f,new Vector3(.18f,.18f,.25f),Quaternion.identity,assets.Flash,false).transform;flash.gameObject.SetActive(false);HeadPose=Node("AnimatedHeadHitShape086 normalized frame only",transform);scaled.gameObject.SetActive(visible);
  }
  void BuildScout(){turret=Node("Source Scout retained _turret",scaled);turret.localPosition=Vector3.up*1.5f;foreach(var part in assets.Scout){var row=part.Source;var r=Shape("Authored Scout bucket "+row.material+(row.turret?" turret":" body"),row.turret?turret:scaled,part.Mesh,Vector3.zero,Vector3.one,Quaternion.identity,assets.ScoutMaterials[row.material],row.shadows);sourceRenderers.Add(part.Original.Key,r);if(row.material==4)optics.Add(r);if(row.range>0)ranged.Add(new Ranged{Renderer=r,End=row.range,Margin=row.margin});}threat=new MaterialPropertyBlock();Muzzle=Node("VisibleWeaponMuzzle source marker",turret);Muzzle.localPosition=new Vector3(.408f,-.3f,.61f);flash=Shape("Source Scout burst-tail flash",turret,assets.Box,Muzzle.localPosition+Vector3.forward*.075f,new Vector3(.18f,.18f,.25f),Quaternion.identity,assets.Flash,false).transform;flash.gameObject.SetActive(false);scaled.gameObject.SetActive(visible);}
  // Source detail visibility ranges20/55m with2m hysteresis; base Scout body
  // has no source range cutoff. Caller supplies actual viewer, no camera lookup.
  public void SetViewerPosition(Vector3 actualViewer){foreach(var entry in ranged){float distance=Vector3.Distance(entry.Renderer.bounds.center,actualViewer);entry.Renderer.enabled=distance<(entry.Renderer.enabled?entry.End+entry.Margin:entry.End-entry.Margin);}}
  Renderer Shape(string name,Transform parent,Mesh mesh,Vector3 at,Vector3 size,Quaternion rotation,Material material,bool shadows){var t=Node(name,parent);t.localPosition=at;t.localRotation=rotation;t.localScale=size;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;renderers.Add(r);return r;}
  public void SetVisible(bool value){visible=value;if(scaled!=null)scaled.gameObject.SetActive(value);}
  public void SetFlash(bool sourceBurstFlash){if(flash!=null)flash.gameObject.SetActive(sourceBurstFlash&&visible);}
  // Source enemy changes emission only; albedo/relative energy remain authored.
  public void SetThreatEmission(Color sourceLinear){if(optics.Count==0)return;var c=R24KestrelAssets.ShaderColor(sourceLinear*2.5f);threat.SetColor("_EmissionColor",c);foreach(var optic in optics)optic.SetPropertyBlock(threat);}
  public bool SetStandingEye(float sourceAuthoritativeEyeHeight){if(variant==1)return false;if(float.IsNaN(sourceAuthoritativeEyeHeight)||float.IsInfinity(sourceAuthoritativeEyeHeight)||sourceAuthoritativeEyeHeight<1.25f||sourceAuthoritativeEyeHeight>2.2f)return false;if(standing>0&&Mathf.Abs(standing-sourceAuthoritativeEyeHeight)<.015f)return false;standing=sourceAuthoritativeEyeHeight;ScaleRatio=standing/1.605f;if(scaled!=null)scaled.localScale=Vector3.one*ScaleRatio;return true;}
  // worldDisplacement is the actual accepted body movement since the previous
  // pose. aimPitch uses original Godot radians (positive looks UP). The root
  // visual-yaw transform is never overwritten, and no transform is queried for AI.
  public void UpdatePose(float delta, Vector3 worldDisplacement, bool grounded, bool aiming, float recoil, float godotAimPitch)
  {
   if (!Ready || disposed) return;
   if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0 || delta > 1 || !Finite(worldDisplacement) ||
       float.IsNaN(recoil) || float.IsInfinity(recoil) || float.IsNaN(godotAimPitch) || float.IsInfinity(godotAimPitch))
    throw new ArgumentOutOfRangeException("finite accepted source pose frame");

   scaled.localScale = Vector3.one * ScaleRatio;
   turret.localPosition = new Vector3(0, 1.5f, -recoil * .12f);
   turret.localRotation = Quaternion.AngleAxis(-godotAimPitch * Mathf.Rad2Deg, Vector3.right);
   if (variant == 1) return;

   float travel = new Vector2(worldDisplacement.x, worldDisplacement.z).magnitude;
   if (travel > 1) travel = 0;
   phase = Mathf.Repeat(phase + travel / 1.15f * Mathf.PI * 2, Mathf.PI * 2);
   float speed = travel / Mathf.Max(delta, .001f);
   locomotion = Mathf.MoveTowards(locomotion, grounded ? Mathf.Clamp01(speed / 1.4f) : 0, delta * 7);
   foreach (int i in controlled)
   {
    bones[i].localPosition = restPoses[i].Position;
    bones[i].localRotation = restPoses[i].Rotation;
   }

   Vector3 direction = Quaternion.Inverse(skeleton.rotation) * worldDisplacement;
   direction.y = 0;
   direction.Normalize();
   if (direction.sqrMagnitude < .01f) direction = Quaternion.Inverse(skeleton.rotation) * transform.forward;
   Vector3 swingAxis = -Vector3.Cross(direction, Vector3.up).normalized;
   foreach (var limb in limbs)
   {
    float swing = Mathf.Sin(phase + limb.Phase);
    Rotate(limb.Thigh, swingAxis, swing * .42f * locomotion);
    Rotate(limb.Shin, Vector3.right, -(.1f + Mathf.Max(0, -swing) * .62f * locomotion));
    Rotate(limb.Foot, Vector3.right, .1f - swing * .18f * locomotion);
   }

   Vector3 pitchAxis = Quaternion.Inverse(skeleton.rotation) * transform.right;
   Rotate(ids["chest"], pitchAxis, Mathf.Clamp(godotAimPitch * .25f - recoil * .08f, -.15f, .15f));
   Rotate(ids["head"], pitchAxis, Mathf.Clamp(godotAimPitch * .65f, -.4f, .4f));
   foreach (var limb in limbs) AimArm(limb);

   var head = bones[ids["head"]];
   HeadPose.SetPositionAndRotation(head.position, head.rotation);
   var inherited = transform.lossyScale;
   HeadPose.localScale = new Vector3(1 / Mathf.Max(.00001f, inherited.x),
    1 / Mathf.Max(.00001f, inherited.y), 1 / Mathf.Max(.00001f, inherited.z));
  }

  Limb BuildLimb(string side, float phaseOffset)
  {
   return new Limb { Thigh = ids["thigh_" + side], Shin = ids["shin_" + side], Foot = ids["foot_" + side],
    UpperArm = ids["upper_arm_" + side], Forearm = ids["forearm_" + side], Hand = ids["hand_" + side],
    Middle = ids["f_middle_01_" + side], Phase = phaseOffset };
  }

  void AimArm(Limb limb)
  {
   Vector3 shoulder = skeleton.InverseTransformPoint(bones[limb.UpperArm].position);
   float sign = Mathf.Sign(turret.InverseTransformPoint(bones[limb.UpperArm].position).x);
   Vector3 destination = skeleton.InverseTransformPoint(turret.TransformPoint(new Vector3(sign * .408f, -.37f, .1f)));
   float a = restPoses[limb.Forearm].Length, b = restPoses[limb.Hand].Length;
   Vector3 offset = destination - shoulder;
   float distance = Mathf.Clamp(offset.magnitude, .0001f, a + b - .00001f);
   Vector3 axis = offset.normalized;
   Vector3 hint = Quaternion.Inverse(skeleton.rotation) * (transform.rotation * new Vector3(sign, -.35f, -.4f));
   Vector3 bend = (hint - axis * Vector3.Dot(hint, axis)).normalized;
   float along = (a * a - b * b + distance * distance) / (2 * distance);
   Vector3 elbow = shoulder + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
   Aim(limb.UpperArm, limb.Forearm, elbow);
   Aim(limb.Forearm, limb.Hand, destination);
   Aim(limb.Hand, limb.Middle, skeleton.InverseTransformPoint(turret.TransformPoint(new Vector3(sign * .408f, -.385f, .22f))));
  }

  Quaternion GlobalRotation(int index)
  {
   return index < 0 ? Quaternion.identity : Quaternion.Inverse(skeleton.rotation) * bones[index].rotation;
  }

  void Rotate(int index, Vector3 reflectedAxis, float sourceAngle)
  {
   int parent = assets.Skeleton.bones[index].parent;
   bones[index].localRotation = Quaternion.Inverse(GlobalRotation(parent)) *
    Quaternion.AngleAxis(-sourceAngle * Mathf.Rad2Deg, reflectedAxis) * GlobalRotation(index);
  }

  void Aim(int index, int child, Vector3 destination)
  {
   Vector3 origin = skeleton.InverseTransformPoint(bones[index].position);
   Vector3 shaft = skeleton.InverseTransformPoint(bones[child].position) - origin, wanted = destination - origin;
   if (shaft.sqrMagnitude < 1e-9f || wanted.sqrMagnitude < 1e-9f) return;
   int parent = assets.Skeleton.bones[index].parent;
   bones[index].localRotation = Quaternion.Inverse(GlobalRotation(parent)) *
    Quaternion.FromToRotation(shaft.normalized, wanted.normalized) * GlobalRotation(index);
  }
  static Matrix4x4 Matrix(float[] data){var m=new Matrix4x4();for(int i=0;i<16;i++)m[i/4,i%4]=data[i];return m;}
  static Vector3 V(float[] p){return new Vector3(p[0],p[1],p[2]);}static Quaternion Q(float[] p){return new Quaternion(p[0],p[1],p[2],p[3]);}static bool Finite(Vector3 p){return !float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);}
  internal R24RagdollSource CaptureCorpseSource(){if(!Ready||disposed)throw new InvalidOperationException("Ready source art required for corpse");var lease=R24KestrelAssets.Acquire();if(lease!=assets){lease.Release();throw new InvalidOperationException("One source material/geometry lifetime");}return new R24RagdollSource{Geometry=assets.Original,Variant=variant,Frames=new[]{scaled,turret},Bones=bones,Model=model,Materials=variant==1?assets.ScoutMaterials:new[]{assets.Dark,assets.Edge,assets.Gun,assets.Threat},Renderers=new Dictionary<string,Renderer>(sourceRenderers),Release=lease.Release};}
  public void Dispose(){if(disposed)return;disposed=true;Ready=false;StopAllCoroutines();if(scaled!=null){scaled.gameObject.SetActive(false);Destroy(scaled.gameObject);}if(HeadPose!=null)Destroy(HeadPose.gameObject);if(assets!=null){assets.Release();assets=null;}}
  void OnDestroy(){Dispose();}
 }
}
#endif
