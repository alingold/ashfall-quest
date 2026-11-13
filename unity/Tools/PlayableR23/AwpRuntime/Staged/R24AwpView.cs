#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed class R24AwpView:MonoBehaviour
    {
        readonly List<Renderer> magazines=new List<Renderer>();readonly Dictionary<Transform,Vector3> bolts=new Dictionary<Transform,Vector3>();R23Visuals visuals;Transform art,offered,held,insertArrow,chargeArrow;bool initialized;
        public bool Ready{get;private set;}public string Failure{get;private set;}public Transform Model{get;private set;}public Transform Muzzle{get;private set;}public R24AwpScope Scope{get;private set;}
        public void InitializeModel(Transform actualModel){if(initialized||actualModel==null)throw new ArgumentException("AWP actual model root required");initialized=true;Model=actualModel;StartCoroutine(Guard(Load()));}
        IEnumerator Guard(IEnumerator work){var jobs=new Stack<IEnumerator>();jobs.Push(work);while(jobs.Count>0){object value;try{var top=jobs.Peek();if(!top.MoveNext()){jobs.Pop();continue;}value=top.Current;}catch(Exception e){Failure=e.ToString();yield break;}var nested=value as IEnumerator;if(nested!=null)jobs.Push(nested);else yield return value;}Ready=true;}
        IEnumerator Load()
        {
            visuals=new R23Visuals();art=Child("Source default AWP complete21916 triangles",Model);yield return visuals.Load("awp",art);yield return visuals.Load("awp-chamber",art);
            Muzzle=Child("Source AWP2.5deg calibrated muzzle",Model);Muzzle.localPosition=V(R24AwpDefinition.Muzzle);Muzzle.localRotation=Quaternion.Euler(2.5f,0,0);
            offered=Child("Source AWP offered magazine",Model.parent);offered.localPosition=V(R24AwpDefinition.Offer);held=Child("Source AWP support-held magazine",transform);
            foreach(var renderer in art.GetComponentsInChildren<MeshRenderer>())
            {if(renderer.name.StartsWith("Bolt/",StringComparison.Ordinal))bolts.Add(renderer.transform,renderer.transform.localPosition);if(!renderer.name.StartsWith("Magazine/",StringComparison.Ordinal))continue;magazines.Add(renderer);foreach(var parent in new[]{offered,held}){var copy=Child("Actual source AWP magazine mesh",parent);copy.localPosition=-V(R24AwpDefinition.MagazineHome);copy.gameObject.AddComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;copy.gameObject.AddComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterial;}}
            if(magazines.Count==0||bolts.Count==0)throw new InvalidOperationException("Actual AWP Magazine/Bolt partitions missing");insertArrow=Child("Actual source AWP UP insertion arrow",Model);insertArrow.localPosition=new Vector3(-.045f,-.28f,R24AwpDefinition.MagazineHome.Z);yield return visuals.Load("awp-insert-arrow",insertArrow);chargeArrow=Child("Actual source AWP BACK bolt arrow",Model);chargeArrow.localPosition=V(R24AwpDefinition.Bolt)+new Vector3(.025f,.025f,0);yield return visuals.Load("awp-charge-arrow",chargeArrow);
            foreach(var cue in new[]{insertArrow,chargeArrow})foreach(var renderer in cue.GetComponentsInChildren<Renderer>()){renderer.sharedMaterial.SetColor("_EmissionColor",new Color(.05f,.72f,.75f)*.65f);renderer.sharedMaterial.EnableKeyword("_EMISSION");}visuals.ReleaseLoadedBytes();PresentDock();
        }
        public void InitializeScope(Camera head,Transform cameraRoot,Func<Vector3,Vector3,bool> clear){if(Scope!=null)throw new InvalidOperationException("AWP scope initialized");Scope=gameObject.AddComponent<R24AwpScope>();Scope.Initialize(head,cameraRoot,CurrentCandidate,clear);}
        public R23ShotPose CurrentCandidate(){return new R23ShotPose(F(Muzzle.position),F(Muzzle.right),F(Muzzle.up),F(Muzzle.forward));}
        public void Present(HandlingSnapshot state,Transform support,bool visible)
        {if(!Ready)return;insertArrow.gameObject.SetActive(visible&&(state.Phase==ReloadPhase.NeedsMagazine||state.Phase==ReloadPhase.HeldMagazine));chargeArrow.gameObject.SetActive(visible&&state.Phase==ReloadPhase.NeedsBolt);bool seated=state.Phase==ReloadPhase.Ready||state.Phase==ReloadPhase.NeedsBolt;foreach(var magazine in magazines)magazine.enabled=seated;foreach(var b in bolts)b.Key.localPosition=b.Value-Vector3.forward*state.BoltTravel;offered.gameObject.SetActive(visible&&state.Phase==ReloadPhase.NeedsMagazine);held.gameObject.SetActive(visible&&support!=null&&state.Phase==ReloadPhase.HeldMagazine);if(held.gameObject.activeSelf){held.SetPositionAndRotation(support.position,support.rotation*R23TrackedGripPose.LocalRotation);held.localScale=Vector3.one;}if(!visible&&Scope!=null)Scope.Shutdown();}
        public void PresentDock(){foreach(var magazine in magazines)magazine.enabled=true;foreach(var b in bolts)b.Key.localPosition=b.Value;ClearTransient();}
        public bool IsTransient(Transform item){return insertArrow!=null&&(item==insertArrow||item.IsChildOf(insertArrow))||chargeArrow!=null&&(item==chargeArrow||item.IsChildOf(chargeArrow));}
        public void ClearTransient(){if(insertArrow!=null)insertArrow.gameObject.SetActive(false);if(chargeArrow!=null)chargeArrow.gameObject.SetActive(false);if(offered!=null)offered.gameObject.SetActive(false);if(held!=null)held.gameObject.SetActive(false);if(Scope!=null)Scope.SetInput(new R24AwpScopeInput());}
        static Transform Child(string name,Transform parent){var t=new GameObject(name).transform;t.gameObject.layer=18;t.SetParent(parent,false);return t;}static Vector3 V(Ashfall.Core.Float3 a){return new Vector3(a.X,a.Y,a.Z);}static Ashfall.Core.Float3 F(Vector3 a){return new Ashfall.Core.Float3(a.x,a.y,a.z);}
        void OnDisable(){ClearTransient();}void OnDestroy(){ClearTransient();if(Scope!=null)Destroy(Scope);if(insertArrow!=null)Destroy(insertArrow.gameObject);if(chargeArrow!=null)Destroy(chargeArrow.gameObject);if(art!=null)Destroy(art.gameObject);if(Muzzle!=null)Destroy(Muzzle.gameObject);if(offered!=null)Destroy(offered.gameObject);if(held!=null)Destroy(held.gameObject);if(visuals!=null)visuals.Dispose();}
    }
}
#endif
