#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

namespace Ashfall.Integration.WorldRuntime
{
    public sealed class WorldLifetimeDriver : MonoBehaviour
    {
        sealed class Entry
        {
            internal WorldSourceRegistration Source;
            internal NativeWorld World;
            internal Action<WorldSourceRegistration,WorldInvalidation> Handler;
        }
        static readonly List<WorldLifetimeDriver> drivers=new List<WorldLifetimeDriver>();
        static bool barrierRunning;
        Entry current;
        bool changing;
        public string LastFailure {get;private set;}
        public NativeWorld Current {get{return current==null?null:current.World;}}
        public static long PhysicsBarrierCount {get;private set;}
        void OnEnable(){if(!drivers.Contains(this))drivers.Add(this);SceneManager.sceneUnloaded+=SceneUnloaded;}
        void OnDisable(){SceneManager.sceneUnloaded-=SceneUnloaded;drivers.Remove(this);Withdraw();}
        void OnDestroy(){drivers.Remove(this);Withdraw();}
        void SceneUnloaded(Scene scene)
        {if(current!=null&&(!current.Source.Source||current.Source.Source.gameObject.scene==scene))Withdraw();}
        void Withdraw()
        {var old=current;current=null;Close(old);}
        static void Close(Entry entry)
        {if(entry==null)return;entry.Source.Invalidated-=entry.Handler;entry.World.Dispose();}
        void Retired(Entry entry,WorldSourceRegistration source,WorldInvalidation reason)
        {
            // Captured entry identity is authoritative. A delayed old callback cannot touch a replacement.
            if(!ReferenceEquals(entry.Source,source))return;
            if(ReferenceEquals(current,entry))current=null;
            Close(entry);
        }
        public void Stage(WorldSourceRegistration source)
        {StageInternal(source,null);}
        internal void StageInternal(WorldSourceRegistration source,Func<int,string> fixtureLayers)
        {
            if(changing||barrierRunning)throw new InvalidOperationException("Reentrant world lifetime mutation");
            changing=true;Entry next=null;
            try
            {
                if(!Application.isPlaying||!isActiveAndEnabled||source==null)throw new InvalidOperationException("Active PlayMode host and registered source required");
                source.Validate();
                if(source.Source.gameObject.scene!=gameObject.scene)throw new InvalidOperationException("Source and driver must share scene lifetime");
                EnsureBarrier();
                var native=fixtureLayers==null?NativeWorld.Stage(source.World,source.Approval,source.Policy,source.Receipt,source.Materials,source.Actors,source.Hazards):
                    NativeWorld.StageInternal(source.World,source.Approval,source.Policy,source.Receipt,source.Materials,source.Actors,source.Hazards,fixtureLayers);
                next=new Entry {Source=source,World=native};
                var captured=next;next.Handler=(s,r)=>Retired(captured,s,r);source.Invalidated+=next.Handler;
                SceneManager.MoveGameObjectToScene(native.Root,gameObject.scene);
                source.Validate();native.ValidateBeforePhysics();source.Validate();native.SealBeforePhysics();
                var old=current;current=next;next=null;Close(old);LastFailure=null;
            }
            finally{if(next!=null)Close(next);changing=false;}
        }
        public void Activate()
        {
            if(changing||barrierRunning||current==null)throw new InvalidOperationException("No staged world or reentrant host mutation");
            changing=true;
            var entry=current;
            try{entry.Source.Validate();EnsureBarrier();entry.World.Activate();entry.Source.Validate();entry.World.SealBeforePhysics();
                if(!ReferenceEquals(current,entry))throw new InvalidOperationException("World replaced during activation");}
            catch{if(ReferenceEquals(current,entry))current=null;Close(entry);throw;}
            finally{changing=false;}
        }
        void BeforePhysics()
        {
            var entry=current;if(entry==null)return;
            if(changing){LastFailure="Reentrant physics lane";Withdraw();return;}
            changing=true;
            try
            {
                entry.Source.Validate();
                if(entry.Source.Source.gameObject.scene!=gameObject.scene)throw new InvalidOperationException("World source moved scenes");
                if(Physics.simulationMode!=SimulationMode.FixedUpdate)throw new InvalidOperationException("Only the registered fixed physics lane is supported");
                entry.World.ValidateBeforePhysics();entry.Source.Validate();entry.World.SealBeforePhysics();
                if(Physics.simulationMode!=SimulationMode.FixedUpdate)throw new InvalidOperationException("Physics lane changed during validation");
            }
            catch(Exception e){LastFailure=e.GetType().Name+": "+e.Message;if(ReferenceEquals(current,entry))current=null;Close(entry);}
            finally{changing=false;}
        }
        // Called by the actual PlayerLoop immediately before native 3D physics, after FixedUpdate scripts.
        static void PhysicsBarrier()
        {
            if(barrierRunning)throw new InvalidOperationException("Reentrant native physics barrier");
            barrierRunning=true;
            try
            {
                PhysicsBarrierCount++;var snapshot=drivers.ToArray();
                foreach(var driver in snapshot)if(driver&&driver.isActiveAndEnabled)driver.BeforePhysics();
                // No provider callbacks in this final ALL-world pass: a later source cannot tamper an earlier world.
                foreach(var driver in snapshot)if(driver&&driver.isActiveAndEnabled)driver.SealPhysics();
            }
            finally{barrierRunning=false;}
        }
        void SealPhysics()
        {
            var entry=current;if(entry==null)return;
            try{entry.Source.ValidateLifetimeOnly();entry.World.SealBeforePhysics();
                if(Physics.simulationMode!=SimulationMode.FixedUpdate)throw new InvalidOperationException("Physics lane changed");}
            catch(Exception e){LastFailure=e.GetType().Name+": "+e.Message;if(ReferenceEquals(current,entry))current=null;Close(entry);}
        }
        struct WorldPhysicsBarrier { }
        static void EnsureBarrier()
        {
            if(Physics.simulationMode!=SimulationMode.FixedUpdate)throw new InvalidOperationException("Manual/script physics requires a separate root scheduler");
            var loop=PlayerLoop.GetCurrentPlayerLoop();int found=0;
            Install(ref loop,ref found);
            if(found!=1)throw new InvalidOperationException("Exactly one native PhysicsFixedUpdate required");
            PlayerLoop.SetPlayerLoop(loop);
        }
        static void Install(ref PlayerLoopSystem node,ref int found)
        {
            if(node.subSystemList==null)return;
            var list=new List<PlayerLoopSystem>();
            foreach(var original in node.subSystemList)
            {
                var child=original;
                if(child.type==typeof(WorldPhysicsBarrier))continue;
                if(child.type==typeof(FixedUpdate.PhysicsFixedUpdate))
                {found++;list.Add(new PlayerLoopSystem {type=typeof(WorldPhysicsBarrier),updateDelegate=PhysicsBarrier});}
                Install(ref child,ref found);list.Add(child);
            }
            node.subSystemList=list.ToArray();
        }
    }
}
#endif
