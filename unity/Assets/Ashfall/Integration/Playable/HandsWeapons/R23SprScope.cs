#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using UnityEngine;using UnityEngine.Rendering.Universal;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Actual two-eye 520x307 SPR image. Never changes the HMD camera or bore authority.
    [DefaultExecutionOrder(1200)]
    internal sealed partial class R23SprScope : MonoBehaviour
    {
        readonly Camera[] views=new Camera[2];
        readonly RenderTexture[] images=new RenderTexture[2];
        readonly List<Renderer> rearGlass=new List<Renderer>();
        readonly Float3[] corners=new Float3[4];
        Camera head;Transform muzzle,lens;
        Material material;Renderer lensRenderer;
        Func<Vector3,Vector3,bool> clear;Func<Vector3> sharedAim;
        bool allowed,initialized;
        const double OpticalInterval=1.0/30;
        double nextOpticalTime;
        int opticalFrame=-1;
        public bool Active { get; private set; }
        public void Initialize(Camera view,Transform bore,Transform cameraRoot,Func<Vector3,Vector3,bool> pathClear,Func<Vector3> displayedAim=null) {
            head=view;muzzle=bore;clear=pathClear;sharedAim=displayedAim;WidenOcular();
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))if(renderer.name.StartsWith("RearOcularGlass/"))rearGlass.Add(renderer);
            var shader=Resources.Load<Shader>("R23SprScope");if(shader==null)throw new InvalidOperationException("Missing R23SprScope shader");material=new Material(shader);material.SetFloat("_Magnification",R23SprScopeMath.Magnification);
            var aperture=GameObject.CreatePrimitive(PrimitiveType.Quad);aperture.name="Source live SPR aperture";aperture.layer=18;aperture.transform.SetParent(transform,false);aperture.transform.localPosition=new Vector3(0,.103f,-.0185f);aperture.transform.localScale=new Vector3(R23SprScopeMath.Width*WindowScale,R23SprScopeMath.Height*WindowScale,1);aperture.GetComponent<Collider>().enabled=false;Destroy(aperture.GetComponent<Collider>());lens=aperture.transform;lensRenderer=aperture.GetComponent<Renderer>();lensRenderer.sharedMaterial=material;lensRenderer.enabled=false;
            for(int i=0;i<2;i++) {
                images[i]=new RenderTexture(R23SprScopeMath.PixelWidth,R23SprScopeMath.PixelHeight,24,RenderTextureFormat.ARGB32);images[i].name="Source SPR "+(i==0?"left":"right")+" angular image";images[i].antiAliasing=1;images[i].useMipMap=false;images[i].filterMode=FilterMode.Bilinear;images[i].wrapMode=TextureWrapMode.Clamp;images[i].Create();
                var eye=new GameObject("Source SPR "+(i==0?"left":"right")+" optical camera");eye.layer=18;eye.transform.SetParent(cameraRoot,false);views[i]=eye.AddComponent<Camera>();views[i].enabled=false;var data=views[i].GetUniversalAdditionalCameraData();data.allowXRRendering=false;data.renderShadows=false;data.renderPostProcessing=false;data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;views[i].allowDynamicResolution=false;views[i].stereoTargetEye=StereoTargetEyeMask.None;views[i].targetTexture=images[i];views[i].aspect=(float)R23SprScopeMath.PixelWidth/R23SprScopeMath.PixelHeight;views[i].nearClipPlane=.05f;views[i].farClipPlane=head.farClipPlane;views[i].allowHDR=false;views[i].allowMSAA=false;views[i].cullingMask=1|(1<<8)|(1<<19)|(1<<24);/* World, committed builds8, ballistic fragments19 and live enemy actors24; exclude own weapon art18 and candidate previews2. */views[i].clearFlags=head.clearFlags;views[i].backgroundColor=head.backgroundColor;views[i].depth=head.depth-20+i;
            }
            material.SetTexture("_OpticalLeft",images[0]);material.SetTexture("_OpticalRight",images[1]);initialized=true;Application.onBeforeRender+=Refresh;
        }
        public void SetAllowed(bool value) { allowed=value;if(!value)Shutdown(); }
        void LateUpdate() { Refresh(); }
        [BeforeRenderOrder(1200)]
        void Refresh() {
            if(!initialized||!allowed||!isActiveAndEnabled||head==null||muzzle==null){Shutdown();return;}
            Vector3 forward=muzzle.forward,eyeOffset=head.transform.position-lens.position;float relief=Vector3.Dot(eyeOffset,-forward);float radial=(eyeOffset+forward*relief).magnitude;
            bool aligned=R23SprScopeMath.Alignment(relief,radial,Vector3.Dot(head.transform.forward,forward),true);
            if(!aligned||!clear(head.transform.position,transform.TransformPoint(new Vector3(0,.103f,.27f)))){Shutdown();return;}
            // Use this frame's live head and only the separation from the rendered eyes.
            // A previous-frame world-space eye position includes the player's last strafe.
            Vector3 eyeOffsetFromCenter=R26ScopeSight.EyeOffset(head);
            double now=Time.unscaledTimeAsDouble;
            if(!Active||now>=nextOpticalTime){
                nextOpticalTime=Active&&now-nextOpticalTime<OpticalInterval?nextOpticalTime+OpticalInterval:now+OpticalInterval;
                opticalFrame=Time.frameCount;
            }
            bool capture=opticalFrame==Time.frameCount;
            var displayed=new R23ShotPose(F(muzzle.position),F(muzzle.right),F(muzzle.up),F(forward));var center=R23SprScopeMath.ReticleCenter(displayed,F(lens.position));Vector3 target=muzzle.position+forward*SourceRifleDefinition.FirearmRange,reticle=new Vector3(center.X,center.Y,center.Z);
            RaycastHit hit;if(sharedAim!=null)target=sharedAim();else if(Physics.Raycast(muzzle.position,forward,out hit,SourceRifleDefinition.FirearmRange,1|(1<<19),QueryTriggerInteraction.Ignore))target=hit.point;
            for(int eye=0;eye<2;eye++) {
                views[eye].enabled=capture;
                if(!capture)continue; // Keep the texture and its sampling extent paired.
                views[eye].farClipPlane=head.farClipPlane;
                Vector3 position=head.transform.position+(eye==0?-eyeOffsetFromCenter:eyeOffsetFromCenter);
                views[eye].transform.position=position;views[eye].transform.rotation=muzzle.rotation;int corner=0;var inverseRotation=Quaternion.Inverse(muzzle.rotation);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2){var p=inverseRotation*(lens.TransformPoint(new Vector3(.5f*x,.5f*y,0))-position);corners[corner++]=new Float3(p.x,p.y,p.z);}
                float vertical=R23SprScopeMath.HalfVerticalTangent(corners);views[eye].fieldOfView=Mathf.Atan(vertical)*Mathf.Rad2Deg*2;
                var extent=new Vector2(vertical*views[eye].aspect,vertical);var tangent=R23SprScopeMath.TargetTangent(displayed,F(position),F(target));var aim=new Vector2(tangent.X,tangent.Y);
                material.SetVector(eye==0?"_TangentLeft":"_TangentRight",new Vector4(extent.x,extent.y,0,0));material.SetVector(eye==0?"_AimLeft":"_AimRight",new Vector4(aim.x,aim.y,0,0));
            }
            material.SetVector("_OpticRight",muzzle.right);material.SetVector("_OpticUp",muzzle.up);material.SetVector("_OpticForward",forward);material.SetVector("_ReticleCenter",reticle);
            float reference=1+R23SprScopeMath.ReticleDepth/.20f;material.SetVector("_ReticleHalfExtent",new Vector4(lens.lossyScale.x*.5f*reference/WindowScale,lens.lossyScale.y*.5f*reference/WindowScale,0,0));
            Active=true;lensRenderer.enabled=true;foreach(var glass in rearGlass)glass.enabled=false;
        }
        static Float3 F(Vector3 value){return new Float3(value.x,value.y,value.z);}
        void Shutdown() { Active=false;if(lensRenderer!=null)lensRenderer.enabled=false;foreach(var camera in views)if(camera!=null)camera.enabled=false;foreach(var glass in rearGlass)if(glass!=null)glass.enabled=true; }
        void OnDisable() { Shutdown(); }
#if UNITY_EDITOR
        internal Vector3 FixtureLensPosition { get { return lens.position; } }
        internal Vector3 FixtureLensScale { get { return lens.lossyScale; } }
        internal int FixtureLensLayer { get { return lens.gameObject.layer; } set { lens.gameObject.layer=value; } }
        internal bool FixtureLensVisible { get { return lensRenderer!=null&&lensRenderer.enabled; } }
        internal bool FixtureShaderSupported { get { return material!=null&&material.shader!=null&&material.shader.isSupported; } }
        internal Camera FixtureCamera(int eye) { return views[eye]; }
        internal RenderTexture FixtureImage(int eye) { return images[eye]; }
        internal int FixtureEyeIndex(Camera camera) { return camera==views[0]?0:camera==views[1]?1:-1; }
        internal void RefreshFixture() { Refresh(); }
#endif
        void OnDestroy() { Application.onBeforeRender-=Refresh;Shutdown();ReleaseOcularMeshes();for(int i=0;i<views.Length;i++){if(views[i]!=null)Destroy(views[i].gameObject);if(images[i]!=null){images[i].Release();Destroy(images[i]);}}if(material!=null)Destroy(material); }
    }
}
#endif
