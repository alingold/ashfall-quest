#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Core;
using Ashfall.Integration.Playable.Vehicles;
namespace Ashfall.Integration.Playable.Climbing
{
    public sealed partial class R23HeadCollision
    {
        // Bound each capsule advance at high speed; each segment still sweeps its entire length.
        // Reuse the common filtered/backface-aware capsule query without touching Exclusion ownership.
        public Vector3 BikeMotion(Vector3 center,Vector3 forward,Vector3 desired)
        {
            if(!Ready||!Finite(center)||!Finite(desired))return Vector3.zero;
            int steps=Mathf.Max(1,Mathf.CeilToInt(desired.magnitude/.25f));
            if(steps>256)return Vector3.zero;
            Vector3 total=Vector3.zero,step=desired/steps;
            for(int i=0;i<steps;i++){
                Vector3 safe=VehicleMotion(center+total,forward,step);total+=safe;
                if((safe-step).sqrMagnitude>.00000001f)break;
            }
            return total;
        }
        readonly Collider[] helicopterOverlaps=new Collider[96];
        readonly RaycastHit[] helicopterHits=new RaycastHit[96];
        public bool HelicopterGround(Vector3 at,out RaycastHit floor)
        {return RayQuery(at+Vector3.up*.2f,at+Vector3.down*200,true,out floor)&&floor.normal.y>=FloorNormal;}
        public bool HelicopterClear(Vector3 root,float yaw)
        {return HelicopterVolumeClear(root,yaw,false)&&HelicopterVolumeClear(root,yaw,true);}
        static Vector3 HVector(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        bool HelicopterVolumeClear(Vector3 root,float yaw,bool rotor)
        {return HelicopterBoxClear(HVector(R25HelicopterClearance.Center(new Float3(root.x,root.y,root.z),yaw,rotor?1:0)),HVector(R25HelicopterClearance.Half(rotor?1:0)),Quaternion.Euler(0,yaw,0));}
        bool HelicopterBoxClear(Vector3 center,Vector3 half,Quaternion q)
        {
            int count=Physics.OverlapBoxNonAlloc(center,half,helicopterOverlaps,q,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore);
            if(count==helicopterOverlaps.Length)return false;
            for(int i=0;i<count;i++)if(Relevant(helicopterOverlaps[i]))return false;
            return true;
        }
        public Vector3 HelicopterMotion(Vector3 root,float yaw,Vector3 desired)
        {
            if(!Ready||!Finite(root)||!Finite(desired))return Vector3.zero;
            var q=Quaternion.Euler(0,yaw,0);float length=desired.magnitude;if(length<.000001f)return Vector3.zero;
            bool backfaces=Physics.queriesHitBackfaces;
            try{
                Physics.queriesHitBackfaces=true;
                return HVector(R25HelicopterClearance.Sweep(new Float3(root.x,root.y,root.z),yaw,new Float3(desired.x,desired.y,desired.z),
                    delegate(Float3 c,Float3 h){return HelicopterBoxClear(HVector(c),HVector(h),q);},
                    delegate(Float3 c,Float3 h,Float3 d){
                        int count=Physics.BoxCastNonAlloc(HVector(c),HVector(h),HVector(d)/length,helicopterHits,q,length+.02f,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore);
                        if(count==helicopterHits.Length)return 0;
                        float f=1;for(int i=0;i<count;i++)if(Relevant(helicopterHits[i].collider))f=Mathf.Min(f,Mathf.Clamp01((helicopterHits[i].distance-.02f)/length));return f;
                    }));
            }finally{Physics.queriesHitBackfaces=backfaces;}
        }

    }
}
#endif
