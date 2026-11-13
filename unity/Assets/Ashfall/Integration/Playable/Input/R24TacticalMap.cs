#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.Integration.Playable.TeamLaunch;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ashfall.Integration.Playable.Input
{
    // Source tactical_map1000: a held support trigger opens a north-up board.
    // Presentation only: no tracked pose, health, inventory or network authority.
    public sealed partial class R24TacticalMap : MonoBehaviour
    {
        R23PlayableLoop root;
        Transform board,canvas;
        Material flat,letters,imageMaterial,previewMaterial;
        Mesh drawing,imageMesh,previewMesh;
        Font font;
        readonly List<TextMesh> texts=new List<TextMesh>();
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> indices=new List<int>();
        readonly List<Texture2D> pingIcons=new List<Texture2D>();
        Material pingMaterial;Mesh pingMesh;Transform pingView;
        int textCount,captureGeneration;float nextDraw;
        string boundMap,boundHash;R24LaunchMapInfo info;
        TacticalProjection projection;Texture2D overview;Coroutine capture;
        public bool Opened {get;private set;}
        public string Failure {get;private set;}
        public int OverviewCaptures {get;private set;}
        public string OverviewCaptureInfo {get;private set;}
        public int Draws {get;private set;}
        public Vector2 PlayerPixel {get;private set;}
        public Vector2 PlayerHeading {get;private set;}
        public Texture2D Overview {get{return overview;}}
        public int OverviewDepthBits {get;private set;}
        public Texture TopImage {get{return imageMaterial==null?null:imageMaterial.mainTexture;}}
        public Transform Board {get{return board;}}
        public void Initialize(R23PlayableLoop owner)
        {
            if(root!=null||owner==null)throw new ArgumentException("Actual Root required");root=owner;
            var shader=Resources.Load<Shader>("R23VrUi");font=Resources.Load<Font>("R23GodotUI");
            if(shader==null||font==null)throw new InvalidOperationException("Retained source map UI resources");
            flat=new Material(shader);flat.renderQueue=4112;letters=new Material(shader);letters.SetFloat("_Font",1);letters.renderQueue=4114;
            var imageShader=Resources.Load<Shader>("R24TacticalImage");if(imageShader==null)throw new InvalidOperationException("Retained opaque tactical image shader");
            imageMaterial=new Material(imageShader);imageMaterial.renderQueue=4111;previewMaterial=new Material(shader);previewMaterial.renderQueue=4113;
            pingMaterial=new Material(shader);pingMaterial.renderQueue=4114;
            board=new GameObject("Source tactical board 1.65x1m").transform;board.SetParent(transform,false);
            canvas=new GameObject("Source map 1400x850 coordinates").transform;canvas.SetParent(board,false);canvas.localPosition=new Vector3(-.825f,.5f,0);canvas.localScale=new Vector3(1.65f/1400,1f/850,1);
            drawing=Part("Source grid / players / zone",flat,out _);
            imageMesh=Part("Actual north-up map image",imageMaterial,out _);previewMesh=Part("Source map screenshot",previewMaterial,out _);pingMesh=Part("Actual current ping",pingMaterial,out pingView);
            for(int i=0;i<8;i++){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!t.LoadImage(R23TeamMarksAssets.Icon(i),false))throw new InvalidOperationException("Source ping icon");t.wrapMode=TextureWrapMode.Clamp;pingIcons.Add(t);}
            board.gameObject.SetActive(false);
        }
        Mesh Part(string title,Material material,out Transform part)
        {
            part=new GameObject(title).transform;part.SetParent(canvas,false);part.gameObject.layer=5;
            var mesh=new Mesh{name=title};mesh.MarkDynamic();part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return mesh;
        }
        public void Sample(bool pressed,bool eligible)
        {
            bool open=pressed&&eligible&&isActiveAndEnabled;
            if(open&&(!root.LaunchMapCatalog.Ready||!root.LaunchMapCatalog.TryRead(root.Map.MapId,out info)))open=false;
            if(open&&(boundMap!=root.Map.MapId||boundHash!=root.Map.ManifestSha256))BindMap();
            Opened=open;board.gameObject.SetActive(open);if(open){Follow();if(Time.unscaledTime>=nextDraw){nextDraw=Time.unscaledTime+.1f;Draw();}}
        }
        public void Close(){Opened=false;if(board!=null)board.gameObject.SetActive(false);}
        void BindMap()
        {
            ReleaseOverview();boundMap=root.Map.MapId;boundHash=root.Map.ManifestSha256;
            var origin=root.Map.transform.TransformPoint(V(info.RepresentedOriginUnity));
            projection=new TacticalProjection(F(origin),info.CatalogMinX,info.CatalogMinZ,info.CatalogMaxX,info.CatalogMaxZ);
            imageMaterial.mainTexture=null;
            var builtin=Resources.Load<Texture2D>("R24TacticalMaps/"+boundMap+"_top");
            // Builtins use their actual authored source top PNG; archives capture
            // their actual loaded layer0 geometry once, with authored catalog bounds.
            if(builtin!=null)imageMaterial.mainTexture=builtin;
            else capture=StartCoroutine(CaptureOverview(++captureGeneration,boundMap,boundHash));
            var choice=World.R23MapCatalog.Find(boundMap);var option=new UI.R23MapOption(boundMap,choice==null?boundMap:choice.Label);
            previewMaterial.mainTexture=Resources.Load<Texture2D>(option.ThumbnailResource);nextDraw=0;
        }
        IEnumerator CaptureOverview(int generation,string mapId,string hash)
        {
            yield return null;
            if(root==null||!root.Map.IsLoaded||root.Map.MapId!=mapId||root.Map.ManifestSha256!=hash||generation!=captureGeneration)yield break;
            GameObject go=null;RenderTexture target=null;Texture2D snapshot=null;
            try
            {
                float width=info.CatalogMaxX-info.CatalogMinX,depth=info.CatalogMaxZ-info.CatalogMinZ;
                target=new RenderTexture(1024,Mathf.Max(128,Mathf.RoundToInt(1024*depth/width)),24,RenderTextureFormat.ARGB32);target.name="One actual north-up tactical overview";target.useMipMap=false;if(!target.Create())throw new InvalidOperationException("Actual overview target creation failed");
                go=new GameObject("One-shot source archive overview");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.orthographic=true;camera.aspect=width/depth;camera.orthographicSize=depth*.5f;camera.nearClipPlane=.1f;camera.farClipPlane=2500;camera.cullingMask=1;camera.allowHDR=false;camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=root.Rig.Head.backgroundColor;camera.targetTexture=target;
                Vector3 origin=V(info.RepresentedOriginUnity);float top=info.HasArchiveMetadata?info.SolidMaxY-info.RemovedOriginGodot.Y:root.Map.transform.InverseTransformPoint(root.Map.Bounds.max).y;
                go.transform.position=root.Map.transform.TransformPoint(origin+new Vector3((info.CatalogMinX+info.CatalogMaxX)*.5f,top-origin.y+10,-(info.CatalogMinZ+info.CatalogMaxZ)*.5f));go.transform.rotation=Quaternion.LookRotation(root.Map.transform.TransformDirection(Vector3.down),root.Map.transform.TransformDirection(Vector3.forward));
                int candidates=0,inFrustum=0,completed=0;var planes=GeometryUtility.CalculateFrustumPlanes(camera);
                foreach(var r in root.Map.GetComponentsInChildren<Renderer>())if(r.enabled&&(camera.cullingMask&(1<<r.gameObject.layer))!=0){candidates++;if(GeometryUtility.TestPlanesAABB(planes,r.bounds))inFrustum++;}
                Action<ScriptableRenderContext,Camera> endCamera=delegate(ScriptableRenderContext context,Camera rendered){if(rendered==camera)completed++;};
                RenderPipelineManager.endCameraRendering+=endCamera;
                try
                {
                    // The installed URP's own RuntimeTests use this synchronous
                    // request with an explicit destination. It is independent
                    // of the editor/game-view render loop and XR swapchain.
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                }
                finally{RenderPipelineManager.endCameraRendering-=endCamera;OverviewCaptureInfo="camera="+go.transform.position+" forward="+go.transform.forward+" mask="+camera.cullingMask+" candidates="+candidates+" frustum="+inFrustum+" endCamera="+completed+" created="+target.IsCreated();}
                if(completed<1)throw new InvalidOperationException("URP did not complete the actual overview request: "+OverviewCaptureInfo);
                // Match archive_overview_r8: freeze viewport pixels into an
                // independent image BEFORE releasing its camera/viewport.
                // No render target or depth buffer stays resident for this UI.
                var previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=target;snapshot=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);snapshot.name="Source frozen tactical overview";snapshot.ReadPixels(new Rect(0,0,target.width,target.height),0,0);snapshot.wrapMode=TextureWrapMode.Clamp;snapshot.filterMode=FilterMode.Bilinear;
#if UNITY_EDITOR
                    var pixels=snapshot.GetPixels32();int min=255,max=0;long red=0,green=0,blue=0,alpha=0;foreach(var p in pixels){min=Math.Min(min,Math.Min(p.r,Math.Min(p.g,p.b)));max=Math.Max(max,Math.Max(p.r,Math.Max(p.g,p.b)));red+=p.r;green+=p.g;blue+=p.b;alpha+=p.a;}double divisor=pixels.Length*255.0;OverviewCaptureInfo+=" BEFOREcameraRelease RGBmean="+red/divisor+","+green/divisor+","+blue/divisor+" range="+min+".."+max+" alphaMean="+alpha/divisor;
#endif
                    snapshot.Apply(false,true);
                }
                finally{RenderTexture.active=previous;}
                OverviewDepthBits=target.depth;overview=snapshot;snapshot=null;imageMaterial.mainTexture=overview;OverviewCaptures++;nextDraw=0;
            }
            catch(Exception e){Failure=e.ToString();Debug.LogError("Tactical map overview failed: "+e);}
            finally{if(go!=null)Destroy(go);if(target!=null){target.Release();Destroy(target);}if(snapshot!=null)Destroy(snapshot);capture=null;}
        }
        void Follow()
        {
            Vector3 forward=root.Rig.Head.transform.forward;forward.y=0;if(forward.sqrMagnitude<.00001f)forward=board.forward;forward.Normalize();
            board.SetPositionAndRotation(root.Rig.Head.transform.position+forward*1.7f,Quaternion.LookRotation(forward,Vector3.up));
        }
        [BeforeRenderOrder(1350)]void RefreshLate(){if(Opened)Follow();}
        void OnEnable(){Application.onBeforeRender+=RefreshLate;}
        void OnDisable(){Application.onBeforeRender-=RefreshLate;Close();}
        void ReleaseOverview(){captureGeneration++;if(capture!=null){StopCoroutine(capture);capture=null;}if(overview!=null){Destroy(overview);overview=null;}if(imageMaterial!=null)imageMaterial.mainTexture=null;}
        void OnDestroy(){Application.onBeforeRender-=RefreshLate;ReleaseOverview();foreach(var t in pingIcons)if(t!=null)Destroy(t);foreach(var m in new[]{flat,letters,imageMaterial,previewMaterial,pingMaterial,backgroundMaterial})if(m!=null)Destroy(m);foreach(var mesh in new[]{drawing,imageMesh,previewMesh,pingMesh,background})if(mesh!=null)Destroy(mesh);if(board!=null)Destroy(board.gameObject);}
        static Vector3 V(Float3 v){return new Vector3(v.X,v.Y,v.Z);}static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
    }
}
#endif
