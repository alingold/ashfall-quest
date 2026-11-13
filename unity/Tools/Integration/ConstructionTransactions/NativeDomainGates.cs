#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.ConstructionRuntime;
using Ashfall.Integration.ConstructionRuntime.Engine;
using Ashfall.Modules.Construction;
// Coordinator-only under engine_lane.py; compile success is not gate execution.
public static class ConstructionPublicationNativeGates
{
    public static void Run(Material panel,Material valid,Material invalid)
    {
        var root=new GameObject("Construction R3 domain gate");ConstructionView view=null;
        try {
            view=new ConstructionView(root.transform,panel,valid,invalid);
            var piece=new PlacedPanel("build_0_1","a",new Panel("floor",new Ashfall.Core.Float3(),1,false,0),140);
            var first=new ConstructionPublication(1,"place","a","c",new string('a',64),998,new[]{piece},"s",0,"host");
            if(!view.Apply(first)||view.Apply(first))throw new Exception("native publication retry");
            var variants=new[]{
                new ConstructionPublication(2,"place","a","c",new string('a',64),998,new[]{piece},"other",0,"host"),
                new ConstructionPublication(2,"place","a","c",new string('a',64),998,new[]{piece},"s",1,"host"),
                new ConstructionPublication(2,"place","a","c",new string('b',64),998,new[]{piece},"s",0,"host")};
            foreach(var publication in variants){bool rejected=false;try{view.Apply(publication);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("native domain alias");}
            view.Preview(new BuildDecision("",new System.Collections.Generic.List<Panel>{piece.Shape,piece.Shape},2));
            if(root.GetComponentsInChildren<MeshCollider>().Length!=1)throw new Exception("ghost gained collider");
            view.Preview(null);if(root.GetComponentsInChildren<MeshCollider>().Length!=1)throw new Exception("ghost affected publication");
        } finally {if(view!=null)view.Dispose();UnityEngine.Object.Destroy(root);}
        // Coordinator must yield deferred Destroy, then census Mesh/GameObject/Material
        // lifetimes, inspect appearance and repeat under nonidentity/scaled parents.
    }
}
#endif
