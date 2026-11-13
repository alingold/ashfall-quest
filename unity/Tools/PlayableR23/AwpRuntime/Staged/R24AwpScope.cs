#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Core;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public struct R24AwpScopeInput {public bool Valid,Supported,SupportHeld,PrimaryReady,SupportReady;public Vector2 Stick;}
    // Actual awp_scope.gd: one768x768 shared binocular scene at30Hz, 4/8x.
    // Aperture uses each render's inverse projection; never changes the HMD/IPD.
    [DefaultExecutionOrder(1200)]
    public sealed class R24AwpScope:MonoBehaviour
    {
        Camera head,view;RenderTexture image;Material material;Transform overlay;Mesh mesh;MeshRenderer renderer;
        Func<R23ShotPose> candidate;Func<Vector3,Vector3,bool> clear;R24AwpScopeInput input;readonly R24AwpZoom zoom=new R24AwpZoom();
        bool initialized,gripLatch,displayedValid,pole,presentationPole,presentationValid;Vector3 lastRight=Vector3.right,presentationRight=Vector3.right;float elapsed=1f/30;
        R23ShotPose displayed;public bool Active{get;private set;}public float Magnification{get{return zoom.Magnification;}}public long FramesRequested{get;private set;}
        public bool TryReadDisplayedPose(out R23ShotPose pose){pose=displayed;return Active&&displayedValid;}
        public void Initialize(Camera viewer,Transform cameraRoot,Func<R23ShotPose> currentUnpublishedCandidate,Func<Vector3,Vector3,bool> actualPathClear)
        {
            if(initialized||viewer==null||cameraRoot==null||currentUnpublishedCandidate==null||actualPathClear==null)throw new ArgumentException("AWP actual optical inputs");head=viewer;candidate=currentUnpublishedCandidate;clear=actualPathClear;
            var shader=Resources.Load<Shader>("R24AwpScope");if(shader==null)throw new InvalidOperationException("Source AWP angular compositor missing");material=new Material(shader);material.enableInstancing=true;
            image=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32);image.name="Source AWP shared infinite-distance optical image";image.antiAliasing=1;image.useMipMap=false;image.filterMode=FilterMode.Bilinear;image.wrapMode=TextureWrapMode.Clamp;image.Create();material.SetTexture("_OpticalView",image);
            var cameraObject=new GameObject("Source AWP30Hz shared optical camera");cameraObject.transform.SetParent(cameraRoot,false);view=cameraObject.AddComponent<Camera>();view.enabled=false;view.stereoTargetEye=StereoTargetEyeMask.None;view.targetTexture=image;view.aspect=1;view.nearClipPlane=.05f;view.farClipPlane=Mathf.Max(220,head.farClipPlane);view.cullingMask=1|(1<<19);view.clearFlags=head.clearFlags;view.backgroundColor=head.backgroundColor;view.allowHDR=false;view.allowMSAA=false;view.depth=head.depth-20;view.fieldOfView=R24AwpMath.OpticalFov(4);
            overlay=new GameObject("Source AWP central angular scope and black surround").transform;overlay.SetParent(head.transform,false);overlay.localPosition=new Vector3(0,0,.45f);overlay.gameObject.layer=18;
            mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.bounds=new Bounds(Vector3.zero,Vector3.one*10000);overlay.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=overlay.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.allowOcclusionWhenDynamic=false;renderer.enabled=false;initialized=true;Application.onBeforeRender+=RefreshPresentation;
        }
        public void SetInput(R24AwpScopeInput sample){input=sample;if(!sample.Valid)Shutdown();}
        void LateUpdate()
        {
            if(!initialized)return;view.enabled=false;var pose=candidate();bool valid=input.Valid&&isActiveAndEnabled&&head!=null&&head.gameObject.activeInHierarchy&&pose.Valid&&clear(head.transform.position,V(pose.Origin));
            if(!input.SupportHeld)gripLatch=false;else if(input.Supported)gripLatch=true;
            bool scoped=valid&&input.PrimaryReady&&input.SupportReady&&input.SupportHeld&&gripLatch;float oldZoom=zoom.Magnification;zoom.Sample(input.Stick.x,input.Stick.y,scoped,valid);
            if(!scoped){Shutdown();elapsed=1f/30;return;}if(oldZoom!=zoom.Magnification)elapsed=1f/30;elapsed+=Mathf.Max(0,Time.unscaledDeltaTime);
            if(elapsed>=1f/30)
            {
                elapsed%=1f/30;Vector3 forward=V(pose.Forward),right=LevelRight(forward,lastRight,pole,displayedValid);if(Mathf.Abs(forward.y)>.98f)pole=true;lastRight=right;var up=Vector3.Cross(forward,right).normalized;
                displayed=new R23ShotPose(pose.Origin,F(right),F(up),pose.Forward);displayedValid=true;
                view.transform.SetPositionAndRotation(V(pose.Origin),Quaternion.LookRotation(forward,up));view.fieldOfView=R24AwpMath.OpticalFov(zoom.Magnification);view.farClipPlane=Mathf.Max(220,head.farClipPlane);view.clearFlags=head.clearFlags;view.backgroundColor=head.backgroundColor;view.enabled=true;FramesRequested++;
            }
            Active=true;renderer.enabled=true;RefreshPresentation();
        }
        static Vector3 LevelRight(Vector3 forward,Vector3 previous,bool crossed,bool have)
        {
            forward.Normalize();Vector3 up=Mathf.Abs(forward.y)>.99999f?Vector3.forward:Vector3.up;var level=Vector3.Cross(up,forward).normalized;if(!have)return level;
            var projected=Vector3.ProjectOnPlane(previous,forward);if(projected.sqrMagnitude<.00001f)return level;projected.Normalize();if(Mathf.Abs(forward.y)>.98f)return projected;return crossed&&Vector3.Dot(level,projected)<0?-level:level;
        }
        [BeforeRenderOrder(1200)]void RefreshPresentation()
        {
            if(!initialized||!Active||head==null)return;Vector3 f=head.transform.forward,r=LevelRight(f,presentationRight,presentationPole,presentationValid);if(Mathf.Abs(f.y)>.98f)presentationPole=true;presentationRight=r;presentationValid=true;material.SetVector("_ViewRight",r);material.SetFloat("_AngularScale",1f/3);
        }
        public void Shutdown(){Active=false;displayedValid=false;gripLatch=false;pole=presentationPole=presentationValid=false;zoom.Reset();if(view!=null)view.enabled=false;if(renderer!=null)renderer.enabled=false;}
        static Vector3 V(Float3 a){return new Vector3(a.X,a.Y,a.Z);}static Float3 F(Vector3 a){return new Float3(a.x,a.y,a.z);}
        void OnDisable(){Shutdown();}void OnDestroy(){Application.onBeforeRender-=RefreshPresentation;Shutdown();if(view!=null)Destroy(view.gameObject);if(overlay!=null)Destroy(overlay.gameObject);if(image!=null){image.Release();Destroy(image);}if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
#endif
