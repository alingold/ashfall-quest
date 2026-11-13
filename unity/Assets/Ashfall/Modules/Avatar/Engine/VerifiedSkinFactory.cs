#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Modules.Avatar
{
    internal sealed class SkinBinding
    {
        internal VerifiedSkin Data; internal Transform[] Nodes;
        internal void Apply(ISkinPose pose) {
            if(pose==null)throw new AvatarValidationException("Pose required");
            float[][] local=new FrozenSkinPose(Data,pose.CopyLocalMatrices()).CopyLocalMatrices();
            for(int i=0;i<local.Length;i++){var m=VerifiedSkinFactory.Matrix(local[i]);Nodes[i].localPosition=m.GetColumn(3);Nodes[i].localRotation=m.rotation;Nodes[i].localScale=m.lossyScale;}
        }
    }
    internal static class VerifiedSkinFactory
    {
        internal static Matrix4x4 Matrix(float[] m) { var result=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)result[r,c]=m[r*4+c];return result; }
        internal static GameObject Create(PreparedPart part,VerifiedSkin data,Transform parent,List<UnityEngine.Object> resources,out SkinBinding binding,HandMaterialReceipt receipt=null) {
            binding=null;
            data.RequireBinding(part);
            if(receipt!=null) NormalMapGeometry.Validate(data.CopyVertices(),data.CopyNormals(),data.CopyUv(),data.CopyIndices());
            if(receipt==null&&data.MaterialSlots!="default")throw new InvalidOperationException("Authored skin materials require a verified material adapter");
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Required URP Lit shader unavailable");
            var root=new GameObject(part.Node+"-skin");root.SetActive(false);root.transform.SetParent(parent,false);
            var local=data.CopyLocalMatrices();var parents=data.CopyParents();var names=data.CopyNames();var transforms=new Transform[local.Length];
            // All source nodes, including non-joint ancestors, stay inside this inert root.
            for(int i=0;i<transforms.Length;i++){transforms[i]=new GameObject(names[i]+"#"+i).transform;transforms[i].SetParent(root.transform,false);}
            for(int i=0;i<transforms.Length;i++)transforms[i].SetParent(parents[i]<0?root.transform:transforms[parents[i]],false);
            for(int i=0;i<transforms.Length;i++){var m=Matrix(local[i]);transforms[i].localPosition=m.GetColumn(3);transforms[i].localRotation=m.rotation;transforms[i].localScale=m.lossyScale;}
            var mesh=new Mesh();resources.Add(mesh);float[] v=data.CopyVertices(),n=data.CopyNormals(),uv=data.CopyUv(),w=data.CopyWeights();int[] inf=data.CopyInfluences();
            var vertices=new Vector3[v.Length/3];var normals=new Vector3[vertices.Length];var uvs=new Vector2[vertices.Length];var weights=new BoneWeight[vertices.Length];
            for(int i=0;i<vertices.Length;i++){vertices[i]=new Vector3(v[i*3],v[i*3+1],v[i*3+2]);normals[i]=new Vector3(n[i*3],n[i*3+1],n[i*3+2]);uvs[i]=new Vector2(uv[i*2],uv[i*2+1]);weights[i]=new BoneWeight{boneIndex0=inf[i*4],boneIndex1=inf[i*4+1],boneIndex2=inf[i*4+2],boneIndex3=inf[i*4+3],weight0=w[i*4],weight1=w[i*4+1],weight2=w[i*4+2],weight3=w[i*4+3]};}
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uvs;mesh.triangles=data.CopyIndices();mesh.boneWeights=weights;
            mesh.RecalculateTangents();
            if(receipt!=null) RequireNormalBasis(mesh);
            int[] joints=data.CopyJoints();var bones=new Transform[joints.Length];var bm=data.CopyBindMatrices();var binds=new Matrix4x4[bm.Length];for(int i=0;i<bones.Length;i++){bones[i]=transforms[joints[i]];binds[i]=Matrix(bm[i]);}mesh.bindposes=binds;mesh.RecalculateBounds();
            Material material;if(receipt!=null)material=ReceiptMaterialFactory.Create(shader,receipt,resources);else {material=new Material(shader);resources.Add(material);material.SetColor("_BaseColor",Color.white);}
            var renderer=transforms[data.MeshNode].gameObject.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones;renderer.rootBone=root.transform;renderer.sharedMaterial=material;renderer.localBounds=mesh.bounds;renderer.updateWhenOffscreen=true;
            AvatarView.ValidateInertHierarchy(root);binding=new SkinBinding{Data=data,Nodes=transforms};root.SetActive(true);return root;
        }
        internal static void RequireNormalBasis(Mesh mesh) {
            var normals=mesh.normals;var tangents=mesh.tangents;
            if(tangents.Length!=mesh.vertexCount)throw new AvatarValidationException("Missing generated tangent basis");
            for(int i=0;i<tangents.Length;i++) {
                var t=tangents[i];var v=new Vector3(t.x,t.y,t.z);var n=normals[i];
                if(!Finite(t.x)||!Finite(t.y)||!Finite(t.z)||!Finite(t.w)||
                   Math.Abs(v.sqrMagnitude-1)>0.002f || Math.Abs(n.sqrMagnitude-1)>0.002f ||
                   Math.Abs(Vector3.Dot(n,v))>0.002f || Math.Abs(t.w)!=1)
                    throw new AvatarValidationException("Invalid generated normal-map basis");
            }
        }
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
    }
}
#endif


