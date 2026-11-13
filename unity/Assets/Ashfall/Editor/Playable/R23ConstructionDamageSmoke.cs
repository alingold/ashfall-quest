#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Activities;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Explicit future PlayMode fixture. Native ray/collider assertions cannot be
    // replaced by the managed checks; this worker only staged/API-compiled it.
    public static class R23ConstructionDamageSmoke
    {
        static void Need(bool okay,string message){if(!okay)throw new Exception(message);}
        static InputFrame Frame(ref long tick,bool trigger){return new InputFrame{Tick=++tick,Tracked=true,TriggerRight=trigger};}
        static string Place(R23VRActivities activity,Transform right,Vector3 site,int level,ref long tick)
        {
            var before=new HashSet<string>(StringComparer.Ordinal);foreach(var p in activity.PanelSnapshot)before.Add(p.Id);
            right.position=site+new Vector3(.3f,1.2f+CandidateGeometry.Side*level,.2f);right.rotation=Quaternion.identity;Physics.SyncTransforms();
            activity.TickConstruction(Frame(ref tick,false),true,true,false,false);
            activity.TickConstruction(Frame(ref tick,true),true,true,false,false);
            activity.TickConstruction(Frame(ref tick,false),true,true,false,false);
            foreach(var p in activity.PanelSnapshot)if(!before.Contains(p.Id))return p.Id;
            throw new Exception("Actual native trigger/release did not place: "+activity.BuildStatus);
        }
        static PlacedPanel Panel(R23VRActivities activity,string id)
        {foreach(var panel in activity.PanelSnapshot)if(panel.Id==id)return panel;throw new Exception("Source panel missing "+id);}
        static Ray RayAt(PlacedPanel panel)
        {var c=panel.Shape.Center;return new Ray(new Vector3(c.X,c.Y,c.Z)-Vector3.forward*.5f,Vector3.forward);}
        static Collider Hit(R23VRActivities activity,string id)
        {
            Physics.SyncTransforms();RaycastHit hit;var ray=RayAt(Panel(activity,id));
            Need(Physics.Raycast(ray,out hit,1,Physics.AllLayers,QueryTriggerInteraction.Ignore),"No actual nearest native panel collider hit");
            PlacedPanel panel;Need(activity.TryReadPanel(hit.collider,out panel)&&panel.Id==id,"Native nearest collider failed its real panel identity");return hit.collider;
        }
        public static void Run()
        {
            Need(Application.isPlaying,"Construction damage native fixture requires PlayMode");
            var owned=new List<GameObject>();R23VRActivities activity=null;long tick=0;float side=CandidateGeometry.Side;
            Vector3 site=new Vector3(side*1000+side*.5f,0,side*.5f);
            try
            {
                var map=new GameObject("Editor-only damage fixture map");owned.Add(map);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Actual native fixture support";floor.transform.SetParent(map.transform,false);floor.transform.position=site-Vector3.up*.1f;floor.transform.localScale=new Vector3(24,.2f,24);
                var actor=new GameObject("Editor-only damage fixture actor");owned.Add(actor);actor.transform.position=site;var body=actor.AddComponent<CharacterController>();body.height=1.7f;body.radius=.24f;body.center=Vector3.up*.85f;
                var head=new GameObject("Accepted fixture head").transform;head.SetParent(actor.transform,false);head.localPosition=Vector3.up*1.7088f;
                var left=new GameObject("Accepted fixture left").transform;left.SetParent(actor.transform,false);left.localPosition=new Vector3(-.3f,1.2f,.2f);
                var right=new GameObject("Accepted fixture right").transform;right.SetParent(actor.transform,false);
                var owner=new GameObject("Editor-only damage activity owner");owned.Add(owner);activity=owner.AddComponent<R23VRActivities>();activity.Initialize(actor.transform,head,left,right,body,map.transform);
                string lower=Place(activity,right,site,0,ref tick),upper=Place(activity,right,site,1,ref tick),top=Place(activity,right,site,2,ref tick);
                Need(activity.PanelCount==3&&activity.Balance==996,"Native stack/debit differs from source service");
                Collider lowerCollider=Hit(activity,lower),upperCollider=Hit(activity,upper),topCollider=Hit(activity,top);Ray removedRay=RayAt(Panel(activity,lower));
                Need(!activity.TryDamageCollider(floor.GetComponent<Collider>(),999),"Map collider authorized foreign panel damage");
                Need(activity.TryDamageCollider(lowerCollider,25)&&Panel(activity,lower).Health==155&&activity.Balance==996,"Native impact did not commit actual25 health damage");
                activity.TickStructure(0,true);Need(!activity.TryDamageCollider(lowerCollider,1)&&Panel(activity,lower).Health==155,"Paused impact mutated source health");activity.TickStructure(0,false);
                Need(activity.TryDamageCollider(lowerCollider,155)&&activity.PanelCount==2&&!lowerCollider.enabled&&!lowerCollider.gameObject.activeInHierarchy,"Destroyed native collider was not withdrawn immediately");
                RaycastHit hit;Need(!Physics.Raycast(removedRay,out hit,1,Physics.AllLayers,QueryTriggerInteraction.Ignore),"Destroyed panel still occludes native ray");
                activity.TickStructure(.29f,false);Need(activity.PanelCount==2,"Branch collapsed before source300ms");activity.TickStructure(100,true);Need(activity.PanelCount==2,"Paused time advanced support cascade");
                activity.TickStructure(.01f,false);Need(activity.PanelCount==1&&!upperCollider.enabled&&Panel(activity,top).Health==180,"First source cascade depth failed native withdrawal");
                activity.TickStructure(.30f,false);Need(activity.PanelCount==0&&!topCollider.enabled&&activity.Balance==996,"Second source cascade depth failed or refunded charges");
                string replacement=Place(activity,right,site,0,ref tick);Need(replacement!=lower&&Panel(activity,replacement).Health==180&&activity.Balance==995,"Destroyed slot rebuild did not consume fresh source charge/identity");
                Need(!activity.TryDamageCollider(lowerCollider,1)&&Hit(activity,replacement)!=lowerCollider,"Old collider alias could damage a rebuilt panel");
                Collider replacementCollider=Hit(activity,replacement);floor.SetActive(false);Physics.SyncTransforms();activity.TickStructure(0,false);activity.TickStructure(.30f,false);
                Need(activity.PanelCount==0&&!replacementCollider.enabled&&activity.Balance==995,"Actual native map-support loss did not collapse panel without refund");
                Debug.Log("R23 CONSTRUCTION DAMAGE PASS: native nearest ray, owner health155/180, paused hit/time protection, immediate collider withdrawal,300/600ms support cascade, paid same-slot rebuild and native support loss");
            }
            finally
            {
                if(activity!=null)activity.ResetRound();foreach(var go in owned)if(go!=null){go.SetActive(false);UnityEngine.Object.Destroy(go);}Physics.SyncTransforms();
            }
        }
    }
}
#endif
