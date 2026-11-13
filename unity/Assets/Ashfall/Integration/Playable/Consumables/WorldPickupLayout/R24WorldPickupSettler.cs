#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
namespace Ashfall.Integration.Playable.Consumables.WorldPickupLayout
{
    public static class R24WorldPickupSettler
    {
        public static bool TryReadLowestVisibleMesh(Transform prop,out float lowest)
        {
            lowest=float.PositiveInfinity;if(prop==null)return false;
            foreach(var filter in prop.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(filter.sharedMesh==null||renderer==null||!renderer.enabled||filter.GetComponent<R24SettleIgnoreMesh>()!=null)continue;
                bool shown=true;for(Transform at=filter.transform;at!=null&&at!=prop;at=at.parent)if(!at.gameObject.activeSelf){shown=false;break;}if(!shown)continue;
                // Godot checks local MeshInstance visibility, not ancestor world visibility.
                if(filter.transform==prop&&!prop.gameObject.activeSelf)continue;
                var bounds=filter.sharedMesh.bounds;Vector3 min=bounds.min,max=bounds.max;var matrix=filter.transform.localToWorldMatrix;float meshLowest;
                if(!R24WorldPickupLayout.TryLowestCornerY(new Ashfall.Core.Float3(min.x,min.y,min.z),new Ashfall.Core.Float3(max.x,max.y,max.z),matrix.m10,matrix.m11,matrix.m12,matrix.m13,out meshLowest))return false;lowest=Mathf.Min(lowest,meshLowest);
            }
            return !Single.IsInfinity(lowest)&&!Single.IsNaN(lowest);
        }
        public static bool TryReadSettledPosition(Transform prop,Transform actor,out Vector3 position,bool wallMounted=false)
        {
            position=prop==null?Vector3.zero:prop.position;
            if(wallMounted||!Alive(prop)||!Finite(position))return false;
            RaycastHit closest=new RaycastHit();float distance=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(position+Vector3.up*R24WorldPickupLayout.RayUp,Vector3.down,R24WorldPickupLayout.RayLength,1,QueryTriggerInteraction.Ignore))
                if(hit.collider!=null&&(actor==null||!hit.collider.transform.IsChildOf(actor))&&hit.distance<distance){closest=hit;distance=hit.distance;}
            if(Single.IsInfinity(distance))return false;float lowest,settled;
            if(!TryReadLowestVisibleMesh(prop,out lowest)||!R24WorldPickupLayout.TrySettleY(position.y,closest.point.y,lowest,out settled))return false;
            position.y=settled;return true;
        }
        public static IEnumerator Defer(Transform prop,Transform actor,Func<bool> collisionReady,Func<bool> stillAvailable,Action<Vector3> acceptPosition,bool wallMounted=false,Action<R24PickupSettleOutcome> finished=null)
        {
            if(collisionReady==null||stillAvailable==null||acceptPosition==null)throw new ArgumentNullException("Actual map readiness, custody guard and accepted-state setter required");
            if(!Alive(prop)||wallMounted){if(finished!=null)finished(wallMounted?R24PickupSettleOutcome.WallMounted:R24PickupSettleOutcome.Cancelled);yield break;}var instance=prop.GetEntityId();
            while(Alive(prop)&&prop.GetEntityId().Equals(instance)&&stillAvailable())
            {
                while(!collisionReady()){if(!Alive(prop)||!prop.GetEntityId().Equals(instance)||!stillAvailable()){if(finished!=null)finished(R24PickupSettleOutcome.Cancelled);yield break;}yield return null;}
                yield return new WaitForFixedUpdate();
                if(!Alive(prop)||!prop.GetEntityId().Equals(instance)||!stillAvailable()){if(finished!=null)finished(R24PickupSettleOutcome.Cancelled);yield break;}if(!collisionReady())continue;
                Physics.SyncTransforms();Vector3 point;bool found=TryReadSettledPosition(prop,actor,out point);if(found&&stillAvailable())acceptPosition(point);else if(found){if(finished!=null)finished(R24PickupSettleOutcome.Cancelled);yield break;}
                if(finished!=null)finished(found?R24PickupSettleOutcome.Settled:R24PickupSettleOutcome.UnchangedNoHitOrMesh);yield break;
            }
            if(finished!=null)finished(R24PickupSettleOutcome.Cancelled);
        }
        static bool Alive(Transform prop){return prop!=null&&prop.gameObject.scene.IsValid()&&prop.gameObject.scene.isLoaded;}
        static bool Finite(Vector3 point){return new Ashfall.Core.Float3(point.x,point.y,point.z).IsFinite;}
    }
}
#endif
