#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    // Port of weapon_inventory.gd's reachable static drop search. No item or
    // inventory mutation occurs until the caller has a complete valid pose.
    public static class R23WorldItemPlacement
    {
        static bool Relevant(Collider collider,Transform player)
        {return collider!=null&&collider.gameObject.layer!=19&&collider.gameObject.layer!=20&&collider.gameObject.layer!=22&&collider.enabled&&!collider.isTrigger&&collider.transform!=player&&!collider.transform.IsChildOf(player);}
        public static bool TryDrop(Transform head,Transform player,Bounds localBounds,out Vector3 position,out Quaternion rotation,float hover=.75f-.3048f)
        {
            position=Vector3.zero;Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            rotation=Quaternion.LookRotation(forward,Vector3.up);var rotated=new Bounds(rotation*localBounds.center,Vector3.zero);
            for(int i=0;i<8;i++){Vector3 c=localBounds.center+Vector3.Scale(localBounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));rotated.Encapsulate(rotation*c);}
            if(rotated.size.sqrMagnitude<.000001f)return false;
            float[] angles={0,.25f,-.25f,.5f,-.5f,.8f,-.8f,0};Physics.SyncTransforms();
            for(int attempt=0;attempt<angles.Length;attempt++)
            {
                Vector3 direction=Quaternion.AngleAxis(-angles[attempt]*Mathf.Rad2Deg,Vector3.up)*forward;
                Vector3 start=head.position+direction*(attempt==7?.85f:1);RaycastHit floor=new RaycastHit();float nearest=float.PositiveInfinity;
                foreach(var hit in Physics.RaycastAll(start,Vector3.down,20,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(Relevant(hit.collider,player)&&hit.distance<nearest){nearest=hit.distance;floor=hit;}
                if(float.IsPositiveInfinity(nearest)||floor.normal.y<.65f)continue;
                Vector3 candidate=floor.point+Vector3.up*(hover-rotated.min.y);if(Vector3.Distance(head.position,candidate)>2.2f)continue;
                Vector3 segment=candidate-head.position;bool blocked=false;
                foreach(var hit in Physics.RaycastAll(head.position,segment.normalized,segment.magnitude,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(Relevant(hit.collider,player)){blocked=true;break;}
                if(blocked)continue;
                foreach(var collider in Physics.OverlapBox(candidate+rotated.center,rotated.extents+Vector3.one*.001f,Quaternion.identity,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(Relevant(collider,player)){blocked=true;break;}
                if(blocked)continue;position=candidate;return true;
            }
            return false;
        }
    }
}
#endif
