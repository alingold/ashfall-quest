#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Input;
using Ashfall.Integration.Playable.Consumables;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Real production UI, root callbacks, wheel, supply and weapon owners.
    // XR must be OFF. Simulated native input/captures do not verify a headset.
    // Run in a disposable smoke scene: cleanup restarts its actual round/loot.
    public sealed class R24R6UiSmoke
    {
        [Serializable] sealed class Group { public string name="",error="";public bool passed;public List<string> observations=new List<string>(); }
        [Serializable] sealed class Receipt { public bool success,nativeExecuted,headsetVerified=false;public string startedUtc="",finishedUtc="",error="";public List<Group> groups=new List<Group>(); }
        const string PrefKey="ashfall.r23.personal-ui-settings.v1";
        const string Hint="GRIP / CLICK EQUIPS  |  TRIGGER / RIGHT CLICK DROPS";
        R23PlayableLoop loop;Receipt receipt;Camera probe;Transform panel;bool touched;
        public bool Passed {get;private set;}public string Json {get;private set;}
        public bool AutoFinish=true;
        static void Need(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        static bool Near(float a,float b,float tolerance=.00002f){return Mathf.Abs(a-b)<=tolerance;}
        void Note(Group g,string value){g.observations.Add(value);Debug.Log("R6 UI "+g.name+": "+value);}
        Group Add(string name){var g=new Group{name=name};receipt.groups.Add(g);Debug.Log("R6 UI BEGIN "+name);return g;}
        // One callable entry. Root routes this only after the coordinated capture slot.
        public IEnumerator Run()
        {
            receipt=new Receipt{startedUtc=DateTime.UtcNow.ToString("o")};float deadline=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<deadline){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){receipt.error=loop==null?"Actual playable root absent":loop.Failure??"Actual root ready timeout";Finish();yield break;}
            var saved=new Saved(loop);receipt.nativeExecuted=true;
            try
            {
                var guard=Add("ActualRootUiSetup");var run=Drive(guard,Setup(guard));while(run.MoveNext())yield return run.Current;
                if(guard.passed)
                {
                    var lobby=Add("ActualLobbyLoadoutRouteAnd180pxButtons");run=Drive(lobby,Lobby(lobby));while(run.MoveNext())yield return run.Current;
                    var settings=Add("ActualWeaponSettingsBinding");run=Drive(settings,Settings(settings));while(run.MoveNext())yield return run.Current;
                    var wheel=Add("ActualWheelBackpackAndWalkingAnchor");run=Drive(wheel,Backpack(wheel));while(run.MoveNext())yield return run.Current;
                    var equip=Add("ActualOwnedActionBackpackEquip");run=Drive(equip,Equip(equip));while(run.MoveNext())yield return run.Current;
                    var paddles=Add("ActualReviveFooterCallback");run=Drive(paddles,Paddles(paddles));while(run.MoveNext())yield return run.Current;
                    var capacity=Add("ActualBackpackSupplyCapacity3To6");run=Drive(capacity,Capacity(capacity));while(run.MoveNext())yield return run.Current;
                }
            }
            finally
            {
                var cleanup=Add("ActualSceneAndPreferenceCleanup");
                try{if(touched)saved.Restore(loop);if(probe!=null)UnityEngine.Object.Destroy(probe.gameObject);cleanup.passed=true;Note(cleanup,"Original tracked local poses/settings/flags restored; actual round restarted (capacity3), fixture supply props hidden; map geometry unchanged.");}
                catch(Exception e){cleanup.error=e.ToString();Debug.LogError(e);}
                Finish();
            }
        }
        void Finish()
        {
            receipt.success=receipt.groups.Count==8&&receipt.error.Length==0;foreach(var g in receipt.groups)receipt.success&=g.passed&&g.error.Length==0;
            receipt.finishedUtc=DateTime.UtcNow.ToString("o");Passed=receipt.success;Json=JsonUtility.ToJson(receipt,true);Debug.Log("R6 UI TERMINAL "+Json);if(AutoFinish)R23SmokeControl.Finish(Passed,Json);
        }
        IEnumerator Drive(Group group,IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            try
            {
                while(stack.Count>0)
                {
                    bool moved=false;object value=null;
                    try{moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}
                    catch(Exception e){group.error=e.ToString();Debug.LogError("R6 UI FAIL "+group.name+": "+e);break;}
                    if(!moved){var completed=stack.Pop() as IDisposable;if(completed!=null)completed.Dispose();continue;}
                    var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;
                }
                if(group.error.Length==0){group.passed=true;Debug.Log("R6 UI PASS "+group.name);}
            }
            finally{while(stack.Count>0){var d=stack.Pop() as IDisposable;if(d!=null)d.Dispose();}}
        }
        IEnumerator Setup(Group g)
        {
            Need(!UnityEngine.XR.XRSettings.isDeviceActive,"XR-off fixture only; refusing to overwrite a live headset");touched=true;
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
            Need(loop.Map.IsLoaded&&loop.Lobby!=null&&loop.UI!=null&&loop.WeaponWheel.Ready,"Actual map/lobby/UI/wheel unavailable");
            // This is a tracked-pose simulation, not a calibration receipt write.
            loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Head.transform.localRotation=Quaternion.identity;
            loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.20f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.20f);
            loop.Rig.Left.localRotation=loop.Rig.Right.localRotation=Quaternion.identity;
            panel=loop.UI.transform.Find("R23 eye-level menu");Need(panel!=null,"Actual UI panel absent");
            probe=new GameObject("R6 UI detached native capture camera").AddComponent<Camera>();probe.enabled=false;probe.CopyFrom(loop.Rig.Head);probe.enabled=false;probe.stereoTargetEye=StereoTargetEyeMask.None;
            Note(g,"Actual loaded map "+loop.Map.MapId+"; source UI root + existing real owners; XR off, no actor stand-in");yield return null;
        }
        IEnumerator Lobby(Group g)
        {
            loop.ReturnToLobby();yield return null;string map=loop.Map.MapId;Vector3 anchor=loop.Lobby.MenuAnchor.position;
            var before=Inventory();int starts=0,entries=0;Action start=delegate{starts++;};Action<string,bool> enter=delegate(string id,bool mode){entries++;};loop.UI.StartRequested+=start;loop.UI.MapSelected+=enter;
            try
            {
                Transform weapons=Button("WEAPONS"),outfits=Button("OUTFITS");SourceButton(weapons,450,712,180,64);SourceButton(outfits,650,712,180,64);Capture(g,"r6-ui-menu",true);
                Click(weapons);Need(loop.UI.Page==R23UiPage.Weapons,"Lobby WEAPONS failed to open independent inspection");
                string first=loop.UI.CurrentViewedWeaponId;Need(!String.IsNullOrEmpty(first),"Actual source catalog first item not published");
                float end=Time.realtimeSinceStartup+30;while(!loop.Gallery.IsWeaponReady(first)&&loop.Gallery.WeaponFailure(first)==null&&Time.realtimeSinceStartup<end)yield return null;
                Need(loop.Gallery.Active&&loop.Gallery.ViewedWeaponId==first&&loop.Gallery.VisiblePreviewId==first&&loop.Gallery.VisiblePreviewCount==1,"Actual viewed item lacks a single genuine gallery model: "+loop.Gallery.WeaponFailure(first));
                Need(starts==0&&entries==0&&loop.Map.MapId==map&&loop.Lobby.Visible&&Vector3.Distance(anchor,loop.Lobby.MenuAnchor.position)<.00001f,"Catalog click started/changed map or moved source menu anchor");Need(before==Inventory(),"Private inspection mutated actual carried ammo/instances");
                Click(Button(">"));string changed=loop.UI.CurrentViewedWeaponId;Need(changed!=first,"Actual NEXT catalog pointer failed");Click(Button("BACK"));Click(Button("WEAPONS"));Need(loop.UI.CurrentViewedWeaponId==first,"Opening source WEAPONS did not reset inspection to source index0");
                Note(g,"Actual pointer: 180x64px source WEAPONS/OUTFITS, private gallery, NEXT/BACK/reopen index0, no Start/map/inventory mutation");
            }
            finally{loop.UI.StartRequested-=start;loop.UI.MapSelected-=enter;}
        }
        IEnumerator Settings(Group g)
        {
            loop.ReturnToLobby();Click(Button("SETTINGS"));Need(loop.UI.Page==R23UiPage.Settings,"Actual SETTINGS pointer route failed");Capture(g,"r6-ui-settings",false);Click(Button("WEAPONS"));
            Need(loop.UI.Page==R23UiPage.Settings&&loop.UI.CurrentViewedWeaponId==null&&!loop.Gallery.Active,"Settings WEAPONS incorrectly entered catalog");
            Need(LabelPrefix("MOTION DAMPING")!=null&&LabelPrefix("OFFHAND INFLUENCE")!=null&&LabelPrefix("GRIP TOGGLE")!=null&&LabelPrefix("DOMINANT HAND")!=null,"Actual four source tuning controls absent");
            Slide("MOTION DAMPING",.375f);Slide("OFFHAND INFLUENCE",.4f);
            Need(Near(loop.UI.Settings.MotionDamping,.75f,.0001f)&&Near(loop.Gameplay.MotionDamping,.75f,.0001f),"Actual UI damping slider failed real weapon owner binding");
            Need(Near(loop.UI.Settings.OffhandInfluence,.4f,.0001f)&&Near(loop.Gameplay.OffhandInfluence,.4f,.0001f),"Actual UI offhand slider failed real weapon owner binding");
            bool grip=loop.UI.Settings.GripToggle;Click(LabelPrefix("GRIP TOGGLE").transform.parent);Need(loop.UI.Settings.GripToggle!=grip,"Actual grip-toggle pointer failed");Click(LabelPrefix("GRIP TOGGLE").transform.parent);
            Capture(g,"r6-ui-weapon-settings",false);Click(Button("BACK TO SETTINGS"));Need(Button("WEAPONS")!=null,"Actual BACK TO SETTINGS failed");
            Note(g,"Settings>WEAPONS is the real four-control settings page; pointer sliders applied .75 damping/.40 offhand to production Hands owner, without catalog grants");yield return null;
        }
        IEnumerator Backpack(Group g)
        {
            loop.StartPlaying();loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);Neutral();
            for(int i=0;i<20;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);yield return null;
            string before=Inventory();OpenThroughWheel();Need(loop.UI.Page==R23UiPage.Loadout&&!loop.WeaponWheel.Active,"Actual existing wheel BACKPACK callback failed");Need(Inventory()==before,"Opening backpack changed actual owned ammo/instances");
            Need(Label("BACKPACK")!=null&&Label(Hint)!=null,"Actual pinned backpack title/control hints absent");Need(CountLabel("LOCKED")==3,"Source initial three-slot backpack should show three locked inventory cells");
            int pictures=0;foreach(var renderer in loop.UI.GetComponentsInChildren<MeshRenderer>())if(renderer.name.StartsWith("Original backpack picture ",StringComparison.Ordinal)){pictures++;Need(renderer.sharedMaterial.GetTexture("_IconTex")!=null,"Actual backpack picture missing texture");}Need(pictures>=9,"Actual owned weapon and reserve pictures absent");
            Vector3 board=panel.position,head=loop.Rig.Head.transform.localPosition,walk=loop.Rig.LocomotionAnchorPosition;Quaternion rotation=panel.rotation;
            loop.Rig.Head.transform.localPosition=head+new Vector3(.12f,0,.08f);yield return null;yield return null;
            Need(Vector3.Distance(panel.position,board)<.0001f&&Quaternion.Angle(panel.rotation,rotation)<.001f,"Backpack followed physical head motion instead of frozen opened board");
            // Run the actual source motor/collision path, not a test anchor provider.
            for(int i=0;i<14;i++)loop.Rig.SmokeXrMove(new Vector2(.35f,0),.02f);yield return null;yield return null;
            Vector3 accepted=loop.Rig.LocomotionAnchorPosition-walk;Need(new Vector2(accepted.x,accepted.z).magnitude>.005f,"Native source floor/site refused walking; no replacement platform supplied");
            Need(Vector3.Distance(panel.position-board,accepted)<.0003f&&Quaternion.Angle(panel.rotation,rotation)<.001f,"Actual root walking anchor translation/yaw preservation failed");
            loop.Rig.Head.transform.localPosition=head;Capture(g,"r6-ui-backpack",false);
            Note(g,"Existing wheel/root BACKPACK route; actual action/profile pictures and six pinned inventory cells; raw-head change leaves board fixed, real native walking translates board by "+accepted);
        }
        IEnumerator Equip(Group g)
        {
            if(loop.UI.Page!=R23UiPage.Loadout)OpenThroughWheel();var before=loop.Gameplay.OwnedWeaponActionIds;Need(before.Length>0,"Actual owned kit absent");string chosen=before[0],profile=loop.Gameplay.ResolveWeaponProfile(chosen);
            Transform icon=Find("Original backpack picture "+profile);Need(icon!=null,"Actual owned action picture missing: "+profile);string request=null;int hand=-1;Action<string,int> seen=delegate(string id,int h){request=id;hand=h;};loop.UI.EquipRequested+=seen;
            try{Grip(icon.parent,1);Need(request==chosen&&hand==1,"Backpack emitted caption/profile instead of exact retained action ID");Need(loop.UI.Page==R23UiPage.Playing,"Actual equip callback did not close backpack");Neutral();loop.SmokeUiUpdate();Need(loop.Gameplay.HasWeapon&&loop.Gameplay.ActiveWeaponActionId==chosen&&loop.Gameplay.PrimaryHand==1,"Actual queued root equip was not accepted by the retained weapon owner");Need(before.Length==loop.Gameplay.OwnedWeaponActionIds.Length,"Backpack equip granted inventory");Note(g,"Actual fresh-grip UI intent -> exact action ID -> real Root.Update queued equip -> retained Hands owner; no direct fixture EquipOwned call");}
            finally{loop.UI.EquipRequested-=seen;}yield return null;
        }
        IEnumerator Paddles(Group g)
        {
            float end=Time.realtimeSinceStartup+30;while(loop.Paddles!=null&&!loop.Paddles.Ready&&loop.Paddles.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Paddles!=null&&loop.Paddles.Ready,"Actual reusable gear decode failed: "+(loop.Paddles==null?"absent":loop.Paddles.Failure));
            Neutral();loop.StowPaddles();if(loop.UI.Page!=R23UiPage.Playing)loop.UI.SetPlaying();OpenThroughWheel();
            var icon=Find("Source icon backpack_r23_revive");Need(icon!=null,"Actual source REVIVE glyph absent");var texture=icon.GetComponent<Renderer>().sharedMaterial.GetTexture("_IconTex");Need(texture!=null&&texture.width==256&&texture.height==171,"Actual preserved source revive picture not bound");
            string before=Inventory();Click(Button("REVIVE PADDLES"));Need(loop.Paddles.State.Active&&loop.UI.Page==R23UiPage.Playing,"Actual source footer did not invoke root reusable gear equip");Need(before==Inventory(),"Reusable zero-slot gear mutated owned weapons/ammo");
            HandSample left=Hand(loop.Rig.Left),right=Hand(loop.Rig.Right);loop.StepItemInput(.02f,left,right,true,false);loop.UI.OpenLoadout();Need(LabelPrefix("PADDLES CHARGE")!=null&&Label(Hint)!=null,"Real gear status replaced source equip/drop hints");Capture(g,"r6-ui-backpack-revive",false);
            Note(g,"Actual source RGBA revive picture, click -> Root.TogglePaddles -> production reusable owner; real charge status and control hints coexist; no revive/network completion claimed");loop.UI.SetPlaying();loop.StowPaddles();yield return null;
        }
        IEnumerator Capacity(Group g)
        {
            loop.RestartRound();loop.Rig.GameplayBlocked=false;Neutral();Need(loop.InventoryCapacity==3,"Actual round reset did not restore source capacity3");float end=Time.realtimeSinceStartup+30;while(!loop.Supplies.Ready&&loop.Supplies.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Supplies.Ready,"Actual backpack supply decode unavailable: "+loop.Supplies.Failure);
            var point=loop.Rig.Left.position;string prefix="r6-ui-capacity/";
            for(int i=0;i<4;i++)
            {
                string id=prefix+i;Need(loop.Supplies.Spawn(id,"backpack",point),"Actual unique backpack supply registration failed");Neutral();int got=loop.Supplies.TryCollect(id,1);Need(got==(i<3?1:0),"Actual root source upgrade acceptance/cap mismatch at "+i);Need(loop.InventoryCapacity==Mathf.Min(6,4+i),"Actual shared root capacity mismatch");loop.Supplies.SetWorldItemVisible(id,false);
                if(i<3){OpenThroughWheel();Need(CountLabel("LOCKED")==2-i,"Actual pinned UI locked-cell count did not follow real owner capacity");Click(Button("X"));}
            }
            OpenThroughWheel();Need(CountLabel("LOCKED")==0&&CountLabel("EMPTY")==3,"Actual capacity6 failed to unlock three additional source inventory cells");Capture(g,"r6-ui-backpack-six-slots",false);Click(Button("X"));
            Note(g,"Three real decoded world backpack supplies -> Supplies.TryCollect -> production Root.GrantWorldSupply -> shared capacity3,4,5,6 and actual UI unlocks; fourth refused, no fake UI options");yield return null;
        }
        void Neutral(){loop.Rig.SmokeXrMove(Vector2.zero,.00001f);HandSample left=Hand(loop.Rig.Left),right=Hand(loop.Rig.Right);loop.Rig.SmokeTrackedHands(left,right);loop.StepItemInput(.02f,left,right,true,false);}
        HandSample Hand(Transform t){var at=loop.Rig.transform.InverseTransformPoint(t.position);return new HandSample{Tracked=true,Position=new Float3(at.x,at.y,at.z)};}
        void OpenThroughWheel()
        {
            var ray=new Ray(loop.Rig.Head.transform.position,loop.Rig.Head.transform.forward);var anchor=loop.Rig.LocomotionAnchorPosition;
            loop.WeaponWheel.Cancel();loop.WeaponWheel.Step(.02f,false,true,ray,anchor);loop.WeaponWheel.Step(.11f,true,true,ray,anchor);Need(loop.WeaponWheel.Active,"Actual wheel failed source hold threshold");
            ray=new Ray(ray.origin,(loop.WeaponWheel.FixtureSectorPoint(4)-ray.origin).normalized);loop.WeaponWheel.Step(.02f,true,true,ray,anchor);Need(loop.WeaponWheel.HighlightedSector==4,"Actual ray did not select source backpack sector");Need(loop.WeaponWheel.Step(.02f,false,true,ray,anchor),"Actual wheel release/root backpack callback refused");Need(loop.UI.Page==R23UiPage.Loadout,"Actual backpack page did not open");
        }
        string Inventory(){var rows=new List<string>();foreach(var id in loop.Gameplay.OwnedWeaponActionIds){var s=loop.Gameplay.ReadWeaponState(id);rows.Add(id+":"+s.Loaded+":"+loop.Gameplay.ReadReserve(id));}rows.Sort(StringComparer.Ordinal);return String.Join("|",rows.ToArray());}
        TextMesh Label(string exact){foreach(var t in loop.UI.GetComponentsInChildren<TextMesh>())if(t.text==exact)return t;return null;}
        TextMesh LabelPrefix(string prefix){foreach(var t in loop.UI.GetComponentsInChildren<TextMesh>())if(t.text.StartsWith(prefix,StringComparison.Ordinal))return t;return null;}
        int CountLabel(string exact){int count=0;foreach(var t in loop.UI.GetComponentsInChildren<TextMesh>())if(t.text==exact)count++;return count;}
        Transform Find(string name){foreach(var t in loop.UI.GetComponentsInChildren<Transform>())if(t.name==name)return t;return null;}
        Transform Button(string caption){foreach(var t in loop.UI.GetComponentsInChildren<TextMesh>())if(t.text==caption&&t.transform.parent.Find("Button background")!=null)return t.transform.parent;throw new InvalidOperationException("Active actual UI button missing: "+caption);}
        void SourceButton(Transform button,float x,float y,float w,float h)
        {var size=button.Find("Button background").GetComponent<MeshFilter>().sharedMesh.bounds.size;Need(Near(size.x,w*2.2f/1280)&&Near(size.y,h*1.375f/800),"Source button dimensions differ: "+button.name);Need(Vector2.Distance(button.localPosition,new Vector2(((x+w*.5f)/1280-.5f)*2.2f,(.5f-(y+h*.5f)/800)*1.375f))<.00002f,"Source button pixel coordinates differ: "+button.name);}
        void Click(Transform surface){Point(surface,0,false,false,false);Point(surface,0,true,false,false);Point(surface,0,false,false,false);}
        void Grip(Transform surface,int hand){Point(surface,0,false,false,true,hand);Point(surface,0,false,true,true,hand);Point(surface,0,false,false,true,hand);}
        void Point(Transform surface,float x,bool trigger,bool grip,bool loadout,int hand=0)
        {Vector3 at=surface.TransformPoint(new Vector3(x,0,0)),origin=at-surface.forward*.6f;if(loadout)loop.UI.UpdateLoadoutPointer(hand,origin,surface.forward,trigger,grip,true);else loop.UI.UpdatePointer(hand,origin,surface.forward,trigger,true);}
        void Slide(string prefix,float fraction)
        {var label=LabelPrefix(prefix);Need(label!=null,"Actual source numeric caption absent: "+prefix);Transform surface=null;float y=label.transform.localPosition.y-.047f;foreach(var t in panel.GetComponentsInChildren<Transform>())if(t.Find("Slider track")!=null&&Mathf.Abs(t.localPosition.y-y)<.00001f)surface=t;Need(surface!=null,"Actual numeric slider surface absent: "+prefix);float x=(fraction-.5f)*.9f;Point(surface,x,false,false,false);Point(surface,x,true,false,false);Point(surface,x,false,false,false);}
        void Capture(Group g,string suffix,bool lobby)
        {
            loop.UI.RefreshTextLayout();Transform canvas=panel;Bounds bounds;
            if(lobby)bounds=new Bounds(Vector3.zero,new Vector3(loop.Lobby.MenuSize.x,loop.Lobby.MenuSize.y,0));
            else
            {
                canvas=Find(loop.UI.Page==R23UiPage.Loadout?"Backpack backdrop":"SettingsBoard");
                Need(canvas!=null,"Actual capture canvas missing for "+loop.UI.Page);var mesh=canvas.GetComponent<MeshFilter>();
                Need(mesh!=null&&mesh.sharedMesh!=null,"Actual capture canvas mesh missing");bounds=mesh.sharedMesh.bounds;
            }
            // Measure the actual world corners. Never move the production panel,
            // tracking origin, head, gallery dock or source menu anchor for a picture.
            var corners=new Vector3[8];Vector3 center=canvas.TransformPoint(bounds.center);float halfWidth=0,halfHeight=0,depth=0;
            for(int i=0;i<8;i++)
            {
                var local=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var world=canvas.TransformPoint(local);corners[i]=world;var delta=world-center;
                halfWidth=Mathf.Max(halfWidth,Mathf.Abs(Vector3.Dot(delta,panel.right)));halfHeight=Mathf.Max(halfHeight,Mathf.Abs(Vector3.Dot(delta,panel.up)));depth=Mathf.Max(depth,Mathf.Abs(Vector3.Dot(delta,panel.forward)));
            }
            const float margin=.06f;probe.orthographic=false;probe.usePhysicalProperties=false;probe.lensShift=Vector2.zero;
            // Frozen R6 player.gd:177-182 desktop camera (XR projection is runtime-owned).
            probe.rect=new Rect(0,0,1,1);probe.fieldOfView=85;probe.aspect=1280f/720;probe.nearClipPlane=.045f;probe.farClipPlane=650;
            probe.ResetProjectionMatrix();probe.ResetWorldToCameraMatrix();probe.ResetCullingMatrix();
            float distance=R24UiCaptureFit.Distance(halfWidth,halfHeight,depth,probe.fieldOfView,probe.aspect,margin);
            probe.transform.SetPositionAndRotation(center-panel.forward*distance,panel.rotation);
            foreach(var corner in corners)
            {
                var uv=probe.WorldToViewportPoint(corner);
                Need(uv.z>probe.nearClipPlane&&uv.x>=margin-.001f&&uv.x<=1-margin+.001f&&uv.y>=margin-.001f&&uv.y<=1-margin+.001f,"Probe framing clipped an actual source canvas corner: "+uv);
            }
            Note(g,"Simulated desktop full-canvas capture (actual world corners, 6% margin, source desktop 85deg detached probe, distance "+distance.ToString("F3")+"m; production layout untouched, not matched HMD/source-camera projection): "+R23SmokeControl.Capture(probe,suffix));
        }
        sealed class Saved
        {
            readonly bool rig,manual,pump,external,calibration,lobby,blocked,prefExists;readonly string pref;readonly R23UiSettings settings;readonly Vector3 root,origin,head,left,right;readonly Quaternion rootQ,originQ,headQ,leftQ,rightQ;
            internal Saved(R23PlayableLoop p){rig=p.Rig.enabled;manual=p.ManualSmoke;pump=p.ExternalProbePump;external=p.Gameplay.ExternalInputPump;calibration=p.Calibration.SuppressPersistence;lobby=p.Lobby.Visible;blocked=p.Rig.GameplayBlocked;settings=p.UI.Settings;prefExists=PlayerPrefs.HasKey(PrefKey);pref=PlayerPrefs.GetString(PrefKey,"");root=p.Rig.transform.position;rootQ=p.Rig.transform.rotation;origin=p.Rig.TrackingOrigin.localPosition;originQ=p.Rig.TrackingOrigin.localRotation;head=p.Rig.Head.transform.localPosition;headQ=p.Rig.Head.transform.localRotation;left=p.Rig.Left.localPosition;leftQ=p.Rig.Left.localRotation;right=p.Rig.Right.localPosition;rightQ=p.Rig.Right.localRotation;}
            internal void Restore(R23PlayableLoop p)
            {p.StowPaddles();p.StartPlaying();p.RestartRound();for(int i=0;i<4;i++)p.Supplies.SetWorldItemVisible("r6-ui-capacity/"+i,false);p.UI.SetSettings(settings);p.Gameplay.SetWeaponTuning(settings.MotionDamping,settings.OffhandInfluence);p.Gameplay.SetGripToggle(settings.GripToggle);p.Rig.ApplySettings(settings);p.Climbing.FlingPower=settings.FlingPower;if(prefExists)PlayerPrefs.SetString(PrefKey,pref);else PlayerPrefs.DeleteKey(PrefKey);PlayerPrefs.Save();if(lobby)p.ReturnToLobby();p.Rig.Body.enabled=false;p.Rig.transform.SetPositionAndRotation(root,rootQ);p.Rig.TrackingOrigin.SetLocalPositionAndRotation(origin,originQ);p.Rig.Head.transform.SetLocalPositionAndRotation(head,headQ);p.Rig.Left.SetLocalPositionAndRotation(left,leftQ);p.Rig.Right.SetLocalPositionAndRotation(right,rightQ);p.Rig.Body.enabled=true;p.Rig.GameplayBlocked=blocked;p.Calibration.SuppressPersistence=calibration;p.Gameplay.ExternalInputPump=external;p.ExternalProbePump=pump;p.ManualSmoke=manual;p.Rig.enabled=rig;p.UI.Recenter();}
        }
    }
}
#endif

// Pure perspective fit shared by the actual native probe and CPU corner cases.
namespace Ashfall.Editor.Playable
{
    public static class R24UiCaptureFit
    {
        public static float Distance(float halfWidth,float halfHeight,float depth,float verticalFov,float aspect,float margin)
        {
            if(!(halfWidth>0)||!(halfHeight>0)||!(depth>=0)||!(verticalFov>0&&verticalFov<179)||!(aspect>0)||!(margin>=0&&margin<.5f))throw new System.ArgumentOutOfRangeException("Finite positive canvas/projection required");
            foreach(float value in new[]{halfWidth,halfHeight,depth,verticalFov,aspect,margin})if(float.IsInfinity(value)||float.IsNaN(value))throw new System.ArgumentOutOfRangeException("Finite canvas/projection required");
            double tangent=System.Math.Tan(verticalFov*System.Math.PI/360),usable=1-2*margin;
            // Closest corner (depth) must still fit both viewport dimensions.
            return (float)System.Math.Max(.15,depth+System.Math.Max(halfHeight/tangent,halfWidth/(tangent*aspect))/usable);
        }
    }
}
