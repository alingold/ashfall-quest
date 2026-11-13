#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ashfall.Editor.Playable
{
    // Independent mesh-data oracle: no PhysX ray, overlap or penetration queries.
    // Signed distances use authored winding, not an assertion that an open mesh
    // has a globally defined inside. Crossings are measured against the start side.
    public sealed class R25HeadclipGeometry
    {
        public sealed class Triangle
        {
            public Vector3 a, b, c, normal;
            public Bounds bounds;
            public string id;
        }
        public sealed class Sample
        {
            public float distance = float.PositiveInfinity, signed;
            public string triangle = "";
            public Vector3 point, normal;
        }
        sealed class Node
        {
            public Bounds bounds;
            public int first, count;
            public Node left, right;
        }
        readonly List<Triangle> triangles = new List<Triangle>();
        Node tree;
        public IList<Triangle> Triangles { get { return triangles; } }
        public void Add(MeshCollider collider)
        {
            if (collider.sharedMesh == null || !collider.enabled || !collider.gameObject.activeInHierarchy) return;
            var vertices = collider.sharedMesh.vertices;
            var indices = collider.sharedMesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = collider.transform.TransformPoint(vertices[indices[i]]);
                Vector3 b = collider.transform.TransformPoint(vertices[indices[i + 1]]);
                Vector3 c = collider.transform.TransformPoint(vertices[indices[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) continue;
                Bounds bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                triangles.Add(new Triangle { a = a, b = b, c = c, normal = normal.normalized, bounds = bounds,
                    id = collider.name + "/" + collider.sharedMesh.name + "/" + (i / 3) });
            }
        }
        public void AddBox(BoxCollider box)
        {
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++) vertices[i] = box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            int[] indices = { 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, 0, 4, 6, 0, 6, 2,
                1, 3, 7, 1, 7, 5, 0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3 };
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Bounds bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
                triangles.Add(new Triangle { a = a, b = b, c = c, normal = Vector3.Cross(b - a, c - a).normalized,
                    bounds = bounds, id = box.name + "/box/" + (i / 3) });
            }
        }
        public void Build() { tree = BuildNode(0, triangles.Count); }
        Node BuildNode(int first, int count)
        {
            if (count == 0) return null;
            Bounds bounds = triangles[first].bounds;
            for (int i = first + 1; i < first + count; i++) bounds.Encapsulate(triangles[i].bounds);
            var node = new Node { bounds = bounds, first = first, count = count };
            if (count <= 12) return node;
            int axis = bounds.size.x > bounds.size.y ? 0 : 1;
            if (bounds.size.z > bounds.size[axis]) axis = 2;
            triangles.Sort(first, count, Comparer<Triangle>.Create(delegate(Triangle x, Triangle y) {
                return x.bounds.center[axis].CompareTo(y.bounds.center[axis]); }));
            int half = count / 2; node.left = BuildNode(first, half); node.right = BuildNode(first + half, count - half);
            return node;
        }
        public Sample Eye(Vector3 eye)
        {
            var sample = new Sample(); Nearest(tree, eye, null, sample); return sample;
        }
        public Sample Rectangle(Vector3 eye, Vector3[] corners)
        {
            var sample = new Sample(); Nearest(tree, eye, corners, sample); return sample;
        }
        void Nearest(Node node, Vector3 eye, Vector3[] corners, Sample result)
        {
            if (node == null) return;
            float reach = 0;
            if (corners != null) foreach (Vector3 p in corners) reach = Mathf.Max(reach, Vector3.Distance(eye, p));
            if (Mathf.Sqrt(node.bounds.SqrDistance(eye)) > result.distance + reach) return;
            if (node.left != null)
            {
                Node first = node.left, second = node.right;
                if (first.bounds.SqrDistance(eye) > second.bounds.SqrDistance(eye)) { first = node.right; second = node.left; }
                Nearest(first, eye, corners, result); Nearest(second, eye, corners, result); return;
            }
            for (int i = node.first; i < node.first + node.count; i++)
            {
                var t = triangles[i]; Vector3 closest = Closest(eye, t);
                float distance = Vector3.Distance(eye, closest);
                if (corners != null)
                {
                    distance = float.PositiveInfinity;
                    for (int j = 0; j < 4; j++)
                    {
                        Vector3 p = corners[j], q = corners[(j + 1) % 4];
                        if (SegmentTriangle(p, q, t)) { distance = 0; break; }
                        distance = Mathf.Min(distance, Vector3.Distance(p, Closest(p, t)),
                            EdgeDistance(p, q, t.a, t.b), EdgeDistance(p, q, t.b, t.c), EdgeDistance(p, q, t.c, t.a));
                    }
                    var r0 = Make(corners[0], corners[1], corners[2]);
                    var r1 = Make(corners[0], corners[2], corners[3]);
                    foreach (var p in new[] { t.a, t.b, t.c }) distance = Mathf.Min(distance,
                        Vector3.Distance(p, Closest(p, r0)), Vector3.Distance(p, Closest(p, r1)));
                    if (SegmentTriangle(t.a,t.b,r0)||SegmentTriangle(t.b,t.c,r0)||SegmentTriangle(t.c,t.a,r0)
                        ||SegmentTriangle(t.a,t.b,r1)||SegmentTriangle(t.b,t.c,r1)||SegmentTriangle(t.c,t.a,r1)) distance=0;
                }
                if (distance >= result.distance) continue;
                result.distance = distance; result.signed = distance * (Vector3.Dot(eye - t.a, t.normal) < 0 ? -1 : 1);
                result.triangle = t.id; result.point = closest; result.normal = t.normal;
            }
        }
        static Triangle Make(Vector3 a, Vector3 b, Vector3 c) { return new Triangle { a = a, b = b, c = c }; }
        public bool Crosses(Vector3 from, Vector3 to) { return Crosses(tree, from, to); }
        bool Crosses(Node node, Vector3 from, Vector3 to)
        {
            if (node == null) return false;
            var b = new Bounds(from, Vector3.zero); b.Encapsulate(to); b.Expand(.00001f);
            if (!b.Intersects(node.bounds)) return false;
            if (node.left != null) return Crosses(node.left, from, to) || Crosses(node.right, from, to);
            for (int i = node.first; i < node.first + node.count; i++) if (SegmentTriangle(from, to, triangles[i])) return true;
            return false;
        }
        public static bool SegmentTriangle(Vector3 from, Vector3 to, Triangle t)
        {
            Vector3 d = to - from, e1 = t.b - t.a, e2 = t.c - t.a, p = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, p); if (Mathf.Abs(det) < 1e-10f) return false;
            Vector3 s = from - t.a; float u = Vector3.Dot(s, p) / det; if (u < 0 || u > 1) return false;
            Vector3 q = Vector3.Cross(s, e1); float v = Vector3.Dot(d, q) / det; if (v < 0 || u + v > 1) return false;
            float along = Vector3.Dot(e2, q) / det; return along > .00001f && along < .99999f;
        }
        public static Vector3 Closest(Vector3 p, Triangle t)
        {
            Vector3 ab=t.b-t.a, ac=t.c-t.a, ap=p-t.a;
            float d1=Vector3.Dot(ab,ap), d2=Vector3.Dot(ac,ap); if(d1<=0&&d2<=0)return t.a;
            Vector3 bp=p-t.b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return t.b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return t.a+ab*(d1/(d1-d3));
            Vector3 cp=p-t.c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return t.c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return t.a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return t.b+(t.c-t.b)*((d4-d3)/(d4-d3+d5-d6));
            float den=1/(va+vb+vc);return t.a+ab*(vb*den)+ac*(vc*den);
        }
        static float EdgeDistance(Vector3 p, Vector3 q, Vector3 a, Vector3 b)
        {
            Vector3 u=q-p,v=b-a,w=p-a;float aa=Vector3.Dot(u,u),bb=Vector3.Dot(u,v),cc=Vector3.Dot(v,v),dd=Vector3.Dot(u,w),ee=Vector3.Dot(v,w);
            if(aa<1e-12f||cc<1e-12f)return Mathf.Min(Vector3.Distance(p,a),Vector3.Distance(q,b));
            float den=aa*cc-bb*bb,s=den>1e-12f?Mathf.Clamp01((bb*ee-cc*dd)/den):0;
            float t=(bb*s+ee)/cc;if(t<0){t=0;s=Mathf.Clamp01(-dd/aa);}else if(t>1){t=1;s=Mathf.Clamp01((bb-dd)/aa);}
            return (w+u*s-v*t).magnitude;
        }
        // All authored slanted downward triangles are searched. Sampling has an
        // explicit resolution; it cannot prove an absence of smaller crossings.
        public List<Vector3> OverhangSites(float spacing, out int faces, out int samples, bool includeFlatFloors = false)
        {
            faces=0;samples=0;var sites=new List<Vector3>();var seen=new HashSet<string>();
            foreach(var t in triangles)
            {
                if(t.normal.y>=-.1f||t.normal.y<=-.99f)continue;faces++;
                int n=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max((t.b-t.a).magnitude,(t.c-t.a).magnitude)/spacing));
                // Cap extremely large boundary sheets, disclose in scan metadata.
                n=Mathf.Min(n,128);
                for(int i=0;i<n;i++)for(int j=0;j<n-i;j++)
                {
                    float u=(i+.3333f)/n,v=(j+.3333f)/n;Vector3 p=t.a+(t.b-t.a)*u+(t.c-t.a)*v;samples++;
                    Triangle floor;Vector3 foot;
                    if(!Floor(p-Vector3.up*.02f,2.2f,out floor,out foot)||Mathf.Abs(floor.normal.y)<.4226f||!includeFlatFloors&&Mathf.Abs(floor.normal.y)>.99f)continue;
                    float gap=p.y-foot.y;if(gap<.35f||gap>2.15f)continue;
                    string key=Mathf.RoundToInt(p.x/spacing)+":"+Mathf.RoundToInt(p.y/spacing)+":"+Mathf.RoundToInt(p.z/spacing);
                    if(seen.Add(key))sites.Add(foot+Vector3.up*1.7088f);
                }
            }
            return sites;
        }
        public bool Floor(Vector3 point,float depth,out Triangle floor,out Vector3 foot)
        {
            floor=null;foot=Vector3.zero;float best=depth;
            FloorNode(tree,point,ref best,ref floor,ref foot);return floor!=null;
        }
        void FloorNode(Node node,Vector3 p,ref float best,ref Triangle floor,ref Vector3 foot)
        {
            if(node==null||p.x<node.bounds.min.x||p.x>node.bounds.max.x||p.z<node.bounds.min.z||p.z>node.bounds.max.z||p.y<node.bounds.min.y||p.y-best>node.bounds.max.y)return;
            if(node.left!=null){FloorNode(node.left,p,ref best,ref floor,ref foot);FloorNode(node.right,p,ref best,ref floor,ref foot);return;}
            for(int i=node.first;i<node.first+node.count;i++)
            {
                var t=triangles[i];if(Mathf.Abs(t.normal.y)<.4226f)continue;
                float distance=Vector3.Dot(p-t.a,t.normal)/t.normal.y;if(distance<0||distance>best)continue;
                Vector3 q=p-Vector3.up*distance;if((Closest(q,t)-q).sqrMagnitude>1e-7f)continue;
                best=distance;floor=t;foot=q;
            }
        }
    }
}
#endif
