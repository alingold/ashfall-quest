#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.AudioTraversal;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.JumpPads;
using Ashfall.Integration.Playable.World;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Explicit native integration oracles and desktop render captures. XR delivery
    // and listening acceptance remain separate; compile-only is never a pass.
    public sealed class R23FeedbackSmoke
    {
        [Serializable]sealed class Group{public string name="",error="";public bool passed;public List<string> observations=new List<string>();}
        [Serializable]sealed class Result
        {public bool success,headsetVerified=false,hapticDeliveryVerified=false,listeningVerified=false;public string error="",startedUtc="",finishedUtc="";public List<Group> groups=new List<Group>();}
        static void Need(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
        static Group Add(Result r,string name){var g=new Group{name=name};r.groups.Add(g);Debug.Log("R23 FEEDBACK GROUP BEGIN "+name);return g;}
        static void Note(Group g,string text){g.observations.Add(text);Debug.Log("R23 FEEDBACK "+g.name+": "+text);}
        static void Fail(Group g,Exception e){g.error=e.ToString();Debug.LogError("R23 FEEDBACK GROUP FAIL "+g.name+": "+e);}
        static void Run(Group g,Action action){try{action();g.passed=true;Debug.Log("R23 FEEDBACK GROUP PASS "+g.name);}catch(Exception e){Fail(g,e);}}
        public IEnumerator Start()
        {
            var r=new Result{startedUtc=DateTime.UtcNow.ToString("o")};R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){r.error=loop==null?"Playable root absent":loop.Failure??"Initial source load timed out";Finish(r);yield break;}
            var setup=Add(r,"RootPlayingSetup");Run(setup,delegate{
                loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;
                loop.Gameplay.ExternalInputPump=true;loop.StartPlaying();Need(loop.Ready&&loop.Map.IsLoaded,"Actual map unavailable");
                loop.Rig.GameplayBlocked=false;
                loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);loop.Gameplay.SetWeaponPresentationSuppressed(false);
                Need(loop.Map.MapId==R23MapCatalog.PaintballId,"Healing oracle requires actual authored Paintball map");
            });
            var feedback=Add(r,"ActualGunFeedbackAndHapticRequests");Run(feedback,delegate{Gun(loop,feedback);});
            var impacts=Add(r,"ActualCommittedSurfaceImpactMarks");Run(impacts,delegate{ImpactMarks(loop,impacts);});
            var magnum=Add(r,"ActualMagnumPickupGesturesAndPersistence");Run(magnum,delegate{Magnum(loop,magnum);});
            var audio=Add(r,"ActualTraversalDecodeAndSourceControl");end=Time.realtimeSinceStartup+45;
            while(loop.TraversalAudio!=null&&!loop.TraversalAudio.Ready&&loop.TraversalAudio.LoadFailure==null&&Time.realtimeSinceStartup<end)yield return null;
            Run(audio,delegate{Traversal(loop,audio);});
            var ping=Add(r,"ActualWorldPingNativeQueries");Run(ping,delegate{Ping(loop,ping);});
            var marks=Add(r,"ActualSourcePrimaryTeamMarks");var iterator=Ashfall.Integration.Playable.Input.R23TeamMarksFixture.Run(loop.Rig.Head.transform,loop.Rig.transform,new Ray(loop.Rig.Head.transform.position,Vector3.down),delegate(string note){Note(marks,note);},delegate(string suffix){var rotation=loop.Rig.Head.transform.rotation;try{loop.Rig.Head.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);Note(marks,"Desktop capture: "+R23SmokeControl.Capture(loop.Rig.Head,suffix));}finally{loop.Rig.Head.transform.rotation=rotation;}return null;});
            bool moved=true;while(moved){try{moved=iterator.MoveNext();}catch(Exception e){Fail(marks,e);moved=false;}if(moved)yield return null;}
            if(marks.error.Length==0){marks.passed=true;Debug.Log("R23 FEEDBACK GROUP PASS "+marks.name);}
            var healing=Add(r,"AuthoredPaintballRootHealthCallback");end=Time.realtimeSinceStartup+30;
            while(loop.HealthStations!=null&&!loop.HealthStations.Ready&&loop.HealthStations.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            Run(healing,delegate{Heal(loop,healing);});
            var walls=Add(r,"SourceHealingNativeWallHoldouts");iterator=HealthWalls();
            moved=true;while(moved){try{moved=iterator.MoveNext();}catch(Exception e){Fail(walls,e);moved=false;}if(moved)yield return null;}
            if(walls.error.Length==0){walls.passed=true;Debug.Log("R23 FEEDBACK GROUP PASS "+walls.name);}
            var preview=Add(r,"SourceConstructionPreviewNative");iterator=R23ConstructionPreviewSmoke.Run(delegate(Ashfall.Integration.Playable.Activities.R23VRActivities activities){
                var camera=new GameObject("Temporary source preview capture camera").AddComponent<Camera>();
                try{var views=activities.PreviewVisualRoot.GetComponentsInChildren<MeshRenderer>();Need(views.Length>0,"Preview capture has no geometry");var bounds=views[0].bounds;for(int i=1;i<views.Length;i++)bounds.Encapsulate(views[i].bounds);camera.transform.position=bounds.center+new Vector3(3,2,-4);camera.transform.LookAt(bounds.center);camera.nearClipPlane=.03f;camera.farClipPlane=80;Note(preview,"Desktop preview capture: "+R23SmokeControl.Capture(camera,"source-construction-preview"));}
                finally{UnityEngine.Object.Destroy(camera.gameObject);}
            });moved=true;
            while(moved){try{moved=iterator.MoveNext();}catch(Exception e){Fail(preview,e);moved=false;}if(moved)yield return null;}
            if(preview.error.Length==0){preview.passed=true;Debug.Log("R23 FEEDBACK GROUP PASS "+preview.name);}
            var gallery=Add(r,"ActualMagnumGalleryIsolation");Run(gallery,delegate{loop.ReturnToLobby();Need(loop.Gallery.Show("magnum"),"Root gallery refused actual Magnum");});
            end=Time.realtimeSinceStartup+30;while(!loop.Gallery.ViewState.Ready&&loop.Gallery.MagnumFailure==null&&Time.realtimeSinceStartup<end)yield return null;
            if(gallery.error.Length==0)Run(gallery,delegate{Gallery(loop,gallery);});
            var panels=Add(r,"ActualAlfaCarriedGunBreakAndRestart");loop.SelectMap(R23MapCatalog.AlphaId,true);end=Time.realtimeSinceStartup+180;
            while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            Run(panels,delegate{AlfaPanels(loop,panels);});
            Finish(r);
        }
        static void Finish(Result r)
        {r.success=r.error.Length==0&&r.groups.Count==12;foreach(var g in r.groups)r.success&=g.passed&&g.error.Length==0;r.finishedUtc=DateTime.UtcNow.ToString("o");var json=JsonUtility.ToJson(r,true);Debug.Log("R23 FEEDBACK TERMINAL "+json);R23SmokeControl.Finish(r.success,json);}
        static void ImpactMarks(R23PlayableLoop loop,Group g)
        {
            var hands=loop.Gameplay;var right=loop.Rig.Right;Vector3 old=right.localPosition;Quaternion oldQ=right.localRotation;GameObject wall=null;
            try
            {
                hands.ResetRound();hands.NotifyFocus(true);hands.SetGameplayPaused(false);hands.SetWeaponPresentationSuppressed(false);Need(hands.SelectWeapon("mk18"),"Actual source rifle missing");
                hands.TickInput(.02f,Frame());right.position=hands.WeaponGripPosition;hands.TickInput(.02f,Frame(.8f));Need(hands.HasWeapon,"Physical rifle grip failed before impact test");
                Vector3 forward=loop.Rig.Head.transform.forward,point=loop.Rig.Head.transform.position+forward*2;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Native committed world-surface impact oracle";wall.transform.SetPositionAndRotation(point,Quaternion.LookRotation(forward));wall.transform.localScale=new Vector3(2,2,.1f);
                right.position=loop.Rig.Head.transform.position+forward*.3f;var bore=Bore(loop);for(int i=0;i<4;i++)right.rotation=Quaternion.FromToRotation(bore.forward,(point-bore.position).normalized)*right.rotation;
                hands.TickInput(.2f,Frame(.8f));hands.ClearImpactMarks();Physics.SyncTransforms();long accepted=hands.ImpactMarksAccepted;
                Need(hands.Fire(.12f)&&hands.ImpactMarksAccepted==accepted+1&&hands.ImpactMarkCount==1&&hands.ImpactLastProjectionRays==9,"Accepted actual world shot did not create one source conformed mark");
                int slot=hands.ImpactLastSlot;for(int i=0;i<9;i++){Vector3 vertex;Need(hands.TryReadImpactMarkVertex(slot,i,out vertex),"Source patch vertex missing");Need(Mathf.Abs(Vector3.Dot(vertex-(point-forward*.05f),-forward)-.003f)<.001f,"Conformed source mark floated away from actual wall surface");}
                Note(g,"Desktop persistent mark capture: "+R23SmokeControl.Capture(loop.Rig.Head,"source-impact-mark"));
                Need(hands.HolsterCurrent()&&hands.ImpactMarkCount==1,"Holstering erased persistent world mark");hands.SetWeaponPresentationSuppressed(true);Need(hands.ImpactMarkCount==1,"Weapon presentation suppression erased mark");
                hands.SetGameplayPaused(true);float age=hands.ReadImpactMarkAge(slot);hands.AdvanceImpactMarksForFixture(85);Need(hands.ReadImpactMarkAge(slot)==age,"Paused source mark aged");
                hands.SetGameplayPaused(false);hands.AdvanceImpactMarksForFixture(85);Need(hands.ImpactMarkCount==1&&hands.ReadImpactMarkAge(slot)>=85&&hands.ReadImpactMarkAge(slot)<85.1f,"Source90s mark lifetime was shortened");hands.AdvanceImpactMarksForFixture(5);Need(hands.ImpactMarkCount==0,"Source mark did not expire after90s");
                Note(g,"Actual committed hit, nine native verification rays, nine surface vertices at3mm, persistent holster/suppression, pause and90s expiry; no headset appearance claim");
            }
            finally{hands.ResetRound();hands.SetWeaponPresentationSuppressed(false);hands.SetGameplayPaused(false);right.localPosition=old;right.localRotation=oldQ;if(wall!=null){wall.SetActive(false);UnityEngine.Object.Destroy(wall);}Physics.SyncTransforms();}
        }
        static void AlfaPanels(R23PlayableLoop loop,Group g)
        {
            Need(loop.Ready&&loop.Map.MapId==R23MapCatalog.AlphaId&&loop.Map.DestructibleCount==930,loop.Failure??"Actual ALFA source panels unavailable");
            loop.StartPlaying();loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);loop.Gameplay.SetWeaponPresentationSuppressed(false);
            var shader=Resources.Load<Shader>("R23ArchiveGlass");Need(shader!=null&&shader.isSupported&&!UnityEditor.ShaderUtil.ShaderHasError(shader),"Actual source glass shader failed native compilation");
            RaycastHit hit=new RaycastHit();Vector3 point=Vector3.zero,normal=Vector3.zero;bool found=false;string id="";
            foreach(var row in loop.Map.DestructibleDefinitions)
            {
                if(!row.glass)continue;point=loop.Map.transform.TransformPoint(new Vector3(row.probe[0],row.probe[1],row.probe[2]));normal=loop.Map.transform.TransformDirection(new Vector3(row.normal[0],row.normal[1],row.normal[2])).normalized;
                foreach(float side in new[]{1f,-1f})
                {RaycastHit candidate;if(!Physics.Raycast(point-normal*side*.12f,normal*side,out candidate,.24f,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore))continue;bool broken;string actual;if(loop.Map.TryReadDestructible(candidate.collider,out actual,out broken)&&actual==row.id&&!broken){hit=candidate;normal*=side;id=actual;found=true;break;}}
                if(found)break;
            }
            Need(found,"No intact native source glass face at any actual panel probe");
            loop.Rig.SetSpawn(point-normal*1.2f-Vector3.up*1.7088f,point);loop.Gameplay.SetPickupHome(loop.Rig.Head.transform.position+normal*.4f-Vector3.up*.2f,Quaternion.LookRotation(normal));
            Need(loop.Gameplay.SelectWeapon("mk18"),"Source MK18 unavailable for native glass hit");loop.Gameplay.TickInput(.02f,Frame());loop.Rig.Right.position=loop.Gameplay.WeaponGripPosition;loop.Gameplay.TickInput(.02f,Frame(.8f));Need(loop.Gameplay.HasWeapon,"Actual carried grip failed before glass test");
            loop.Rig.Right.position=point-normal*.55f;var bore=Bore(loop);for(int i=0;i<4;i++)loop.Rig.Right.rotation=Quaternion.FromToRotation(bore.forward,(point-bore.position).normalized)*loop.Rig.Right.rotation;
            loop.Gameplay.TickInput(.2f,Frame(.8f));Physics.SyncTransforms();Note(g,"Intact source glass capture: "+R23SmokeControl.Capture(loop.Rig.Head,"alfa-glass-intact"));
            int ammo=loop.Gameplay.Ammo;Need(loop.Gameplay.Fire(.12f)&&loop.Gameplay.Ammo==ammo-1,"Actual carried shot was not accepted");
            string owner;bool isBroken;Need(loop.Map.TryReadDestructible(hit.collider,out owner,out isBroken)&&owner==id&&isBroken&&loop.Map.BrokenDestructibleCount==1,"Real WorldHit did not retire the exact source assembly");
            Need(!hit.collider.enabled&&!Physics.Raycast(point-normal*.12f,normal,.24f,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore),"Retired source glass still blocks the actual native aperture");
            Note(g,"Broken source glass capture: "+R23SmokeControl.Capture(loop.Rig.Head,"alfa-glass-broken"));
            loop.RestartRound();Need(loop.Map.BrokenDestructibleCount==0&&hit.collider.enabled&&Physics.Raycast(point-normal*.12f,normal,.24f,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore),"Actual round restart failed to restore source pane collision");
            Note(g,"Actual MK18→WorldHit→source panel "+id+" retires render/collision on one hit and restart restores; 930 source assemblies loaded, no headset/glass appearance acceptance");
        }
        static R23HandsInputFrame Frame(float grip=0)
        {return new R23HandsInputFrame{LeftTracked=true,RightTracked=true,RightGrip=grip};}
        static R23HandsInputFrame MagnumFrame(float grip,float support=0,float trigger=0,bool click=false)
        {return new R23HandsInputFrame{LeftTracked=true,RightTracked=true,RightGrip=grip,LeftGrip=support,RightTrigger=trigger,RightStickClick=click};}
        static void Magnum(R23PlayableLoop loop,Group g)
        {
            var hands=loop.Gameplay;var left=loop.Rig.Left;var right=loop.Rig.Right;
            Vector3 oldLeft=left.localPosition,oldRight=right.localPosition;Quaternion oldLeftQ=left.localRotation,oldRightQ=right.localRotation;
            try
            {
                hands.ResetRound();hands.NotifyFocus(true);hands.SetGameplayPaused(false);hands.SetWeaponPresentationSuppressed(false);
                Need(hands.MagnumVisualReady&&hands.OwnedWeaponIds.Length==3&&!hands.OwnsWeapon("magnum"),"Real Magnum model/starter capacity state missing");
                Need(hands.DropOwned("akm",loop.Rig.Head.transform.position+loop.Rig.Head.transform.forward,Quaternion.identity),"Could not free one existing real inventory slot");
                Vector3 pickup;Quaternion rotation;Need(hands.TryGetDroppedPose("magnum",out pickup,out rotation),"Actual unowned Magnum world pickup missing");
                right.position=pickup;hands.TickInput(.02f,MagnumFrame(0));hands.TickInput(.02f,MagnumFrame(.8f));
                Need(hands.HasWeapon&&hands.ActiveWeaponId=="magnum"&&hands.OwnedWeaponIds.Length==3,"Physical Magnum pickup failed or granted fourth slot");
                string instance=hands.WeaponInstanceId;int reserve=hands.ReadReserve("magnum"),ammo=hands.Ammo,mask=hands.MagnumChamberMask;
                Need(ammo==7&&reserve==1200,"Native Magnum must start with seven chambers and finite source reserve");
                hands.TickInput(.2f,MagnumFrame(.8f,trigger:.8f));Need(hands.Ammo==ammo-1&&hands.MagnumChamberMask!=mask,"Accepted physical Magnum trigger did not consume one chamber");
                int after=hands.Ammo;hands.TickInput(.2f,MagnumFrame(.8f,trigger:.8f));hands.TickInput(.2f,MagnumFrame(.8f,trigger:.8f));Need(hands.Ammo==after,"Held Magnum trigger repeated after cooldown");
                hands.TickInput(.02f,MagnumFrame(.8f));hands.TickInput(.2f,MagnumFrame(.8f,trigger:.8f));Need(hands.Ammo==after-1&&hands.ReadReserve("magnum")==reserve,"Fresh Magnum trigger or reserve accounting failed");
                hands.TickInput(.02f,MagnumFrame(.8f));hands.TickInput(.02f,MagnumFrame(.8f,click:true));
                Need(hands.ReloadStage==Ashfall.Modules.WeaponHandling.ReloadPhase.CylinderOpen,"Right-stick click did not open actual cylinder");
                hands.TickInput(.02f,MagnumFrame(.8f));left.position=hands.ReloadOfferPosition;hands.TickInput(.02f,MagnumFrame(.8f,.8f));
                var weapon=Bore(loop).parent;left.position=hands.ReloadInsertionPosition+weapon.forward*.01f;hands.TickInput(.02f,MagnumFrame(.8f,.8f));
                Need(hands.ReloadStage==Ashfall.Modules.WeaponHandling.ReloadPhase.CylinderLoaded,"Physical forward cylinder load failed");
                int used=7-after+1;hands.TickInput(.02f,MagnumFrame(.8f));left.position=hands.ChargingGripPosition;hands.TickInput(.02f,MagnumFrame(.8f,.8f));left.position+=weapon.up*.06f;hands.TickInput(.02f,MagnumFrame(.8f,.8f));
                Need(hands.ReloadStage==Ashfall.Modules.WeaponHandling.ReloadPhase.Ready&&hands.Ammo==7&&hands.ReadReserve("magnum")==reserve-used,"Physical fresh-grip close or exact reserve debit failed");
                hands.TickInput(.02f,MagnumFrame(.8f));mask=hands.MagnumChamberMask;reserve=hands.ReadReserve("magnum");
                Need(hands.HolsterCurrent()&&hands.EquipOwned("magnum",1)&&hands.MagnumChamberMask==mask,"Magnum holster/equip changed cylinder state");
                Need(hands.DropOwned("magnum",pickup,rotation)&&hands.TryGetDroppedPose("magnum",out pickup,out rotation),"Real Magnum drop failed");
                right.position=pickup;hands.TickInput(.02f,MagnumFrame(0));hands.TickInput(.02f,MagnumFrame(.8f));
                Need(hands.ActiveWeaponId=="magnum"&&hands.HasWeapon&&hands.WeaponInstanceId==instance&&hands.MagnumChamberMask==mask&&hands.ReadReserve("magnum")==reserve&&hands.OwnedWeaponIds.Length==3,"Recollect changed native identity/ammo/reserve/capacity");
                Note(g,"Actual world pickup, seven chambers, semi-auto release/repress, stick opening, support-hand load/close, finite debit and persistent holster/drop/recollect; simulated tracked poses, not headset gesture acceptance");
            }
            finally{hands.ResetRound();left.localPosition=oldLeft;left.localRotation=oldLeftQ;right.localPosition=oldRight;right.localRotation=oldRightQ;}
        }
        static Transform Bore(R23PlayableLoop loop)
        {foreach(var t in loop.Rig.Right.GetComponentsInChildren<Transform>(true))if(t.name=="Actual source muzzle")return t;throw new InvalidOperationException("Held source bore missing");}
        static void Gun(R23PlayableLoop loop,Group g)
        {
            var hands=loop.Gameplay;var right=loop.Rig.Right;var left=loop.Rig.Left;var head=loop.Rig.Head.transform;
            Vector3 oldRight=right.localPosition,oldLeft=left.localPosition,oldHead=head.localPosition;Quaternion oldRightQ=right.localRotation,oldLeftQ=left.localRotation,oldHeadQ=head.localRotation;
            int haptics=0;Action<int,float,float> pulse=delegate(int hand,float amplitude,float seconds){Need(hand>=0&&hand<=1&&amplitude>0&&seconds>0,"Malformed haptic request");haptics++;};hands.HapticRequested+=pulse;
            try
            {
                hands.ResetRound();hands.NotifyFocus(true);hands.SetGameplayPaused(false);Need(hands.SelectWeapon("mk18"),"Actual MK18 persistent instance unavailable");
                hands.TickInput(.02f,Frame());right.position=hands.WeaponGripPosition;Physics.SyncTransforms();hands.TickInput(.02f,Frame(.8f));Need(hands.HasWeapon&&hands.ActiveWeaponId=="mk18","Actual analog grip pickup failed");
                var bore=Bore(loop);right.position=head.position+head.right*.25f-Vector3.up*.2f;
                if(hands.PracticeTargetCount>0){Vector3 aim=hands.PracticeAimPoint(0,false);for(int i=0;i<4;i++)right.rotation=Quaternion.FromToRotation(bore.forward,(aim-bore.position).normalized)*right.rotation;}
                hands.TickInput(.12f,Frame(.8f));Physics.SyncTransforms();hands.RefreshFeedbackPresentation();
                Need(hands.GunAmmoPanelReady&&hands.GunAmmoPanelVisible&&hands.GunAmmoPanelText.StartsWith(hands.Ammo+"\n",StringComparison.Ordinal),"On-gun actual ammo panel unavailable/stale");
                Vector3 rawR=right.localPosition,rawL=left.localPosition,rawH=head.localPosition,borePoint=bore.position,boreDirection=bore.forward;Quaternion rawRQ=right.localRotation,rawLQ=left.localRotation,rawHQ=head.localRotation;
                int ammo=hands.Ammo;long shots=hands.FeedbackAcceptedShots;float targetTotal=0;
                if(hands.PracticeTargetCount>0){var t=hands.ReadPracticeTarget(0);targetTotal=t.Health+t.Shield;}
                Need(hands.Fire(.12f)&&hands.Ammo==ammo-1&&hands.FeedbackAcceptedShots==shots+1,"Native accepted fire did not debit once/publish one feedback shot");hands.RefreshFeedbackPresentation();
                Need(hands.MuzzleFlashVisible&&hands.VisualRecoilDistance>0&&Mathf.Abs(hands.VisualRecoilPitchRadians)>0&&hands.TracerVisible,"Committed shot lacks flash/recoil/tracer");
                Need(hands.GunAmmoPanelText.StartsWith(hands.Ammo+"\n",StringComparison.Ordinal),"Gun ammo panel did not update to owner count");
                Need(Vector3.Distance(hands.TracerWorldStart,borePoint)<.0001f&&Vector3.Distance(bore.position,borePoint)<.0001f&&Vector3.Distance(bore.forward,boreDirection)<.0001f,"Visual recoil changed actual neutral bore or tracer origin");
                Need(right.localPosition==rawR&&left.localPosition==rawL&&head.localPosition==rawH&&right.localRotation==rawRQ&&left.localRotation==rawLQ&&head.localRotation==rawHQ,"Feedback overwrote raw tracked poses");
                Need(haptics>0,"Accepted tracked shot emitted no haptic REQUEST");
                Note(g,"Desktop gun-feedback capture: "+R23SmokeControl.Capture(loop.Rig.Head,"source-gun-feedback"));
                if(hands.PracticeTargetCount>0){var t=hands.ReadPracticeTarget(0);Need(t.Health+t.Shield<targetTotal,"Actual source bore ray failed to damage root practice target");Note(g,"Actual root practice target damaged by accepted source bore ray");}
                float total=hands.Health+hands.Shield;hands.ApplyPlayerDamage(35);Need(Mathf.Abs(total-hands.Health-hands.Shield-35)<.001f,"Actual player health/shield damage did not apply");
                hands.SetGameplayPaused(true);int stoppedAmmo=hands.Ammo,requests=haptics;Need(!hands.Fire(.1f)&&hands.Ammo==stoppedAmmo&&haptics==requests,"Paused owner accepted shot/haptic request");hands.RefreshFeedbackPresentation();
                Need(!hands.GunAmmoPanelVisible&&!hands.MuzzleFlashVisible&&!hands.TracerVisible&&hands.VisualRecoilDistance==0,"Pause left source feedback visible");
                hands.SetGameplayPaused(false);Need(hands.HolsterCurrent(),"Actual holster failed");hands.RefreshFeedbackPresentation();Need(!hands.GunAmmoPanelVisible&&!hands.MuzzleFlashVisible&&!hands.TracerVisible,"Holster retained source feedback");
                hands.ResetRound();hands.RefreshFeedbackPresentation();Need(!hands.GunAmmoPanelVisible&&!hands.TracerVisible&&hands.FeedbackAcceptedShots==0&&hands.VisualRecoilDistance==0,"Round reset retained feedback identity/presentation");
                Note(g,"Real grip pickup, one ammo debit, gun panel, flash/kick/tracer, damage, pause/holster/reset; hapticRequests="+haptics+" (request only; no XR delivery claim)");
            }
            finally{hands.HapticRequested-=pulse;hands.ResetRound();right.localPosition=oldRight;right.localRotation=oldRightQ;left.localPosition=oldLeft;left.localRotation=oldLeftQ;head.localPosition=oldHead;head.localRotation=oldHeadQ;}
        }
        static void Traversal(R23PlayableLoop loop,Group g)
        {
            var audio=loop.TraversalAudio;Need(audio!=null&&audio.Ready&&audio.LoadFailure==null&&audio.LoadedClipCount==18,"Actual18 traversal clips did not decode: "+(audio==null?"missing":audio.LoadFailure));
            int walk=0,sprint=0,landing=0;Action<R23Footfall,string> footfall=delegate(R23Footfall kind,string surface){if(kind==R23Footfall.Walk)walk++;if(kind==R23Footfall.Sprint)sprint++;if(kind==R23Footfall.Landing)landing++;Note(g,"FootfallPlayed "+kind+" surface="+surface);};audio.FootfallPlayed+=footfall;
            try
            {
                audio.SetSfxVolume(1);audio.ResetRound();var sample=new R23TraversalSample{Enabled=true,Grounded=true,HorizontalSpeed=2};
                for(int i=0;i<6;i++)audio.Tick(sample,.1f);sample.Sprinting=true;for(int i=0;i<4;i++)audio.Tick(sample,.1f);
                sample.Grounded=false;sample.VerticalSpeed=-6;audio.Tick(sample,.1f);sample.Grounded=true;sample.VerticalSpeed=0;audio.Tick(sample,.1f);
                Need(walk>0&&sprint>0&&landing>0,"Native decoded source footfall Play events missing");
                var foot=loop.Rig.transform.Find("R23 source footsteps").GetComponent<AudioSource>();Need(foot.clip!=null&&foot.clip.length>0&&foot.pitch>0,"Footfall source did not bind actual decoded PCM clip/pitch");
                sample.Grounded=false;sample.Gliding=true;sample.HorizontalSpeed=10.5f;sample.VerticalSpeed=-2.8f;for(int i=0;i<4;i++)audio.Tick(sample,.1f);
                var glide=loop.Rig.Head.transform.Find("R23 source glide").GetComponent<AudioSource>();Need(glide.clip!=null&&glide.clip.length>0&&glide.loop&&audio.LastFrame.GlideGain>0&&glide.volume>0&&Mathf.Abs(glide.pitch-audio.LastFrame.GlidePitch)<.0001f,"Glide actual source clip/control state not applied");
                Note(g,"Decoded18; actual AudioSource Play paths/events and clip/gain/pitch checked; engine isPlaying foot="+foot.isPlaying+" glide="+glide.isPlaying+"; listeningVerified=false");
                audio.Suspend();Need(!foot.isPlaying&&!glide.isPlaying&&audio.LastFrame.GlideGain==0,"Suspend retained traversal source playback/state");
            }
            finally{audio.FootfallPlayed-=footfall;audio.Suspend();audio.SetSfxVolume(loop.UI.Settings.SfxVolume);}
        }
        static GameObject Box(List<GameObject> owned,string name,Vector3 at,Vector3 size,int layer=0,Transform parent=null)
        {var go=new GameObject(name);owned.Add(go);go.layer=layer;if(parent!=null)go.transform.SetParent(parent,false);go.transform.position=at;go.AddComponent<BoxCollider>().size=size;return go;}
        static void Ping(R23PlayableLoop loop,Group g)
        {
            var ping=loop.WorldPing;Need(ping!=null,"Root world ping missing");var owned=new List<GameObject>();Vector3 head=loop.Rig.Head.transform.localPosition;bool backfaces=Physics.queriesHitBackfaces;
            try
            {
                ping.Tick(0,true);Need(ping.TryPlace(new Ray(loop.Rig.Head.transform.position,Vector3.down))&&ping.Visible,"Actual source map downward ping found no supported native world");
                Vector3 site=new Vector3(7000,200,7000);Box(owned,"Ping native world",site+Vector3.forward*5,new Vector3(4,4,.1f));
                var cover=Box(owned,"Ping constructed native cover",site+Vector3.forward*3,new Vector3(4,4,.1f),8);
                Box(owned,"Ping source ballistic-only",site+Vector3.forward*2,new Vector3(4,4,.1f),19);
                Box(owned,"Ping own actor collider",site+Vector3.forward,new Vector3(4,4,.1f),0,loop.Rig.transform);Physics.SyncTransforms();
                Need(ping.TryPlace(new Ray(site,Vector3.forward))&&Mathf.Abs(ping.SurfacePoint.z-(site.z+2.95f))<.003f,"Ping failed constructed cover/self/ballistic exclusion");
                Need(Vector3.Distance(ping.Position,ping.SurfacePoint+ping.SurfaceNormal*.035f)<.001f&&ping.Remaining==20,"Ping source normal offset/lifetime differs");
                cover.SetActive(false);Need(ping.TryPlace(new Ray(site,Vector3.forward))&&Mathf.Abs(ping.SurfacePoint.z-(site.z+4.95f))<.003f,"Ping failed main world after cover removed");
                Need(Physics.queriesHitBackfaces==backfaces&&loop.Rig.Head.transform.localPosition==head,"Ping query leaked backface flag or wrote tracked head");
                ping.Tick(19.9f,true);Need(ping.Visible,"Ping expired before20seconds");ping.Tick(.2f,true);Need(!ping.Visible&&ping.Remaining==0,"Ping did not expire");
                ping.Tick(0,true);Need(ping.TryPlace(new Ray(site,Vector3.forward)),"Ping replacement failed");ping.Tick(0,false);Need(!ping.Visible&&ping.Remaining==0,"Invalid focus/pause did not clear ping");ping.ClearAll();
                Note(g,"Actual root component/native map, constructed cover, actor/ballistic exclusions, .035offset,20s expiration and invalid/map cleanup");
            }
            finally{ping.ClearAll();foreach(var go in owned){go.SetActive(false);UnityEngine.Object.Destroy(go);}Physics.SyncTransforms();}
        }
        static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static void Heal(R23PlayableLoop loop,Group g)
        {
            var stations=loop.HealthStations;var health=loop.Gameplay;Need(stations!=null&&stations.Ready&&stations.MapId==R23MapCatalog.PaintballId&&stations.StationCount==17,"Actual17 Paintball stations unavailable");
            Vector3 previous=loop.Rig.transform.position,forward=loop.Rig.transform.forward;
            try
            {
                health.ResetRound();health.NotifyFocus(true);health.SetGameplayPaused(false);health.ApplyPlayerDamage(health.Shield+40);Need(Mathf.Abs(health.Health-60)<.001f,"Actual health owner damage setup failed");
                var row=stations.AuthoredStations[0];Vector3 at=U(row.Center)+Vector3.right*.65f+U(row.Normal)*.12f;loop.Rig.SetSpawn(at,at+forward);loop.Rig.GameplayBlocked=false;
                for(int i=0;i<80;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);Physics.SyncTransforms();
                Vector3 feet=new Vector3(loop.Rig.Head.transform.position.x,loop.Rig.transform.position.y,loop.Rig.Head.transform.position.z);RaycastHit support;
                Need(loop.Rig.HeadCollision.Support(loop.Rig.transform.position,loop.Rig.Head.transform.position,out support)&&Mathf.Abs(loop.Rig.transform.position.y-support.point.y)<.06f,"Authored health contact must be reachable on the actual source floor");
                Need(stations.RestoreBound&&stations.Step(feet,.1f,true)&&stations.LastApplicationAccepted&&health.Health==health.MaxHealth&&health.Shield==health.MaxShield,"Actual R18 contact did not restore both root health and shield");
                double remaining=stations.CooldownRemaining(0);Need(remaining>14.5&&remaining<=15,"Accepted restore did not start source15s station recharge");
                health.ApplyPlayerDamage(35);float before=health.Health+health.Shield;
                Need(!stations.Step(feet,.1f,true)&&health.Health+health.Shield==before,"Same station restored again during source recharge");
                stations.ResetContact();Need(stations.CooldownRemaining(0)>14.5,"Contact reset erased shared station recharge");
                Need(!stations.Step(feet,.1f,false)&&health.Health+health.Shield==before,"Blocked station restored health or shield");
                health.ApplyPlayerDamage(1000);Need(health.Health==0&&!stations.Step(feet,.1f,true)&&health.Health==0,"Station resurrected dead actual root owner");
                stations.ResetContact();Note(g,"Actual authored "+row.Id+" reachable feet="+feet+"; R18 full health+shield restore, shared15s recharge, reset/pause/dead rejection; source ring/audio require capture/listening separately");
            }
            finally{health.ResetRound();stations.ResetContact();loop.Rig.SetSpawn(previous,previous+forward);}
        }
        static void Gallery(R23PlayableLoop loop,Group g)
        {
            var gallery=loop.Gallery;var hands=loop.Gameplay;var right=loop.Rig.Right;Vector3 old=right.localPosition;Quaternion oldQ=right.localRotation;
            string owned=String.Join("|",hands.OwnedWeaponIds);int carried=hands.ReadWeaponState("magnum").Loaded,reserve=hands.ReadReserve("magnum");
            try
            {
                Need(gallery.ViewState.Ready&&gallery.MagnumPreviewVisible&&gallery.ViewedWeaponId=="magnum"&&gallery.MagnumFailure==null,"Loaded gallery did not present actual Magnum art");
                var offset=Ashfall.Integration.Playable.Gallery.R23GalleryRules.DockOffset(new Float3(loop.Rig.Head.transform.forward.x,loop.Rig.Head.transform.forward.y,loop.Rig.Head.transform.forward.z));
                right.position=loop.Rig.Head.transform.position+U(offset);gallery.Sample(.02f,Frame(),true,1);gallery.Sample(.02f,Frame(.8f),true,1);
                Need(gallery.ViewState.Live&&gallery.HoldingHand==1&&gallery.ViewState.Ammo==7&&gallery.ViewState.Reserve==1200,"Physical gallery grip did not create its private finite source owner");
                gallery.Sample(.2f,MagnumFrame(.8f,trigger:.8f),true,1);Need(gallery.ViewState.Ammo==6&&gallery.ViewState.PreviewShots==1,"Gallery Magnum did not fire its own native chamber");
                Note(g,"Desktop gallery capture: "+R23SmokeControl.Capture(loop.Rig.Head,"source-magnum-gallery"));
                Need(String.Join("|",hands.OwnedWeaponIds)==owned&&hands.ReadWeaponState("magnum").Loaded==carried&&hands.ReadReserve("magnum")==reserve,"Gallery changed carried inventory/ammunition");
                gallery.Hide();Need(!gallery.Active&&!gallery.MagnumPreviewVisible&&gallery.HoldingHand==-1,"Gallery hide retained Magnum ownership/art");
                Note(g,"Actual Magnum gallery model, physical grip and finite source fire; carried inventory/chambers/reserve unchanged; no headset or listening claim");
            }
            finally{gallery.Hide();right.localPosition=old;right.localRotation=oldQ;}
        }
        // Original staged wall holdouts, independent physical site and real HealthService.
        sealed class Sight:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return true;}}
        static IEnumerator HealthWalls()
        {
            var host=new GameObject("R23 station wall fixture");var map=new GameObject("R23 station wall map");var actor=new GameObject("R23 station wall actor");GameObject wall=null;Mesh mesh=null;
            try
            {
                map.transform.position=Vector3.one*10000;var health=new R23RoundState(new string[0],new Sight());health.Damage(40);
                var stations=host.AddComponent<R23HealthStations>();stations.Initialize("paintball",actor.transform,map.transform,delegate(){if(health.Health<=0||health.Health==health.MaxHealth&&health.Shield==health.MaxShield)return false;return health.ApplyPlayerGain(health.MaxHealth-health.Health,health.MaxShield-health.Shield);});
                float end=Time.realtimeSinceStartup+30;while(!stations.Ready&&Time.realtimeSinceStartup<end&&stations.Failure==null)yield return null;
                Need(stations.Ready&&stations.StationCount==17,"Wall fixture actual Paintball rows failed to load");
                var row=stations.AuthoredStations[0];Vector3 center=U(row.Center),feet=center+new Vector3(.5f,.1f,0);actor.transform.position=feet;
                var own=actor.AddComponent<BoxCollider>();own.center=Vector3.up*.1f;own.size=Vector3.one*.4f;Physics.SyncTransforms();Need(stations.Step(feet,.1f,true)&&health.Health==health.MaxHealth&&health.Shield==health.MaxShield,"Open native source R18 restoration failed");
                // Each independent wall case starts a fresh station lifetime, so
                // the prior source15s recharge cannot hide an occlusion failure.
                health.Damage(35);float total=health.Health+health.Shield;stations.SetMap("paintball",true);
                wall=new GameObject("Health fixture native wall");wall.transform.position=center+new Vector3(.25f,.21f,0);var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(.08f,.5f,.5f);Physics.SyncTransforms();Need(!stations.Step(feet,.1f,true)&&health.Health+health.Shield==total&&stations.CooldownRemaining(0)==0,"Actual wall did not block R18 restoration");
                wall.layer=19;Physics.SyncTransforms();Need(stations.Step(feet,.1f,true)&&health.Health==health.MaxHealth&&health.Shield==health.MaxShield,"Ballistic-only wall blocked source physical contact");
                health.Damage(35);total=health.Health+health.Shield;stations.SetMap("paintball",true);
                wall.layer=8;Physics.SyncTransforms();Need(!stations.Step(feet,.1f,true)&&health.Health+health.Shield==total&&stations.CooldownRemaining(0)==0,"Constructed cover failed to block healing");
                stations.SetMap("paintball",true);box.enabled=false;wall.layer=0;mesh=new Mesh();mesh.vertices=new[]{new Vector3(0,-1,-1),new Vector3(0,1,-1),new Vector3(0,0,1)};mesh.triangles=new[]{0,1,2};mesh.RecalculateBounds();wall.AddComponent<MeshCollider>().sharedMesh=mesh;Physics.SyncTransforms();Need(!stations.Step(feet,.1f,true)&&health.Health+health.Shield==total&&stations.CooldownRemaining(0)==0,"One-sided backface wall failed source contact blocking");
                Debug.Log("R23 FEEDBACK actual source HealthStations native wall/cover/backface/ballistic holdouts PASS");
            }
            finally{foreach(var go in new[]{host,map,actor,wall})if(go!=null){go.SetActive(false);UnityEngine.Object.Destroy(go);}if(mesh!=null)UnityEngine.Object.Destroy(mesh);Physics.SyncTransforms();}
        }
    }
}
#endif
