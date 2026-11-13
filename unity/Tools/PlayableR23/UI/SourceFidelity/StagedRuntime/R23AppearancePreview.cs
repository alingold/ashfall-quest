#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable.Appearance;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;

namespace Ashfall.Integration.Playable.UI
{
    // One independent source avatar, not a second player. No XR/root/collision writes.
    [DefaultExecutionOrder(50)]
    public sealed class R23AppearancePreview:MonoBehaviour
    {
        const float SourceRadiansPerSecond=.54f,SourceFacingHoldSeconds=.6f;
        const float NormalizedStandingEye=1.7088f;
        Transform viewer,fixture,avatar,head,left,right,ownerObject,sourceAnchor;
        R23PlayerAppearance appearance;
        R23Visuals gloveVisuals;
        readonly Stack<IEnumerator> gloveJobs=new Stack<IEnumerator>();
        readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
        Coroutine gloveLoad;
        bool initialized,anchored,glovesReady,glovesLoading;
        string requested,gloveFailure;
        float hold;
        public float RotationMultiplier=3;
        // Source lateral offset was 1.35 m; current centered opaque UI needs side clearance.
        public float RightOffset=3.1f;
        public bool Visible {get;private set;}
        public bool Loading {get{return appearance!=null&&(appearance.Loading||glovesLoading);}}
        public bool Ready {get{return appearance!=null&&appearance.Ready&&glovesReady;}}
        public string Failure {get{return appearance!=null&&appearance.Failure!=null?appearance.Failure:gloveFailure;}}
        public string SelectedStyleId {get{return appearance==null?null:appearance.SelectedStyleId;}}
        public Vector3 WorldPosition {get{return fixture==null?Vector3.zero:fixture.position;}}
        public void Initialize(Transform playerHeadOrUiAnchor)
        {
            if(initialized)throw new InvalidOperationException("Preview already initialized");
            if(playerHeadOrUiAnchor==null)throw new ArgumentNullException("playerHeadOrUiAnchor");
            initialized=true;viewer=playerHeadOrUiAnchor;
            fixture=new GameObject("R23 single world-fixed appearance podium").transform;
            avatar=Child(fixture,"Full-size source character");avatar.localPosition=new Vector3(0,.16f,0);
            head=Child(avatar,"Read-only preview eye");head.localPosition=new Vector3(0,1.68f,0);
            left=Child(avatar,"Source gallery left glove");right=Child(avatar,"Source gallery right glove");
            // Source _lobby_hand_pose for the three original Argon kits; no tracked-hand correction.
            left.localPosition=new Vector3(-.26f,.94f,.04f);left.localRotation=Quaternion.AngleAxis(.2f*Mathf.Rad2Deg,Vector3.forward);
            right.localPosition=new Vector3(.26f,.94f,.04f);right.localRotation=Quaternion.AngleAxis(-.2f*Mathf.Rad2Deg,Vector3.forward);
            ownerObject=new GameObject("R23 appearance preview asset owner").transform;
            appearance=ownerObject.gameObject.AddComponent<R23PlayerAppearance>();
            appearance.GloveMaterialChanged+=ApplyGloves;
            BuildPodium();fixture.gameObject.SetActive(false);
        }
        public bool Show(string styleId)
        {
            Require();bool supported=false;foreach(var style in appearance.AvailableStyles)if(style.Id==styleId)supported=true;
            if(!supported)return false;
            if(Visible&&requested==styleId)return true;
            if(!anchored)CaptureWorldAnchor();
            // Source gallery turns toward the viewer briefly on entry/selection, without moving.
            Vector3 face=Vector3.ProjectOnPlane(viewer.position-avatar.position,Vector3.up);
            if(face.sqrMagnitude>.0001f)avatar.rotation=Quaternion.LookRotation(face.normalized,Vector3.up);
            hold=SourceFacingHoldSeconds;requested=styleId;Visible=true;fixture.gameObject.SetActive(true);
            if(appearance.RequestedStyleId==null)appearance.InitializePreview(avatar,head,left,right,styleId);
            else appearance.SelectStyle(styleId);
            appearance.SetPresentationState(true,true);
            if(!glovesReady&&!glovesLoading)StartGloves();return true;
        }
        public void Hide()
        {
            Visible=false;if(appearance!=null)appearance.SetPresentationState(false,true);
            if(fixture!=null)fixture.gameObject.SetActive(false);
        }
        // Explicit host seam for a new map/menu location; never follows the headset per frame.
        public void Reanchor()
        {Require();CaptureWorldAnchor();if(Visible){string style=requested;Visible=false;Show(style);}}
        public void SetWorldAnchor(Transform avatarAnchor,bool sceneOwnsPodium=true)
        {
            Require();if(avatarAnchor==null)throw new ArgumentNullException("avatarAnchor");sourceAnchor=avatarAnchor;CaptureWorldAnchor();
            for(int i=0;i<fixture.childCount;i++)
            {
                var child=fixture.GetChild(i);
                if(child.name=="One source pedestal"||child.name=="Source cyan pedestal rim"||child.name=="Preview fill")child.gameObject.SetActive(!sceneOwnsPodium);
            }
        }
        void CaptureWorldAnchor()
        {
            if(sourceAnchor!=null){fixture.SetPositionAndRotation(sourceAnchor.position-Vector3.up*.16f,sourceAnchor.rotation);avatar.rotation=sourceAnchor.rotation;anchored=true;return;}
            Vector3 forward=Vector3.ProjectOnPlane(viewer.forward,Vector3.up);
            if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;forward.Normalize();
            Vector3 rightAxis=Vector3.Cross(Vector3.up,forward);
            // Source lobby player z3.8, pedestal z-.65 and x1.35, reflected to Unity +Z.
            float lateral=float.IsNaN(RightOffset)||float.IsInfinity(RightOffset)?3.1f:Mathf.Clamp(RightOffset,1.35f,8);
            Vector3 position=viewer.position+forward*4.45f+rightAxis*lateral;
            position.y=viewer.position.y-NormalizedStandingEye+.15f;
            // The source lobby has a flat podium floor. In a selected map the
            // same fixed preview must stand above its real sloping ground.
            float expected=position.y-.15f,best=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(position+Vector3.up*8,Vector3.down,20,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                if(!(hit.collider is CharacterController)&&hit.normal.y>.5f&&Mathf.Abs(hit.point.y-expected)<best){best=Mathf.Abs(hit.point.y-expected);position.y=hit.point.y+.15f;}
            fixture.SetPositionAndRotation(position,Quaternion.LookRotation(forward,Vector3.up));anchored=true;
        }
        void Update()
        {
            if(!Visible||avatar==null)return;
            float dt=Mathf.Clamp(Time.unscaledDeltaTime,0,.1f);
            if(hold>0){hold=Mathf.Max(0,hold-dt);return;}
            float gain=float.IsNaN(RotationMultiplier)||float.IsInfinity(RotationMultiplier)?3:Mathf.Clamp(RotationMultiplier,0,6);
            // Reflection reverses source positive Godot Y rotation.
            avatar.Rotate(Vector3.up,-SourceRadiansPerSecond*gain*dt*Mathf.Rad2Deg,Space.World);
        }
        void ApplyGloves(Material material)
        {
            foreach(var hand in new[]{left,right})if(hand!=null)
                foreach(var renderer in hand.GetComponentsInChildren<Renderer>(true))if(material!=null)renderer.sharedMaterial=material;
        }
        void StartGloves()
        {
            gloveFailure=null;glovesLoading=true;gloveVisuals=new R23Visuals();gloveJobs.Push(GloveSequence());gloveLoad=StartCoroutine(PumpGloves());
        }
        IEnumerator GloveSequence()
        {yield return gloveVisuals.Load("left",left);yield return gloveVisuals.Load("right",right);}
        IEnumerator PumpGloves()
        {
            while(gloveJobs.Count>0)
            {
                var job=gloveJobs.Peek();object wait=null;bool moved=false;
                try{moved=job.MoveNext();if(moved)wait=job.Current;}
                catch(Exception error){gloveFailure="Preview gloves: "+error.Message;}
                if(gloveFailure!=null){CancelGloves();yield break;}
                if(!moved){var disposable=job as IDisposable;if(disposable!=null)disposable.Dispose();gloveJobs.Pop();continue;}
                var child=wait as IEnumerator;if(child!=null){gloveJobs.Push(child);continue;}
                yield return wait;
            }
            gloveVisuals.ReleaseLoadedBytes();glovesLoading=false;glovesReady=true;gloveLoad=null;ApplyGloves(appearance.CurrentGloveMaterial);
        }
        void CancelGloves()
        {
            while(gloveJobs.Count>0){var job=gloveJobs.Pop() as IDisposable;if(job!=null)job.Dispose();}
            if(gloveVisuals!=null){gloveVisuals.Dispose();gloveVisuals=null;}
            foreach(var hand in new[]{left,right})if(hand!=null)
                for(int i=hand.childCount-1;i>=0;i--){var child=hand.GetChild(i);child.gameObject.SetActive(false);Destroy(child.gameObject);}
            glovesLoading=glovesReady=false;gloveLoad=null;
        }
        void BuildPodium()
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("Preview needs URP Lit");
            var dark=new Material(shader);resources.Add(dark);dark.SetColor("_BaseColor",new Color(.106f,.141f,.196f));dark.SetFloat("_Metallic",.5f);dark.SetFloat("_Smoothness",.52f);
            var cyan=new Material(shader);resources.Add(cyan);cyan.SetColor("_BaseColor",new Color(.094f,.89f,.94f));cyan.SetColor("_EmissionColor",new Color(.14f,1.335f,1.41f));cyan.EnableKeyword("_EMISSION");
            MeshItem("One source pedestal",Cylinder(.83f,.30f,48),Vector3.zero,dark);
            MeshItem("Source cyan pedestal rim",Ring(.76f,.018f,64,8),new Vector3(0,.16f,0),cyan);
            // Source lobby fill lights; scoped to this visible presentation and no shadows.
            foreach(float side in new[]{-1f,1f})
            {
                var fill=Child(fixture,"Preview fill");fill.localPosition=new Vector3(side*2,2.8f,-1.2f);
                var light=fill.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.71f,.91f,1);light.intensity=2;light.range=6;light.shadows=LightShadows.None;
            }
        }
        void MeshItem(string name,Mesh mesh,Vector3 at,Material material)
        {
            resources.Add(mesh);var t=Child(fixture,name);t.localPosition=at;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        static Mesh Cylinder(float radius,float height,int steps)
        {
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=steps;i++)
            {
                float angle=i*Mathf.PI*2/steps,x=Mathf.Sin(angle),z=Mathf.Cos(angle);
                positions.Add(new Vector3(x*radius,-height*.5f,z*radius));positions.Add(new Vector3(x*radius,height*.5f,z*radius));
                normals.Add(new Vector3(x,0,z));normals.Add(new Vector3(x,0,z));uv.Add(new Vector2((float)i/steps,0));uv.Add(new Vector2((float)i/steps,1));
                if(i<steps){int b=i*2;triangles.AddRange(new[]{b,b+2,b+1,b+2,b+3,b+1});}
            }
            foreach(float side in new[]{-1f,1f})
            {
                int center=positions.Count;positions.Add(new Vector3(0,side*height*.5f,0));normals.Add(Vector3.up*side);uv.Add(new Vector2(.5f,.5f));
                for(int i=0;i<=steps;i++){float angle=i*Mathf.PI*2/steps,x=Mathf.Sin(angle),z=Mathf.Cos(angle);positions.Add(new Vector3(x*radius,side*height*.5f,z*radius));normals.Add(Vector3.up*side);uv.Add(new Vector2(x*.5f+.5f,z*.5f+.5f));if(i<steps)triangles.AddRange(side>0?new[]{center,center+i+1,center+i+2}:new[]{center,center+i+2,center+i+1});}
            }
            return Mesh("Source display cylinder",positions,normals,uv,triangles);
        }
        static Mesh Ring(float radius,float tube,int steps,int sides)
        {
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=steps;i++)for(int j=0;j<=sides;j++)
            {
                float a=i*Mathf.PI*2/steps,b=j*Mathf.PI*2/sides;var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var normal=radial*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b);
                positions.Add(radial*radius+normal*tube);normals.Add(normal);uv.Add(new Vector2((float)i/steps,(float)j/sides));
                if(i<steps&&j<sides){int k=i*(sides+1)+j;triangles.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}
            }
            return Mesh("Source cyan rim",positions,normals,uv,triangles);
        }
        static Mesh Mesh(string name,List<Vector3> positions,List<Vector3> normals,List<Vector2> uv,List<int> triangles)
        {var mesh=new Mesh();mesh.name=name;mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;}
        static Transform Child(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        void Require(){if(!initialized)throw new InvalidOperationException("Initialize preview first");}
        void OnDisable(){Hide();if(glovesLoading){if(gloveLoad!=null)StopCoroutine(gloveLoad);CancelGloves();}}
        void OnDestroy()
        {
            Hide();StopAllCoroutines();CancelGloves();
            if(appearance!=null)appearance.GloveMaterialChanged-=ApplyGloves;
            if(ownerObject!=null)Destroy(ownerObject.gameObject);if(fixture!=null)Destroy(fixture.gameObject);
            foreach(var resource in resources)if(resource!=null)Destroy(resource);resources.Clear();
        }
    }
}
#endif
