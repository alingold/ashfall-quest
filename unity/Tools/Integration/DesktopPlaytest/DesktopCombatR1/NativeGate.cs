#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DesktopCombatR1
{
    // Root calls on its serialized native lane, after a physics barrier. No auto bootstrap.
    public static class NativeGate
    {
        static void Check(bool value,string name){if(!value)throw new InvalidOperationException("DesktopCombat native: "+name);}
        public static void Run()
        {
            var owner=new GameObject("desktop-combat-fixture");var player=new GameObject("local-player");var camera=new GameObject("local-view");var target=GameObject.CreatePrimitive(PrimitiveType.Cube);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mouse mouse=null;
            try{
                player.transform.position=new Vector3(0,200,0);var controller=player.AddComponent<CharacterController>();controller.center=new Vector3(0,0,0);camera.transform.SetParent(player.transform,false);
                target.transform.position=new Vector3(0,200,10);wall.transform.position=new Vector3(4,200,5);
                var context=new DesktopPlaytestContext{PlayerRoot=player.transform,Controller=controller,View=camera.AddComponent<Camera>()};context.Targets.Add(target.GetComponent<Collider>());
                var weapons=owner.AddComponent<DesktopWeapons>();weapons.Initialize(context);Physics.SyncTransforms();
                mouse=InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.Update();weapons.PumpDesktopInput(.016f);Check(context.Hits==1&&weapons.Sandbox.Snapshot.Loaded==23,"installed InputSystem click; view ray excludes player; one debit");
                InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();
                weapons.TickControls(.016f,true,false);Check(context.Hits==1&&weapons.Sandbox.Snapshot.Loaded==23,"cadence");
                wall.transform.position=new Vector3(0,200,5);Physics.SyncTransforms();weapons.TickControls(.2f,true,false);Check(context.Hits==1&&weapons.Sandbox.Snapshot.Loaded==22,"wall occludes");
                wall.transform.position=new Vector3(4,200,5);Physics.SyncTransforms();
                weapons.TickControls(.016f,false,true);for(int i=0;i<20;i++)weapons.TickControls(.1f,false,false);
                Check(weapons.Sandbox.Snapshot.Loaded==24&&weapons.Sandbox.Reserve==94,"reload");
                for(int i=0;i<10;i++)weapons.TickControls(.2f,true,false);Check(context.RemainingTargets==0&&context.Hits==8,"real target death");
                context.Paused=true;long shots=weapons.Sandbox.Snapshot.Shots;weapons.TickControls(.25f,true,true);Check(weapons.Sandbox.Snapshot.Shots==shots,"pause");context.Paused=false;
                var retained=context.Targets[0];context.RequestReset();Check(context.Targets[0]==retained&&retained.enabled&&context.Hits==0&&context.RemainingTargets==1&&weapons.Sandbox.Reserve==96,"reset retains arena collider");
                Debug.Log("PASS DesktopCombatR1 native InputSystem synthetic click/ray/ammo/reload/death/reset; physical user click remains manual");
            }finally{if(mouse!=null)InputSystem.RemoveDevice(mouse);UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(camera);UnityEngine.Object.DestroyImmediate(player);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(wall);}
        }
    }
}
#endif

