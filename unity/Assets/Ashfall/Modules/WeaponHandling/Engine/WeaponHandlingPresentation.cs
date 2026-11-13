#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Modules.WeaponHandling.Engine
{
    // Coordinator binds authored visual children only. No root, collider, tracked
    // controller/head, or XR camera transform is writable by this adapter.
    public sealed class WeaponHandlingPresentation : MonoBehaviour
    {
        [SerializeField] Transform recoilVisual=null;
        [SerializeField] Transform boltVisual=null;
        [SerializeField] GameObject magazineVisual=null, offeredMagazine=null, heldMagazine=null;
        [SerializeField] GameObject insertGuide=null, rackGuide=null;
        Vector3 restPosition, boltRest;
        Quaternion restRotation;
        bool ready;
        void Awake()
        {
            // Refuse obvious authority bindings; visual nodes must be descendants.
            ready = recoilVisual != null && recoilVisual != transform && recoilVisual.IsChildOf(transform)
                && (boltVisual == null || (boltVisual != transform && boltVisual != recoilVisual && boltVisual.IsChildOf(transform)));
            if (!ready) { enabled = false; return; }
            restPosition = recoilVisual.localPosition; restRotation = recoilVisual.localRotation;
            if (boltVisual != null) boltRest = boltVisual.localPosition;
        }
        public void Present(HandlingSnapshot state, bool inputAvailable, bool supportTracked, int reserveRounds)
        {
            if (!ready) return;
            recoilVisual.localPosition = restPosition + Vector3.back * state.RecoilDistance;
            recoilVisual.localRotation = restRotation * Quaternion.Euler(-state.RecoilPitch * Mathf.Rad2Deg, -state.RecoilYaw * Mathf.Rad2Deg, 0);
            if (boltVisual != null) boltVisual.localPosition = boltRest + Vector3.back * state.BoltTravel;
            Show(magazineVisual, state.Phase != ReloadPhase.NeedsMagazine && state.Phase != ReloadPhase.HeldMagazine);
            Show(offeredMagazine, inputAvailable && state.Phase == ReloadPhase.NeedsMagazine && reserveRounds != 0);
            Show(heldMagazine, inputAvailable && supportTracked && state.Phase == ReloadPhase.HeldMagazine);
            Show(insertGuide, inputAvailable && (state.Phase == ReloadPhase.NeedsMagazine || state.Phase == ReloadPhase.HeldMagazine));
            Show(rackGuide, inputAvailable && (state.Phase == ReloadPhase.NeedsRack || state.Phase == ReloadPhase.NeedsBolt));
        }
        void OnDisable()
        {
            if (!ready) return;
            recoilVisual.localPosition = restPosition; recoilVisual.localRotation = restRotation;
            if (boltVisual != null) boltVisual.localPosition = boltRest;
            Show(offeredMagazine,false); Show(heldMagazine,false); Show(insertGuide,false); Show(rackGuide,false);
        }
        static void Show(GameObject obj, bool value) { if (obj != null) obj.SetActive(value); }
    }
}
#endif
