#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using UnityEngine;
// Coordinator-only. Requires an actual source168 head/scene approval binding.
// These are legacy native boundary regressions, not prepared-root certification.
public static class MotionPreparedOwnersR3NativeGates
{
    static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
    static void Check(bool b,string message){if(!b)throw new InvalidOperationException(message);}
    public static void AssertPhysicalAndPivotObservations(CharacterController body,UnityPlayerGeometry geometry,RootMotionProposal proposal)
    {
        Check(body!=null&&geometry!=null&&geometry.Approved,"actual approved native binding required");
        var before=body.transform.position;var rotation=body.transform.rotation;
        var result=new UnityExclusivePlayerMotor(body,new PlayerGeometryPreflight(geometry)).Apply(proposal,.02f);
        Check(result.HasPhaseFeedback,"scenario refused rather than exercising a physical solve");
        var actual=body.transform.position-before;
        var phases=new Vector3(result.AppliedPhysicalTranslation.X+result.AppliedPivotCorrection.X,result.AppliedPhysicalTranslation.Y+result.AppliedPivotCorrection.Y,result.AppliedPhysicalTranslation.Z+result.AppliedPivotCorrection.Z);
        Check((actual-phases).sqrMagnitude<1e-8f,"receipt differs from actual root");
        var expected=new Vector3(result.AppliedPhysicalTranslation.X,result.AppliedPhysicalTranslation.Y,result.AppliedPhysicalTranslation.Z)/.02f;
        if(result.CeilingBlocked&&expected.y>0)expected.y=0;
        Check((new Vector3(result.Velocity.X,result.Velocity.Y,result.Velocity.Z)-expected).sqrMagnitude<1e-6f,"pivot leaked into physical momentum");
        if(!result.Complete)Check(body.transform.rotation==rotation,"incomplete pivot changed yaw");
    }
    public static void AssertMovedStructureRefusesBeforeRoot(CharacterController body,UnityPlayerGeometry geometry,Float3 head,BoxCollider blocker,Vector3 blockingPosition)
    {
        Check(body!=null&&geometry!=null&&geometry.Approved&&blocker!=null&&blocker.enabled&&!blocker.isTrigger&&blocker.gameObject.layer==8,"real structural fixture required");
        var preflight=new PlayerGeometryPreflight(geometry);
        var proposal=RootMotionSafety.Plan(F(body.transform.position),head,new Float3(.1f,0,0),0,true);
        Check(preflight.Validate(proposal).Accepted,"initial scene must actually be clear");
        blocker.transform.position=blockingPosition;Physics.SyncTransforms();
        Check(!geometry.RootSweep(proposal.RootPosition,proposal.Translation).Accepted,"moved structure must obstruct actual sweep");
        var before=body.transform.position;var rotation=body.transform.rotation;
        var result=new UnityExclusivePlayerMotor(body,preflight).Apply(proposal,.02f);
        Check(!result.Complete&&body.transform.position==before&&body.transform.rotation==rotation,"moved geometry refusal changed root");
    }
}
#endif
