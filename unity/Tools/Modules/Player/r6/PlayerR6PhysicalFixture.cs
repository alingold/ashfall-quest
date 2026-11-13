#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Climbing.Engine;
using Ashfall.Integration.ClimbingMotor.Engine;
using UnityEngine;
using UnityEngine.SceneManagement;
// Real geometry fixture, never auto-created. Coordinator supplies an actual approval
// capability for the exact query revision; this source cannot mint one.
public sealed class PlayerR6PhysicalFixture:PlayerR6NativeGates.IFixture,IDesktopViewPort
{
 readonly IUnityConvexApproval approval;readonly long revision;
 Scene source,query;Mesh mesh;GameObject actor;Transform eye;
 public CharacterController Body {get;private set;}
 public UnityPlayerDriver Driver {get;private set;}
 public UnityApprovedConvexHeadQueries Head {get;private set;}
 public IDesktopViewPort View {get{return this;}}
 public float LastPitch {get;private set;}
 public Float3 HeadPosition {get{return F(eye.position);}}
 public PlayerR6PhysicalFixture(IUnityConvexApproval approval,long revision)
 {if(approval==null||revision<0)throw new ArgumentException("coordinator approval/revision required");this.approval=approval;this.revision=revision;}
 public void SetPitchRadians(float value){LastPitch=value;}
 static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
 static GameObject Make(Scene scene,string name,Vector3 at)
 {var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=at;return go;}
 public void Configure(string scenario)
 {
  if(source.IsValid())throw new InvalidOperationException("one sealed geometry scenario per fixture");
  source=SceneManager.CreateScene("PlayerR6-source-"+Guid.NewGuid());
  query=SceneManager.CreateScene("PlayerR6-query-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
  actor=Make(source,"fixture actor",Vector3.zero);actor.SetActive(false);actor.layer=10;
  Body=actor.AddComponent<CharacterController>();Body.radius=.3f;Body.height=1.8f;Body.center=new Vector3(0,.9f,0);Body.skinWidth=.02f;Body.slopeLimit=45;Body.minMoveDistance=0;
  var tracking=Make(source,"tracking",Vector3.zero).transform;tracking.SetParent(actor.transform,false);
  eye=Make(source,"tracked eye fixture",Vector3.zero).transform;eye.SetParent(tracking,false);eye.localPosition=new Vector3(scenario=="offset-head"?.2f:0,1.65f,0);
  Driver=actor.AddComponent<UnityPlayerDriver>();Driver.TrackingSpace=tracking;Driver.Head=eye;Driver.ExclusivePoseWriterConfirmed=true;Driver.enabled=false;
  if(scenario=="penetration")actor.transform.position=new Vector3(0,-.05f,0);
  if(scenario=="self-authored-capsule")
  {var child=Make(source,"forbidden extra capsule",Vector3.zero);child.transform.SetParent(actor.transform,false);child.AddComponent<CapsuleCollider>();}
  var bindings=new List<ConvexProxyBinding>();
  AddBox(bindings,"floor",new Vector3(0,-.5f,0),new Vector3(10,1,10));
  if(scenario=="wall"||scenario=="corner")AddBox(bindings,"wall-x",new Vector3(.35f,1,0),new Vector3(.1f,2,4));
  if(scenario=="corner")AddBox(bindings,"wall-z",new Vector3(0,1,.35f),new Vector3(4,2,.1f));
  if(scenario=="ceiling")AddBox(bindings,"ceiling",new Vector3(0,1.85f,0),new Vector3(4,.1f,4));
  var proxy=Make(query,"exact source head hull",Vector3.zero);proxy.layer=10;
  var rigid=proxy.AddComponent<Rigidbody>();rigid.isKinematic=true;rigid.useGravity=false;
  var hull=proxy.AddComponent<MeshCollider>();mesh=ExplicitConvexContext.CreateSourceMesh();hull.sharedMesh=mesh;hull.convex=true;
  actor.SetActive(true);Driver.BindLifecycle("player-r6-native-fixture","fixture actor",1);Driver.DesktopView=this;
  Physics.SyncTransforms();query.GetPhysicsScene().Simulate(.02f);
  var context=new ExplicitConvexContext(query,rigid,hull,actor.transform,revision,approval,bindings.ToArray());
  if(!context.Validate())throw new InvalidOperationException("fixture exact source context/layers invalid");
  var scene=new UnityConvexClimbScene(context);
  var collision=new ConvexClimbCollision(scene,new UnityRootYawHeadSource(actor.transform,new Float3()),new CollisionLayers{HeadMask=1<<8,StructureMask=1<<8,SupportMask=1<<8},.70710678f);
  Head=new UnityApprovedConvexHeadQueries(scene,collision);
  Driver.BindApprovedGeometry(Head,1<<8);
  if(scenario=="desktop-initialization")Driver.enabled=true;
 }
 void AddBox(List<ConvexProxyBinding> bindings,string id,Vector3 at,Vector3 size)
 {
  var original=Make(source,id,at);original.layer=8;var shape=original.AddComponent<BoxCollider>();shape.size=size;
  var copy=Make(query,id+" proxy",at);copy.layer=8;var proxy=copy.AddComponent<BoxCollider>();proxy.size=size;
  bindings.Add(new ConvexProxyBinding(shape,proxy,new ConvexBodyIdentity{BodyId="player-r6-fixture/"+id+"/body",OwnerId="player-r6-fixture/"+id,Approval="fixture geometry only",AuthoredClimbable=true}));
 }
 public void Dispose()
 {
  if(Driver!=null)Driver.enabled=false;
  if(!Application.isPlaying)
  {
   if(source.IsValid())foreach(var go in source.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(go);
   if(query.IsValid())foreach(var go in query.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(go);
  }
  if(source.IsValid())SceneManager.UnloadSceneAsync(source);
  if(query.IsValid())SceneManager.UnloadSceneAsync(query);
  if(mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}
 }
}
#endif
