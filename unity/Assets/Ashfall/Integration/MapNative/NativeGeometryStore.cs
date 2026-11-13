#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.MapNative
{
    // Trusted explicit source appearance conversion. Must bind ALL source material, texture,
    // image and sampler dependencies; no default shader/color/texture inference here.
    // Borrowed originals survive disposal. Matches attests clone semantics and revocation.
    public interface ISourceMaterialResolver
    {
        Material Resolve(PrimitiveBinding binding);
        bool Matches(PrimitiveBinding binding,Material original,Material clone);
    }
    public sealed class NativeGeometryStore : IGeometryStore
    {
        sealed class Appearance {internal PrimitiveBinding Binding;internal Material Original,Clone;internal MaterialIntegrityR3 Integrity;}
        sealed class Row {internal GameObject Node;internal Mesh Mesh;internal MeshFilter Filter;internal MeshRenderer Renderer;internal float[] Positions,Normals,Uv;internal int[] Indices;internal Appearance Appearance;internal Bounds Bounds;internal SubMeshDescriptor Submesh;internal VertexAttributeDescriptor[] Attributes;internal uint RenderingLayer;internal ShadowCastingMode Shadows;internal bool ReceiveShadows;internal LightProbeUsage LightProbes;internal ReflectionProbeUsage ReflectionProbes;internal MotionVectorGenerationMode Motion;internal bool Occlusion;internal int Lightmap,RealtimeLightmap;internal Vector4 LightmapScale,RealtimeLightmapScale;internal ReceiveGI GI;}
        readonly List<Row> rows=new List<Row>();readonly Dictionary<string,Appearance> appearances=new Dictionary<string,Appearance>();
        readonly ISourceMaterialResolver resolver;readonly int lane;
        readonly GameObject root;bool closed,visible;PreviewLifetime lifetime;
        readonly int maxMeshes,maxAppearances;readonly long maxPayload;long payload;
        public long PackedBytes {get{return payload;}} public int MaterialCloneCount {get{return appearances.Count;}}
        public bool PreviewActive {get{return visible;}}
        public int MeshCount {get{return rows.Count;}}
        public NativeGeometryStore(ISourceMaterialResolver resolver):this(resolver,20000,4096,536870912){}
        public NativeGeometryStore(ISourceMaterialResolver resolver,int maxMeshes,int maxAppearances,long maxPayload)
        {if(maxMeshes<1||maxMeshes>20000||maxAppearances<1||maxAppearances>4096||maxPayload<1||maxPayload>536870912)throw new ArgumentException("native resource budget");this.maxMeshes=maxMeshes;this.maxAppearances=maxAppearances;this.maxPayload=maxPayload;if(resolver==null)throw new ArgumentNullException("resolver");this.resolver=resolver;lane=Thread.CurrentThread.ManagedThreadId;root=new GameObject("MapNative/pilot-quarantine");root.SetActive(false);}
        void Lane(){if(Thread.CurrentThread.ManagedThreadId!=lane)throw new InvalidOperationException("Unity main control lane required");}
        static Vector3[] V(float[] a){if(a==null)return null;var r=new Vector3[a.Length/3];for(int i=0;i<r.Length;i++)r[i]=new Vector3(a[3*i],a[3*i+1],a[3*i+2]);return r;}
        public void Add(PackedBatch b)
        {
            Lane();if(closed)throw new ObjectDisposedException("NativeGeometryStore");
            try {
                if(b==null||b.Binding==null||visible)throw new InvalidOperationException("staged batch required");
                long size=checked((long)(b.Positions.Length+(b.Normals==null?0:b.Normals.Length)+(b.Uv==null?0:b.Uv.Length)+b.Indices.Length)*4);
                if(rows.Count>=maxMeshes||size>maxPayload-payload)throw new InvalidOperationException("native geometry budget");
                Appearance a;if(!appearances.TryGetValue(b.Binding.Id,out a)) {
                    if(appearances.Count>=maxAppearances)throw new InvalidOperationException("native appearance budget");
                    var original=resolver.Resolve(b.Binding);if(!original)throw new InvalidOperationException("source appearance unavailable");
                    a=new Appearance {Binding=b.Binding,Original=original,Clone=new Material(original),Integrity=new MaterialIntegrityR3(original)};appearances.Add(b.Binding.Id,a);
                    if(!resolver.Matches(b.Binding,original,a.Clone))throw new InvalidOperationException("source appearance mismatch");
                }
                var row=new Row {Positions=(float[])b.Positions.Clone(),Normals=b.Normals==null?null:(float[])b.Normals.Clone(),Uv=b.Uv==null?null:(float[])b.Uv.Clone(),Indices=(int[])b.Indices.Clone(),Appearance=a};rows.Add(row);
                row.Mesh=new Mesh();row.Mesh.indexFormat=IndexFormat.UInt16;row.Mesh.vertices=V(b.Positions);
                if(b.Normals!=null)row.Mesh.normals=V(b.Normals);
                if(b.Uv!=null){var uv=new Vector2[b.Uv.Length/2];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(b.Uv[2*i],b.Uv[2*i+1]);row.Mesh.uv=uv;}
                row.Mesh.triangles=b.Indices;row.Mesh.RecalculateBounds();row.Bounds=row.Mesh.bounds;Finite(row.Bounds);row.Submesh=row.Mesh.GetSubMesh(0);row.Attributes=row.Mesh.GetVertexAttributes();
                row.Node=new GameObject(b.Binding.Id);row.Node.transform.SetParent(root.transform,false);
                row.Filter=row.Node.AddComponent<MeshFilter>();row.Filter.sharedMesh=row.Mesh;
                row.Renderer=row.Node.AddComponent<MeshRenderer>();row.Renderer.sharedMaterial=a.Clone;row.Renderer.enabled=false;
                row.RenderingLayer=row.Renderer.renderingLayerMask;row.Shadows=row.Renderer.shadowCastingMode;row.ReceiveShadows=row.Renderer.receiveShadows;row.LightProbes=row.Renderer.lightProbeUsage;row.ReflectionProbes=row.Renderer.reflectionProbeUsage;row.Motion=row.Renderer.motionVectorGenerationMode;row.Occlusion=row.Renderer.allowOcclusionWhenDynamic;payload+=size;
                row.Lightmap=row.Renderer.lightmapIndex;row.RealtimeLightmap=row.Renderer.realtimeLightmapIndex;row.LightmapScale=row.Renderer.lightmapScaleOffset;row.RealtimeLightmapScale=row.Renderer.realtimeLightmapScaleOffset;row.GI=EditorReceiveGI(row.Renderer);
                Need(row.Renderer.enlightenVertexStream==null);
            }catch{Dispose();throw;}
        }
        // receiveGI is an Editor-only authoring API. Player lighting remains
        // checked through the runtime lightmap/probe state above.
        static ReceiveGI EditorReceiveGI(MeshRenderer renderer)
        {
#if UNITY_EDITOR
            return renderer.receiveGI;
#else
            return ReceiveGI.LightProbes;
#endif
        }
        static void Need(bool v){if(!v)throw new InvalidOperationException("quarantine native dependency/graph changed");}
        static void Finite(Bounds b){foreach(float f in new[]{b.center.x,b.center.y,b.center.z,b.extents.x,b.extents.y,b.extents.z,b.min.x,b.min.y,b.min.z,b.max.x,b.max.y,b.max.z})Need(!Single.IsNaN(f)&&!Single.IsInfinity(f));Need(b.extents.x>=0&&b.extents.y>=0&&b.extents.z>=0);}
        static bool Identity(Transform t){return t.localPosition.Equals(Vector3.zero)&&t.localRotation.Equals(Quaternion.identity)&&t.localScale.Equals(Vector3.one);}
        static void Equal<T>(T[] a,T[] b){Need(a.Length==b.Length);for(int i=0;i<a.Length;i++)Need(a[i].Equals(b[i]));}
        public void Validate()
        {
            Lane();if(closed)throw new ObjectDisposedException("NativeGeometryStore");
            try {
                foreach(var a in appearances.Values)Need(a.Original&&a.Clone&&resolver.Resolve(a.Binding)==a.Original&&a.Integrity.Matches(a.Original,true)&&a.Integrity.Matches(a.Clone,false)&&resolver.Matches(a.Binding,a.Original,a.Clone));
                if(visible){Need(lifetime!=null);lifetime.Check();}
                Need(root&&root.activeSelf==visible&&root.transform.parent==null&&Identity(root.transform)&&root.layer==0&&root.GetComponents<Component>().Length==1&&root.transform.childCount==rows.Count);
                foreach(var r in rows) {
                    Need(r.Node&&r.Mesh&&r.Filter&&r.Renderer&&r.Node.transform.parent==root.transform&&Identity(r.Node.transform)&&r.Node.transform.childCount==0&&r.Node.layer==0&&r.Node.activeSelf);
                    Need(r.Node.GetComponents<Component>().Length==3&&r.Filter.sharedMesh==r.Mesh&&r.Renderer.enabled==visible&&r.Renderer.sharedMaterials.Length==1&&r.Renderer.sharedMaterial==r.Appearance.Clone);
                    Need(!r.Renderer.additionalVertexStreams&&!r.Renderer.forceRenderingOff&&!r.Renderer.HasPropertyBlock()&&r.Renderer.localBounds.Equals(r.Bounds)&&r.Renderer.bounds.Equals(r.Bounds));
                    Need(r.Renderer.renderingLayerMask==r.RenderingLayer&&r.Renderer.shadowCastingMode==r.Shadows&&r.Renderer.receiveShadows==r.ReceiveShadows&&r.Renderer.lightProbeUsage==r.LightProbes&&r.Renderer.reflectionProbeUsage==r.ReflectionProbes&&r.Renderer.motionVectorGenerationMode==r.Motion&&r.Renderer.allowOcclusionWhenDynamic==r.Occlusion&&r.Renderer.probeAnchor==null&&r.Renderer.lightProbeProxyVolumeOverride==null&&r.Renderer.sortingLayerID==0&&r.Renderer.sortingOrder==0);
                    Need(r.Renderer.lightmapIndex==r.Lightmap&&r.Renderer.realtimeLightmapIndex==r.RealtimeLightmap&&r.Renderer.lightmapScaleOffset.Equals(r.LightmapScale)&&r.Renderer.realtimeLightmapScaleOffset.Equals(r.RealtimeLightmapScale)&&EditorReceiveGI(r.Renderer)==r.GI&&r.Renderer.enlightenVertexStream==null);
                    Need(r.Mesh.indexFormat==IndexFormat.UInt16&&r.Mesh.subMeshCount==1&&r.Mesh.GetTopology(0)==MeshTopology.Triangles&&r.Mesh.bounds.Equals(r.Bounds));
                    Finite(r.Mesh.bounds);Need(r.Mesh.GetSubMesh(0).Equals(r.Submesh)&&r.Mesh.tangents.Length==0&&r.Mesh.colors.Length==0&&r.Mesh.blendShapeCount==0&&r.Mesh.bindposes.Length==0);Equal(r.Mesh.GetVertexAttributes(),r.Attributes);
                    Equal(r.Mesh.vertices,V(r.Positions));Equal(r.Mesh.triangles,r.Indices);Equal(r.Mesh.GetIndices(0,false),r.Indices);
                    Equal(r.Mesh.normals,r.Normals==null?new Vector3[0]:V(r.Normals));
                    var uv=r.Mesh.uv;Need(uv.Length==(r.Uv==null?0:r.Uv.Length/2));for(int i=0;i<uv.Length;i++)Need(uv[i].x==r.Uv[i*2]&&uv[i].y==r.Uv[i*2+1]);
                }
            }catch{Dispose();throw;}
        }
        static void Release(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        // Root calls only after FullGeometry.Validate, on the Unity main lane. This is a
        // quarantined visual preview, never map/gameplay approval. No collider is created.
        public void ActivatePreview(PreviewLifetime owner)
        {
            Lane();if(owner==null)throw new ArgumentNullException("owner");
            try{Validate();if(visible)throw new InvalidOperationException("already active");owner.Check();lifetime=owner;lifetime.Revoked+=Revoke;visible=true;foreach(var r in rows)r.Renderer.enabled=true;root.SetActive(true);Validate();}catch{Dispose();throw;}
        }
        void Revoke(){Dispose();}
        public void Withdraw(){Lane();visible=false;if(lifetime!=null){lifetime.Revoked-=Revoke;lifetime=null;}if(root)root.SetActive(false);foreach(var r in rows){if(r.Renderer)r.Renderer.enabled=false;if(r.Node)r.Node.SetActive(false);}}
        public void Dispose(){Lane();if(closed)return;closed=true;Withdraw();foreach(var r in rows){Release(r.Node);Release(r.Mesh);}Release(root);foreach(var a in appearances.Values)Release(a.Clone);rows.Clear();appearances.Clear();payload=0;}
    }
}
#endif
