#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Modules.Presentation;
using Ashfall.Integration.PersonalSettings;
using Ashfall.Integration.PersonalSettings.Engine;
using Ashfall.Modules.HealthStates;
using Ashfall.Core;
namespace Ashfall.Tools.TargetFeedbackEvents
{
 // Compiled only. Root invokes under engine_lane; no automatic import activation.
 public static class NativeGates
 {
  static void Check(bool b,string m) { if(!b)throw new Exception(m); }
  public static void Run()
  {
   var root=new GameObject("QuarantinedTargetFeedbackR10");
   try {
    var cameraObject=new GameObject("camera");cameraObject.transform.SetParent(root.transform);var camera=cameraObject.AddComponent<Camera>();
    var target=new GameObject("canonicalAnchor");target.transform.SetParent(root.transform);target.transform.position=new Vector3(1,2,10);
    var labels=new TextMesh[48];for(int i=0;i<48;i++){var go=new GameObject("preallocated"+i);go.transform.SetParent(root.transform);labels[i]=go.AddComponent<TextMesh>();}
    var hud=new HudModel();hud.Update(new HudInput(),false,0);var view=new NativeTargetFeedbackView(hud);
    view.Emit("t",1,false);view.BindResources(root.transform,camera,labels);view.BindTargetAnchor("t",target.transform);
    foreach(var l in labels)Check(!l.GetComponent<Renderer>().enabled,"inert binding");
    var world=new Host();var hp=new HealthService("native-r10",0,0,true,world,world,world);
    Check(hp.Register(new CommandContext("native-r10","t","reg",1,0,true),"t",-1,false).Accepted,"register");hp.DrainTransitions();
    var binding=new TargetFeedbackBinding(hp,"native-r10",0,"t",hud,view);
    for(int i=0;i<3;i++){
     var before=hp.Read("t");Check(hp.ApplyLoss(new CommandContext("native-r10","t","hit"+i,2+i,0,true),"t",before.Revision,i==0?7:i==1?.25f:2.5f,0,false).Accepted,"loss");
     foreach(var e in hp.DrainTransitions()){binding.ObserveTransition(e);if(i>0){binding.OnAuthorizedOutgoingTransition(e);binding.OnAuthorizedOutgoingTransition(e);}}
    }
    Check(labels[0].text=="1"&&labels[1].text=="3"&&labels[2].text=="","foreign excluded and exact per-event duplicate-safe");
    Check(labels[0].color==new Color(1,240/255f,173/255f,1),"health color");
    Vector3 start=labels[0].transform.position;
    Check(Vector3.Distance(start,new Vector3(1.55f,2.10f,10))<.0001f,"source event position");
    target.transform.position+=Vector3.right*20;view.Refresh(.64f);
    Check(Vector3.Distance(labels[0].transform.position,start+Vector3.up*.0896f)<.0001f,"detached event rise");
    Check(Math.Abs(labels[0].color.a-.21f/.22f)<.0001f,"source fade");
    view.Refresh(.22f);Check(labels[0].text==""&&!labels[1].GetComponent<Renderer>().enabled,".85 expiry");
    view.Emit("t",.1f,true);Check(labels[0].color==new Color(78/255f,216/255f,1,1),"shield color");
    hud.Update(new HudInput(),true,0);hud.Update(new HudInput(),false,0);view.Emit("t",4,false);Check(labels[0].text=="4"&&labels[1].text=="","reset before next emit");
    view.Clear();view.Refresh(0);Check(labels[0].text=="","clear no reappearance");
    for(int i=0;i<49;i++)view.Emit("t",i+1,false);Check(labels[0].text=="49"&&labels[1].text=="2","48 bound oldest eviction");
    camera.enabled=false;view.Refresh(0);Check(labels[0].text=="","disabled camera clear");camera.enabled=true;view.Refresh(0);Check(labels[0].text=="","no camera reappearance");
    view.Emit("unknown",1,false);Check(labels[0].text=="","unknown inert");
    view.Emit("t",1,false);target.SetActive(false);view.Refresh(0);target.SetActive(true);view.Refresh(0);Check(labels[0].text=="","retired anchor event never reappears");
    var originalParent=labels[0].transform.parent;labels[0].transform.SetParent(null);view.Refresh(0);labels[0].transform.SetParent(originalParent);view.Refresh(0);Check(labels[0].text=="","resource provenance loss clears");
    Exception workerError=null;var worker=new System.Threading.Thread(()=>{try{view.Emit("t",1,false);}catch(Exception ex){workerError=ex;}});worker.Start();worker.Join();Check(workerError is InvalidOperationException,"main lane");
    view.Dispose();view.Dispose();view.Emit("t",1,false);Check(labels[0].text=="","dispose terminal");
    var legacy=new NativeTargetFeedbackView(hud);legacy.BindTarget("first",labels[0]);legacy.BindTarget("second",labels[1]);legacy.Emit("second",.2f,false);Check(labels[1].text=="1","legacy designated label");legacy.Emit("second",2.5f,false);Check(labels[1].text=="1"&&labels[0].text=="3","legacy concurrent bounded resources");legacy.Dispose();
    var rejected=new NativeTargetFeedbackView(hud);bool invalid=false;try{rejected.BindResources(root.transform,camera,new[]{labels[0],labels[0]});}catch(ArgumentException){invalid=true;}Check(invalid,"duplicate resource rejected");rejected.BindResources(root.transform,camera,new[]{labels[0]});rejected.Dispose();
   } finally { UnityEngine.Object.DestroyImmediate(root); }
  }
  sealed class Host:IAuthorityGate,IHealthWorld,IInteractionQuery
  {
   public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
   public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("fixture");}
   public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}
   public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return false;}
  }
 }
}
#endif

