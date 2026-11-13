#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Integration.Playable.Gallery
{
    internal delegate bool R23GalleryRaycast(Vector3 origin,Vector3 direction,float range,out RaycastHit hit);
    // Owned preview art only. Mesh/material decoding, optics and cues reuse the playable ports.
    internal sealed class R23GalleryModel:IDisposable
    {
        internal readonly SourceRifleDefinition Definition;
        internal readonly Transform Root,Model,Muzzle;
        readonly Transform offered,held;
        readonly List<Renderer> magazines=new List<Renderer>();
        readonly Dictionary<Transform,Vector3> bolts=new Dictionary<Transform,Vector3>();
        readonly List<Material> materials=new List<Material>();
        Vector3 magazineHome;float reloadScale;bool leftHanded;R23GalleryFit fit;
        R23Audio audio;R23SprScope scope;
        R23Mp9Laser laser;Camera viewer;Func<Vector3,Vector3,bool> clear;R23GalleryRaycast raycast;Transform insertArrow,chargeArrow;
        readonly R23WeaponFeedbackState profileFeedback;
        Vector3 sourceMuzzle;
        static readonly Vector3 Grip=new Vector3(0,-.057f,-.025f);
        public R23GalleryModel(SourceRifleDefinition definition,Transform parent) {
            if(definition==null||!(definition.Id=="mk18"||definition.Id=="akm"||definition.Id=="spr"||definition.Id=="cx4"||definition.Id=="px4"||Fitted(definition.Id)))throw new ArgumentException("Actual source rifle profile required");
            Definition=definition;reloadScale=definition.ReloadScale;profileFeedback=new R23WeaponFeedbackState(definition.Id);Root=new GameObject("R23 gallery "+definition.DisplayName).transform;Root.SetParent(parent,false);Root.gameObject.SetActive(false);
            Model=new GameObject("Source art").transform;Model.SetParent(Root,false);Model.localScale=Vector3.one*definition.ModelScale;
            Muzzle=new GameObject("Independent preview source bore").transform;Muzzle.SetParent(Root,false);sourceMuzzle=Fitted(definition.Id)?Vector3.zero:SourceMuzzle(definition.Id);Muzzle.localPosition=sourceMuzzle*definition.ModelScale;
            magazineHome=Fitted(definition.Id)?Vector3.zero:MagazineHome(definition.Id);
            offered=new GameObject("Source offered preview magazine").transform;offered.SetParent(Root,false);offered.localPosition=V(definition.Offer)*definition.ModelScale;offered.localScale=Vector3.one*definition.ModelScale;
            held=new GameObject("Source held preview magazine").transform;held.SetParent(parent,false);held.localScale=Vector3.one*definition.ModelScale;held.gameObject.SetActive(false);
        }
        public IEnumerator Load(R23Visuals visuals,Camera head,Transform cameraRoot,Func<Vector3,Vector3,bool> clearContact,float volume,R23GalleryRaycast nativeRaycast=null) {
            viewer=head;clear=clearContact;raycast=nativeRaycast;
            if(Fitted(Definition.Id))
            {
                yield return R23GalleryFit.Load(Definition.Id,Definition.ModelScale,delegate(R23GalleryFit source){fit=source;});
                if(Mathf.Abs(fit.reloadScale-Definition.ReloadScale)>.00001f||fit.topFeed!=Definition.TopFeed)throw new InvalidOperationException("Production gallery fit/control geometry mismatch: "+Definition.Id);
                reloadScale=fit.reloadScale;magazineHome=R23GalleryFit.Vector(fit.magazineHome);sourceMuzzle=R23GalleryFit.Vector(fit.muzzle);Muzzle.localPosition=sourceMuzzle*Definition.ModelScale;offered.localPosition=V(Definition.Offer)*reloadScale;
            }
            yield return visuals.Load(Definition.Id,Model);
            if(Definition.Id=="mk18")Model.gameObject.AddComponent<R23ReflexOptic>().Initialize(Definition.ModelScale,Muzzle);
            else if(Definition.Id=="akm")Model.gameObject.AddComponent<R23ReflexOptic>().InitializeAkRings();
            else if(Definition.Id=="cx4")Model.gameObject.AddComponent<R23ReflexOptic>().InitializeCx4Rings(Muzzle);
            else if(Definition.Id=="spr"){var housing=new GameObject("Accepted Gemini preview scope").transform;housing.SetParent(Model,false);yield return visuals.Load("spr-scope",housing);scope=housing.gameObject.AddComponent<R23SprScope>();scope.Initialize(head,Muzzle,cameraRoot,clear);scope.SetAllowed(false);}
            if(Catalog5(Definition.Id))Model.gameObject.AddComponent<SourceCatalog5Optics>().Initialize(Definition.Id,Definition.ModelScale,Muzzle);
            else if(fit!=null&&fit.opticScale>0)Model.gameObject.AddComponent<R23ReflexOptic>().Initialize(Definition.ModelScale,Muzzle,fit.opticMountHeight,fit.opticScale);
            if(Definition.TopFeed)
            {
                insertArrow=new GameObject("Source P90 FORWARD insert arrow").transform;insertArrow.SetParent(Model,false);insertArrow.localRotation=Quaternion.FromToRotation(Vector3.up,Vector3.forward);insertArrow.gameObject.SetActive(false);yield return visuals.Load("p90-insert-arrow",insertArrow);
                chargeArrow=new GameObject("Source P90 BACK charge arrow").transform;chargeArrow.SetParent(Model,false);chargeArrow.gameObject.SetActive(false);yield return visuals.Load("p90-charge-arrow",chargeArrow);
                foreach(var arrow in new[]{insertArrow,chargeArrow})foreach(var renderer in arrow.GetComponentsInChildren<MeshRenderer>(true)){renderer.sharedMaterial.SetColor("_EmissionColor",new Color(229f/255,172f/255,34f/255)*.45f);renderer.sharedMaterial.EnableKeyword("_EMISSION");}
            }
            foreach(var renderer in Model.GetComponentsInChildren<MeshRenderer>(true)) {
                if(renderer.name.StartsWith("Bolt/")||renderer.name.StartsWith("Slide/"))bolts.Add(renderer.transform,renderer.transform.localPosition);
                if(!renderer.name.StartsWith("Magazine/"))continue;magazines.Add(renderer);
                foreach(var parent in new[]{offered,held}){var copy=new GameObject("Source preview magazine mesh");copy.layer=18;copy.transform.SetParent(parent,false);copy.transform.localPosition=-magazineHome;copy.AddComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;copy.AddComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterial;}
            }
            if(bolts.Count==0&&(Definition.Id=="mk18"||Definition.Id=="cx4")) {
                var handle=GameObject.CreatePrimitive(PrimitiveType.Cube);handle.name="Source authored "+Definition.Id+" preview handle";handle.layer=18;handle.transform.SetParent(Model,false);handle.transform.localPosition=V(Definition.Charging);handle.transform.localScale=new Vector3(.032f,.012f,.022f);UnityEngine.Object.Destroy(handle.GetComponent<Collider>());
                var steel=new Material(Shader.Find("Universal Render Pipeline/Lit"));steel.SetColor("_BaseColor",new Color(.29f,.32f,.33f));steel.SetFloat("_Metallic",.65f);steel.SetFloat("_Smoothness",.64f);materials.Add(steel);handle.GetComponent<Renderer>().sharedMaterial=steel;bolts.Add(handle.transform,handle.transform.localPosition);
            }
            if(Definition.Id=="mp9"){if(raycast==null||clear==null)throw new InvalidOperationException("Source MP9 gallery requires actual world query provider");laser=new R23Mp9Laser(Root);}
            audio=new R23Audio(Root,Muzzle);audio.SetSfxVolume(volume);yield return audio.Load();Static();
        }
        public void Dock(Transform gallery,Vector3 position,Quaternion rotation){Root.SetParent(gallery,true);Root.position=position;Root.rotation=rotation;Root.localScale=Vector3.one;Root.gameObject.SetActive(true);Static();}
        public void Attach(Transform hand,Transform support,bool primaryLeft=false){leftHanded=primaryLeft;Root.gameObject.SetActive(true);Root.SetParent(hand,false);Root.localPosition=fit!=null?R23GalleryFit.Vector(fit.weaponGripOrigin):Definition.Id=="cx4"||Definition.Id=="px4"?Vector3.zero:R23TrackedGripPose.GripCenteredOffset(Grip,Definition.ModelScale);Root.localRotation=R23TrackedGripPose.LocalRotation;Root.localScale=Vector3.one;held.SetParent(support,false);held.localPosition=Vector3.zero;held.localRotation=R23TrackedGripPose.LocalRotation;held.localScale=Vector3.one*Definition.ModelScale;}
        void Static(){foreach(var part in magazines)if(part!=null)part.enabled=fit==null||!fit.hideSeatedMagazine;foreach(var pair in bolts)if(pair.Key!=null)pair.Key.localPosition=pair.Value;Model.localPosition=Vector3.zero;Model.localRotation=Quaternion.identity;profileFeedback.Reset();RefreshTrackedPresentation();offered.gameObject.SetActive(false);held.gameObject.SetActive(false);if(insertArrow!=null)insertArrow.gameObject.SetActive(false);if(chargeArrow!=null)chargeArrow.gameObject.SetActive(false);if(laser!=null)laser.Hide();if(scope!=null)scope.SetAllowed(false);if(audio!=null)audio.Stop();}
        // Same render-only state and pose resolver as the carried weapon. Advance
        // once in Sample; late tracking only reprojects the already observed state.
        public void AdvanceFeedback(float seconds,bool supported){profileFeedback.SetSupported(supported);profileFeedback.Advance(seconds);RefreshTrackedPresentation();}
        public void RefreshTrackedPresentation(){
            var q=Root.rotation;var neutral=new R23ShotPose(F(Root.position),F(q*Vector3.right),F(q*Vector3.up),F(q*Vector3.forward));R23ShotPose model,shot;
            R23WeaponPoseMath.Resolve(neutral,F(sourceMuzzle),Definition.ModelScale,profileFeedback.Distance,profileFeedback.PitchRadians,profileFeedback.YawRadians,R23WeaponPoseMath.UprightPulse(Definition.Id),out model,out shot);
            Model.SetPositionAndRotation(V(model.Origin),Quaternion.LookRotation(V(model.Forward),V(model.Up)));
            Muzzle.SetPositionAndRotation(V(shot.Origin),Quaternion.LookRotation(V(shot.Forward),V(shot.Up)));
        }
        public void Apply(HandlingSnapshot state,bool focused,bool supportReady)
        {
            profileFeedback.AcceptedShot(state.Shots,state.Supported);RefreshTrackedPresentation();
            foreach(var part in magazines)if(part!=null)part.enabled=(fit==null||!fit.hideSeatedMagazine)&&(state.Phase==ReloadPhase.Ready||state.Phase==ReloadPhase.NeedsRack);
            float slide=state.Phase!=ReloadPhase.Ready?0:profileFeedback.SlideTravel;
            foreach(var pair in bolts)if(pair.Key!=null)pair.Key.localPosition=pair.Value+new Vector3(fit!=null&&fit.chargingMirrorX&&leftHanded?-2*Definition.Charging.X:0,0,-state.BoltTravel-slide);
            if(fit!=null&&fit.heldMagazineGunBasis)held.rotation=Root.rotation;
            if(insertArrow!=null){insertArrow.gameObject.SetActive(false);insertArrow.localPosition=V(state.Phase==ReloadPhase.NeedsMagazine?Definition.Offer:Definition.Insert)+Vector3.up*(.04f/Definition.ModelScale);}
            if(chargeArrow!=null){chargeArrow.gameObject.SetActive(false);chargeArrow.localPosition=V(Definition.ChargingPoint(leftHanded))+Vector3.up*(.04f/Definition.ModelScale);}
            if(laser!=null)
            {
                Vector3 start=Muzzle.position,end=start+Muzzle.forward*180;bool visible=focused&&Root.gameObject.activeInHierarchy&&clear(viewer.transform.position,start);RaycastHit hit;
                if(visible&&raycast(start,Muzzle.forward,180,out hit))end=hit.point;
                laser.Present(start,end,viewer.transform.position,Muzzle.right,visible);
            }
            offered.gameObject.SetActive(focused&&state.Phase==ReloadPhase.NeedsMagazine);held.gameObject.SetActive(focused&&supportReady&&state.Phase==ReloadPhase.HeldMagazine);if(scope!=null)scope.SetAllowed(focused);
        }
        public Vector3 SupportLocal(Vector3 position){return Root.InverseTransformPoint(position)/reloadScale;}
        public float ForegripDistance(Vector3 position){return Vector3.Distance(position,Root.TransformPoint(V(Definition.SupportPoint)*reloadScale));}
        public bool ScopeActive { get { return scope!=null&&scope.Active; } }
        public void Cue(string action,bool shot=false){if(audio!=null)audio.Play(Definition.Id,action,shot);}
        public void SetVolume(float value){if(audio!=null)audio.SetSfxVolume(value);}
        public void Hide(){Static();Root.gameObject.SetActive(false);}
        public void Dispose(){if(audio!=null)audio.Dispose();if(laser!=null)laser.Dispose();if(scope!=null)scope.SetAllowed(false);if(held!=null)UnityEngine.Object.Destroy(held.gameObject);if(Root!=null)UnityEngine.Object.Destroy(Root.gameObject);foreach(var material in materials)UnityEngine.Object.Destroy(material);}
        static bool Catalog5(string id){return id=="1911"||id=="tec9"||id=="rfb"||id=="fal"||id=="s85";}
        static bool Fitted(string id){return Smg(id)||Catalog5(id);}
        static bool Smg(string id){return id=="ump"||id=="p90"||id=="mp5"||id=="mp9";}
        static Vector3 SourceMuzzle(string id){return new Vector3(0,.045f,id=="cx4"?.62f:id=="px4"?.29f:id=="akm"?.76f:.69f);}
        static Vector3 MagazineHome(string id)
        {
            switch(id){case "mk18":return new Vector3(-.00045859f,-.0960232f,.17046988f);case "akm":return new Vector3(0,-.0897345f,.20337826f);case "spr":return new Vector3(0,-.05928609f,.11562737f);case "cx4":return new Vector3(-1.74634852e-8f,-.12042805f,-.0107801325f);case "px4":return new Vector3(0,-.0662978311f,.091493664f);default:throw new ArgumentException("Source magazine profile unavailable");}
        }
        static Float3 F(Vector3 value){return new Float3(value.x,value.y,value.z);}
        static Vector3 V(Float3 value){return new Vector3(value.X,value.Y,value.Z);}
    }
}
#endif
