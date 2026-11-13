#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Input;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // ONE XR-off production menu/gallery interaction. Caller owns engine admission.
    // Requires a ready source lobby, then restores raw scene/input flags without
    // restarting/refilling any inventory or injecting a floor/target/audio signal.
    public sealed class R24MenuGalleryInteractionFixture
    {
        [Serializable] sealed class Receipt
        {
            public bool passed,executed,cleanup,walk,turn,loadingBlocked,heldInputsBlocked,dockFixed,heldFire,freeHandMenu,movementRequested=true;
            public bool headsetVerified=false,audibilityVerified=false;
            public string error="",cleanupError="",profile="",map="",inventoryBefore="",inventoryAfter="";
            public float acceptedWalk,yawDegrees,walkSetting,itemMovementGain,walkStepSeconds,walkDistanceLimit,rootYBeforeWalk,headYBeforeWalk;public int previewShots,settleTicks,walkTicks;public bool supportedBeforeWalk;public List<string> notes=new List<string>();public List<string> audio=new List<string>();
        }
        R23PlayableLoop loop;Receipt result;Action<string> note;Action<Camera,string> capture;Saved saved;
        readonly float[] grip=new float[2],trigger=new float[2];float deadline;string carried,health;int builds,charges;string mode;GameObject passiveAudio;
        public bool Passed {get;private set;}public string Json {get;private set;}public string Error {get{return result==null?"":result.error+result.cleanupError;}}
        public IEnumerator Run(R23PlayableLoop actualRoot,Action<string> observation=null,Action<Camera,string> captureUnityOnlyUnmatched=null,bool exerciseMovement=true)
        {
            loop=actualRoot;note=observation;capture=captureUnityOnlyUnmatched;result=new Receipt{movementRequested=exerciseMovement};deadline=Time.realtimeSinceStartup+350;
            var stack=new Stack<IEnumerator>();stack.Push(Exercise());
            try
            {
                while(stack.Count>0)
                {
                    bool more=false;object value=null;
                    try{Bound();more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}
                    catch(Exception e){result.error=e.ToString();Note("FAIL "+result.error);break;}
                    if(!more){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}
                    var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;
                }
            }
            finally
            {
                while(stack.Count>0){var d=stack.Pop() as IDisposable;if(d!=null)d.Dispose();}
                try
                {
                    if(saved!=null){Array.Clear(grip,0,2);Array.Clear(trigger,0,2);Pump();loop.Gallery.Hide();loop.UI.ShowReady();saved.Restore(loop);}
                    if(passiveAudio!=null)UnityEngine.Object.Destroy(passiveAudio);
                    result.cleanup=true;Note("Cleanup restored actual raw root/origin/head/hands, rig/body/input flags, menu page; no inventory reset/refill, settings/volumes untouched.");
                }
                catch(Exception e){result.cleanupError=e.ToString();Note("CLEANUP FAIL "+e);}
                result.passed=result.error.Length==0&&result.cleanupError.Length==0&&result.executed&&(!result.movementRequested||result.walk&&result.turn&&result.heldInputsBlocked)&&result.loadingBlocked&&result.dockFixed&&result.heldFire&&result.freeHandMenu;
                Passed=result.passed;Json=JsonUtility.ToJson(result,true);Note("TERMINAL Unity-only/XR-off; native not established until caller executes. "+Json);
            }
        }
        IEnumerator Exercise()
        {
            Check(!UnityEngine.XR.XRSettings.isDeviceActive,"XR disabled; refuse to overwrite a live tracked headset");
            Check(loop!=null,"actual Root supplied");while(!loop.Ready&&loop.Failure==null){Bound();yield return null;}
            Check(loop.Ready&&loop.Rig!=null&&loop.UI!=null&&loop.Gallery!=null,"actual loaded Root ready: "+loop.Failure);
            Check(loop.Lobby!=null&&loop.Lobby.Ready&&loop.Lobby.Visible&&loop.UI.Page==R23UiPage.Ready,"caller must enter actual source lobby/Ready first; no mode/map reset by fixture");
            saved=new Saved(loop);result.executed=true;result.map=loop.Map.MapId;loop.ExternalProbePump=true;loop.ManualSmoke=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
            loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Head.transform.localRotation=Quaternion.identity;
            loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.2f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.2f);loop.Rig.Left.localRotation=loop.Rig.Right.localRotation=Quaternion.identity;
            carried=Inventory();health=Health();result.inventoryBefore=carried;builds=loop.Activities.PanelSnapshot.Length;charges=loop.Activities.Balance;mode=loop.Activities.Mode.ToString();
            Note("Lobby entry grenade held="+(loop.Explosives==null?"unavailable":loop.Explosives.HeldId??"none")+" owned="+(loop.Explosives==null?0:loop.Explosives.OwnedItems.Length)+"; no grenade granted/equipped by fixture.");
            Check(loop.Explosives==null||loop.Explosives.HeldId==null,"source lobby transition must stow any grenade while retaining its inventory");
            for(int i=0;i<6;i++){Pump();loop.Rig.SmokeMenuMove(Vector2.zero,0,.02f);yield return null;}
            Check(loop.Rig.MenuLocomotion&&loop.Rig.GameplayBlocked,"lobby movement enabled independently of source gameplay block");Floor();
            // The authored spawn starts above its floor. Exercise ordinary neutral
            // motor ticks until real support is acquired; a downward ray alone
            // does not establish the grounded walking precondition.
            while(!loop.Rig.Supported&&result.settleTicks<150){loop.Rig.SmokeMenuMove(Vector2.zero,0,.02f);result.settleTicks++;Pump();yield return null;}
            result.supportedBeforeWalk=loop.Rig.Supported;result.rootYBeforeWalk=loop.Rig.transform.position.y;result.headYBeforeWalk=loop.Rig.Head.transform.position.y;
            Check(result.supportedBeforeWalk,"actual neutral motor settled on lobby floor; ticks="+result.settleTicks+" rootY="+result.rootYBeforeWalk+" headY="+result.headYBeforeWalk+" velocity="+loop.Rig.MotionVelocity);Floor();
            if(result.movementRequested){
            Vector3 start=loop.Rig.transform.position,raw=loop.Rig.Head.transform.localPosition;
            // Bound the requested travel, rather than rejecting valid configured
            // walking above an arbitrary one-metre limit. Preserve user settings.
            result.walkSetting=loop.UI.Settings.WalkMultiplier;
            result.itemMovementGain=loop.Rig.ReadItemMovementMultiplier==null?1:loop.Rig.ReadItemMovementMultiplier();
            Check(!float.IsNaN(result.itemMovementGain)&&!float.IsInfinity(result.itemMovementGain)&&result.itemMovementGain>0&&result.itemMovementGain<=4,"finite actual equipped movement gain="+result.itemMovementGain);
            float speed=PlayerTuning.WalkSpeed*result.walkSetting*result.itemMovementGain;
            result.walkStepSeconds=.02f;result.walkTicks=Mathf.Clamp(Mathf.FloorToInt(.5f/(.45f*result.walkStepSeconds*speed)),1,14);
            result.walkDistanceLimit=result.walkTicks*result.walkStepSeconds*speed*.45f+.015f;
            Note("Walk intent: setting="+result.walkSetting+" itemGain="+result.itemMovementGain+" ticks="+result.walkTicks+" dt="+result.walkStepSeconds+" travelLimit="+result.walkDistanceLimit);
            for(int i=0;i<result.walkTicks;i++){loop.Rig.SmokeMenuMove(new Vector2(.45f,0),0,result.walkStepSeconds);Pump();yield return null;}
            result.acceptedWalk=Vector3.ProjectOnPlane(loop.Rig.transform.position-start,Vector3.up).magnitude;
            Check(result.acceptedWalk>.025f&&result.acceptedWalk<=result.walkDistanceLimit,"real menu motor accepted bounded floor walk="+result.acceptedWalk+" limit="+result.walkDistanceLimit+" root="+loop.Rig.transform.position);Floor();
            Check(Vector3.Distance(raw,loop.Rig.Head.transform.localPosition)<.0001f,"accepted artificial motion preserved raw tracked head sample");result.walk=true;
            Quaternion beforeTurn=loop.Rig.transform.rotation;
            for(int i=0;i<14;i++){loop.Rig.SmokeMenuMove(Vector2.zero,.9f,.02f);Pump();yield return null;}
            result.yawDegrees=Quaternion.Angle(beforeTurn,loop.Rig.transform.rotation);Check(result.yawDegrees>1,"actual configured menu turn accepted="+result.yawDegrees);result.turn=true;
            for(int i=0;i<5;i++){loop.Rig.SmokeXrHands(1,1,.02f);var f=loop.Rig.LastFrame;Check(!f.GripLeft&&!f.GripRight&&!f.TriggerLeft&&!f.TriggerRight&&!f.Jump&&!f.Reload&&!f.Sprint,"MenuLocomotion strips actual raw held grips/triggers in Advance");Pump();yield return null;}
            Unchanged();result.heldInputsBlocked=true;
            }else Note("Accepted walk/turn/held-input groups skipped; neutral real floor support is setup only.");
            // Actual pointer callback opens source catalog; no direct Gallery.Show.
            yield return Click(Button("LOADOUT"),1-loop.Gameplay.PrimaryHand);
            Check(loop.UI.Page==R23UiPage.Weapons&&loop.Gallery.Active,"actual lobby LOADOUT callback opened private gallery");
            string first=loop.UI.CurrentViewedWeaponId;result.profile=first;Check(SourceRifleDefinition.ForId(first)!=null,"one current standard source gallery profile required: "+first);
            yield return WaitGallery(first);var model=Model(first);Vector3 dock=model.position;Quaternion dockQ=model.rotation;Dock(model);
            loop.Rig.Head.transform.localPosition+=new Vector3(.18f,.02f,.08f);loop.Rig.Head.transform.localRotation=Quaternion.Euler(0,17,0);Pump();yield return null;
            Check(Vector3.Distance(model.position,dock)<.0001f&&Quaternion.Angle(model.rotation,dockQ)<.01f,"source dock fixed across raw head motion");
            yield return Click(Button(">"),1-loop.Gameplay.PrimaryHand);string browsed=loop.UI.CurrentViewedWeaponId;Check(browsed!=first,"real catalog NEXT changed viewed item");yield return WaitGallery(browsed);Dock(Model(browsed));
            yield return Click(Button("<"),1-loop.Gameplay.PrimaryHand);Check(loop.UI.CurrentViewedWeaponId==first,"real previous browse restored current source profile");yield return WaitGallery(first);model=Model(first);Dock(model);Check(Vector3.Distance(dock,model.position)<.0001f,"browsing preserved same source dock");result.dockFixed=true;
            int hand=loop.Gameplay.PrimaryHand;var tracked=Hand(hand);tracked.position=model.position;tracked.rotation=Quaternion.LookRotation(loop.Lobby.MenuAnchor.forward,Vector3.up)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);Pump();grip[hand]=.8f;Pump();yield return null;
            Check(loop.Gallery.ViewState.Live&&loop.Gallery.HoldingHand==hand,"actual fresh grip through Root owns preview hand="+loop.Gallery.HoldingHand);
            Transform back=Button("BACK");Point(back,hand);trigger[hand]=0;Pump();int shots=loop.Gallery.ViewState.PreviewShots,ammo=loop.Gallery.ViewState.Ammo;
            Check(loop.UI.Page==R23UiPage.Weapons,"held neutral aimed at actual BACK retains Weapons page");trigger[hand]=.9f;
            for(int i=0;i<8&&loop.Gallery.ViewState.PreviewShots==shots;i++){Pump();ObserveAudio("preview-fire-step"+i);yield return null;Bound();}
            result.previewShots=loop.Gallery.ViewState.PreviewShots-shots;
            Check(result.previewShots>0&&loop.Gallery.ViewState.Ammo<ammo,"actual private trigger accepted shots="+result.previewShots+" ammo="+ammo+"->"+loop.Gallery.ViewState.Ammo);
            Check(loop.UI.Page==R23UiPage.Weapons&&loop.Gallery.HoldingHand==hand,"shooting at actual BACK did not click board or release preview custody");Unchanged();result.heldFire=true;
            ObserveAudio("after-preview-shot");if(capture!=null)capture(loop.Rig.Head,"unity-only-unmatched-menu-gallery-held-back");trigger[hand]=0;Pump();
            // Keep the firing hand holding while the other hand uses real BACK.
            yield return Click(Button("BACK"),1-hand);Check(loop.UI.Page==R23UiPage.Ready&&!loop.Gallery.Active&&loop.Gallery.HoldingHand<0,"free hand actual BACK remains usable while other hand holds preview");result.freeHandMenu=true;grip[hand]=0;Pump();Unchanged();
            loop.UI.BeginLoading("MENU INTERACTION PROBE");Pump();Check(!loop.Rig.MenuLocomotion&&loop.Rig.GameplayBlocked,"actual Loading page blocks menu motor and gameplay");Vector3 blockedAt=loop.Rig.transform.position;Quaternion blockedQ=loop.Rig.transform.rotation;
            for(int i=0;i<6;i++){loop.Rig.SmokeMenuMove(Vector2.one,1,.02f);Pump();yield return null;}
            Check(Vector3.ProjectOnPlane(loop.Rig.transform.position-blockedAt,Vector3.up).magnitude<.001f&&Quaternion.Angle(blockedQ,loop.Rig.transform.rotation)<.01f,"loading rejected real move/turn root="+loop.Rig.transform.position);result.loadingBlocked=true;loop.UI.ShowReady();Pump();Unchanged();result.inventoryAfter=Inventory();
        }
        void Bound(){if(Time.realtimeSinceStartup>=deadline)throw new TimeoutException("Menu/gallery work exceeded350s; caller reserves cleanup within360s");}
        void Note(string text){result.notes.Add(text);UnityEngine.Debug.Log("MENU GALLERY FIXTURE "+text);if(note!=null)note(text);}
        void Check(bool okay,string text){Note((okay?"PASS ":"REFUSED ")+text);if(!okay)throw new InvalidOperationException(text);}
        Transform Hand(int hand){return hand==0?loop.Rig.Left:loop.Rig.Right;}
        HandSample Sample(int hand){Vector3 p=loop.Rig.transform.InverseTransformPoint(Hand(hand).position);return new HandSample{Tracked=true,Grip=grip[hand],Trigger=trigger[hand],Position=new Float3(p.x,p.y,p.z)};}
        void Pump(){loop.Rig.SmokeTrackedHands(Sample(0),Sample(1));loop.SmokeUiUpdate();UnityEngine.Physics.SyncTransforms();}
        Transform Button(string caption){foreach(var t in loop.UI.GetComponentsInChildren<TextMesh>())if(t.text==caption&&t.transform.parent.Find("Button background")!=null)return t.transform.parent;throw new InvalidOperationException("Actual active button missing: "+caption);}
        void Point(Transform surface,int hand){Hand(hand).SetPositionAndRotation(surface.position-surface.forward*.5f,Quaternion.LookRotation(surface.forward,surface.up)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation));}
        IEnumerator Click(Transform surface,int hand){Point(surface,hand);trigger[hand]=0;Pump();yield return null;trigger[hand]=.9f;Pump();yield return null;trigger[hand]=0;Pump();yield return null;}
        IEnumerator WaitGallery(string id){float until=Mathf.Min(deadline,Time.realtimeSinceStartup+90);while(!loop.Gallery.IsWeaponReady(id)&&loop.Gallery.WeaponFailure(id)==null&&Time.realtimeSinceStartup<until){Pump();yield return null;}string witness="requested="+id+" ready="+loop.Gallery.IsWeaponReady(id)+" viewed="+loop.Gallery.ViewedWeaponId+" visibleCount="+loop.Gallery.VisiblePreviewCount+" visibleId="+loop.Gallery.VisiblePreviewId+" actualVisibleIds=["+String.Join(",",loop.Gallery.VisiblePreviewIds)+"] issue="+loop.Gallery.WeaponFailure(id);Note("BEFORE single preview assertion "+witness);Check(loop.Gallery.IsWeaponReady(id)&&loop.Gallery.VisiblePreviewCount==1&&loop.Gallery.VisiblePreviewId==id,"actual single source gallery "+witness);}
        Transform Model(string id){if(id=="dt11"){var field=loop.Gallery.GetType().GetField("dt11Root",BindingFlags.Instance|BindingFlags.NonPublic);var actual=field==null?null:field.GetValue(loop.Gallery) as Transform;if(actual!=null&&actual.gameObject.activeInHierarchy)return actual;throw new InvalidOperationException("Actual source DT11 browse root unavailable");}var definition=SourceRifleDefinition.ForId(id);string name=definition==null?null:"R23 gallery "+definition.DisplayName;foreach(var t in loop.Gallery.GetComponentsInChildren<Transform>(true))if(t.name==name&&t.gameObject.activeInHierarchy)return t;throw new InvalidOperationException("Actual source profile dock root unavailable: "+id);}
        void Dock(Transform model){Vector3 expected=loop.Lobby.MenuAnchor.TransformPoint(new Vector3(0,.25f,-1.15f));Check(Vector3.Distance(model.position,expected)<.0001f,"actual source menu-anchor dock expected="+expected+" observed="+model.position);}
        void Floor(){RaycastHit hit;bool found=UnityEngine.Physics.Raycast(loop.Rig.Head.transform.position+Vector3.up*.1f,Vector3.down,out hit,4,1,QueryTriggerInteraction.Ignore);Check(found&&hit.collider.transform.root.name=="R23 StandaloneLobbyMap091"&&hit.normal.y>.5f,"actual authored lobby support="+(found?hit.collider.name+" at "+hit.point:"missing"));}
        string Inventory(){var rows=new List<string>();foreach(string id in loop.Gameplay.OwnedWeaponActionIds){var s=loop.Gameplay.ReadWeaponState(id);rows.Add(id+":"+s.Loaded+":"+s.Seated+":"+s.Phase+":"+s.Shots);}foreach(string family in new[]{"pistol","smg","rifle","shells","sniper"})rows.Add(family+":"+loop.Gameplay.ReadAmmoReserveFamily(family));if(loop.Consumables!=null)foreach(var c in loop.Consumables.OwnedItems)rows.Add("food:"+c.Id+":"+c.Kind+":"+c.Slot);if(loop.Explosives!=null)foreach(var g in loop.Explosives.OwnedItems)rows.Add("grenade:"+g.Id+":"+g.Kind+":"+g.Slot);rows.Sort(StringComparer.Ordinal);return String.Join("|",rows.ToArray());}
        string Health(){var rows=new List<string>{loop.Gameplay.Health+":"+loop.Gameplay.Shield+":"+loop.Gameplay.Hits};for(int i=0;i<loop.Gameplay.PracticeTargetCount;i++){var h=loop.Gameplay.ReadPracticeTarget(i);if(h!=null)rows.Add(i+":"+h.Health+":"+h.Shield);}return String.Join("|",rows.ToArray());}
        void Unchanged(){Check(Inventory()==carried&&Health()==health,"real carried IDs/ammo/family reserves/player and practice Health unchanged");Check(loop.Activities.PanelSnapshot.Length==builds&&loop.Activities.Balance==charges&&loop.Activities.Mode.ToString()==mode&&!loop.Climbing.LeftAnchored&&!loop.Climbing.RightAnchored,"no construction/flight/climb admission from lobby input");Check(loop.Rig.GameplayBlocked,"source gameplay remains blocked in lobby");}
        void ObserveAudio(string phase)
        {
            try
            {
                var type=loop.GetType().Assembly.GetType("Ashfall.Integration.Playable.Diagnostics.R24QuestPerformanceProbe");if(type==null){result.audio.Add(phase+": ReadAudio type unavailable");return;}
                if(passiveAudio==null){passiveAudio=new GameObject("Inactive passive existing ReadAudio adapter (no tone or runtime pump)");passiveAudio.SetActive(false);var component=passiveAudio.AddComponent(type);type.GetField("root",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(component,loop);}
                var reader=type.GetMethod("ReadAudio",BindingFlags.Instance|BindingFlags.NonPublic);object snapshot=reader.Invoke(passiveAudio.GetComponent(type),null);string data=phase+": "+JsonUtility.ToJson(snapshot);result.audio.Add(data);Note("SOFTWARE AUDIO ONLY "+data);
            }
            catch(Exception e){string text=phase+": audio observation unavailable: "+e;result.audio.Add(text);Note(text);}
        }
        sealed class Saved
        {
            readonly bool manual,pump,rig,body,external,calibration,blocked,menu;readonly Vector3 root,origin,head,left,right;readonly Quaternion rootQ,originQ,headQ,leftQ,rightQ;readonly HandSample leftInput,rightInput;
            internal Saved(R23PlayableLoop p){manual=p.ManualSmoke;pump=p.ExternalProbePump;rig=p.Rig.enabled;body=p.Rig.Body.enabled;external=p.Gameplay.ExternalInputPump;calibration=p.Calibration.SuppressPersistence;blocked=p.Rig.GameplayBlocked;menu=p.Rig.MenuLocomotion;root=p.Rig.transform.position;rootQ=p.Rig.transform.rotation;origin=p.Rig.TrackingOrigin.localPosition;originQ=p.Rig.TrackingOrigin.localRotation;head=p.Rig.Head.transform.localPosition;headQ=p.Rig.Head.transform.localRotation;left=p.Rig.Left.localPosition;leftQ=p.Rig.Left.localRotation;right=p.Rig.Right.localPosition;rightQ=p.Rig.Right.localRotation;leftInput=p.Rig.LeftInput;rightInput=p.Rig.RightInput;}
            internal void Restore(R23PlayableLoop p){p.Rig.Body.enabled=false;p.Rig.transform.SetPositionAndRotation(root,rootQ);p.Rig.TrackingOrigin.SetLocalPositionAndRotation(origin,originQ);p.Rig.Head.transform.SetLocalPositionAndRotation(head,headQ);p.Rig.Left.SetLocalPositionAndRotation(left,leftQ);p.Rig.Right.SetLocalPositionAndRotation(right,rightQ);p.Rig.Body.enabled=body;p.Rig.SmokeTrackedHands(leftInput,rightInput);p.Rig.GameplayBlocked=blocked;p.Rig.MenuLocomotion=menu;p.Calibration.SuppressPersistence=calibration;p.Gameplay.ExternalInputPump=external;p.ManualSmoke=manual;p.ExternalProbePump=pump;p.Rig.enabled=rig;UnityEngine.Physics.SyncTransforms();}
        }
    }
}
#endif
