#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.ConstructionRuntime.Engine;
public static class ConstructionOwnerNativeFixture
{
    // Root-only invocation under engine_lane.py; synthetic fixture, no map approval.
    public static void Run()
    {
        if(OwnerTests.Main()!=0||RetainedPreparedTests.Main()!=0)throw new Exception("CPU fixture failure");
        var parent=new GameObject("construction-owner-r5-fixture");
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)throw new Exception("installed URP Lit shader required");
        var material=new Material(shader);
        try {using(var view=new ConstructionView(parent.transform,material,material,material))
            {if(view.Apply(null))throw new Exception("null publication applied");}}
        finally {UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(parent);}
    }
}
#endif
