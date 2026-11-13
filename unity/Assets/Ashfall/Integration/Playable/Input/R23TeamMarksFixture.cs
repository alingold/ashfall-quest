#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
namespace Ashfall.Integration.Playable.Input
{
    // Root may yield this in its admitted native smoke. Uses an actual loaded
    // world ray, original production physics, source image decode and owned voice.
    // It does not report controller delivery, listening or network verification.
    public static class R23TeamMarksFixture
    {
        public static IEnumerator Run(Transform head,Transform actor,Ray actualWorldRay,Action<string> report=null,Func<string,IEnumerator> capture=null)
        {
            var go=new GameObject("Isolated actual-source local marks smoke");var marks=go.AddComponent<R23TeamMarks>();
            try
            {
                marks.Initialize(head,actor);Require(marks.Ready,"source render resources ready: "+marks.Failure);
                Require(marks.SourceAudioReady,"original17280 mono frames decoded");
                marks.Sample(0,false,Vector2.zero,actualWorldRay,true,actor.position);
                marks.Sample(.05f,true,Vector2.zero,actualWorldRay,true,actor.position);
                Require(marks.InputHeld&&!marks.WheelOpen,"press captured before threshold");
                marks.Sample(.06f,true,Vector2.up,actualWorldRay,true,actor.position);
                Require(marks.WheelOpen&&marks.SelectedIndex==0,"hold opens and raw stick chooses HOLD");
                if(capture!=null)yield return capture("source-team-mark-wheel-hold");
                // Release along an entirely different ray: mark must still use
                // the original hit. The second source shortcut below independently
                // re-queries the original ray to test the retained capture.
                Ray moved=new Ray(actualWorldRay.origin,-actualWorldRay.direction);
                Require(marks.Sample(0,false,Vector2.zero,moved,true,actor.position),"release creates actual world mark");
                var first=marks.Current;Require(first!=null&&first.Index==0&&!first.Networked,"immutable HOLD local receipt");
                Require(marks.TryPlace(actualWorldRay),"source support shortcut queries actual world");
                Require(Vector3.Distance(first.Position,marks.Current.Position)<.001f,"release target was captured on press, not new backward ray");
                Require(first.Id!=marks.Current.Id&&marks.Current.Index==0,"single-current replacement and preferred action");
                Require(Vector3.Distance(marks.IconWorldPosition,marks.Current.Position+marks.Current.Normal.normalized*.035f+Vector3.up*.5f)<.001f,"source world offset and raised icon");
                if(report!=null)report("TeamMarksActualWorld: source press/release query, wheel, single-current receipt, actual PCM decode PASS; output/listening/device delivery unclaimed");
                if(capture!=null)yield return capture("source-team-mark-world-column");
                marks.Sample(.11f,true,Vector2.down,actualWorldRay,true,actor.position);
                Require(marks.WheelOpen&&marks.SelectedIndex==4,"target wheel selection");
                Require(marks.Sample(0,false,Vector2.zero,actualWorldRay,true,actor.position)&&marks.Current.SurfaceDecal,"target creates source paint mark");
                Require(marks.VisibleColumnDots==0,"source target has no dotted column");
                Require(Vector3.Distance(marks.IconWorldPosition,marks.Current.Position+marks.Current.Normal.normalized*.004f)<.001f,"source paint offset");
                marks.Tick(0,true);Require(!marks.MarkVisible,"tactical map hides icon");marks.Tick(0,false);
                marks.Sample(.11f,true,Vector2.left,actualWorldRay,true,actor.position);marks.CancelInput();
                Require(!marks.WheelOpen&&!marks.InputHeld&&marks.Current!=null,"pause cancels gesture while retaining mark");
                marks.Sample(1,true,Vector2.right,actualWorldRay,true,actor.position);Require(!marks.InputHeld,"held after cancellation requires neutral");
                marks.Tick(30.01f);Require(marks.Current==null&&!marks.MarkVisible,"unscaled lifetime expires native visual");
                marks.ClearAll();Require(!marks.WheelOpen&&marks.Current==null,"reset cleanup");
                if(report!=null)report("TeamMarksNativeLifecycle: target decal offsets, tactical visibility, pause neutral, TTL and reset PASS");
            }
            finally{marks.ClearAll();UnityEngine.Object.Destroy(go);}
        }
        static void Require(bool condition,string label){if(!condition)throw new InvalidOperationException("TeamMarks fixture: "+label);}
    }
}
#endif
