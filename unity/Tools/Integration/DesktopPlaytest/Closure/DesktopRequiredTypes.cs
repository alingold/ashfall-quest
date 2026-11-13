#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.DesktopPlaytest;
namespace Ashfall.Tools.DesktopClosure
{
    // Compile-time type/interface checks; called only deliberately by root.
    public static class DesktopRequiredTypes
    {
        static void Module<T>() where T : MonoBehaviour, IDesktopPlaytestModule { }
        public static void CheckTypes()
        {
            Module<Ashfall.Integration.DesktopPlaytest.Pawn.DesktopPawn>();
            Module<Ashfall.Integration.DesktopPlaytest.Arena.DesktopArena>();
            Module<Ashfall.Integration.DesktopPlaytest.Combat.DesktopWeapons>();
            Module<Ashfall.Integration.DesktopPlaytest.Hud.DesktopHud>();
#if DESKTOP_WITH_CONSTRUCTION
            Module<Ashfall.Integration.DesktopPlaytest.Construction.DesktopConstruction>();
#endif
        }
        // Native fixture after root initializes Arena, Pawn, Combat, [Construction], Hud.
        // No bootstrap, asset activation or input simulation. Root must separately play controls.
        public static void VerifyInitialized(DesktopPlaytestContext context)
        {
            CheckTypes();
            if(context==null || context.PlayerRoot==null || context.Controller==null || context.View==null)
                throw new InvalidOperationException("desktop pawn/context incomplete");
            if(context.Controller.transform!=context.PlayerRoot || !context.View.transform.IsChildOf(context.PlayerRoot))
                throw new InvalidOperationException("desktop root/camera ownership mismatch");
            if(context.Targets.Count<5) throw new InvalidOperationException("desktop requires five targets");
            foreach(var target in context.Targets)
                if(target==null || !target.enabled || !target.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("desktop target unavailable");
            Debug.Log("DesktopClosure initialized-context PASS; movement/fire/reload/build/quit execution still required");
        }
    }
}
#endif
