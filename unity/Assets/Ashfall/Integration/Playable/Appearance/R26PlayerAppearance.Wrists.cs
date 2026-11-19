#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Appearance
{
    public sealed partial class R23PlayerAppearance
    {
        readonly GameObject[] armNodes=new GameObject[2];readonly Mesh[] armMeshes=new Mesh[2];
        readonly R26AvatarArmSurface[] armSurfaces={new R26AvatarArmSurface(),new R26AvatarArmSurface()};
        void CacheArmSurfaces()
        {
            DisposeArmSurfaces();CacheMirrorHead();
            for(int i=0;i<2;i++)
            {
                string prefix=i==0?"L":"R";
                var node=new GameObject(prefix+" continuous arm");node.transform.SetParent(current.Root.transform,false);armNodes[i]=node;
                var surface=armSurfaces[i];var mesh=new Mesh();mesh.name="R26 continuous sleeve and wrist";mesh.MarkDynamic();armMeshes[i]=mesh;
                Vector2 sleeveUv=joints.SleeveUv[i];
                var uv=new Vector2[R26AvatarArmSurface.VertexCount];
                for(int r=0;r<R26AvatarArmSurface.Rings;r++)for(int j=0;j<R26AvatarArmSurface.Stride;j++)
                {
                    float t=(float)j/R26AvatarArmSurface.Sides;
                    uv[r*R26AvatarArmSurface.Stride+j]=r<R26AvatarArmSurface.WristStart
                        ?sleeveUv+new Vector2((t-.5f)*.003f,(float)r/R26AvatarArmSurface.Rings*.004f)
                        :new Vector2(t,(float)(r-R26AvatarArmSurface.WristStart)/(R26AvatarArmSurface.Rings-1-R26AvatarArmSurface.WristStart));
                }
                for(int j=0;j<R26AvatarArmSurface.Stride;j++)uv[R26AvatarArmSurface.SleeveEnd+j]=sleeveUv;
                mesh.vertices=surface.Vertices;mesh.uv=uv;mesh.subMeshCount=2;
                mesh.SetTriangles(R26AvatarArmSurface.Indices(false),0,false);mesh.SetTriangles(R26AvatarArmSurface.Indices(true),1,false);
                node.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{jointUpper[i].GetComponent<MeshRenderer>().sharedMaterial,current.GloveMaterial};
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        static Vector2 SleeveClothUv(Mesh source,JointSpan span)
        {
            var points=source.vertices;var normals=source.normals;var uv=source.uv;var triangles=source.triangles;
            float height=Mathf.Lerp(span.From.y,span.To.y,.68f),best=float.PositiveInfinity;Vector2 sample=Vector2.zero;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                float score=Mathf.Abs((points[a].y+points[b].y+points[c].y)/3-height)+.4f*Mathf.Abs((normals[a].y+normals[b].y+normals[c].y)/3);
                if(score<best){best=score;sample=(uv[a]+uv[b]+uv[c])/3;}
            }
            return sample;
        }
        void PresentArmSurface(int i,Vector3 seat,Vector3 shoulder,Vector3 elbow,Vector3 wrist,Vector3 handAxis,Vector3 handRight,float scale,Vector3 pole,Quaternion body)
        {
            var surface=armSurfaces[i];Quaternion inverse=Quaternion.Inverse(body);
            surface.Pose(Vector3.zero,inverse*(shoulder-seat),inverse*(elbow-seat),inverse*(wrist-seat),inverse*handAxis,inverse*handRight,scale,pole);
            armNodes[i].transform.SetPositionAndRotation(seat,body);
            var mesh=armMeshes[i];mesh.vertices=surface.Vertices;mesh.normals=surface.Normals;mesh.tangents=surface.Tangents;mesh.RecalculateBounds();
            JointSegment(jointUpper[i],joints.Upper[i],seat+body*surface.UpperFrom,seat+body*surface.UpperTo,.105f,body);
            JointSegment(jointForearm[i],joints.Forearm[i],seat+body*surface.ForeFrom,seat+body*surface.ForeTo,.085f,body);
        }
        void DisposeArmSurfaces()
        {
            for(int i=0;i<2;i++){if(armNodes[i]!=null)Destroy(armNodes[i]);if(armMeshes[i]!=null)Destroy(armMeshes[i]);armNodes[i]=null;armMeshes[i]=null;}
        }
        // Layer 29 is render-only local head art. Hide it from the owning eye,
        // retain it for the range reflection, without toggling renderers mid-pass.
        public const int MirrorHeadLayer=29;
        Camera mirrorOwnerEye;int mirrorOwnerMask;bool mirrorMaskOwned;
        void CacheMirrorHead()
        {
            bool local=!RemoteMode&&!PreviewMode;var part=current.Parts["AvatarHead"];
            if(local)
            {
                if(mirrorOwnerEye==null){mirrorOwnerEye=head.GetComponent<Camera>();if(mirrorOwnerEye!=null){mirrorOwnerMask=mirrorOwnerEye.cullingMask;mirrorMaskOwned=(mirrorOwnerMask&(1<<MirrorHeadLayer))!=0;mirrorOwnerEye.cullingMask&=~(1<<MirrorHeadLayer);}}
                foreach(var renderer in part.GetComponentsInChildren<MeshRenderer>(true))renderer.gameObject.layer=MirrorHeadLayer;
            }
        }
        void PresentLocalMirrorHead(){current.Parts["AvatarHead"].gameObject.SetActive(true);}
        void ReleaseMirrorHead(){if(mirrorOwnerEye!=null&&mirrorMaskOwned)mirrorOwnerEye.cullingMask|=1<<MirrorHeadLayer;mirrorOwnerEye=null;mirrorMaskOwned=false;}
    }
}
#endif
