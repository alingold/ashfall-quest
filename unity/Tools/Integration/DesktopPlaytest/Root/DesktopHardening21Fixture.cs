#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Hud;
using UnityEngine;
public static class DesktopHardening21Fixture
{
    static void Need(bool value,string label){if(!value)throw new Exception("Desktop hardening: "+label);}
    public static IEnumerator Run()
    {
        foreach(var size in new[]{new Vector2Int(320,240),new Vector2Int(640,480),new Vector2Int(1280,800),new Vector2Int(3840,2160),new Vector2Int(800,1200)})
        {
            float scale=DesktopHud.ScaleFor(size.x,size.y);float w=size.x/scale,h=size.y/scale;
            Rect play=DesktopHud.PlayingPanelFor(w,h);Need(!play.Contains(new Vector2(w/2,h/2)),"playing panel leaves aim clear "+size);
            Need(play.xMin>=0&&play.yMin>=0&&play.xMax<=w&&play.yMax<=h,"playing panel bounds");
        }
        foreach(string phase in new[]{"Pawn","Combat","Construction","Hud"})
        {
            var host=new GameObject("Root startup fault / "+phase);host.SetActive(false);
            var bootstrap=host.AddComponent<DesktopPlaytestBootstrap>();bootstrap.NativeFailureAfter=phase;host.SetActive(true);
            var context=bootstrap.Context;
            Need(context!=null&&context.Retired&&context.Paused,"failure retires/pauses context "+phase);
            foreach(Transform child in host.transform)Need(!child.gameObject.activeSelf,"partial module immediately disabled");
            Need(context.Controller==null||!context.Controller.enabled,"failed pawn collision withdrawn");
            Need(context.View==null||!context.View.enabled,"failed pawn camera withdrawn");
            int resets=0;context.ResetRequested+=delegate{resets++;};context.RequestReset();Need(resets==0,"retired context ignores future reset");
            bootstrap.Awake();Need(System.Object.ReferenceEquals(context,bootstrap.Context),"repeated Awake cannot restart failed bootstrap");
            yield return null;yield return null;
            Need(context.PlayerRoot==null&&context.Controller==null&&context.View==null&&context.Targets.Count==0,"deferred owned cleanup "+phase);
            UnityEngine.Object.Destroy(host);yield return null;
        }
        Debug.Log("PASS desktop hardening: five viewport aim regions and four injected startup cleanup paths");
    }
}
#endif
