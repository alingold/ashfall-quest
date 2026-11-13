#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Modules.Player;
// Root-callable only in an isolated empty default physics scene, on its serialized lane.
// Never invoked by the leaf. Throws on failure; root owns recording and cleanup.
public static class NativeRootOutcomeFixtures
{
 static void Check(bool v,string label){if(!v)throw new Exception(label);}
 static RootMotionProposal Proposal(CharacterController b,float yaw)
 {var p=b.transform.position;return RootMotionSafety.Plan(new Float3(p.x,p.y,p.z),new Float3(p.x+.3f,p.y+1.7f,p.z),new Float3(.1f,0,.2f),yaw,true);}
 public static void RunEmptyEnvelope()
 {
  var g=new GameObject("NativeRootOutcomeR1 isolated fixture");g.transform.position=new Vector3(10000,10000,10000);
  var b=g.AddComponent<CharacterController>();b.minMoveDistance=0;
  var o=NativeRootPublication.CreateIsolated(b);
  try
  {
   Check(object.ReferenceEquals(o,NativeRootPublication.CreateIsolated(b)),"one owner per actual body");
   NativeRootPublication.Prepared p,q;NativeRootPublication.Receipt r,s;
   Check(!o.PrepareProduction(out p).Accepted&&p==null,"production remains refused");
   Check(o.PrepareIsolated(Proposal(b,.2f),.02f,out p).Accepted,"prepare actual body");
   Check(!o.PrepareIsolated(Proposal(b,0),.02f,out q).Accepted,"exclusive reservation");
   b.radius+=.01f;Check(o.Publish(p,out r)==NativeRootStatus.Refused&&r==null,"hull drift before writes");p.Dispose();
   Check(o.PrepareIsolated(Proposal(b,0),.02f,out p).Accepted,"retry after abort");
   var obstacle=new GameObject("moving geometry");var box=obstacle.AddComponent<BoxCollider>();box.transform.position=g.transform.position;
   Physics.SyncTransforms();Check(o.Publish(p,out r)==NativeRootStatus.Refused&&r==null,"geometry entering reserved envelope");
   UnityEngine.Object.DestroyImmediate(obstacle);p.Dispose();Physics.SyncTransforms();
   Check(o.PrepareIsolated(Proposal(b,.2f),.02f,out p).Accepted,"prepare physical plus pivot");
   var before=g.transform.position;
   Check(o.Publish(p,out r)==NativeRootStatus.AppliedPendingAccounting&&r!=null,"irreversible typed result");
   Check(r.PoseKnown&&r.PhysicalFlagsKnown&&r.Phase==NativeRootPhase.Completed,"exact completed native phases");
   Check((new Vector3(r.PhysicalDelta.X+r.PivotDelta.X,r.PhysicalDelta.Y+r.PivotDelta.Y,r.PhysicalDelta.Z+r.PivotDelta.Z)-(g.transform.position-before)).sqrMagnitude<1e-10f,"phase conservation");
   Check(object.ReferenceEquals(r,o.PendingResult),"retained exact receipt");
   Check(o.Publish(p,out s)==NativeRootStatus.Refused&&s==null,"no physical replay");
   Check(!o.Account(r).Accepted&&!o.Account(null).Accepted,"no boolean or null accounting");
   Check(!o.PrepareIsolated(Proposal(b,0),.02f,out q).Accepted,"pending blocks next motion");
   b.enabled=false;o.Dispose();Check(object.ReferenceEquals(r,o.PendingResult),"disable/dispose retention");
   var terminal=o.Retire(NativeRootTerminalReason.UnrecoverableAccounting);
   Check(terminal!=null&&object.ReferenceEquals(terminal.Result,r),"typed terminal retains result");
   Check(object.ReferenceEquals(terminal,o.Retire(NativeRootTerminalReason.OwnerRetired)),"idempotent terminal accounting");
  }
  finally{o.Dispose();UnityEngine.Object.DestroyImmediate(g);}
 }
 public static IEnumerator Expiry()
 {
  var g=new GameObject("NativeRootOutcomeR1 frame expiry");g.transform.position=new Vector3(20000,20000,20000);
  var b=g.AddComponent<CharacterController>();var o=NativeRootPublication.CreateIsolated(b);
  NativeRootPublication.Prepared p;Check(o.PrepareIsolated(Proposal(b,0),.02f,out p).Accepted,"expiry prepare");
  yield return null;
  try{NativeRootPublication.Receipt r;Check(o.Publish(p,out r)==NativeRootStatus.Refused&&r==null,"actual Unity frame expiry");}
  finally{p.Dispose();o.Dispose();UnityEngine.Object.DestroyImmediate(g);}
 }
}
#endif
