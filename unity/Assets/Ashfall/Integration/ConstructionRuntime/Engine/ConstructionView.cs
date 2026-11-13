#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Integration.ConstructionRuntime;
using Ashfall.Modules.Construction;
namespace Ashfall.Integration.ConstructionRuntime.Engine
{
    // Explicitly invoked by coordinator. No initializer, scene or bootstrap mutation.
    // Rendering/collider mirror only; authoritative queries use attested triangles.
    public sealed class ConstructionView : IDisposable
    {
        readonly Transform parent;readonly Material material,validPreview,invalidPreview;
        readonly Dictionary<string,GameObject> bodies=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        readonly List<GameObject> previews=new List<GameObject>();long lastSerial;string hostInstance,session,mapHash;long epoch;
        public ConstructionView(Transform root,Material approvedPanel,Material valid,Material invalid)
        {
            if(root==null||approvedPanel==null||valid==null||invalid==null)throw new ArgumentException("explicit_materials_required");
            if(LayerMask.LayerToName(8)!="WorldStructure")throw new ArgumentException("reserved_layer_unbound");
            parent=root;material=approvedPanel;validPreview=valid;invalidPreview=invalid;
        }
        GameObject Create(PlacedPanel p,Material paint,bool collision)
        {
            var body=new GameObject("Construction "+p.Id);body.transform.SetParent(parent,false);
            // Coordinates are world SI: parenting cannot alter attested geometry.
            body.transform.position=Vector3.zero;body.transform.rotation=Quaternion.identity;body.transform.localScale=Vector3.one;
            if(body.transform.lossyScale!=Vector3.one) {UnityEngine.Object.Destroy(body);throw new InvalidOperationException("scaled_parent");}
            body.layer=8;var triangles=AttestedWorld.PanelTriangles(p,new string('0',64));var vertices=new Vector3[36];var indices=new int[36];
            for(int i=0;i<12;i++){var t=triangles[i];vertices[i*3]=V(t.A);vertices[i*3+1]=V(t.B);vertices[i*3+2]=V(t.C);}
            for(int i=0;i<36;i++)indices[i]=i;var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            body.AddComponent<MeshFilter>().sharedMesh=mesh;body.AddComponent<MeshRenderer>().sharedMaterial=paint;
            if(collision){var collider=body.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=false;collider.isTrigger=false;}
            return body;
        }
        static Vector3 V(Ashfall.Core.Float3 v){return new Vector3(v.X,v.Y,v.Z);}
        static void Remove(GameObject body){body.SetActive(false);var filter=body.GetComponent<MeshFilter>();if(filter!=null)UnityEngine.Object.Destroy(filter.sharedMesh);UnityEngine.Object.Destroy(body);}
        public bool Apply(ConstructionPublication publication)
        {
            if(publication==null)return false;
            if(hostInstance!=null&&(hostInstance!=publication.HostInstance||session!=publication.Session||epoch!=publication.Epoch||mapHash!=publication.MapHash))throw new InvalidOperationException("view_publication_domain_mismatch");
            if(publication.Serial<=lastSerial)return false;
            hostInstance=publication.HostInstance;session=publication.Session;epoch=publication.Epoch;mapHash=publication.MapHash;
            foreach(var body in bodies.Values)Remove(body);bodies.Clear();
            foreach(var p in publication.Panels)bodies.Add(p.Id,Create(p,material,true));lastSerial=publication.Serial;return true;
        }
        public void Preview(BuildDecision decision)
        {
            foreach(var body in previews)Remove(body);previews.Clear();if(decision==null)return;
            var plan=decision.Panels;if(plan.Length>2)throw new InvalidOperationException("preview_budget");
            int index=0;foreach(var p in plan)previews.Add(Create(new PlacedPanel("preview/"+(index++),"preview",p,0),decision.Accepted?validPreview:invalidPreview,false));
        }
        public void Dispose(){foreach(var body in bodies.Values)Remove(body);bodies.Clear();foreach(var body in previews)Remove(body);previews.Clear();}
    }
}
#endif
