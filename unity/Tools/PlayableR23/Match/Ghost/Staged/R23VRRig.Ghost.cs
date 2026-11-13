#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        bool sourceGhostMotion,ghostPriorHeadHull;int ghostPriorLayer;LayerMask ghostPriorInclude,ghostPriorExclude;
        public bool SourceGhostMotion{get{return sourceGhostMotion;}}
        // CharacterController remains enabled: tracked climbing still evaluates it.
        // The existing head hull owns all world collision; only source actor-role4
        // is removed from that hull's queries. No global Physics matrix mutation.
        public bool SetSourceGhostMotion(bool value,Func<Collider,bool> actualSourceActorLayer4)
        {
            if(value==sourceGhostMotion){if(value)HeadCollision.SetSourceGhostActorFilter(actualSourceActorLayer4);return true;}
            if(value&&(Body==null||HeadCollision==null||!HeadCollision.Ready||actualSourceActorLayer4==null))return false;
            DisarmMotion();if(Climbing!=null)Climbing.ResetMotion();if(Activities!=null)Activities.ResetCarrierMotion();ClearCarrierMotion();
            if(value)
            {
                ghostPriorLayer=Body.gameObject.layer;ghostPriorInclude=Body.includeLayers;ghostPriorExclude=Body.excludeLayers;ghostPriorHeadHull=UseSourceHeadHull;
                Body.gameObject.layer=2;Body.includeLayers=0;Body.excludeLayers=~0;UseSourceHeadHull=true;
                HeadCollision.SetSourceGhostActorFilter(actualSourceActorLayer4);sourceGhostMotion=true;
            }
            else
            {
                if(Body!=null){Body.gameObject.layer=ghostPriorLayer;Body.includeLayers=ghostPriorInclude;Body.excludeLayers=ghostPriorExclude;}
                if(HeadCollision!=null)HeadCollision.SetSourceGhostActorFilter(null);UseSourceHeadHull=ghostPriorHeadHull;sourceGhostMotion=false;
            }
            return true;
        }
    }
}
#endif
