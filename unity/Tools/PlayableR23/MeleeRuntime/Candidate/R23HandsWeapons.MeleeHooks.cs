#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        // Root resolves only actual map/constructed damage owners; practice
        // owners are resolved internally. Null means an ordinary solid surface.
        public Func<Collider,string> MeleeWorldTargetId;
        public Func<bool> MeleeWorldInputAllowed;
        public float ItemMovementMultiplier{get{return KnifeUsable?R24KnifeRuntime.MovementMultiplier:1;}} // Updated with accepted live melee dispatch, never a preference mutation.
    }
}
#endif
