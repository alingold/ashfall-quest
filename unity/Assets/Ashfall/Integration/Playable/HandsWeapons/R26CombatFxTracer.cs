#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public static class R26CombatFxTracer
    {
        // Two endpoint widths, no camera search, material clone, allocation or raycast.
        public static void Present(LineRenderer line,Vector3 eye)
        {
            line.startWidth=R26CombatFxTracerState.Width(Vector3.Distance(eye,line.GetPosition(0)));line.endWidth=R26CombatFxTracerState.Width(Vector3.Distance(eye,line.GetPosition(1)));
            line.startColor=Color.white;line.endColor=Color.white;
        }
    }
    public sealed partial class R23HandsWeapons
    {
        public Vector3 TracerViewerPosition{get{return head==null?transform.position:head.transform.position;}}
    }
}
#endif
