#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.Exclusion;
namespace Ashfall.Integration.Playable.Climbing
{
    public sealed partial class R23HeadCollision
    {
        // Full xyz standing/crouched body column reaches the feet. LandingMotion
        // retains the supported-floor clamp used by ordinary locomotion.
        public bool PullBodyClear(Vector3 feet,Vector3 eye)
        {
            if(!Ready||!Finite(feet)||!Finite(eye))return false;
            Vector3 bottom,top;PullColumn(feet,eye,out bottom,out top);
            EnsureBodyProxy(new R25BodyColumnRules());
            try{
                bodyCapsule.radius=.17f;bodyCapsule.height=(top-bottom).magnitude+.34f;bodyCapsule.direction=1;
                bodyProxy.transform.SetPositionAndRotation((bottom+top)*.5f,Quaternion.identity);
                bodyCapsule.enabled=true;Physics.SyncTransforms();
                int count=Physics.OverlapCapsuleNonAlloc(bottom,top,.17f,eyeOverlaps,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore);
                if(count==eyeOverlaps.Length)return false;
                for(int i=0;i<count;i++){
                    var c=eyeOverlaps[i];if(!Relevant(c)||c==bodyCapsule)continue;
                    Vector3 away;float depth;
                    if(Physics.ComputePenetration(bodyCapsule,bodyProxy.position,Quaternion.identity,c,c.transform.position,c.transform.rotation,out away,out depth)&&depth>.0001f)return false;
                }
                return true;
            }finally{bodyCapsule.enabled=false;}
        }
        static void PullColumn(Vector3 feet,Vector3 eye,out Vector3 bottom,out Vector3 top)
        {
            bottom=new Vector3(eye.x,feet.y+.17f,eye.z);
            top=new Vector3(eye.x,Mathf.Max(bottom.y,eye.y-.17f),eye.z);
        }
        // World grapples sweep the entire body in xyz and slide along contact
        // planes, including floors, without consuming clearance below the feet.
        public Vector3 TraversalBodyMotion(Vector3 feet,Vector3 eye,Vector3 desired)
        {
            if(!Finite(desired)||!PullBodyClear(feet,eye))return Vector3.zero;
            desired=LandingMotion(feet,eye,desired);
            Vector3 bottom,top;PullColumn(feet,eye,out bottom,out top);
            Vector3 total=Vector3.zero,remaining=desired;
            bool backfaces=Physics.queriesHitBackfaces;
            try{
                Physics.queriesHitBackfaces=true;
                for(int pass=0;pass<4;pass++){
                    float length=remaining.magnitude;if(length<.000001f)break;
                    RaycastHit hit;
                    if(!NearestBodyHit(bottom+total,top+total,remaining/length,length+.009f,.17f,out hit)){total+=remaining;break;}
                    Vector3 advance=remaining*Mathf.Clamp01((hit.distance-.009f)/length);
                    total+=advance;remaining=Vector3.ProjectOnPlane(remaining-advance,hit.normal);
                }
                return PullBodyClear(feet+total,eye+total)?total:Vector3.zero;
            }finally{Physics.queriesHitBackfaces=backfaces;}
        }
        public Vector3 PullBodyMotion(Vector3 feet,Vector3 eye,Vector3 desired)
        {
            if(!PullBodyClear(feet,eye))return Vector3.zero;
            desired=LandingMotion(feet,eye,desired);
            float length=desired.magnitude;if(length<.000001f)return Vector3.zero;
            Vector3 bottom,top;PullColumn(feet,eye,out bottom,out top);
            bool backfaces=Physics.queriesHitBackfaces;
            try{
                Physics.queriesHitBackfaces=true;RaycastHit hit;
                Vector3 safe=NearestBodyHit(bottom,top,desired/length,length+.009f,.17f,out hit)?desired*Mathf.Clamp01((hit.distance-.009f)/length):desired;
                return PullBodyClear(feet+safe,eye+safe)?safe:Vector3.zero;
            }finally{Physics.queriesHitBackfaces=backfaces;}
        }
    }
}
#endif
