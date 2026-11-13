#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Construction;
using UnityEngine;
namespace Ashfall.DesktopConstructionFixtures
{
    public static class NativeFixture
    {
        // Root runs only in an isolated synthetic scene, after Physics.SyncTransforms.
        // No auto-start, no engine execution by the implementation leaf.
        public static void Run(){Run(false);}
        public static void RunArena(){Run(true);}
        static void Run(bool actualArena)
        {
            var root=new GameObject("ConstructionFixture");
            try
            {
                if(!actualArena)
                {
                    var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Floor";floor.layer=8;
                    floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(40,1,40);
                }
                var pawn=new GameObject("Pawn");pawn.transform.SetParent(root.transform);pawn.transform.position=new Vector3(1,1,-2);
                var controller=pawn.AddComponent<CharacterController>();controller.height=1.8f;controller.radius=.3f;
                var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.SetParent(pawn.transform,false);
                var context=new DesktopPlaytestContext{PlayerRoot=pawn.transform,Controller=controller,View=camera};
                if(actualArena)root.AddComponent<Ashfall.Integration.DesktopPlaytest.Arena.DesktopArena>().Initialize(context);
                var module=root.AddComponent<DesktopConstruction>();Physics.SyncTransforms();module.Initialize(context);
                Require(module.Balance==999,"local-seed "+context.ConstructionStatus);
                Require(module.Evaluate(false),"preview "+context.ConstructionStatus);
                Require(module.Balance==999 && module.PanelCount==0,"preview-no-cost");
                Require(module.Evaluate(true),"place "+context.ConstructionStatus);
                Require(module.Balance==998 && module.PanelCount==1,"actual-placement-charge");
                Physics.SyncTransforms();var boxes=UnityEngine.Object.FindObjectsByType<BoxCollider>();bool built=false;
                foreach(var box in boxes)if(box.name.StartsWith("build_",StringComparison.Ordinal))built=box.enabled && box.bounds.size.y>2;
                Require(built,"built-physical-box");
                context.Paused=true;Require(!module.Evaluate(true) && module.Balance==998,"pause-no-cost");
                context.Paused=false;pawn.transform.position=new Vector3(1,20,-2);camera.transform.rotation=Quaternion.LookRotation(Vector3.up);
                Require(!module.Evaluate(true) && module.Balance==998,"unsupported-no-cost");
                context.RequestReset();Require(module.Balance==999 && module.PanelCount==0,"new-local-sandbox-reset");
                Debug.Log("DesktopConstructionR1 native PASS");
            }
            finally{root.SetActive(false);UnityEngine.Object.Destroy(root);}
        }
        static void Require(bool ok,string label){if(!ok)throw new InvalidOperationException("DesktopConstructionR1: "+label);}
    }
}
#endif
