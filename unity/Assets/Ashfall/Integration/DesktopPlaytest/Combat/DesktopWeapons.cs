#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Ashfall.Integration.DesktopPlaytest.Combat
{
    public sealed class DesktopWeapons : MonoBehaviour, IDesktopPlaytestModule, IInteractionQuery
    {
        sealed class Target { public Collider Collider;public string Id;public Renderer Renderer;public Color Original;public float Flash; }
        readonly List<Target> targets=new List<Target>();
        DesktopPlaytestContext context;
        LocalRifleSandbox sandbox;
        string feedback="Ready";
        public LocalRifleSandbox Sandbox{get{return sandbox;}}
        public void Initialize(DesktopPlaytestContext value)
        {
            if(context!=null)throw new InvalidOperationException("Already initialized");
            if(value==null||value.View==null||value.PlayerRoot==null)throw new ArgumentException("Arena and Pawn must initialize first");
            var staged=new List<Target>();var distinct=new HashSet<Collider>();
            for(int i=0;i<value.Targets.Count;i++){
                Collider collider=value.Targets[i];if(collider==null)continue;
                if(!distinct.Add(collider)||staged.Count>=63)throw new ArgumentException("Distinct bounded target registry required");
                var renderer=collider.GetComponent<Renderer>();
                staged.Add(new Target{Collider=collider,Id="local-target/"+i,Renderer=renderer,Original=renderer!=null&&renderer.sharedMaterial!=null?renderer.sharedMaterial.color:Color.white});
            }
            var ids=new string[staged.Count];for(int i=0;i<ids.Length;i++)ids[i]=staged[i].Id;
            var prepared=new LocalRifleSandbox(ids,this);
            context=value;targets.AddRange(staged);sandbox=prepared;
            context.Hits=0;context.RemainingTargets=targets.Count;feedback="Ready";
            context.ResetRequested+=ResetSandbox;Publish();
        }
        public void ResetSandbox()
        {
            if(context==null||context.Retired)return;
            var ids=new string[targets.Count];for(int i=0;i<targets.Count;i++)ids[i]=targets[i].Id;
            var prepared=new LocalRifleSandbox(ids,this);prepared.SuspendControls();
            foreach(var target in targets){target.Flash=0;Paint(target,target.Original);}
            if(sandbox!=null)sandbox.Retire();sandbox=prepared;context.Hits=0;context.RemainingTargets=targets.Count;feedback="Ready";Publish();
        }
        void Update()
        {
            PumpDesktopInput(Mathf.Min(Time.deltaTime,.25f));
        }
        public void PumpDesktopInput(float dt)
        {
            if(context==null||sandbox==null)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            TickControls(dt,mouse!=null&&mouse.leftButton.isPressed,keyboard!=null&&keyboard.rKey.wasPressedThisFrame);
        }
        // Callable by root's native fixture; same path as installed mouse/keyboard input.
        public void TickControls(float dt,bool fire,bool reload)
        {
            if(sandbox==null||context==null)return;
            if(context.Retired){sandbox.Retire();return;}
            if(sandbox.Step(dt,fire,reload,context.Paused)){
                RaycastHit hit;
                feedback="Miss";
                if(Nearest(context.View.transform.position,context.View.transform.forward,100,out hit)){
                    foreach(Target target in targets)if(Available(target)&&target.Collider==hit.collider){
                        RuleResult result=sandbox.Hit(target.Id,To(context.View.transform.position),To(hit.point));
                        if(result.Accepted){context.Hits++;target.Flash=.12f;feedback=sandbox.Read(target.Id).Life==LifeState.Alive?"Hit":"Target down";}
                        else feedback="Hit denied: "+result.Reason;
                        break;
                    }
                }
            }
            context.RemainingTargets=0;
            int unavailable=0;
            foreach(Target target in targets){if(!Available(target)){unavailable++;target.Flash=0;continue;}bool alive=sandbox.Read(target.Id).Life==LifeState.Alive;if(alive)context.RemainingTargets++;if(!context.Paused)target.Flash=Mathf.Max(0,target.Flash-dt);Paint(target,!alive?Color.gray:target.Flash>0?Color.red:target.Original);}
            if(unavailable>0)feedback=unavailable+" target(s) unavailable; course changed";
            Publish();
        }
        bool Available(Target target){return target.Collider!=null&&target.Collider.enabled&&target.Collider.gameObject.activeInHierarchy&&context.Targets.Contains(target.Collider);}
        void OnApplicationFocus(bool focused){if(!focused&&sandbox!=null)sandbox.SuspendControls();}
        void OnApplicationPause(bool paused){if(paused&&sandbox!=null)sandbox.SuspendControls();}
        void OnDisable(){if(sandbox!=null)sandbox.SuspendControls();}
        void Publish(){var s=sandbox.Snapshot;context.CombatStatus="Local rifle "+s.Loaded+"/24 | reserve "+sandbox.Reserve+" | "+(sandbox.Reloading?"Reloading":feedback)+" | LMB fire, R reload";}
        static Float3 To(Vector3 v){return new Float3(v.x,v.y,v.z);}
        bool Nearest(Vector3 origin,Vector3 direction,float range,out RaycastHit nearest)
        {
            nearest=new RaycastHit();float distance=float.PositiveInfinity;
            foreach(RaycastHit hit in Physics.RaycastAll(origin,direction,range,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){
                if(hit.collider==context.Controller||hit.collider.transform.IsChildOf(context.PlayerRoot))continue;
                if(hit.distance<distance){nearest=hit;distance=hit.distance;}
            }
            return distance<float.PositiveInfinity;
        }
        public bool HasLineOfSight(string actor,string owner,Float3 origin,Float3 point,float range)
        {
            if(context==null||!origin.IsFinite||!point.IsFinite||float.IsNaN(range)||float.IsInfinity(range)||range<=0)return false;
            Vector3 start=new Vector3(origin.X,origin.Y,origin.Z),end=new Vector3(point.X,point.Y,point.Z);RaycastHit hit;
            if(!Nearest(start,(end-start).normalized,range,out hit))return false;
            foreach(Target target in targets)if(target.Id==owner)return Available(target)&&target.Collider==hit.collider;
            return false;
        }
        static void Paint(Target target,Color color){if(target.Renderer==null)return;var block=new MaterialPropertyBlock();target.Renderer.GetPropertyBlock(block);block.SetColor("_BaseColor",color);block.SetColor("_Color",color);target.Renderer.SetPropertyBlock(block);}
        void OnDestroy(){if(sandbox!=null)sandbox.Retire();if(context!=null)context.ResetRequested-=ResetSandbox;foreach(Target target in targets)Paint(target,target.Original);sandbox=null;context=null;}
    }
}
#endif

