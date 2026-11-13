#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        public Transform SourceLeftGloveRoot{get{return leftVisual;}}
        public Transform SourceRightGloveRoot{get{return rightVisual;}}
        // practice_dummy.gd layer4; Unity uses explicit registered torso/head
        // ownership, not a broad layer0/Rigidbody guess that would remove maps.
        public bool IsSourceActorLayer4(Collider candidate)
        {if(candidate==null)return false;foreach(var target in targets)if(target!=null&&target.Owns(candidate))return true;return false;}
    }
}
#endif
