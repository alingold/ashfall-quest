#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Appearance;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ashfall.Integration.Playable.World
{
    [DefaultExecutionOrder(1600)]
    public sealed class R26RangeMirror:MonoBehaviour
    {
        public const int TextureSize=1536;const int TextureHeight=1920,SurfaceLayer=30;
        const float HalfWidth=1,HalfHeight=1.25f,Near=.03f,Far=60;
        readonly Plane[] viewPlanes=new Plane[6];
        R23PlayableLoop root;Camera reflection;RenderTexture texture;Material face,frame;Mesh panel;Renderer surface;float nextRender;
        readonly UniversalRenderPipeline.SingleCameraRequest request=new UniversalRenderPipeline.SingleCameraRequest();
        public int RenderCount{get;private set;}public Transform Surface{get{return transform;}}
        public void Initialize(R23PlayableLoop loop)
        {
            root=loop;texture=new RenderTexture(TextureSize,TextureHeight,16,RenderTextureFormat.ARGB32);texture.name="Range mirror 1536 x 1920 mono";texture.wrapMode=TextureWrapMode.Clamp;request.destination=texture;
            face=new Material(Shader.Find("Ashfall/R26RangeMirror"));face.SetTexture("_BaseMap",texture);
            frame=new Material(Shader.Find("Universal Render Pipeline/Unlit"));frame.SetColor("_BaseColor",new Color(.12f,.22f,.25f));
            var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Range mirror wall and frame";backing.transform.SetParent(transform,false);backing.transform.localPosition=new Vector3(0,0,-.06f);backing.transform.localScale=new Vector3(2.12f,2.62f,.1f);backing.GetComponent<MeshRenderer>().sharedMaterial=frame;
            var node=new GameObject("2 m x 2.5 m planar mirror");node.layer=SurfaceLayer;node.transform.SetParent(transform,false);
            panel=new Mesh();panel.name="Range mirror plane";panel.vertices=new[]{new Vector3(-HalfWidth,-HalfHeight,0),new Vector3(-HalfWidth,HalfHeight,0),new Vector3(HalfWidth,HalfHeight,0),new Vector3(HalfWidth,-HalfHeight,0)};panel.triangles=new[]{0,1,2,0,2,3};panel.RecalculateBounds();
            node.AddComponent<MeshFilter>().sharedMesh=panel;surface=node.AddComponent<MeshRenderer>();surface.sharedMaterial=face;surface.shadowCastingMode=ShadowCastingMode.Off;
            var cameraNode=new GameObject("Range planar reflection (head-centre, 15 Hz)");cameraNode.transform.SetParent(transform,false);reflection=cameraNode.AddComponent<Camera>();reflection.enabled=false;
            var data=reflection.GetUniversalAdditionalCameraData();data.allowXRRendering=false;data.renderShadows=false;data.renderPostProcessing=false;data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
        }
        void LateUpdate()
        {
            if(root==null||!root.Ready||root.Map.MapId!="range"||root.Rig==null||!gameObject.activeInHierarchy)return;
            var eye=root.Rig.Head;Vector3 toward=transform.position-eye.transform.position;
            if(Time.unscaledTime<nextRender||toward.sqrMagnitude>64||Vector3.Dot(transform.forward,-toward)<=.02f)return;
            GeometryUtility.CalculateFrustumPlanes(eye,viewPlanes);
            // A small bounds margin covers the difference between head-centre and either eye.
            var bounds=surface.bounds;bounds.Expand(.16f);
            if(!GeometryUtility.TestPlanesAABB(viewPlanes,bounds))return;
            nextRender=Time.unscaledTime+1f/15;RenderReflection(eye);
        }
        public void RenderReflection(Camera eye)
        {
            Vector3 normal=transform.forward,point=transform.position;float d=-Vector3.Dot(normal,point);
            Matrix4x4 mirror=Matrix4x4.identity;
            for(int row=0;row<3;row++){for(int col=0;col<3;col++)mirror[row,col]-=2*normal[row]*normal[col];mirror[row,3]=-2*d*normal[row];}
            var eyePosition=eye.transform.position;var reflectedEye=mirror.MultiplyPoint(eyePosition);
            reflection.transform.SetPositionAndRotation(reflectedEye,Quaternion.LookRotation(normal,transform.up));
            reflection.nearClipPlane=Near;reflection.farClipPlane=Far;reflection.clearFlags=eye.clearFlags;reflection.backgroundColor=eye.backgroundColor;reflection.allowHDR=false;reflection.allowMSAA=false;
            reflection.cullingMask=(eye.cullingMask|(1<<R23PlayerAppearance.MirrorHeadLayer))&~(1<<SurfaceLayer);
            // Match the asymmetric frustum to the panel, so every texel covers a visible part of the mirror.
            var aligned=Matrix4x4.TRS(eyePosition,Quaternion.LookRotation(-normal,transform.up),Vector3.one).inverse;
            reflection.worldToCameraMatrix=Matrix4x4.Scale(new Vector3(1,1,-1))*aligned*mirror;
            var center=reflection.worldToCameraMatrix.MultiplyPoint(point);
            if(-center.z<=Near)return;
            float scale=Near/-center.z;
            reflection.projectionMatrix=Matrix4x4.Frustum((center.x-HalfWidth)*scale,(center.x+HalfWidth)*scale,(center.y-HalfHeight)*scale,(center.y+HalfHeight)*scale,Near,Far);
            if(!texture.IsCreated())texture.Create();
            Vector3 clipPoint=reflection.worldToCameraMatrix.MultiplyPoint(point+normal*.015f),clipNormal=reflection.worldToCameraMatrix.MultiplyVector(normal).normalized;
            reflection.projectionMatrix=reflection.CalculateObliqueMatrix(new Vector4(clipNormal.x,clipNormal.y,clipNormal.z,-Vector3.Dot(clipPoint,clipNormal)));
            face.SetMatrix("_MirrorVP",GL.GetGPUProjectionMatrix(reflection.projectionMatrix,true)*reflection.worldToCameraMatrix);
            bool prior=GL.invertCulling;try{GL.invertCulling=!prior;RenderPipeline.SubmitRenderRequest(reflection,request);RenderCount++;}finally{GL.invertCulling=prior;}
        }
        void OnDestroy(){if(texture!=null){texture.Release();Destroy(texture);}if(face!=null)Destroy(face);if(frame!=null)Destroy(frame);if(panel!=null)Destroy(panel);}
    }
}
#endif
