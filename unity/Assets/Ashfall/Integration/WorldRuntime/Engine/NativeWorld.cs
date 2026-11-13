#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using Ashfall.Core;
using Ashfall.Modules.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.WorldRuntime
{
    // Only the trusted host registry may identify foreign actor colliders. Unknowns remain blockers.
    public interface INativeActorIdentity { string ActorId(Collider collider); }
    public interface INativeHazards { bool IsHazard(Vector3 finalFeet, float radius, float height); }
    public sealed class NativeWorld : IDisposable, IWorldExternalHits, IInteractionQuery, ISpawnQuery
    {
        sealed class Body { internal Collider Collider; internal string Id; internal bool Trigger; internal int Layer,Include,Exclude,Priority; internal Vector3 Center,Size; internal float ContactOffset; internal PhysicsMaterial Material; internal MeshColliderCookingOptions Cooking; internal Mesh ExpectedMesh; }
        sealed class Node { internal GameObject Object; internal Component[] Components; internal int Layer; }
        readonly List<Node> nodes=new List<Node>();
        readonly List<Material> borrowed=new List<Material>();
        readonly List<MeshFilter> filters=new List<MeshFilter>();
        readonly List<MeshRenderer> renderers=new List<MeshRenderer>();
        readonly List<Bounds> meshBounds=new List<Bounds>();
        readonly List<Body> bodies=new List<Body>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Material> materials=new List<Material>();
        readonly List<GameObject> children=new List<GameObject>();
        readonly WorldBuildPlan plan;
        readonly IWorldMaterialResolver resolver;
        readonly INativeActorIdentity actors;
        readonly INativeHazards hazards;
        RuntimeBinding binding;
        internal readonly GameObject Root;
        bool disposed,invalidated,activated,checking,inCallback;
        public bool Quarantined {get;private set;}
        public ContentIdentity Identity {get {return plan.Identity;}}
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
        static void Release(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        public static NativeWorld Stage(ApprovedWorld world,IWorldApproval approval,IWorldBuildPolicy policy,
            IRuntimeAttestation receipt,IWorldMaterialResolver materials,INativeActorIdentity actors,INativeHazards hazards)
        {return StageInternal(world,approval,policy,receipt,materials,actors,hazards,LayerMask.LayerToName);}
        // Internal fixture entry does not change TagManager or the global collision matrix.
        internal static NativeWorld StageInternal(ApprovedWorld world,IWorldApproval approval,IWorldBuildPolicy policy,
            IRuntimeAttestation receipt,IWorldMaterialResolver materials,INativeActorIdentity actors,INativeHazards hazards,Func<int,string> layers)
        {
            if(world==null)throw new ArgumentNullException("world");
            world.Revalidate(approval);
            if(receipt==null||!receipt.Matches(world.Identity))throw new InvalidOperationException("Immutable dependency receipt required before native allocation");
            world.Revalidate(approval);
            var plan=world.PrepareBuild(approval,policy,layers);
            var n=new NativeWorld(plan,materials,actors,hazards,false);
            try {n.binding=new RuntimeBinding(world,approval,policy,receipt,layers,n);n.Check();return n;}catch{n.Dispose();throw;}
        }
        public static NativeWorld StageQuarantined(QuarantinedWorld pilot,IWorldBuildPolicy policy,IWorldMaterialResolver materials,
            INativeActorIdentity actors,INativeHazards hazards)
        {return new NativeWorld(pilot.PrepareQuarantinedPilot(policy,LayerMask.LayerToName),materials,actors,hazards,true);}
        NativeWorld(WorldBuildPlan plan,IWorldMaterialResolver resolver,INativeActorIdentity actors,INativeHazards hazards,bool quarantine)
        {
            if(resolver==null||actors==null||hazards==null)throw new ArgumentNullException("Complete material/actor/hazard ports required");
            this.plan=plan;this.resolver=resolver;this.actors=actors;this.hazards=hazards;Quarantined=quarantine;
            Root=new GameObject("WorldRuntime/"+plan.Identity.MapId);Root.SetActive(false);
            try
            {
                var map=plan.Snapshot();var registry=plan.Registry();
                for(int i=0;i<map.Surfaces.Length;i++)
                {
                    var s=map.Surfaces[i];var role=registry.Resolve(s.Id);var go=Child(s.Id);
                    go.layer=role.kind=="structure"?8:role.kind=="decoration"?11:9;
                    var mesh=new Mesh();meshes.Add(mesh);mesh.indexFormat=s.Vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                    mesh.vertices=s.Vertices.Select(V).ToArray();mesh.normals=s.Normals.Select(V).ToArray();mesh.triangles=s.Indices;
                    if(s.Uv.Length>0)mesh.uv=Enumerable.Range(0,s.Uv.Length/2).Select(j=>new Vector2(s.Uv[j*2],s.Uv[j*2+1])).ToArray();
                    mesh.RecalculateBounds();meshBounds.Add(mesh.bounds);var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;filters.Add(filter);
                    var original=resolver.Resolve(plan.Identity,plan.Snapshot().Surfaces[i]);
                    if(!original)throw new InvalidOperationException("Missing material dependency: "+s.Id);
                    borrowed.Add(original);var material=new Material(original);materials.Add(material);
                    if(!resolver.Matches(plan.Identity,plan.Snapshot().Surfaces[i],material))throw new InvalidOperationException("Material dependency mismatch: "+s.Id);
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderers.Add(renderer);
                    if(role.kind=="structure")
                    {
                        var structural=go.AddComponent<MeshCollider>();structural.sharedMesh=mesh;
                        AddBody(structural,s.Id,false,8);bodies[bodies.Count-1].ExpectedMesh=mesh;
                    }
                    AddBoxes(s.Id,role);
                }
                foreach(var pad in map.Pads)AddBoxes(pad.Id,registry.Resolve(pad.Id));
                foreach(var go in children)
                {
                    var expected=new List<Component>();expected.Add(go.transform);
                    expected.AddRange(filters.Where(f=>f.gameObject==go).Cast<Component>());
                    expected.AddRange(renderers.Where(r=>r.gameObject==go).Cast<Component>());
                    expected.AddRange(bodies.Where(b=>b.Collider.gameObject==go).Select(b=>(Component)b.Collider));
                    // Freeze only explicitly constructed components, never trust a post-callback inventory as an allowlist.
                    int surfaceIndex=filters.FindIndex(f=>f.gameObject==go);int layer=9;
                    if(surfaceIndex>=0){var kind=registry.Resolve(map.Surfaces[surfaceIndex].Id).kind;layer=kind=="structure"?8:kind=="decoration"?11:9;}
                    nodes.Add(new Node {Object=go,Components=expected.ToArray(),Layer=layer});
                }
            }catch{Dispose();throw;}
        }
        GameObject Child(string id){var go=new GameObject(id);children.Add(go);go.transform.SetParent(Root.transform,false);return go;}
        void AddBody(Collider c,string id,bool trigger,int layer)
        {c.isTrigger=trigger;var mesh=c as MeshCollider;bodies.Add(new Body {Collider=c,Id=id,Trigger=trigger,Layer=layer,ContactOffset=c.contactOffset,Material=c.sharedMaterial,Cooking=mesh?mesh.cookingOptions:0,Include=c.includeLayers.value,Exclude=c.excludeLayers.value,Priority=c.layerOverridePriority});}
        void AddBoxes(string id,SemanticRecord role)
        {
            if(role.boxes==null)return;
            foreach(var b in role.boxes){var go=Child(id+"/authored-volume");go.layer=9;var c=go.AddComponent<BoxCollider>();
                c.center=new Vector3(b.center[0],b.center[1],b.center[2]);c.size=new Vector3(b.size[0],b.size[1],b.size[2]);AddBody(c,id,true,9);
                bodies[bodies.Count-1].Center=c.center;bodies[bodies.Count-1].Size=c.size;}
        }
        internal void ValidateBeforePhysics(){Check();}
        internal void SealBeforePhysics(){try{ValidateOwned(false);}catch{Invalidate();throw;}}
        void Check()
        {
            if(checking||inCallback){Invalidate();throw new InvalidOperationException("Reentrant world attestation");}
            checking=true;
            // Material ports run after the initial receipt check. Revalidate approval/receipt
            // after those callbacks, then finish with a callback-free structural pass.
            try {ValidateOwned(true);if(binding!=null)binding.Validate();ValidateOwned(false);}catch {Invalidate();throw;}
            finally {checking=false;}
        }
        void ValidateOwned(bool attest)
        {
            if(disposed||invalidated||!Root||Root.activeSelf!=activated||Root.layer!=0||Root.transform.parent||!Root.transform.position.Equals(Vector3.zero)||!Root.transform.rotation.Equals(Quaternion.identity)||!Root.transform.localScale.Equals(Vector3.one))throw new InvalidOperationException("World ownership/transform invalid");
            if(attest&&binding!=null)binding.Validate();
            if(Root.GetComponents<Component>().Any(c=>!c||c.GetType()!=typeof(Transform)))throw new InvalidOperationException("Foreign root component");
            if(Root.transform.childCount!=children.Count)throw new InvalidOperationException("Foreign child");
            // Owned nodes are leaves. childCount includes inactive descendants: no unattested subtree may hide here.
            foreach(var node in nodes)
            {
                var go=node.Object;
                if(!go||go.layer!=node.Layer||go.transform.childCount!=0||go.transform.parent!=Root.transform||!go.transform.localPosition.Equals(Vector3.zero)||!go.transform.localRotation.Equals(Quaternion.identity)||!go.transform.localScale.Equals(Vector3.one)||!go.activeSelf)throw new InvalidOperationException("Body hierarchy/transform/layer changed");
                var actual=go.GetComponents<Component>();
                if(actual.Length!=node.Components.Length||actual.Any(c=>!c)||!actual.SequenceEqual(node.Components))throw new InvalidOperationException("Exact component membership changed");
            }
            foreach(var b in bodies)
            {
                var c=b.Collider;if(!c||!c.enabled||c.isTrigger!=b.Trigger||c.gameObject.layer!=b.Layer||c.contactOffset!=b.ContactOffset||c.sharedMaterial!=b.Material||c.includeLayers.value!=b.Include||c.excludeLayers.value!=b.Exclude||c.layerOverridePriority!=b.Priority)throw new InvalidOperationException("Body routing changed");
                var box=c as BoxCollider;if(box&&(!box.center.Equals(b.Center)||!box.size.Equals(b.Size)))throw new InvalidOperationException("Authored volume changed");
                var mc=c as MeshCollider;if(!ReferenceEquals(b.ExpectedMesh,null)&&(!mc||!b.ExpectedMesh||mc.sharedMesh!=b.ExpectedMesh))throw new InvalidOperationException("Retained structural mesh binding changed");
                if(mc&&(mc.convex||mc.cookingOptions!=b.Cooking))throw new InvalidOperationException("Structural collider policy changed");
            }
            var map=plan.Snapshot();
            for(int i=0;i<meshes.Count;i++)
            {
                var s=map.Surfaces[i];var mesh=meshes[i];
                if(!mesh||!mesh.bounds.Equals(meshBounds[i])||mesh.indexFormat!=(s.Vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16)||mesh.subMeshCount!=1||mesh.GetTopology(0)!=MeshTopology.Triangles||!mesh.vertices.SequenceEqual(s.Vertices.Select(V))||!mesh.triangles.SequenceEqual(s.Indices)||!mesh.normals.SequenceEqual(s.Normals.Select(V))||!mesh.uv.SequenceEqual(Enumerable.Range(0,s.Uv.Length/2).Select(j=>new Vector2(s.Uv[j*2],s.Uv[j*2+1]))))throw new InvalidOperationException("Geometry/UV changed");
                var filter=filters[i];if(!filter||filter.sharedMesh!=mesh)throw new InvalidOperationException("Mesh binding changed");
                var go=filter.gameObject;var renderer=renderers[i];
                if(!renderer||renderer.sharedMaterials.Length!=1||renderer.sharedMaterial!=materials[i]||!renderer.enabled||renderer.forceRenderingOff)throw new InvalidOperationException("Render dependency changed");
                var collider=go.GetComponent<MeshCollider>();if(collider&&collider.sharedMesh!=mesh)throw new InvalidOperationException("Collider mesh changed");
                if(attest&&(!borrowed[i]||resolver.Resolve(plan.Identity,plan.Snapshot().Surfaces[i])!=borrowed[i]||!resolver.Matches(plan.Identity,plan.Snapshot().Surfaces[i],materials[i])))throw new InvalidOperationException("Material revision changed");
            }
        }
        // No player, camera, hand, controller or collider authority is changed here.
        public void Activate()
        {Check();if(Quarantined)throw new InvalidOperationException("Quarantined content cannot activate for gameplay");activated=true;Root.SetActive(true);Physics.SyncTransforms();Check();}
        internal void ActivateFixture(){Check();activated=true;Root.SetActive(true);Physics.SyncTransforms();Check();}
        void Gameplay(){Check();if(Quarantined||binding==null||!Root.activeInHierarchy)throw new InvalidOperationException("Inactive/quarantined world");}
        public WorldQueryHit[] Query(string actorId,Float3 origin,Float3 target)
        {
            Gameplay();try {var a=V(origin);var delta=V(target)-a;float length=delta.magnitude;
            if(!origin.IsFinite||!target.IsFinite||!Finite(length)||length<=0)return null;
            var rows=new List<WorldQueryHit>();
            // All layers and explicit trigger inclusion. No dependency on global queriesHitTriggers.
            foreach(var h in Physics.RaycastAll(a,delta/length,length,~0,QueryTriggerInteraction.Collide))
                rows.Add(Row(h.collider,h.distance));
            // Raycasts omit colliders containing the origin. Conservatively treat every overlap
            // within the fixed 10-micrometre origin sphere as a zero-distance hit.
            foreach(var c in Physics.OverlapSphere(a,.00001f,~0,QueryTriggerInteraction.Collide))rows.Add(Row(c,0));
            Check();return rows.ToArray();}catch{Invalidate();throw;}
        }
        T Callback<T>(Func<T> call)
        {
            if(inCallback){Invalidate();throw new InvalidOperationException("Reentrant world port");}
            inCallback=true;
            try{return call();}catch{Invalidate();throw;}finally{inCallback=false;}
        }
        WorldQueryHit Row(Collider c,float distance)
        {
            var body=bodies.Find(b=>b.Collider==c);
            return new WorldQueryHit(body==null?null:Identity.PayloadSha256,body==null?null:body.Id,
                body==null?Callback(()=>actors.ActorId(c)):null,c.gameObject.layer,distance);
        }
        public bool HasLineOfSight(string actorId,string owner,Float3 origin,Float3 target,float range)
        {
            Gameplay();try {
            if(!binding.HasLineOfSight(actorId,owner,origin,target,range)){Check();return false;}
            var rows=Query(actorId,origin,target);bool result=binding.RouteNative(actorId,owner,range,rows);Check();return result;
            }catch{Invalidate();throw;}
        }
        public bool Trigger(string bodyId,string owner,Float3 point)
        {Gameplay();try{bool result=binding.Trigger(bodyId,owner,point);Check();return result;}catch{Invalidate();throw;}}
        // Data-only pad routing: actual Flight host face/sweep/cooldown and command checks remain external.
        public LoadedPad Pad(string bodyId,string owner,Float3 point)
        {Gameplay();if(!Trigger(bodyId,owner,point))return null;return plan.Snapshot().Pads.SingleOrDefault(p=>p.Id==bodyId&&p.SemanticOwner==owner);}
        public LoadedSpawn[] SafeSpawns(string mode,string team,float height,IWorldBuildPolicy bounds)
        {Gameplay();try{var result=binding.SafeSpawns(mode,team,height,new SpawnProbe(this,height),bounds);Check();return result;}catch{Invalidate();throw;}}
        sealed class SpawnProbe : ISpawnQuery
        {
            readonly NativeWorld world;readonly float height;
            internal SpawnProbe(NativeWorld world,float height){this.world=world;this.height=height;}
            public bool TryFloor(Float3 p,float up,float depth,out Float3 f,out Float3 n){return world.TryFloor(p,up,depth,out f,out n);}
            public bool HasStandingRoom(Float3 p,float r,float h){return world.HasStandingRoom(p,r,h);}
            public bool IsHazard(Float3 p){return world.HazardAt(p,height);}
        }
        public bool TryFloor(Float3 candidate,float up,float depth,out Float3 floor,out Float3 normal)
        {
            Gameplay();floor=normal=new Float3();
            var hits=Physics.RaycastAll(V(candidate)+Vector3.up*up,Vector3.down,depth,~0,QueryTriggerInteraction.Ignore);
            Check();if(hits.Length==0)return false;float min=hits.Min(h=>h.distance);
            var nearest=hits.Where(h=>h.distance<=min+.00001f).ToArray();
            if(nearest.Any(h=>!bodies.Any(b=>b.Collider==h.collider&&b.Layer==8)))return false;
            // Coplanar floor normals must all support standing; choose by point coordinates deterministically.
            if(nearest.Any(h=>h.normal.y<=.7f))return false;
            var hit=nearest.OrderBy(h=>h.point.x).ThenBy(h=>h.point.y).ThenBy(h=>h.point.z).First();floor=F(hit.point);normal=F(hit.normal);return true;
        }
        public bool HasStandingRoom(Float3 feet,float radius,float height)
        {
            Gameplay();if(!feet.IsFinite||!Finite(radius)||!Finite(height)||radius<=0||height<radius*2)return false;
            bool clear=Physics.OverlapCapsule(V(feet)+Vector3.up*radius,V(feet)+Vector3.up*(height-radius),radius,~0,QueryTriggerInteraction.Ignore).Length==0;Check();return clear;
        }
        public bool IsHazard(Float3 feet)
        {return HazardAt(feet,1.8f);}
        bool HazardAt(Float3 feet,float height)
        {
            Gameplay();try {if(!feet.IsFinite)return true;bool hazard=Callback(()=>hazards.IsHazard(V(feet),.3f,height));Check();if(hazard)return true;
            foreach(var c in Physics.OverlapCapsule(V(feet)+Vector3.up*.3f,V(feet)+Vector3.up*(height-.3f),.3f,~0,QueryTriggerInteraction.Collide))
            {var b=bodies.Find(row=>row.Collider==c);if(b!=null&&plan.Registry().Resolve(b.Id).kind=="hazard")return true;}
            Check();return false;}catch{Invalidate();throw;}
        }
        // Main-thread/host-lane only. Detection is terminal; restoring a receipt cannot revive this instance.
        public void Invalidate()
        {
            invalidated=true;activated=false;
            foreach(var body in bodies)if(body.Collider)body.Collider.enabled=false;
            if(Root)Root.SetActive(false);
        }
        public void Dispose()
        {
            if(disposed)return;Invalidate();disposed=true;
            // A tampered/reparented owned node still belongs to this instance. Borrowed resources never do.
            foreach(var child in children)if(child){child.SetActive(false);Release(child);}Release(Root);
            foreach(var mesh in meshes)Release(mesh);foreach(var material in materials)Release(material);
            bodies.Clear();children.Clear();meshes.Clear();materials.Clear();nodes.Clear();borrowed.Clear();filters.Clear();renderers.Clear();meshBounds.Clear();
        }
    }
}
#endif
