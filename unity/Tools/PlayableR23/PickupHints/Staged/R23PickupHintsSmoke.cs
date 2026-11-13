#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Integration.Playable.PickupHints;
using UnityEngine;
public static class R23PickupHintsSmoke
{
    static void Need(bool condition,string label){if(!condition)throw new Exception("pickup hints: "+label);}
    public static IEnumerator Run(Camera acceptedHead,Action<R23PickupHints> inspectOrCapture=null)
    {
        var host=new GameObject("Source pickup-hint fixture");var hints=host.AddComponent<R23PickupHints>();
        try
        {
            hints.Initialize(acceptedHead.transform);Vector3 left=acceptedHead.transform.position+acceptedHead.transform.forward*1.2f-acceptedHead.transform.right*.3f,right=left+acceptedHead.transform.right*.6f;
            hints.Present(true,new R23PickupHintTarget("fixture-source-cx4","CX4",left),new R23PickupHintTarget("fixture-source-soda","SODA",right));yield return null;
            var l=hints.Read(0);var r=hints.Read(1);Need(hints.VisibleCount==2&&l.TargetId=="fixture-source-cx4"&&r.TargetId=="fixture-source-soda","caller identities and physical hand split");Need(l.Center==left&&r.Center==right,"actual world centers retained");Need(l.Color==new Color(.25f,.85f,1)&&r.Color==new Color(1,.8f,.2f),"source cyan/gold hand palette");Need(l.RingVertices==576&&l.RingTriangles==288&&l.LabelGlyphs==3&&r.LabelGlyphs==4,"combined source boxes and30px glyphs");Need(hints.VisualRoot.GetComponentsInChildren<Collider>(true).Length==0,"no collider");Need(hints.VisualRoot.GetComponentsInChildren<MeshRenderer>(true).Length==4,"bounded two rings/two labels");
            if(inspectOrCapture!=null)inspectOrCapture(hints); // actual depth/stereo capture belongs to host.
            hints.Present(true,new R23PickupHintTarget(),new R23PickupHintTarget("fixture-source-soda","SODA",right));Need(hints.VisibleCount==1&&!hints.Read(0).Visible&&hints.Read(0).TargetId==null,"invalid target clears only its hand");
            hints.Present(false,new R23PickupHintTarget("fixture-source-cx4","CX4",left),new R23PickupHintTarget("fixture-source-soda","SODA",right));Need(hints.VisibleCount==0,"lobby/pause host gate clears both");
            Debug.Log("PASS source pickup hints native identity/palette/mesh/glyph/cleanup fixture. Actual occlusion/stereo image requires capture inspection.");
        }
        finally{host.SetActive(false);UnityEngine.Object.Destroy(host);}
    }
}
#endif
