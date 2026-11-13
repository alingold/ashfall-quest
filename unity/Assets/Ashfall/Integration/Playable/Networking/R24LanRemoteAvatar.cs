#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.Appearance;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Networking
{
    // Render-only replica: no XR component, Rigidbody, Collider, health pool,
    // inventory, input sampling or authority/physics writes.
    [DefaultExecutionOrder(900)]
    public sealed class R24LanRemoteAvatar:MonoBehaviour
    {
        Ashfall.Integration.Playable.Traversal.R25TraversalPresentation traversal;Ashfall.Integration.Playable.Traversal.R25WebState webState;
        public void SetWeb(Ashfall.Integration.Playable.Traversal.R25WebState state){webState=state;}
        Transform head,left,right,nameTag;R23PlayerAppearance appearance;R23Visuals gloves;R24LanPose target;Camera viewer;
        double poseReceived;
        bool initialized,handsReady,visible,snapped;float nextStyle;int requestedStyle=-1;readonly List<MeshRenderer> handRenderers=new List<MeshRenderer>();
        public string Actor{get;private set;}public string Failure{get;private set;}public bool Ready{get{return handsReady&&appearance!=null&&appearance.Ready;}}
        public bool Visible{get{return visible&&Ready;}}public R23PlayerAppearance Appearance{get{return appearance;}}
        public float LargestRawHandFrameSeconds{get;private set;}public float LargestGloveStepSeconds{get;private set;}public int ArtUpdateCount{get;private set;}
        public void Initialize(string actor,string name,Camera viewer,R24LanPose pose)
        {
            if(initialized||pose==null||!pose.Valid)throw new ArgumentException("Actual remote presence required");initialized=true;Actor=actor;this.viewer=viewer;target=pose;poseReceived=Time.realtimeSinceStartupAsDouble;
            head=Child("Remote head");left=Child("Remote left glove art");right=Child("Remote right glove art");nameTag=Child("Remote name");
            var text=nameTag.gameObject.AddComponent<TextMesh>();text.text=name;text.richText=false;text.font=Resources.Load<Font>("R23InventoryUI");text.fontSize=64;text.characterSize=.01f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;
            if(text.font==null)throw new InvalidOperationException("Existing source UI font missing");nameTag.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;nameTag.gameObject.layer=18;
            ApplyPose(1);appearance=gameObject.AddComponent<R23PlayerAppearance>();appearance.GloveMaterialChanged+=ApplyGloves;appearance.SetHandArtPointReader(ReadHandPoint);appearance.InitializeRemote(transform,head,left,right,Style(pose.Style));requestedStyle=pose.Style;
            traversal=gameObject.AddComponent<Ashfall.Integration.Playable.Traversal.R25TraversalPresentation>();traversal.Initialize();
            gloves=new R23Visuals();StartCoroutine(LoadGloves());SetVisible(true);
        }
        Transform Child(string label){var t=new GameObject(label).transform;t.SetParent(transform,false);return t;}
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Quaternion Q(R24LanRotation q){return new Quaternion(q.X,q.Y,q.Z,q.W);}
        string Style(int index){foreach(var style in appearance.AvailableStyles)if(style.SourceIndex==index)return style.Id;return "cinder_scout";}
        bool ReadHandPoint(int hand,Vector3 local,out Vector3 world){var t=hand==0?left:right;world=t.TransformPoint(local);return handsReady;}
        public void Receive(R24LanPose pose){Receive(pose,Time.realtimeSinceStartupAsDouble);}
        public void Receive(R24LanPose pose,double receivedAt){if(pose==null||!pose.Valid)throw new ArgumentException("Remote visual pose");poseReceived=receivedAt;target=pose;}
        public void SetVisible(bool value){visible=value;if(!value&&traversal!=null)traversal.Hide();if(appearance!=null)appearance.SetPresentationState(value,false);if(left!=null)left.gameObject.SetActive(value&&handsReady);if(right!=null)right.gameObject.SetActive(value&&handsReady);if(nameTag!=null)nameTag.gameObject.SetActive(value&&Ready);}
        void ApplyGloves(Material material){foreach(var renderer in handRenderers)if(renderer!=null){renderer.sharedMaterial=material;renderer.enabled=material!=null;renderer.shadowCastingMode=ShadowCastingMode.Off;}}
        IEnumerator LoadGloves()
        {
            var jobs=new Stack<IEnumerator>();jobs.Push(gloves.Load("right",right));jobs.Push(gloves.Load("left",left));
            try{while(jobs.Count>0){bool moved=false;object current=null;try{moved=jobs.Peek().MoveNext();if(moved)current=jobs.Peek().Current;}catch(Exception e){Failure=e.ToString();break;}if(!moved){var done=jobs.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}var nested=current as IEnumerator;if(nested!=null)jobs.Push(nested);else yield return current;}}
            finally{while(jobs.Count>0){var done=jobs.Pop()as IDisposable;if(done!=null)done.Dispose();}}
            if(Failure!=null)yield break;handRenderers.AddRange(left.GetComponentsInChildren<MeshRenderer>(true));handRenderers.AddRange(right.GetComponentsInChildren<MeshRenderer>(true));ApplyGloves(appearance.CurrentGloveMaterial);gloves.ReleaseLoadedBytes();handsReady=true;SetVisible(visible);
        }
        void ApplyPose(float alpha)
        {
            // Set the parent first, then overwrite all child world targets. A
            // parent move must not add that displacement to received hands.
            Vector3 lead=target.Riding?V(Ashfall.Integration.Playable.Vehicles.R25BikeRider.Remote(new Float3(),target.Velocity,Time.realtimeSinceStartupAsDouble-poseReceived)):Vector3.zero;
            Vector3 hp=head.position,lp=left.position,rp=right.position;Quaternion hq=head.rotation,lq=left.rotation,rq=right.rotation;
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position,V(target.Root.Position)+lead,alpha),Quaternion.Slerp(transform.rotation,Q(target.Root.Rotation),alpha));
            head.SetPositionAndRotation(Vector3.Lerp(hp,V(target.Head.Position)+lead,alpha),Quaternion.Slerp(hq,Q(target.Head.Rotation),alpha));left.SetPositionAndRotation(Vector3.Lerp(lp,V(target.Left.Position)+lead,alpha),Quaternion.Slerp(lq,Q(target.Left.Rotation),alpha));right.SetPositionAndRotation(Vector3.Lerp(rp,V(target.Right.Position)+lead,alpha),Quaternion.Slerp(rq,Q(target.Right.Rotation),alpha));
        }
        void LateUpdate()
        {
            if(!initialized||target==null)return;float dt=Mathf.Max(.0001f,Time.unscaledDeltaTime);ApplyPose(target.Riding?1:snapped?1-Mathf.Exp(-18*dt):1);snapped=true;
            if(target.Style!=requestedStyle&&Time.unscaledTime>=nextStyle){requestedStyle=target.Style;nextStyle=Time.unscaledTime+1;appearance.SelectStyle(Style(target.Style));}
            if(appearance.Failure!=null)Failure=appearance.Failure;appearance.VehicleSeated=target.Riding;appearance.SetMotionState(target.Supported,target.Gliding,V(target.Velocity));appearance.SetPresentationState(visible&&target.Tracked,false);
            if(visible){traversal.Jet(head.position,transform.rotation,target.Jetting,1,1,null);for(int i=0;i<2;i++){bool attached=webState!=null&&(i==0?webState.LeftAttached:webState.RightAttached);Vector3 anchor=webState==null?Vector3.zero:V(i==0?webState.LeftAnchor:webState.RightAnchor);Vector3 wrist=i==0?left.position:right.position;traversal.Web(i,wrist,anchor,attached,0,Vector3.Distance(wrist,anchor),false,Vector3.zero);}}
            if(handsReady){float handSeconds=Mathf.Min(dt,.25f);LargestRawHandFrameSeconds=Mathf.Max(LargestRawHandFrameSeconds,dt);LargestGloveStepSeconds=Mathf.Max(LargestGloveStepSeconds,handSeconds);gloves.PoseHand(0,"",false,false,target.LeftGrip,0,handSeconds);gloves.PoseHand(1,"",false,false,target.RightGrip,0,handSeconds);ArtUpdateCount++;}nameTag.gameObject.SetActive(Visible&&target.Tracked);nameTag.position=head.position+Vector3.up*.35f;if(viewer!=null)nameTag.rotation=Quaternion.LookRotation(nameTag.position-viewer.transform.position,Vector3.up);
        }
        void OnDestroy(){StopAllCoroutines();if(appearance!=null){appearance.GloveMaterialChanged-=ApplyGloves;appearance.SetPresentationState(false,false);}if(gloves!=null){gloves.Dispose();gloves=null;}}
    }
}
#endif
