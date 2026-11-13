#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using UnityEngine;
namespace Ashfall.Modules.Player
{
    // One centrally bound writer. No Update, gravity, tracked-child or collider-shape writes.
    public sealed class UnityExclusivePlayerMotor:IExclusivePlayerMotor
    {
        readonly CharacterController controller;readonly Transform root;readonly IRootCollisionPreflight queries;
        public UnityExclusivePlayerMotor(CharacterController controller,IRootCollisionPreflight queries)
        {if(controller==null||queries==null)throw new ArgumentException("Approved controller/query binding required");this.controller=controller;root=controller.transform;this.queries=queries;}
        static Float3 F(Vector3 a) {return new Float3(a.x,a.y,a.z);}
        public PlayerMotorResult Apply(RootMotionProposal p,float seconds)
        {
            if(!PlayerInput.Finite(seconds)||seconds<=0||seconds>.1f||!controller.enabled||
                !RootMotionSafety.Approve(p,true,queries).Accepted||(root.position-U(p.RootPosition)).sqrMagnitude>1e-10f)return new PlayerMotorResult();
            RootMotionProposal physicalProposal=p;physicalProposal.PivotCorrection=new Float3();physicalProposal.YawRadians=0;
            if(!RootMotionSafety.Approve(physicalProposal,true,queries).Accepted)return new PlayerMotorResult();
            Vector3 before=root.position;
            CollisionFlags flags=controller.Move(U(p.Translation));Vector3 physical=root.position-before;
            Vector3 pivot=new Vector3();
            // Revalidate against the resolved physical position; a moving/changed backend
            // may deny pivot without hiding the already resolved physical translation.
            RootMotionProposal pivotProposal=p;pivotProposal.RootPosition=F(root.position);
            pivotProposal.HeadPosition=F(U(p.HeadPosition)+physical);pivotProposal.Translation=new Float3();
            bool pivotAuthorized=RootMotionSafety.Approve(pivotProposal,true,queries).Accepted;
            if(pivotAuthorized)
            {Vector3 pivotBefore=root.position;if(p.PivotCorrection.X!=0||p.PivotCorrection.Y!=0||p.PivotCorrection.Z!=0)controller.Move(U(p.PivotCorrection));pivot=root.position-pivotBefore;}
            PlayerMotorResult result=PlayerMotorFeedback.ResolvePhases(p,F(physical),F(pivot),seconds,
                (flags&CollisionFlags.Below)!=0,(flags&CollisionFlags.Above)!=0);
            result.Complete=result.Complete&&pivotAuthorized;
            // A partially applied correction cannot authorize the pivot.
            if(result.Complete)root.Rotate(0,p.YawRadians*Mathf.Rad2Deg,0,Space.World);
            return result;
        }
        static Vector3 U(Float3 a) {return new Vector3(a.X,a.Y,a.Z);}
    }
}
#endif
