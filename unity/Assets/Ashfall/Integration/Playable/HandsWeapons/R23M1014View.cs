#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Modules.WeaponHandling;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Reusable source-art presenter. Host owns accepted poses, input, audio and gameplay.
    public sealed class R23M1014View:MonoBehaviour
    {
        R23Visuals visuals;float carrier;long previousShots;bool initialized,ownsModel;
        public bool Ready {get;private set;}public string Failure {get;private set;}
        public Transform Model {get;private set;}public Transform Muzzle {get;private set;}
        public Transform Bolt {get;private set;}public Transform Pack {get;private set;}public Transform Arrow {get;private set;}
        public float FlashSeconds {get;private set;}
        public void Initialize(Transform correctedGripParent)
        {
            if(initialized)throw new InvalidOperationException("M1014 view already initialized");if(correctedGripParent==null)throw new ArgumentNullException("correctedGripParent");initialized=true;
            ownsModel=true;InitializeModelCore(Node("Original R23 M1014 model",correctedGripParent));
        }
        public void InitializeModel(Transform model){if(initialized||model==null)throw new InvalidOperationException("M1014 view requires one source model");initialized=true;InitializeModelCore(model);}
        void InitializeModelCore(Transform model){
            Model=model;Muzzle=Node("M1014 actual source muzzle",Model);Muzzle.localPosition=V(R23M1014Definition.Muzzle);
            Bolt=Node("M1014 source moving bolt",Model);Bolt.localPosition=V(R23M1014Definition.Handle);Pack=Node("M1014 six-shell pack",Model);Pack.localPosition=V(R23M1014Definition.Offer);Arrow=Node("M1014 UP then BACK",Model);Pack.gameObject.SetActive(false);Arrow.gameObject.SetActive(false);
            visuals=new R23Visuals();StartCoroutine(Load());
        }
        static Transform Node(string name,Transform parent){var go=new GameObject(name);go.layer=18;go.transform.SetParent(parent,false);return go.transform;}
        IEnumerator Load()
        {
            var loads=new[]{visuals.Load("m1014",Model),visuals.Load("m1014-bolt",Bolt),visuals.Load("m1014-pack",Pack),visuals.Load("m1014-arrow",Arrow)};
            // Catch nested-loader failures so optional gallery assets can report independently.
            foreach(var load in loads)
            {
                var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(load);
                try
                {
                    while(stack.Count>0)
                    {
                        bool more;object current=null;
                        try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}
                        catch(Exception error){Failure=error.Message;yield break;}
                        if(!more){var disposable=stack.Pop()as IDisposable;if(disposable!=null)disposable.Dispose();continue;}
                        var nested=current as IEnumerator;if(nested!=null){stack.Push(nested);continue;}yield return current;
                    }
                }
                finally{while(stack.Count>0){var disposable=stack.Pop()as IDisposable;if(disposable!=null)disposable.Dispose();}}
            }
            foreach(var renderer in Arrow.GetComponentsInChildren<Renderer>(true)){renderer.sharedMaterial.SetColor("_EmissionColor",new Color(.05f,.72f,.75f)*.65f);renderer.sharedMaterial.EnableKeyword("_EMISSION");}
            visuals.ReleaseLoadedBytes();Ready=true;
        }
        public void SynchronizeInstance(HandlingSnapshot snapshot){previousShots=snapshot.Shots;Suspend();}
        public void Present(HandlingSnapshot snapshot,int reserve,bool leftHanded,bool visible,float seconds)
        {
            if(Model==null)return;Model.gameObject.SetActive(visible);if(!visible){carrier=FlashSeconds=0;previousShots=snapshot.Shots;return;}
            float dt=float.IsNaN(seconds)||float.IsInfinity(seconds)?0:Mathf.Clamp(seconds,0,.25f);
            if(snapshot.Shots>previousShots){carrier=.095f;FlashSeconds=.055f;}
            else{carrier=Mathf.Max(0,carrier-dt);FlashSeconds=Mathf.Max(0,FlashSeconds-dt);}
            if(snapshot.Shots<previousShots)carrier=FlashSeconds=0;previousShots=snapshot.Shots;
            Model.localPosition=new Vector3(0,0,-snapshot.RecoilDistance);Model.localRotation=Quaternion.Euler(-snapshot.RecoilPitch*Mathf.Rad2Deg,-snapshot.RecoilYaw*Mathf.Rad2Deg,0);Model.localScale=Vector3.one;
            Vector3 handle=V(R23M1014Definition.Handle);if(leftHanded)handle.x=-handle.x;
            Bolt.localPosition=handle-Vector3.forward*(snapshot.BoltTravel+(snapshot.Phase==ReloadPhase.Ready?Mathf.Sin(Mathf.Clamp01(carrier/.095f)*Mathf.PI)*.038f:0));Bolt.localScale=new Vector3(leftHanded?-1:1,1,1);
            Pack.gameObject.SetActive(snapshot.Phase==ReloadPhase.NeedsMagazine&&reserve!=0);
            Arrow.gameObject.SetActive(false);
            Arrow.localPosition=snapshot.Phase==ReloadPhase.NeedsRack?handle:V(R23M1014Definition.Offer);Arrow.localRotation=snapshot.Phase==ReloadPhase.NeedsRack?Quaternion.FromToRotation(Vector3.up,Vector3.back):Quaternion.identity;
        }
        public R23M1014Pose FiringPose()
        {if(Muzzle==null)throw new InvalidOperationException("M1014 view uninitialized");return new R23M1014Pose{Origin=F(Muzzle.position),Forward=F(Muzzle.forward),Right=F(Muzzle.right),Up=F(Muzzle.up)};}
        public bool IsTransient(Transform child){return Pack!=null&&(child==Pack||child.IsChildOf(Pack))||Arrow!=null&&(child==Arrow||child.IsChildOf(Arrow));}
        public void Suspend(){carrier=FlashSeconds=0;if(Pack!=null)Pack.gameObject.SetActive(false);if(Arrow!=null)Arrow.gameObject.SetActive(false);}
        void OnDisable(){Suspend();}
        void OnDestroy(){if(visuals!=null)visuals.Dispose();if(ownsModel&&Model!=null)Destroy(Model.gameObject);Ready=false;}
        static Vector3 V(Ashfall.Core.Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Ashfall.Core.Float3 F(Vector3 p){return new Ashfall.Core.Float3(p.x,p.y,p.z);}
    }
}
#endif
