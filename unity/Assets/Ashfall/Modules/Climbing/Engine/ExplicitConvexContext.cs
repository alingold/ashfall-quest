#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Modules.Climbing.Engine
{
    public sealed class ConvexProxyBinding
    {
        public readonly Collider Source,Proxy;
        public readonly ConvexBodyIdentity Identity;
        public ConvexProxyBinding(Collider source,Collider proxy,ConvexBodyIdentity identity){Source=source;Proxy=proxy;Identity=identity;}
    }
    // No implicit copying or live-scene mutation. Invalidated bindings must be republished
    // in a NEW context/revision by the geometry owner, never repaired during a query.
    public sealed class ExplicitConvexContext:IUnityConvexContext
    {
        readonly Scene scene;readonly Rigidbody head;readonly MeshCollider hull;readonly Transform actor;
        readonly long revision;readonly IUnityConvexApproval approval;
        readonly Dictionary<Collider,ConvexProxyBinding> bindings=new Dictionary<Collider,ConvexProxyBinding>();
        readonly Dictionary<Collider,Matrix4x4> matrices=new Dictionary<Collider,Matrix4x4>();
        readonly Dictionary<Collider,Vector3[]> vertices=new Dictionary<Collider,Vector3[]>();
        readonly Dictionary<Collider,int[]> triangles=new Dictionary<Collider,int[]>();
        readonly Dictionary<Collider,Vector3[]> boxes=new Dictionary<Collider,Vector3[]>();
        readonly Dictionary<Collider,int> layers=new Dictionary<Collider,int>();
        public ExplicitConvexContext(Scene scene,Rigidbody head,MeshCollider hull,Transform actor,long revision,IUnityConvexApproval approval,ConvexProxyBinding[] bodies)
        {
            if(bodies==null||revision<0)throw new ArgumentException("query bindings/revision");
            this.scene=scene;this.head=head;this.hull=hull;this.actor=actor;this.revision=revision;this.approval=approval;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var b in bodies)
            {
                if(b==null||b.Source==null||b.Proxy==null||String.IsNullOrEmpty(b.Identity.BodyId)||!ids.Add(b.Identity.BodyId))throw new ArgumentException("unique body identity");
                bindings.Add(b.Proxy,b);matrices.Add(b.Proxy,b.Source.transform.localToWorldMatrix);
                layers.Add(b.Proxy,b.Source.gameObject.layer);
                var box=b.Source as BoxCollider;if(box!=null)boxes.Add(b.Proxy,new[]{box.center,box.size});
                var mesh=b.Source as MeshCollider;
                if(mesh!=null){if(mesh.sharedMesh==null||!mesh.sharedMesh.isReadable)throw new ArgumentException("readable sealed source mesh");vertices.Add(b.Proxy,mesh.sharedMesh.vertices);triangles.Add(b.Proxy,mesh.sharedMesh.triangles);}
            }
        }
        public Scene QueryScene {get{return scene;}} public Rigidbody HeadProxy {get{return head;}}
        public MeshCollider HeadCollider {get{return hull;}} public Transform ActorRoot {get{return actor;}}
        public long Revision {get{return revision;}} public bool Approved {get{return approval!=null&&approval.ValidateSourceHull(revision).Accepted;}}
        public bool Resolve(Collider c,out ConvexBodyIdentity identity)
        {ConvexProxyBinding b;identity=new ConvexBodyIdentity();if(c==null||!bindings.TryGetValue(c,out b))return false;identity=b.Identity;return true;}
        static bool Unit(Transform t)
        {
            var m=t.localToWorldMatrix;Vector3 x=m.GetColumn(0),y=m.GetColumn(1),z=m.GetColumn(2);
            return Mathf.Abs(x.sqrMagnitude-1)<1e-5f&&Mathf.Abs(y.sqrMagnitude-1)<1e-5f&&Mathf.Abs(z.sqrMagnitude-1)<1e-5f&&
                Mathf.Abs(Vector3.Dot(x,y))<1e-5f&&Mathf.Abs(Vector3.Dot(x,z))<1e-5f&&Mathf.Abs(Vector3.Dot(y,z))<1e-5f&&Vector3.Dot(Vector3.Cross(x,y),z)>.99999f;
        }
        static bool Geometry(Collider a,Collider b)
        {
            if(a.GetType()!=b.GetType())return false;
            var am=a as MeshCollider;var bm=b as MeshCollider;
            if(am!=null)return am.sharedMesh!=null&&am.sharedMesh==bm.sharedMesh&&am.convex==bm.convex&&am.cookingOptions==bm.cookingOptions;
            var ab=a as BoxCollider;var bb=b as BoxCollider;
            return ab!=null&&ab.center==bb.center&&ab.size==bb.size;
        }
        public bool Validate()
        {
            if(!scene.IsValid()||!scene.isLoaded||scene.GetPhysicsScene()==Physics.defaultPhysicsScene||actor==null||head==null||hull==null||
                actor.gameObject.scene==scene||head.gameObject.scene!=scene||hull.attachedRigidbody!=head||!head.isKinematic||head.useGravity||
                !head.detectCollisions||!hull.enabled||hull.isTrigger||!hull.convex||hull.transform!=head.transform||head.transform.parent!=null||!Unit(head.transform)||
                head.gameObject.layer!=10||Physics.GetIgnoreLayerCollision(10,8)||Physics.GetIgnoreLayerCollision(10,9)||
                head.GetComponentsInChildren<Collider>(true).Length!=1||!SourceMesh(hull.sharedMesh)||
                LayerMask.LayerToName(8)!="WorldStructure"||LayerMask.LayerToName(9)!="WorldInteraction"||
                LayerMask.LayerToName(10)!="PlayerActor"||LayerMask.LayerToName(11)!="WorldDecoration")return false;
            foreach(var b in bindings.Values)
            {
                var s=b.Source;var p=b.Proxy;
                if(s==null||p==null||s.gameObject.scene!=actor.gameObject.scene||p.gameObject.scene!=scene||s.transform==actor||s.transform.IsChildOf(actor)||
                    !s.enabled||!p.enabled||!s.gameObject.activeInHierarchy||!p.gameObject.activeInHierarchy||s.isTrigger||p.isTrigger||
                    s.attachedRigidbody!=null||p.attachedRigidbody!=null||s.gameObject.layer!=layers[p]||s.gameObject.layer!=p.gameObject.layer||
                    (p.gameObject.layer!=8&&p.gameObject.layer!=9)||!Unit(s.transform)||!Unit(p.transform)||
                    !s.transform.localToWorldMatrix.Equals(matrices[p])||!s.transform.localToWorldMatrix.Equals(p.transform.localToWorldMatrix)||!Geometry(s,p)||!SealedMesh(p,s as MeshCollider)||!SealedBox(p,s as BoxCollider)||
                    String.IsNullOrEmpty(b.Identity.OwnerId)||String.IsNullOrEmpty(b.Identity.Approval))return false;
            }
            foreach(var go in scene.GetRootGameObjects())foreach(var c in go.GetComponentsInChildren<Collider>(true))
                if(c.enabled&&c.gameObject.activeInHierarchy&&c!=hull&&!bindings.ContainsKey(c))return false;
            // Omitting an occluding source structure must never produce a clear proxy world.
            foreach(var go in actor.gameObject.scene.GetRootGameObjects())foreach(var c in go.GetComponentsInChildren<Collider>(true))
            {
                if(!c.enabled||!c.gameObject.activeInHierarchy||c.isTrigger||c.transform==actor||c.transform.IsChildOf(actor)||
                    (c.gameObject.layer!=8&&c.gameObject.layer!=9))continue;
                bool found=false;foreach(var b in bindings.Values)if(b.Source==c){found=true;break;}
                if(!found)return false;
            }
            return true;
        }
        bool SealedMesh(Collider proxy,MeshCollider source)
        {
            if(source==null)return true;var v=source.sharedMesh.vertices;var t=source.sharedMesh.triangles;
            var old=vertices[proxy];var indices=triangles[proxy];if(v.Length!=old.Length||t.Length!=indices.Length)return false;
            for(int i=0;i<v.Length;i++)if(!v[i].Equals(old[i]))return false;
            for(int i=0;i<t.Length;i++)if(t[i]!=indices[i])return false;return true;
        }
        bool SealedBox(Collider proxy,BoxCollider source)
        {return source==null||(source.center.Equals(boxes[proxy][0])&&source.size.Equals(boxes[proxy][1]));}
        public static bool SourceMesh(Mesh mesh)
        {
            if(mesh==null||!mesh.isReadable)return false;
            var v=mesh.vertices;var exact=SourceHeadConvex.Instance.CopyVertices();if(v.Length!=exact.Length)return false;
            for(int i=0;i<v.Length;i++)if(v[i].x!=exact[i].X||v[i].y!=exact[i].Y||v[i].z!=exact[i].Z)return false;
            var expected=Triangles();var actual=mesh.triangles;if(expected.Length!=actual.Length)return false;
            for(int i=0;i<actual.Length;i++)if(actual[i]!=expected[i])return false;return true;
        }
        static int[] Triangles()
        {
            var t=new List<int>();
            // Winding follows the reflected source azimuth: outward side faces.
            for(int r=0;r<6;r++)for(int s=0;s<24;s++)
            {int a=r*24+s,b=r*24+(s+1)%24,c=a+24,d=b+24;t.Add(a);t.Add(b);t.Add(c);if(r<5){t.Add(b);t.Add(d);t.Add(c);}}
            for(int s=1;s<23;s++){t.Add(0);t.Add(s+1);t.Add(s);}return t.ToArray();
        }
        public static Mesh CreateSourceMesh()
        {
            var source=SourceHeadConvex.Instance.CopyVertices();var vertices=new Vector3[source.Length];
            for(int i=0;i<source.Length;i++)vertices[i]=new Vector3(source[i].X,source[i].Y,source[i].Z);
            var mesh=new Mesh();mesh.name="ASHFALL source goggle convex candidate";mesh.vertices=vertices;mesh.triangles=Triangles();mesh.RecalculateBounds();return mesh;
        }
    }
}
#endif
