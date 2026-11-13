#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Optional original Godot body-group metadata. Default surfaces use the strict
    // nine-ray same-collider path. No color/name inference invents these source flags.
    public sealed class R23ImpactMarkSurface:MonoBehaviour
    {
        public bool RenderedBallisticSurface,PaintedBallisticSurface;
        public bool BallisticProxyIgnore,StairSupport,DamageReceiver,HitReceiver;
        public static bool ExcludesBallistic(Collider collider)
        {
            if(collider==null)return false;var source=collider.GetComponentInParent<R23ImpactMarkSurface>();
            return source!=null&&(source.BallisticProxyIgnore||source.StairSupport);
        }
    }
}
#endif
