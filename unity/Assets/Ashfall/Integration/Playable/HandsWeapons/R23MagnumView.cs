#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Original GLB body plus seven source-built rounds/cylinder. No tracked/root writes.
    public sealed class R23MagnumView:MonoBehaviour
    {
        public bool Ready {get;private set;}public bool Loading {get;private set;}public string Failure {get;private set;}
        public Transform Muzzle {get;private set;}public Transform Model {get;private set;}
        Transform cylinder,offer;R23Visuals visuals;bool ownsModel;
        public bool IsTransient(Transform candidate){return offer!=null&&(candidate==offer||candidate.IsChildOf(offer));}
        readonly List<Renderer>[] chambers=new List<Renderer>[7],rounds=new List<Renderer>[7];
        public void Initialize(Transform unscaledWeaponRoot)
        {
            if(unscaledWeaponRoot==null)throw new ArgumentNullException("unscaledWeaponRoot");
            var model=new GameObject("Source Magnum .84 model").transform;model.SetParent(unscaledWeaponRoot,false);model.localScale=Vector3.one*R23MagnumRuntime.ModelScale;ownsModel=true;InitializeModel(model);
        }
        public void InitializeModel(Transform scaledSourceModel)
        {
            if(visuals!=null||scaledSourceModel==null)throw new InvalidOperationException("Magnum view requires one source model");
            visuals=new R23Visuals();Model=scaledSourceModel;
            cylinder=new GameObject("Source seven-chamber cylinder").transform;cylinder.SetParent(Model,false);offer=new GameObject("Source seven-round offer").transform;offer.SetParent(Model,false);
            Muzzle=new GameObject("Source Magnum bore").transform;Muzzle.SetParent(Model,false);Muzzle.localPosition=V(R23MagnumRuntime.SourceMuzzle);
            Loading=true;StartCoroutine(LoadSafely());
        }
        IEnumerator Load(){yield return visuals.Load("magnum",Model);yield return visuals.Load("magnum-cylinder",cylinder);yield return visuals.Load("magnum-offer",offer);
            for(int i=0;i<7;i++){chambers[i]=new List<Renderer>();rounds[i]=new List<Renderer>();foreach(var renderer in cylinder.GetComponentsInChildren<Renderer>(true))if(renderer.name.StartsWith("LiveRound"+i+"/")||renderer.name.StartsWith("LiveRound"+i+"."))chambers[i].Add(renderer);foreach(var renderer in offer.GetComponentsInChildren<Renderer>(true))if(renderer.name.StartsWith("OfferRound"+i+"."))rounds[i].Add(renderer);if(chambers[i].Count<2||rounds[i].Count<4)throw new InvalidOperationException("Magnum original seven-round part contract: "+i);}
            visuals.ReleaseLoadedBytes();Loading=false;Ready=true;offer.gameObject.SetActive(false);
        }
        IEnumerator LoadSafely(){var stack=new Stack<IEnumerator>();stack.Push(Load());while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception error){Failure=error.ToString();Loading=false;Ready=false;if(Model!=null)Model.gameObject.SetActive(false);yield break;}if(!next){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}}
        public void Present(R23MagnumRuntime runtime,bool visible,bool focused,int primaryHand,Transform supportHand)
        {
            if(primaryHand<0||primaryHand>1)throw new ArgumentOutOfRangeException("primaryHand");if(!Ready)return;
            if(runtime==null||!visible){Model.gameObject.SetActive(false);return;}Model.gameObject.SetActive(true);
            bool left=primaryHand==0,open=runtime.Snapshot.Phase!=Ashfall.Modules.WeaponHandling.ReloadPhase.Ready;
            cylinder.localPosition=V(R23MagnumRuntime.CylinderOffset(left,open));for(int i=0;i<7;i++)foreach(var renderer in chambers[i])renderer.enabled=runtime.ChamberLive(i);
            offer.gameObject.SetActive(focused&&runtime.OfferedRoundCount>0);for(int i=0;i<7;i++)foreach(var renderer in rounds[i])renderer.enabled=i<runtime.OfferedRoundCount;
            if(runtime.ReloadHandOccupied&&supportHand!=null&&focused){offer.SetPositionAndRotation(supportHand.position,Model.parent.rotation);offer.localScale=Vector3.one/R23MagnumRuntime.ModelScale;}
            else{offer.localPosition=V(R23MagnumRuntime.Offer(left));offer.localRotation=Quaternion.identity;offer.localScale=Vector3.one;}
        }
        void OnDisable(){if(Model!=null)Model.gameObject.SetActive(false);}
        void OnDestroy(){StopAllCoroutines();if(ownsModel&&Model!=null)Destroy(Model.gameObject);if(visuals!=null)visuals.Dispose();}
        static Vector3 V(Ashfall.Core.Float3 value){return new Vector3(value.X,value.Y,value.Z);}
    }
}
#endif
