#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ashfall.Core;
namespace Ashfall.Modules.World
{
    // Resolver must attest the actual engine material against the approved slot and payload.
    public interface IWorldMaterialResolver { Material Resolve(ContentIdentity identity, LoadedSurface surface); bool Matches(ContentIdentity identity, LoadedSurface surface, Material material); }
    public sealed class WorldSemanticCollider : MonoBehaviour
    {
        public string SourceId, SemanticOwner, Role; public Transform MapRoot;
    }
    public sealed class WorldPadMarker : MonoBehaviour
    {
        public string SourceId, SemanticOwner; public float Speed, Radius;
        // Data only: central Flight/authority integration handles launch intent.
    }
    public sealed class WorldActorCollider : MonoBehaviour { public string ActorId; }
    public sealed class WorldMapIdentity : MonoBehaviour
    {
        public string PayloadHash {get;private set;}
        internal void Bind(string hash) {if(PayloadHash!=null)throw new InvalidOperationException("Map already bound");PayloadHash=hash;}
    }
    public sealed class UnityWorldInteractionQuery : IInteractionQuery
    {
        readonly Transform root; readonly WorldHitRouting routing; readonly string hash;
        public UnityWorldInteractionQuery(Transform mapRoot,WorldBuildPlan plan)
        { WorldLayers.Validate(LayerMask.LayerToName);if(!mapRoot||mapRoot.lossyScale!=Vector3.one||mapRoot.rotation!=Quaternion.identity)throw new ArgumentException("SI map root required");var identity=mapRoot.GetComponent<WorldMapIdentity>();if(!identity||identity.PayloadHash!=plan.Identity.PayloadSha256)throw new ArgumentException("Map root identity mismatch");root=mapRoot;routing=new WorldHitRouting(plan);hash=plan.Identity.PayloadSha256; }
        public bool HasLineOfSight(string actorId,string semanticOwnerId,Float3 origin,Float3 target,float range)
        {
            if(!origin.IsFinite||!target.IsFinite||float.IsNaN(range)||float.IsInfinity(range)||range<=0)return false;
            var a=root.TransformPoint(WorldUnityAdapter.Vector(origin));var b=root.TransformPoint(WorldUnityAdapter.Vector(target));var distance=Vector3.Distance(a,b);if(distance<=0||distance>range)return false;
            // Include unknown layers too: unclassified occluders must fail closed.
            var hits=Physics.RaycastAll(a,(b-a)/distance,distance,~0,QueryTriggerInteraction.Collide);var rows=new WorldQueryHit[hits.Length];
            for(int i=0;i<hits.Length;i++){var c=hits[i].collider;var owner=c.GetComponent<WorldSemanticCollider>();var actor=c.GetComponentInParent<WorldActorCollider>();rows[i]=new WorldQueryHit(owner&&owner.MapRoot==root?hash:null,owner?owner.SourceId:null,actor?actor.ActorId:null,c.gameObject.layer,hits[i].distance);}
            return routing.CanInteract(actorId,semanticOwnerId,range,rows);
        }
    }
    public sealed class WorldResources : MonoBehaviour
    {
        public readonly List<Mesh> Meshes=new List<Mesh>();
        public readonly List<Material> Materials=new List<Material>();
        void OnDestroy() { foreach(var mesh in Meshes) if(mesh) Destroy(mesh); foreach(var material in Materials) if(material) Destroy(material); }
    }
    public static class WorldUnityAdapter
    {
        public static Vector3 Vector(Float3 p) { return new Vector3(p.X,p.Y,p.Z); }
        // Resolver must supply accepted versioned materials/textures; never silently load arbitrary source paths.
        public static GameObject Build(ApprovedWorld world, IWorldMaterialResolver materials, IWorldApproval approval, IWorldBuildPolicy policy)
        { var root=BuildPlan(world.PrepareBuild(approval,policy,LayerMask.LayerToName),materials); try {world.Revalidate(approval);return root;}catch{UnityEngine.Object.Destroy(root);throw;} }
        public static GameObject BuildQuarantinedPilot(QuarantinedWorld world, IWorldMaterialResolver materials, IWorldBuildPolicy policy)
        { return BuildPlan(world.PrepareQuarantinedPilot(policy,LayerMask.LayerToName),materials); }
        static GameObject BuildPlan(WorldBuildPlan plan, IWorldMaterialResolver materials)
        {
            if(materials==null) throw new ArgumentNullException("materials");
            var map=plan.Snapshot(); var registry=plan.Registry();
            var root=new GameObject(map.Id); root.SetActive(false); var resources=root.AddComponent<WorldResources>();
            root.AddComponent<WorldMapIdentity>().Bind(plan.Identity.PayloadSha256);
            try {
                foreach(var s in map.Surfaces) {
                    var go=new GameObject(s.Id); go.transform.SetParent(root.transform,false);
                    var mesh=new Mesh {name=s.Id,indexFormat=s.Vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16}; resources.Meshes.Add(mesh);
                    var vertices=new Vector3[s.Vertices.Length];var normals=new Vector3[vertices.Length];
                    for(int i=0;i<vertices.Length;i++){vertices[i]=Vector(s.Vertices[i]);normals[i]=Vector(s.Normals[i]);}
                    mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=s.Indices;
                    if(s.Uv.Length>0){var uv=new Vector2[s.Uv.Length/2];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(s.Uv[2*i],s.Uv[2*i+1]);mesh.uv=uv;}
                    mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var index=Array.IndexOf(map.Surfaces,s);
                    var material=materials.Resolve(plan.Identity,plan.Snapshot().Surfaces[index]); if(!material)throw new InvalidOperationException("Unresolved material: "+s.MaterialPath);
                    var frozen=new Material(material); resources.Materials.Add(frozen);
                    if(!materials.Matches(plan.Identity,plan.Snapshot().Surfaces[index],frozen))throw new InvalidOperationException("Material substitution: "+s.Id);
                    go.AddComponent<MeshRenderer>().sharedMaterial=frozen;
                    var role=registry.Resolve(s.Id); go.layer=role.kind=="structure"?WorldLayers.Structure:role.kind=="decoration"?WorldLayers.Decoration:WorldLayers.Interaction;
                    if(role.kind=="structure") {var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh;}
                    if(role.boxes!=null)foreach(var b in role.boxes){var c=go.AddComponent<BoxCollider>();c.center=new Vector3(b.center[0],b.center[1],b.center[2]);c.size=new Vector3(b.size[0],b.size[1],b.size[2]);c.isTrigger=true;}
                    var semantic=go.AddComponent<WorldSemanticCollider>();semantic.SourceId=s.Id;semantic.SemanticOwner=s.SemanticOwner;semantic.Role=role.kind;semantic.MapRoot=root.transform;
                }
                foreach(var p in map.Pads) {
                    var go=new GameObject(p.Id);go.transform.SetParent(root.transform,false);go.transform.localPosition=Vector(p.Position);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,Vector(p.Normal));
                    var marker=go.AddComponent<WorldPadMarker>();marker.SourceId=p.Id;marker.SemanticOwner=p.SemanticOwner;marker.Speed=p.Speed;marker.Radius=p.Radius;
                    var role=registry.Resolve(p.Id);go.layer=WorldLayers.Interaction;
                    // Authored map-local boxes live on identity-transform children; never rotate them twice.
                    foreach(var b in role.boxes){var volume=new GameObject(p.Id+"/volume");volume.transform.SetParent(root.transform,false);volume.layer=WorldLayers.Interaction;var c=volume.AddComponent<BoxCollider>();c.center=new Vector3(b.center[0],b.center[1],b.center[2]);c.size=new Vector3(b.size[0],b.size[1],b.size[2]);c.isTrigger=true;var owner=volume.AddComponent<WorldSemanticCollider>();owner.SourceId=p.Id;owner.SemanticOwner=p.SemanticOwner;owner.Role=role.kind;owner.MapRoot=root.transform;}
                    // Disc contact detection belongs to Flight, avoiding a spherical trigger that launches through walls.
                }
                return root; // Inert staging root; activation remains coordinator controlled.
            } catch { UnityEngine.Object.Destroy(root);throw; }
        }
    }
    public sealed class UnitySpawnQuery : ISpawnQuery
    {
        readonly int mask; readonly Transform mapRoot; readonly Func<Float3,bool> hazard;
        public UnitySpawnQuery(Transform root,int structuralLayerMask,Func<Float3,bool> hazardQuery){WorldLayers.Validate(LayerMask.LayerToName);if(structuralLayerMask!=(1<<WorldLayers.Structure))throw new ArgumentException("Exact structural mask required");if(!root||hazardQuery==null)throw new ArgumentException("Map root and hazard query required");if(root.lossyScale!=Vector3.one||root.rotation!=Quaternion.identity)throw new ArgumentException("SI spawn query requires unit scale and unrotated root");mapRoot=root;mask=structuralLayerMask;hazard=hazardQuery;}
        public bool TryFloor(Float3 p,float up,float depth,out Float3 floor,out Float3 normal)
        {
            RaycastHit hit;var origin=mapRoot.TransformPoint(WorldUnityAdapter.Vector(p));
            bool found=Physics.Raycast(origin+Vector3.up*up,Vector3.down,out hit,depth,mask,QueryTriggerInteraction.Ignore);
            if(found){var owner=hit.collider.GetComponent<WorldSemanticCollider>();found=owner && owner.MapRoot==mapRoot && owner.Role=="structure";}
            var f=found?mapRoot.InverseTransformPoint(hit.point):Vector3.zero;var n=found?mapRoot.InverseTransformDirection(hit.normal):Vector3.up;
            floor=new Float3(f.x,f.y,f.z);normal=new Float3(n.x,n.y,n.z);return found;
        }
        public bool HasStandingRoom(Float3 feet,float radius,float height)
        {
            var p=mapRoot.TransformPoint(WorldUnityAdapter.Vector(feet));
            return height>=radius*2&&!Physics.CheckCapsule(p+Vector3.up*radius,p+Vector3.up*(height-radius),radius,mask,QueryTriggerInteraction.Ignore);
        }
        public bool IsHazard(Float3 feet) { return hazard(feet); }
    }
}
#endif
