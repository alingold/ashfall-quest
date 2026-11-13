#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Two 812-vertex source gloves, existing MeshRenderers/materials. Reuses all
    // arrays; updates only while bones change. No tracked or contact transforms.
    internal sealed class R23GloveArticulation
    {
        [Serializable]public sealed class Node{public string name="";public int parent=-1;public float[] position=null,scale=null,rotation=null,opened=null,grip=null,pistol=null,shaft=null;public bool animated=false,index=false;}
        [Serializable]public sealed class Joint{public int node=0;public float[] inverseBind=null;}
        [Serializable]public sealed class Part{public int vertexCount=0;public float[] positions=null,normals=null,weights=null;public int[] jointIndices=null;}
        [Serializable]public sealed class Data{public int schema=0;public string side="",sourceMeshSha256="";public Node[] nodes=null;public Joint[] joints=null;public Part[] parts=null;public float[] bake=null;}
        sealed class Surface{public Mesh mesh;public Part source;public Vector3[] vertices,normals,sourceTangents;public Vector4[] tangents;}
        readonly Data data;readonly Matrix4x4 bake;readonly Matrix4x4[] world,skin,restLocal,inverseBind;readonly Quaternion[] current;readonly Surface[] surfaces;
        public int Updates{get;private set;}
        static Vector3 V(float[] a,int at=0){return new Vector3(a[at],a[at+1],a[at+2]);}
        static Quaternion Q(float[] a){return new Quaternion(a[0],a[1],a[2],a[3]);}
        static Matrix4x4 M(float[] a){var m=new Matrix4x4();for(int row=0;row<4;row++)for(int col=0;col<4;col++)m[row,col]=a[row*4+col];return m;}
        public R23GloveArticulation(string json,MeshFilter[] filters)
        {
            data=JsonUtility.FromJson<Data>(json);
            if(data==null||data.schema!=1||data.nodes.Length!=28||data.joints.Length!=26||filters.Length!=data.parts.Length)throw new InvalidOperationException("Source glove skin membership");
            bake=M(data.bake);world=new Matrix4x4[data.nodes.Length];restLocal=new Matrix4x4[world.Length];current=new Quaternion[world.Length];skin=new Matrix4x4[data.joints.Length];inverseBind=new Matrix4x4[skin.Length];surfaces=new Surface[filters.Length];
            for(int i=0;i<inverseBind.Length;i++)inverseBind[i]=M(data.joints[i].inverseBind);
            for(int i=0;i<world.Length;i++){var n=data.nodes[i];if(n.parent>=i||n.parent< -1)throw new InvalidOperationException("Source hand parent order");current[i]=Q(n.rotation);restLocal[i]=Matrix4x4.TRS(V(n.position),current[i],V(n.scale));}
            Matrices();
            for(int p=0;p<surfaces.Length;p++){
                var source=data.parts[p];var mesh=filters[p].sharedMesh;int count=mesh.vertexCount;
                if(count!=source.vertexCount||source.positions.Length!=count*3||source.normals.Length!=count*3||source.weights.Length!=count*4||source.jointIndices.Length!=count*4)throw new InvalidOperationException("Source glove vertex/weight membership");
                var s=new Surface{source=source,mesh=mesh,vertices=mesh.vertices,normals=mesh.normals,tangents=mesh.tangents,sourceTangents=new Vector3[count]};
                if(s.tangents.Length!=count)throw new InvalidOperationException("Source glove tangents missing");
                for(int vi=0;vi<count;vi++){
                    float sum=0;var blend=new Matrix4x4();
                    for(int k=0;k<4;k++){int at=vi*4+k,j=source.jointIndices[at];float w=source.weights[at];if(j<0||j>=skin.Length||w<0||float.IsNaN(w)||float.IsInfinity(w))throw new InvalidOperationException("Source glove weight");sum+=w;for(int element=0;element<16;element++)blend[element]+=skin[j][element]*w;}
                    if(Mathf.Abs(sum-1)>2e-6f)throw new InvalidOperationException("Source glove weight sum");
                    s.sourceTangents[vi]=(bake*blend).inverse.MultiplyVector(new Vector3(s.tangents[vi].x,s.tangents[vi].y,s.tangents[vi].z));
                }
                mesh.MarkDynamic();surfaces[p]=s;
            }
            for(int i=0;i<current.Length;i++)if(data.nodes[i].animated)current[i]=Q(data.nodes[i].opened);
            Matrices();Render();
        }
        void Matrices()
        {
            for(int i=0;i<world.Length;i++){var n=data.nodes[i];var local=n.animated?Matrix4x4.TRS(V(n.position),current[i],V(n.scale)):restLocal[i];world[i]=n.parent<0?local:world[n.parent]*local;}
            for(int i=0;i<skin.Length;i++)skin[i]=world[data.joints[i].node]*inverseBind[i];
        }
        public bool Pose(string id,bool sword,bool holding,float grip,float trigger,float dt)
        {
            float blend=R23GlovePoseRules.Blend(dt);if(blend==0)return false;int pose=R23GlovePoseRules.Pose(id,sword);bool changed=false;
            for(int i=0;i<current.Length;i++){
                var n=data.nodes[i];if(!n.animated)continue;
                float amount=R23GlovePoseRules.Closure(id,sword,n.index,n.index?trigger:grip,holding);
                var target=Quaternion.SlerpUnclamped(Q(n.opened),Q(pose==2?n.shaft:pose==1?n.pistol:n.grip),amount);
                if(Quaternion.Dot(current[i],target)<0)target=new Quaternion(-target.x,-target.y,-target.z,-target.w);
                // Quaternion's == uses an angular approximation. It can declare
                // a finger settled while its tip still misses the source pose.
                var q=current[i];
                if(R23GlovePoseRules.Settled(q.x-target.x,q.y-target.y,q.z-target.z,q.w-target.w)){if(!q.Equals(target)){current[i]=target;changed=true;}continue;}
                current[i]=Quaternion.SlerpUnclamped(current[i],target,blend);changed=true;
            }
            if(!changed)return false;Matrices();Render();Updates++;return true;
        }
        void Render()
        {
            foreach(var s in surfaces){var p=s.source;Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
                for(int vi=0;vi<p.vertexCount;vi++){
                    Vector3 v=Vector3.zero,n=Vector3.zero,t=Vector3.zero;var sourceV=V(p.positions,vi*3);var sourceN=V(p.normals,vi*3);
                    for(int k=0;k<4;k++){int at=vi*4+k;float weight=p.weights[at];if(weight==0)continue;var m=skin[p.jointIndices[at]];v+=m.MultiplyPoint3x4(sourceV)*weight;n+=m.MultiplyVector(sourceN)*weight;t+=m.MultiplyVector(s.sourceTangents[vi])*weight;}
                    v=bake.MultiplyPoint3x4(v);n=bake.MultiplyVector(n).normalized;t=bake.MultiplyVector(t);t=(t-n*Vector3.Dot(n,t)).normalized;
                    s.vertices[vi]=v;s.normals[vi]=n;s.tangents[vi]=new Vector4(t.x,t.y,t.z,s.tangents[vi].w);min=Vector3.Min(min,v);max=Vector3.Max(max,v);
                }
                s.mesh.vertices=s.vertices;s.mesh.normals=s.normals;s.mesh.tangents=s.tangents;var bounds=new Bounds();bounds.SetMinMax(min,max);s.mesh.bounds=bounds;
            }
        }
    }
}
#endif
