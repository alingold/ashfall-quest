#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Godot Label3D BILLBOARD_FIXED_Y is evaluated for the rendering camera,
    // including a source-comparison/spectator camera, rather than only the HMD.
    public sealed class R24SourceLabelBillboard:MonoBehaviour
    {
        public Camera Fallback;
        void OnWillRenderObject()
        {
            var camera=Camera.current!=null?Camera.current:Fallback;if(camera==null)return;
            var away=transform.position-camera.transform.position;away.y=0;
            if(away.sqrMagnitude>.000001f)transform.rotation=Quaternion.LookRotation(away,Vector3.up);
        }
    }
}
#endif
