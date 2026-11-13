#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Modules.Avatar
{
    // Constructs geometry directly from sealed decoded bytes, never resolves a prefab.
    internal static class VerifiedRigidMeshFactory
    {
        internal static GameObject Create(PreparedPart part,VerifiedMesh data,Transform parent,List<UnityEngine.Object> resources)
        {
            data.RequireBinding(part);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null) throw new InvalidOperationException("Required URP Lit shader unavailable");
            var root=new GameObject(data.Node); root.SetActive(false); root.transform.SetParent(parent,false);
            var mesh=new Mesh(); resources.Add(mesh);
            float[] v=data.CopyVertices(), n=data.CopyNormals(), uv=data.CopyUv(), rgba=data.CopyColors(), t=data.CopyTransform();
            var vertices=new Vector3[v.Length/3]; var normals=new Vector3[vertices.Length]; var uvs=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++) {
                vertices[i]=new Vector3(v[i*3],v[i*3+1],v[i*3+2]);
                normals[i]=new Vector3(n[i*3],n[i*3+1],n[i*3+2]); uvs[i]=new Vector2(uv[i*2],uv[i*2+1]);
            }
            mesh.vertices=vertices; mesh.normals=normals; mesh.uv=uvs;
            int[][] triangles=data.CopyTriangles(); mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++) mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var materials=new Material[triangles.Length];
            for(int i=0;i<materials.Length;i++) {
                var material=new Material(shader); resources.Add(material);
                material.SetColor("_BaseColor",new Color(rgba[i*4],rgba[i*4+1],rgba[i*4+2],rgba[i*4+3])); materials[i]=material;
            }
            root.AddComponent<MeshRenderer>().sharedMaterials=materials;
            var matrix=new Matrix4x4(); for(int r=0;r<4;r++) for(int c=0;c<4;c++) matrix[r,c]=t[r*4+c];
            root.transform.localPosition=matrix.GetColumn(3); root.transform.localRotation=matrix.rotation; root.transform.localScale=matrix.lossyScale;
            AvatarView.ValidateInertHierarchy(root); root.SetActive(true); return root;
        }
    }
}
#endif
