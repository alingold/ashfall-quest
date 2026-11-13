#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Bots
{
 // Source ragdoll.gd + ragdoll_skin.gd. PhysX CharacterJoint supplies the cone
 // limits in the original joint frame; Godot bias/softness have no exact
 // PhysX analogue. Neither the project timestep nor global physics is changed.
 internal sealed class R24RagdollCorpse:MonoBehaviour
 {
  readonly List<Rigidbody> bodies=new List<Rigidbody>(8);readonly List<BoxCollider> shapes=new List<BoxCollider>(8);readonly List<CharacterJoint> joints=new List<CharacterJoint>(7);readonly List<Mesh> ownedMeshes=new List<Mesh>();readonly List<Renderer> renderers=new List<Renderer>();
  R24RagdollSource source;PhysicsMaterial contact;Vector3 origin;double age;bool paused,retired;Vector3[] savedLinear,savedAngular;bool[] savedKinematic;Transform skin;Transform[] bones;Matrix4x4[] offsets;int[] boneBodies;
  internal bool Retired{get{return retired;}}internal int BodyCount{get{return bodies.Count;}}internal double Age{get{return age;}}internal bool Paused{get{return paused;}}
  internal Rigidbody Body(int index){return bodies[index];}internal IReadOnlyList<CharacterJoint> Joints{get{return joints;}}
  internal IReadOnlyList<Renderer> Renderers{get{return renderers;}}internal Transform[] SkinBones{get{return bones;}}internal Matrix4x4[] SkinOffsets{get{return offsets;}}internal int[] SkinBodyIndices{get{return boneBodies;}}
  internal void Initialize(R24RagdollSource actual,Vector3 at,Vector3 impulse)
  {
   if(source!=null||actual==null)throw new InvalidOperationException("One actual source corpse owner");source=actual;origin=at;var data=actual.Geometry.ForVariant(actual.Variant).Source;
   contact=new PhysicsMaterial("Source corpse friction.75/bounce.02"){dynamicFriction=.75f,staticFriction=.75f,bounciness=.02f,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Maximum};
   foreach(var definition in data.bodies){var node=new GameObject("Source rigid "+definition.name);node.layer=26;node.transform.SetParent(transform,false);Matrix4x4 local=R24EnemySourceGeometry.Matrix(definition.local);node.transform.localPosition=local.GetColumn(3);node.transform.localRotation=local.rotation;var body=node.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.mass=definition.mass;
    // Source COMBINE damping also includes the unmodified Godot world defaults
    // .1/.1. Source project leaves those defaults intact. Solver cadence differs.
    body.linearDamping=definition.linearDamp+.1f;body.angularDamping=definition.angularDamp+.1f;body.maxAngularVelocity=9;body.maxLinearVelocity=12;body.interpolation=RigidbodyInterpolation.None;
    var shape=node.AddComponent<BoxCollider>();shape.size=new Vector3(definition.size[0],definition.size[1],definition.size[2]);shape.sharedMaterial=contact;shape.includeLayers=R24RagdollRules.WorldMask;shape.excludeLayers=~R24RagdollRules.WorldMask;shape.layerOverridePriority=1;bodies.Add(body);shapes.Add(shape);
   }
   Vector3[] anchors=new Vector3[data.joints.Length];Quaternion[] frames=new Quaternion[data.joints.Length];for(int i=0;i<data.joints.Length;i++){var local=R24EnemySourceGeometry.Matrix(data.joints[i].local);anchors[i]=transform.TransformPoint(local.GetColumn(3));frames[i]=transform.rotation*local.rotation;}
   if(actual.Variant==0||actual.Variant==2)ConfigureHuman(anchors,frames);
   for(int i=0;i<data.joints.Length;i++){var definition=data.joints[i];if(definition.a<0||definition.b<0||definition.a>=bodies.Count||definition.b>=bodies.Count||definition.a==definition.b)throw new InvalidOperationException("Source joint ownership");var child=bodies[definition.b];var joint=child.gameObject.AddComponent<CharacterJoint>();joint.connectedBody=bodies[definition.a];joint.autoConfigureConnectedAnchor=false;joint.anchor=child.transform.InverseTransformPoint(anchors[i]);joint.connectedAnchor=joint.connectedBody.transform.InverseTransformPoint(anchors[i]);joint.axis=child.transform.InverseTransformDirection(frames[i]*Vector3.right);joint.swingAxis=child.transform.InverseTransformDirection(frames[i]*Vector3.up);joint.lowTwistLimit=new SoftJointLimit{limit=-definition.twist*Mathf.Rad2Deg};joint.highTwistLimit=new SoftJointLimit{limit=definition.twist*Mathf.Rad2Deg};joint.swing1Limit=joint.swing2Limit=new SoftJointLimit{limit=definition.swing*Mathf.Rad2Deg};joint.enableCollision=false;joints.Add(joint);}
   CopySourceMeshes();if(actual.Model!=null)CopyContinuousSkin(data.bones);
   Physics.SyncTransforms();foreach(var body in bodies){body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;}
   if(!Finite(impulse))impulse=Vector3.zero;bodies[0].AddForce(Vector3.ClampMagnitude(impulse,12)+Vector3.up*1.4f,ForceMode.Impulse);bodies[0].AddTorque(new Vector3(-.6f,-.1f,.4f),ForceMode.Impulse);
  }
  Transform ActualBone(string name){if(source.Bones==null)throw new InvalidOperationException("Source skeleton required");foreach(var bone in source.Bones)if(bone.name==name)return bone;throw new InvalidOperationException("Source bone missing "+name);}
  void Segment(int index,Vector3 start,Vector3 finish,float width,float depth)
  {Vector3 direction=finish-start;if(direction.sqrMagnitude<.00000001f)throw new InvalidOperationException("Source nonzero rigid segment");bodies[index].transform.SetPositionAndRotation((start+finish)*.5f,Quaternion.FromToRotation(Vector3.up,direction.normalized));shapes[index].size=new Vector3(width,Mathf.Max(.1f,direction.magnitude*.85f),depth);}
  static void JointFrame(Vector3[] anchors,Quaternion[] frames,int index,Vector3 at,Vector3 direction){anchors[index]=at;frames[index]=Quaternion.FromToRotation(Vector3.right,direction.normalized);}
  void ConfigureHuman(Vector3[] anchors,Quaternion[] frames)
  {
   Vector3 hips=ActualBone("hips").position,chest=ActualBone("chest").position,head=ActualBone("head").position;Segment(0,hips,chest+Vector3.up*.11f,.36f,.28f);Segment(1,head-Vector3.up*.055f,head+Vector3.up*.13f,.19f,.22f);JointFrame(anchors,frames,0,ActualBone("neck").position,Vector3.up);
   foreach(string side in new[]{"R","L"}){int part=side=="R"?2:5,index=side=="R"?1:4;Vector3 shoulder=ActualBone("upper_arm_"+side).position,wrist=ActualBone("hand_"+side).position,hip=ActualBone("thigh_"+side).position,knee=ActualBone("shin_"+side).position,ankle=ActualBone("foot_"+side).position;Segment(part,shoulder,wrist,.14f,.16f);Segment(part+1,hip,knee,.16f,.19f);Segment(part+2,knee,ankle,.16f,.20f);JointFrame(anchors,frames,index,shoulder,wrist-shoulder);JointFrame(anchors,frames,index+1,hip,knee-hip);JointFrame(anchors,frames,index+2,knee,ankle-knee);}
  }
  void CopySourceMeshes()
  {
   // Preserve original MeshInstance ownership: nearest rigid body to the
   // original node origin, never to an arbitrary triangle/bucket centroid.
   var groups=new Dictionary<string,List<CombineInstance>>();var metadata=new Dictionary<string,R24EnemySourceGeometry.Part>();var bodyFor=new Dictionary<string,int>();
   foreach(var part in source.Geometry.ForVariant(source.Variant).Parts){var frame=source.Frames[part.Source.parent];if(frame==null)throw new InvalidOperationException("Actual visual attachment required");var world=frame.localToWorldMatrix*part.Local;Vector3 point=world.GetColumn(3);int closest=0;float best=float.PositiveInfinity;for(int i=0;i<bodies.Count;i++){float distance=(bodies[i].transform.position-point).sqrMagnitude;if(distance<best){closest=i;best=distance;}}string key=closest+"/"+part.GroupKey;List<CombineInstance> pieces;if(!groups.TryGetValue(key,out pieces)){pieces=new List<CombineInstance>();groups.Add(key,pieces);metadata.Add(key,part);bodyFor.Add(key,closest);}pieces.Add(new CombineInstance{mesh=part.Mesh,transform=bodies[closest].transform.worldToLocalMatrix*world});}
   foreach(var pair in groups){var part=metadata[pair.Key];var mesh=new Mesh{name="Source corpse original leaves "+pair.Key,indexFormat=IndexFormat.UInt32};ownedMeshes.Add(mesh);mesh.CombineMeshes(pair.Value.ToArray(),true,true,false);mesh.RecalculateBounds();var node=new GameObject(mesh.name);node.layer=R24ValleyBots.CombatLayer;node.transform.SetParent(bodies[bodyFor[pair.Key]].transform,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=source.Materials[part.Source.material];renderer.shadowCastingMode=(ShadowCastingMode)part.Source.shadows;Renderer actual;var block=new MaterialPropertyBlock();if(source.Renderers.TryGetValue(part.GroupKey,out actual)&&actual!=null)actual.GetPropertyBlock(block);
    bool optic=source.Variant==0||source.Variant==2?part.Source.material==3:part.Source.material==4;if(optic)block.SetColor("_EmissionColor",R24KestrelAssets.ShaderColor(new Color(1,.3f,.03f)*2.5f));renderer.SetPropertyBlock(block);renderers.Add(renderer);
   }
  }
  void CopyContinuousSkin(R24EnemySourceGeometry.BoneData[] links)
  {
   if(source.Bones==null||source.Bones.Length!=64||links.Length!=64)throw new InvalidOperationException("Source64bone continuous corpse");skin=UnityEngine.Object.Instantiate(source.Model.gameObject,transform,false).transform;skin.name="Original continuous GPU corpse skin";skin.SetPositionAndRotation(source.Model.position,source.Model.rotation);skin.localScale=source.Model.lossyScale;
   var byName=new Dictionary<string,Transform>();foreach(var node in skin.GetComponentsInChildren<Transform>(true)){node.gameObject.layer=R24ValleyBots.CombatLayer;byName[node.name]=node;}bones=new Transform[64];offsets=new Matrix4x4[64];boneBodies=new int[64];
   for(int i=0;i<64;i++){var link=links[i];Transform clone;if(source.Bones[i].name!=link.name||!byName.TryGetValue(link.name,out clone)||link.body<0||link.body>=bodies.Count||link.parent>=i)throw new InvalidOperationException("Source bone mapping/conservation");bones[i]=clone;boneBodies[i]=link.body;offsets[i]=bodies[link.body].transform.worldToLocalMatrix*source.Bones[i].localToWorldMatrix;}
   foreach(var renderer in skin.GetComponentsInChildren<SkinnedMeshRenderer>(true)){foreach(var bone in renderer.bones)if(bone==null||!bone.IsChildOf(skin))throw new InvalidOperationException("Cloned source skin must own clone bones");renderer.updateWhenOffscreen=true;Vector3 scale=renderer.transform.lossyScale;renderer.localBounds=new Bounds(renderer.transform.InverseTransformPoint(origin),new Vector3(86/Mathf.Abs(scale.x),86/Mathf.Abs(scale.y),86/Mathf.Abs(scale.z)));renderers.Add(renderer);}FollowSkin();
  }
  void FollowSkin(){if(bones==null||retired)return;for(int i=0;i<bones.Length;i++){if(bones[i]==null||bodies[boneBodies[i]]==null)return;var world=bodies[boneBodies[i]].transform.localToWorldMatrix*offsets[i];var local=bones[i].parent.worldToLocalMatrix*world;bones[i].localPosition=local.GetColumn(3);bones[i].localRotation=local.rotation;bones[i].localScale=local.lossyScale;}}
  void LateUpdate(){if(!paused)FollowSkin();}
#if UNITY_EDITOR
  public int FixtureFixedSteps {get;private set;}
#endif
  void FixedUpdate(){
#if UNITY_EDITOR
   FixtureFixedSteps++;
#endif
   if(paused||retired)return;foreach(var body in bodies)if(body!=null&&!body.isKinematic)body.AddForce(Vector3.down*9.8f,ForceMode.Acceleration);}
  internal void Step(float dt){if(retired||paused)return;if(Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");age+=dt;if(age>=12){Retire();return;}foreach(var body in bodies){if(body==null||!Finite(body.position)||(body.position-origin).sqrMagnitude>1600){Retire();return;}body.linearVelocity=Vector3.ClampMagnitude(body.linearVelocity,12);body.angularVelocity=Vector3.ClampMagnitude(body.angularVelocity,9);}}
  internal void SetPaused(bool value)
  {
   if(retired||paused==value)return;paused=value;if(value){savedLinear=new Vector3[bodies.Count];savedAngular=new Vector3[bodies.Count];savedKinematic=new bool[bodies.Count];for(int i=0;i<bodies.Count;i++){var body=bodies[i];savedLinear[i]=body.linearVelocity;savedAngular[i]=body.angularVelocity;savedKinematic[i]=body.isKinematic;body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;}}
   else{for(int i=0;i<bodies.Count;i++){var body=bodies[i];body.isKinematic=savedKinematic[i];if(!body.isKinematic){body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearVelocity=savedLinear[i];body.angularVelocity=savedAngular[i];}}savedLinear=savedAngular=null;savedKinematic=null;}
  }
  internal void Retire(){if(retired)return;retired=true;foreach(var shape in shapes)if(shape!=null)shape.enabled=false;foreach(var body in bodies)if(body!=null){body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;}gameObject.SetActive(false);foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);ownedMeshes.Clear();if(contact!=null){Destroy(contact);contact=null;}if(source!=null){source.Dispose();source=null;}Destroy(gameObject);}
  static bool Finite(Vector3 value){return !Single.IsNaN(value.x+value.y+value.z)&&!Single.IsInfinity(value.x+value.y+value.z);}
  void OnDestroy(){Retire();}
 }
}
#endif
