#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.TeamLaunch;
using Ashfall.Integration.Playable.MatchZone;
using Ashfall.Integration.Playable.UI;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Actual XR-off Root/UI/Health/zone/terrain/carrier integration. A compiled
    // fixture is not a native pass, a peer session or headset acceptance.
    public sealed class R24LocalMatchSmoke
    {
        [Serializable] sealed class Group {public string name="",error="";public bool passed;public List<string> notes=new List<string>();}
        [Serializable] sealed class Receipt {public bool success,nativeExecuted,headsetVerified=false;public string error="",startedUtc="",finishedUtc="";public List<Group> groups=new List<Group>();}
        R23PlayableLoop loop;Receipt receipt;Saved saved;float deadline;long buildTick=9000000;
        public bool AutoFinish=true;
        public bool Passed{get;private set;}public string Json{get;private set;}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Actual local-match fixture exceeded180s active bound");}
        void Note(Group g,string text){g.notes.Add(text);Debug.Log("R24 LOCAL MATCH "+g.name+": "+text);}
        Group Add(string name){var g=new Group{name=name};receipt.groups.Add(g);return g;}
        public IEnumerator Run()
        {
            receipt=new Receipt{startedUtc=DateTime.UtcNow.ToString("o")};float readyEnd=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<readyEnd){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){receipt.error="Actual ready XR-disabled playable scene required: "+(loop==null?"no Root":loop.Failure);Finish();yield break;}
            receipt.nativeExecuted=true;saved=new Saved(loop);deadline=Time.realtimeSinceStartup+180;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                loop.Rig.TrackingOrigin.localPosition=Vector3.zero; // controlled XR-off source eye height; saved/restored below
                loop.Rig.Head.transform.SetLocalPositionAndRotation(new Vector3(0,1.7088f,0),Quaternion.identity);
                loop.Rig.Left.SetLocalPositionAndRotation(new Vector3(-.22f,1.16f,.24f),Quaternion.identity);
                loop.Rig.Right.SetLocalPositionAndRotation(new Vector3(.22f,1.16f,.24f),Quaternion.identity);
                foreach(string name in new[]{"ActualDefaultExploreUnchanged","ActualPracticeInfiniteOwners","ActualFfaZoneHealthRespawn","ActualTeamReviveDeckConsoleCarrier"})
                {
                    var g=Add(name);IEnumerator routine=name=="ActualDefaultExploreUnchanged"?Explore(g):name=="ActualPracticeInfiniteOwners"?Practice(g):name=="ActualFfaZoneHealthRespawn"?Ffa(g):Revive(g);
                    var work=Drive(g,routine);while(work.MoveNext())yield return work.Current;
                }
                var cleanup=Add("ActualSceneCleanup");var restore=Drive(cleanup,Restore(cleanup));while(restore.MoveNext())yield return restore.Current;
            }
            finally
            {
                // Also runs on cancellation/timeout; never leave a live source match,
                // altered tracking locals, saved preferences or a carrier behind.
                try{if(loop!=null&&loop.Ready)loop.ReturnToLobby();}catch(Exception e){receipt.error+=" Cleanup lobby: "+e;}
                saved.RestoreFlagsAndPoses(loop);Finish();
            }
        }
        IEnumerator Drive(Group group,IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            try
            {
                while(stack.Count>0)
                {
                    bool moved=false;object value=null;try{Bound();moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}catch(Exception e){group.error=e.ToString();Debug.LogError("R24 LOCAL MATCH FAIL "+group.name+": "+e);break;}
                    if(!moved){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}
                    var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;
                }
                group.passed=group.error.Length==0;
            }
            finally{while(stack.Count>0){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();}}
        }
        void Finish()
        {
            receipt.success=receipt.nativeExecuted&&receipt.error.Length==0&&receipt.groups.Count==5;foreach(var g in receipt.groups)receipt.success&=g.passed&&g.error.Length==0;
            receipt.finishedUtc=DateTime.UtcNow.ToString("o");Passed=receipt.success;Json=JsonUtility.ToJson(receipt,true);Debug.Log("R24 LOCAL MATCH TERMINAL "+Json);if(AutoFinish)R23SmokeControl.Finish(Passed,Json);
        }
        IEnumerator Explore(Group g)
        {
            Note(g,"INITIAL actual defaults selected="+(loop.UI.SelectedMatchRules==null?"null":loop.UI.SelectedMatchRules.PresetId)+" active="+loop.MatchActive);
            Need(loop.UI.SelectedMatchRules==null&&loop.ActualMatchRules==null&&!loop.MatchActive,"Fresh Root auto-selected a local match; do not clear it to manufacture a default pass");
            loop.ReturnToLobby();Need(loop.UI.SetSelectedMap("range"),"Actual installed Range choice unavailable");loop.UI.OpenMatchRules();
            Note(g,"BEFORE default rules: selected="+(loop.UI.SelectedMatchRules==null?"null":loop.UI.SelectedMatchRules.PresetId)+" match="+loop.MatchActive+" actual="+(loop.ActualMatchRules!=null));
            Need(loop.UI.SelectedMatchRules==null&&!loop.UI.RequestMatchStart()&&!loop.MatchActive,"Opening source rule page silently selected/started a match");
            loop.UI.ToggleMenu();loop.UI.OpenSpawnChoice();Need(loop.UI.ConfirmMapSpawn(true),"Existing source Explore GAME SPAWN route refused");
            yield return Wait(delegate{return loop.Ready&&loop.Map.MapId=="range"&&loop.UI.Page==R23UiPage.Playing;},65,"actual Explore Range entry");Pump();
            Need(!loop.MatchActive&&loop.ActualMatchRules==null&&loop.MatchZoneOwner==null&&!loop.TeamLaunch.Eligible&&!loop.Rig.CarrierActive&&loop.UI.LocalMatchLifeState==null,"Explore inherited local match/zone/deck/life policy");
            Need(FloorAt(loop.Rig.transform.position,loop.Map.transform,2)!=null,"Actual Range Explore spawn has no native floor");
            Note(g,"Actual pre-existing UI GAME SPAWN callback loaded Range and entered Explore; no match owner/zone/deck. No peer or scene stand-in.");
        }
        IEnumerator Begin(Group g,string preset)
        {
            Bound();Need(loop.Ready,"Previous actual map not ready");loop.ReturnToLobby();Need(loop.UI.SetSelectedMap("range"),"Actual Range catalog entry absent");loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset(preset);yield return null;
            Note(g,"BEFORE selection "+preset+" actual="+(loop.ActualMatchRules!=null)+" pending="+loop.UI.SelectedMatchRules.PresetId+" map="+loop.UI.SelectedMapId);
            Need(!loop.MatchActive&&loop.ActualMatchRules==null,"Preset selection auto-started a match");
        }
        IEnumerator Start(Group g)
        {
            Need(loop.UI.RequestMatchStart(),"Public actual UI START MATCH request refused");yield return Wait(delegate{return loop.Ready&&loop.MatchActive&&loop.UI.Page==R23UiPage.Playing;},65,"actual UI->Root match start: "+loop.MatchStatus);
            Pump();Need(loop.Gameplay.LocalMatchHealthConfigured&&loop.Map.MapId=="range","UI request did not configure actual loaded map/Health owner");State(g,"AFTER actual start");
        }
        IEnumerator Practice(Group g)
        {
            yield return Begin(g,"practice");yield return Start(g);var h=loop.Gameplay;var r=loop.ActualMatchRules;
            Need(r.Mode=="respawn"&&r.Damage==0&&r.Catalog&&r.InfiniteAmmo&&r.InfiniteBuilds&&r.ScoreLimit==0&&r.TimeLimit==0&&!r.ZoneEnabled,"Actual practice lost source overrides");
            Need(loop.MatchZoneOwner.State==null&&!loop.TeamLaunch.Eligible,"Practice unexpectedly created zone or team deck");
            Need(h.OwnedWeaponActionIds.Length==3&&h.OwnsWeapon("cx4")&&h.OwnsWeapon("px4")&&h.OwnsWeapon("m1014"),"Practice actual source standard kit differs");
            h.NotifyFocus(true);h.SetGameplayPaused(false);Need(h.EquipOwned("m1014",1),"Actual source owned M1014 equip refused");
            Vector3 forward=Vector3.ProjectOnPlane(loop.Rig.Head.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
            loop.Rig.Right.SetPositionAndRotation(loop.Rig.Head.transform.position+forward*.24f-Vector3.up*.22f,Quaternion.LookRotation(forward)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation));
            loop.Rig.Left.position=loop.Rig.Head.transform.position-forward*.15f-Vector3.up*.25f;Physics.SyncTransforms();
            h.TickInput(.02f,WeaponFrame());int stored=h.ReadStoredAmmoReserveFamily("shells"),before=h.Ammo;
            R23ShotPose bore;Need(h.TryReadDisplayedShotPose(out bore)&&MapClear(loop.Rig.Head.transform.position,V(bore.Origin)),"Actual practice head-to-muzzle lane blocked");
            bool shot=h.TickInput(.02f,WeaponFrame(trigger:1));Note(g,"BEFORE unlimited reload acceptedShot="+shot+" loaded="+h.Ammo+" reserve="+h.ReadAmmoReserveFamily("shells")+" stored="+stored);
            Need(shot&&h.Ammo==before-1&&h.ReadAmmoReserveFamily("shells")==-1,"Actual native practice shot/unlimited family policy failed");
            h.TickInput(.02f,WeaponFrame());h.TickInput(.02f,WeaponFrame(click:true));Need(h.ReloadStage==ReloadPhase.NeedsMagazine,"Actual source M1014 stick-click eject refused");h.TickInput(.02f,WeaponFrame());
            loop.Rig.Left.position=h.ReloadOfferPosition;h.TickInput(.02f,WeaponFrame(left:.8f));loop.Rig.Left.position=h.ReloadInsertionPosition;h.TickInput(.02f,WeaponFrame(left:.8f));Need(h.ReloadStage==ReloadPhase.NeedsRack,"Actual practice UP shell-pack insertion refused");
            h.TickInput(.02f,WeaponFrame());loop.Rig.Left.position=h.ChargingGripPosition;h.TickInput(.02f,WeaponFrame(left:.8f));loop.Rig.Left.position-=R23TrackedGripPose.Forward(loop.Rig.Right)*.09f;h.TickInput(.02f,WeaponFrame(left:.8f));
            Need(h.ReloadStage==ReloadPhase.Ready&&h.Ammo==6&&h.ReadAmmoReserveFamily("shells")==-1&&h.ReadStoredAmmoReserveFamily("shells")==stored,"Actual unlimited UP/BACK reload changed stored finite source reserve or failed refill");
            h.HolsterCurrent();loop.Rig.Right.position=loop.Rig.transform.position+new Vector3(.3f,1.2f,.2f);loop.Rig.Right.rotation=Quaternion.identity;Physics.SyncTransforms();int panels=loop.Activities.PanelCount,balance=loop.Activities.Balance;
            Build(false);Build(true);Build(false);Note(g,"AFTER actual construction panels="+loop.Activities.PanelCount+" balance="+loop.Activities.Balance+" status="+loop.Activities.BuildStatus);
            Need(loop.Activities.PanelCount>panels&&loop.Activities.Balance==balance,"Source practice real construction did not place without finite debit");
            AdvanceTo(loop.Gameplay.LocalMatchHealth.ProtectionUntil+.05);float vitality=h.Health+h.Shield;h.ApplyPlayerDamage(25);Need(h.Health+h.Shield==vitality,"Source practice DAMAGE0 mutated actual Health after protection");
            Note(g,"PASS actual source kit, accepted native M1014 discharge and physical UP/BACK unlimited reload, stored reserve unchanged, actual native supported construction free, damage0 after protection.");
        }
        IEnumerator Ffa(Group g)
        {
            yield return Begin(g,"free_for_all");foreach(var pair in new[]{new KeyValuePair<string,object>("zone_wait",0f),new KeyValuePair<string,object>("zone_speed",20f),new KeyValuePair<string,object>("zone_damage",8f),new KeyValuePair<string,object>("damage",0f),new KeyValuePair<string,object>("starting_shield",73),new KeyValuePair<string,object>("protection",10f),new KeyValuePair<string,object>("respawn_delay",1f),new KeyValuePair<string,object>("time_limit",60)})Need(loop.UI.SetMatchRule(pair.Key,pair.Value),"Actual supported rule unavailable: "+pair.Key);
            yield return Start(g);Need(loop.LocalMatchTeam==-1&&loop.ActualMatchRules.Teams==0&&!loop.TeamLaunch.Eligible&&loop.MatchZoneOwner.State!=null,"Actual FFA team/zone/deck mode differs");
            R24LaunchMapInfo info;Need(loop.LaunchMapCatalog.TryRead("range",out info),"Actual source Range zone bounds missing");var initial=R24ZoneMath.Initial(info.CatalogMinX,info.CatalogMinZ,info.CatalogMaxX,info.CatalogMaxZ);var first=loop.MatchZoneOwner.State;
            Need(Math.Abs(first.From.X-initial.X)<.0001&&Math.Abs(first.From.Z-initial.Z)<.0001&&Math.Abs(first.From.Size-initial.Size)<.0001&&Math.Abs(first.Next.Size-Math.Max(12,initial.Size*.65))<.0001,"Actual source square bounds/ratio changed");
            Vector3 outside=Vector3.zero;bool found=false;for(int n=0;n<12&&!found;n++){loop.TickMatch(.1f,true);found=FindZoneFloor(info,false,out outside);}Need(found,"Shrinking actual square left no supported native outside Range floor within source protection window");
            loop.Rig.SetSpawn(outside+Vector3.up*.02f,loop.Map.Bounds.center);loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();double protection=loop.Gameplay.LocalMatchHealth.ProtectionUntil;
            Need(loop.MatchClock+.1<protection,"Fixture failed to find genuine outside floor before protection ended");float hp=loop.Gameplay.Health,shield=loop.Gameplay.Shield;
            loop.TickMatch(.1f,true);State(g,"BEFORE protected outside assert");Need(loop.MatchZone.PlayerOutside&&!loop.MatchZone.LastDamageAccepted&&loop.MatchZone.LastDamageRequest==null&&loop.Gameplay.Health==hp&&loop.Gameplay.Shield==shield,"Actual protected outside actor received zone damage");
            AdvanceTo(protection+.25);State(g,"AFTER actual unprotected zone interval");
            Need(loop.MatchZone.PlayerOutside&&loop.Gameplay.Health<hp&&loop.Gameplay.Shield==73&&loop.ActualMatchRules.Damage==0,"Actual zone failed direct Health/damage0 bypass or debited shield");
            float healthBefore=loop.Gameplay.Health;loop.Gameplay.ApplyPlayerDamage(10);Need(loop.Gameplay.Health==healthBefore&&loop.Gameplay.Shield==73,"Weapon damage0 did not stay separate from actual zone");
            yield return null;R23SmokeControl.Capture(loop.Rig.Head,"local-match-ffa-direct-zone");
            Need(loop.Gameplay.ApplyDirectPlayerDamage(1000),"Actual FFA lethal direct Health callback refused");loop.TickMatch(.01f,true);Pump();var dead=loop.Gameplay.LocalMatchHealth;State(g,"BEFORE actual delayed respawn");
            Need(dead.Life==LifeState.Dead&&!loop.MatchLocalInputAllowed&&loop.UI.LocalMatchLifeState=="respawning"&&loop.UI.LocalMatchLifeCaption.StartsWith("YOU DIED \u00b7 RESPAWN",StringComparison.Ordinal),"Actual FFA death did not bind source respawning life UI/input block");
            AdvanceTo(dead.RespawnAt-.02);Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Dead,"Actual source respawn occurred before delay");AdvanceTo(dead.RespawnAt+.02);Pump();State(g,"AFTER actual delayed respawn");
            Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Alive&&loop.Gameplay.Health==100&&loop.Gameplay.Shield==73&&loop.MatchLocalInputAllowed&&loop.UI.Page==R23UiPage.Playing&&loop.Gameplay.LocalMatchHealth.ProtectionUntil>loop.MatchClock,"Actual source respawn HP/shield/protection/input/UI restore failed");
            Need(!loop.Gameplay.ApplyDirectPlayerDamage(5)&&loop.Gameplay.Health==100,"Actual respawn protection rejected neither direct damage nor source loss");
            AdvanceTo(60.01);State(g,"AFTER actual time cap");Need(!loop.MatchActive&&!loop.MatchLocalInputAllowed&&loop.UI.LocalMatchLifeCaption=="ROUND COMPLETE","Actual source clock did not end round and publish results");
            Note(g,"PASS catalog-local square65% shrink, real source floor outside, protected interval, directHP with shield73/damage0, actual1s respawn and protection, actual60s results. Public TickMatch advanced the real owner clock, not a fake Health clock.");
        }
        IEnumerator Revive(Group g)
        {
            yield return Begin(g,"team_revive");Need(loop.UI.SetMatchRule("zone_enabled",false)&&loop.UI.SetMatchRule("protection",0f)&&loop.UI.SetMatchRule("downed_seconds",10f),"Actual source revive settings unavailable");yield return Start(g);
            Need(loop.OnLaunchDeck&&loop.LocalMatchTeam==0&&loop.TeamLaunch.Ready&&loop.TeamLaunch.Eligible&&loop.TeamLaunch.Visible&&loop.TeamLaunch.DockCount==24,"Actual two-team Range sky hardware/assignment not active");
            Physics.SyncTransforms();Need(FloorAt(loop.Rig.transform.position,loop.TeamLaunch.transform,2)!=null,"Actual assigned sky deck lacks native support");var layout=loop.TeamLaunch.Runtime.Layout;var pod=layout.Pod(0,0);string podId=layout.PodId(0,0);
            loop.Rig.SetSpawn(V(pod.Origin)+Vector3.up*.12f,V(pod.Point(new Float3(0,0,2))));loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();
            Vector3 console=V(pod.Point(new Float3(0,.56f,.8f)));loop.Rig.Right.position=console-V(pod.Forward)*.12f;loop.Rig.Right.rotation=Quaternion.LookRotation(V(pod.Forward));loop.Rig.Left.position=loop.Rig.Head.transform.position-Vector3.up*.25f;
            Pump();Need(loop.TeamLaunch.Runtime.ReadRide("player")==null,"Neutral cabin input launched a pod");Pump(trigger:.9f);var ride=loop.TeamLaunch.Runtime.ReadRide("player");State(g,"AFTER actual console fresh trigger");
            Need(ride!=null&&ride.Plan.PodId==podId&&ride.State==R24LaunchRideState.Flying&&!loop.OnLaunchDeck&&loop.TeamLaunch.FlightCount==1,"Actual Root tracked console input did not launch its own actual pod: "+loop.LaunchFailure);
            Need(FloorAt(V(ride.Plan.End),loop.Map.transform,2)!=null,"Actual pod landing plan lacks genuine terrain support");Vector3 start=loop.Rig.transform.position,rawHead=loop.Rig.Head.transform.localPosition,origin=loop.Rig.TrackingOrigin.localPosition;
            loop.TickMatch(.2f,true);loop.Rig.SmokeXrMove(Vector2.zero,.02f);Physics.SyncTransforms();Note(g,"BEFORE carrier assert actual="+loop.Rig.transform.position+" sourceAccepted="+V(ride.LastAccepted)+" moved="+Vector3.Distance(start,loop.Rig.transform.position));
            Need(loop.Rig.CarrierActive&&ride.HasAccepted&&Vector3.Distance(loop.Rig.transform.position,V(ride.LastAccepted))<.0001f&&Vector3.Distance(start,loop.Rig.transform.position)>2&&loop.Rig.Head.transform.localPosition==rawHead&&loop.Rig.TrackingOrigin.localPosition==origin,"Sole actual Rig carrier failed source motion or rewrote tracking locals");
            yield return null;R23SmokeControl.Capture(loop.Rig.Head,"local-match-pod-actual-carrier");
            Pump(aimFlightConsole:true);Pump(trigger:.9f,aimFlightConsole:true);loop.Rig.SmokeXrMove(Vector2.zero,.02f);State(g,"AFTER actual console eject");
            Need(ride.State==R24LaunchRideState.Released&&!loop.Rig.CarrierActive&&Vector3.Distance(loop.Rig.transform.position,V(ride.LastAccepted))<.0001f,"Actual Root fresh console eject was not accepted once by Rig");
            Need(loop.Gameplay.ApplyDirectPlayerDamage(1000),"Actual revive lethal direct Health callback refused");loop.TickMatch(.01f,true);Pump();State(g,"AFTER actual revive downing");
            Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Downed&&!loop.MatchLocalInputAllowed&&loop.UI.LocalMatchLifeState=="downed"&&loop.UI.LocalMatchLifeCaption=="YOU ARE A GHOST \u00b7 WAITING FOR REVIVE","Actual revive mode showed death/restart instead of source downed notice");
            yield return null;R23SmokeControl.Capture(loop.Rig.Head,"local-match-actual-ghost-waiting");double until=loop.Gameplay.LocalMatchHealth.DownedUntil;AdvanceTo(until+.02);State(g,"AFTER actual no-ally bleedout");
            Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Dead&&!loop.Gameplay.TryRespawnLocalMatch()&&!loop.MatchLocalInputAllowed&&loop.UI.LocalMatchLifeState=="eliminated","Actual revive bleedout incorrectly respawned or allowed input");
            Note(g,"PASS real Range deck floor, own cabin fresh tracked console, native terrain landing plan, Rig-owned carry/raw tracking preservation and fresh eject; real downed then elimination. No fabricated ally/peer/revive success.");
        }
        IEnumerator Restore(Group g)
        {
            loop.ReturnToLobby();loop.UI.ClearMatchSelection();if(loop.Map.MapId!=saved.Map){loop.SelectMap(saved.Map,false);yield return Wait(delegate{return loop.Ready&&loop.Map.MapId==saved.Map;},60,"original actual map cleanup");loop.ReturnToLobby();}
            if(saved.SelectedMap!=null)Need(loop.UI.SetSelectedMap(saved.SelectedMap),"Original pending map selection cannot be restored");
            Need(!loop.MatchActive&&loop.ActualMatchRules==null&&loop.MatchZoneOwner==null&&!loop.Rig.CarrierActive&&!loop.TeamLaunch.Eligible&&loop.UI.LocalMatchLifeState==null,"Actual match/zone/carrier/life policy survived EndMatch");
            Note(g,"Actual local session ended, original map restored through real loader; round reset, no fake floor or colliders created; tracking/flags restored by outer finally.");
        }
        IEnumerator Wait(Func<bool> condition,float seconds,string why)
        {float end=Mathf.Min(deadline,Time.realtimeSinceStartup+seconds);while(!condition()&&loop.Failure==null&&Time.realtimeSinceStartup<end){Bound();yield return null;}Need(condition(),"Timed out: "+why+" / "+loop.Failure+" / "+loop.MatchStatus);}
        void Pump(float trigger=0,bool aimFlightConsole=false)
        {
            Bound();loop.Rig.SmokeXrMove(Vector2.zero,.02f);
            if(aimFlightConsole)
            {
                var ride=loop.TeamLaunch.Runtime.ReadRide("player");Need(ride!=null,"No actual ride for console intent");var frame=ride.Frame;
                // Root advances its actual host clock before sampling input. Simulate
                // the tracked hand at that frame's real console, not a stale dock.
                frame.Origin=ride.Position(loop.MatchClock+Mathf.Max(0,Time.unscaledDeltaTime));frame.Origin=R24LaunchMath.Add(frame.Origin,new Float3(0,-.12f,0));
                loop.Rig.Right.position=V(frame.Point(new Float3(0,.56f,.8f)))-V(frame.Forward)*.12f;loop.Rig.Right.rotation=Quaternion.LookRotation(V(frame.Forward));
            }
            loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true,Position=F(loop.Rig.transform.InverseTransformPoint(loop.Rig.Left.position))},new HandSample{Tracked=true,Position=F(loop.Rig.transform.InverseTransformPoint(loop.Rig.Right.position)),Trigger=trigger});Physics.SyncTransforms();loop.SmokeUiUpdate();
        }
        static R23HandsInputFrame WeaponFrame(float left=0,float trigger=0,bool click=false){return new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftGrip=left,RightGrip=.8f,RightTrigger=trigger,RightStickClick=click};}
        void Build(bool trigger){loop.Activities.TickConstruction(new InputFrame{Tick=++buildTick,Tracked=true,TriggerRight=trigger},true,true,false,false);}
        void AdvanceTo(double target){while(loop.MatchActive&&loop.MatchClock+1e-6<target){Bound();loop.TickMatch((float)Math.Min(.1,target-loop.MatchClock),true);}}
        void State(Group g,string stage){var h=loop.Gameplay.LocalMatchHealth;Note(g,stage+": active="+loop.MatchActive+" clock="+loop.MatchClock.ToString("F3")+" life="+(h==null?"null":h.Life.ToString())+" hp/shield="+loop.Gameplay.Health+"/"+loop.Gameplay.Shield+" protection="+(h==null?0:h.ProtectionUntil)+" root="+loop.Rig.transform.position+" deck="+loop.OnLaunchDeck+" carrier="+loop.Rig.CarrierActive+" page="+loop.UI.Page+" lifeCaption="+loop.UI.LocalMatchLifeCaption);}
        bool FindZoneFloor(R24LaunchMapInfo info,bool inside,out Vector3 position)
        {
            var square=loop.MatchZoneOwner.State.Current(loop.MatchClock);Vector3 origin=V(info.RepresentedOriginUnity);var bounds=loop.Map.Bounds;
            for(int z=1;z<20;z++)for(int x=1;x<20;x++)
            {
                var probe=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/20f),bounds.max.y+3,Mathf.Lerp(bounds.min.z,bounds.max.z,z/20f));RaycastHit best=new RaycastHit();float distance=float.PositiveInfinity;
                foreach(var hit in Physics.RaycastAll(probe,Vector3.down,bounds.size.y+8,1<<0,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.normal.y>.7f&&Mathf.Abs(hit.point.y-loop.Map.Spawn.y)<1&&hit.distance<distance){best=hit;distance=hit.distance;}
                if(float.IsPositiveInfinity(distance))continue;Vector3 p=loop.Map.transform.InverseTransformPoint(best.point)-origin;if(square.Contains(p.x,-p.z)!=inside)continue;
                bool blocked=false;foreach(var c in Physics.OverlapCapsule(best.point+Vector3.up*.35f,best.point+Vector3.up*1.45f,.23f,1<<0,QueryTriggerInteraction.Ignore))if(c.transform.IsChildOf(loop.Map.transform)){blocked=true;break;}if(blocked)continue;position=best.point;return true;
            }
            position=Vector3.zero;return false;
        }
        static Collider FloorAt(Vector3 foot,Transform owner,float distance){Physics.SyncTransforms();foreach(var hit in Physics.RaycastAll(foot+Vector3.up*.35f,Vector3.down,distance,1<<0,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(owner)&&hit.normal.y>.7f)return hit.collider;return null;}
        bool MapClear(Vector3 a,Vector3 b){Vector3 delta=b-a;if(delta.sqrMagnitude<.000001f)return true;foreach(var hit in Physics.RaycastAll(a,delta.normalized,delta.magnitude,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform))return false;return true;}
        sealed class Saved
        {
            public readonly string Map,SelectedMap;readonly bool manual,external,enabled,pump,persist,blocked;readonly Vector3 root,origin,head,left,right;readonly Quaternion rootQ,headQ,leftQ,rightQ;
            public Saved(R23PlayableLoop l){var r=l.Rig;Map=l.Map.MapId;SelectedMap=l.UI.SelectedMapId;manual=l.ManualSmoke;external=l.ExternalProbePump;enabled=r.enabled;pump=l.Gameplay.ExternalInputPump;persist=l.Calibration.SuppressPersistence;blocked=r.GameplayBlocked;root=r.transform.position;rootQ=r.transform.rotation;origin=r.TrackingOrigin.localPosition;head=r.Head.transform.localPosition;headQ=r.Head.transform.localRotation;left=r.Left.localPosition;leftQ=r.Left.localRotation;right=r.Right.localPosition;rightQ=r.Right.localRotation;}
            public void RestoreFlagsAndPoses(R23PlayableLoop l){var r=l.Rig;r.transform.SetPositionAndRotation(root,rootQ);r.TrackingOrigin.localPosition=origin;r.Head.transform.SetLocalPositionAndRotation(head,headQ);r.Left.SetLocalPositionAndRotation(left,leftQ);r.Right.SetLocalPositionAndRotation(right,rightQ);r.ClearCarrierMotion();l.Gameplay.SetWeaponTuning(l.UI.Settings.MotionDamping,l.UI.Settings.OffhandInfluence);l.Calibration.SuppressPersistence=persist;l.Gameplay.ExternalInputPump=pump;r.GameplayBlocked=blocked;r.enabled=enabled;l.ExternalProbePump=external;l.ManualSmoke=manual;Physics.SyncTransforms();}
        }
    }
}
#endif
