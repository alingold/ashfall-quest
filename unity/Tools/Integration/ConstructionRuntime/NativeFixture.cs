#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.ConstructionRuntime;
using Ashfall.Integration.ConstructionRuntime.Engine;
// Coordinator invokes only inside serialized engine_lane.py. No auto initializer.
// Native resource census must run after deferred Unity destruction has settled.
public static class ConstructionNativeFixture
{
    public static void Run(ConstructionPublication actualPublication,Material panel,Material valid,Material invalid)
    {
        if(actualPublication==null||actualPublication.Panels.Length==0)throw new ArgumentException("real_host_publication_required");
        var root=new GameObject("Construction native fixture");
        ConstructionView view=null;
        try
        {
            view=new ConstructionView(root.transform,panel,valid,invalid);
            if(!view.Apply(actualPublication)||view.Apply(actualPublication))throw new Exception("publication_serial_gate");
            if(root.GetComponentsInChildren<MeshCollider>().Length!=actualPublication.Panels.Length)throw new Exception("collider_count");
            foreach(var collider in root.GetComponentsInChildren<MeshCollider>())
                if(collider.sharedMesh==null||collider.convex||collider.isTrigger||collider.gameObject.layer!=8)throw new Exception("collider_binding");
        }
        finally {if(view!=null)view.Dispose();UnityEngine.Object.Destroy(root);}
    }
}
#endif
