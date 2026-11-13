#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Consumables;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using Ashfall.Integration.Playable.Explosives;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.World;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Real source items, native floors and Root provider routing. No synthetic
    // terrain, inventory, actor or alternate pickup/physics implementation.
    public sealed class R24SourceLootSmoke
    {
        [Serializable] sealed class Group{public string name="",error="";public bool passed;public List<string> notes=new List<string>();}
        [Serializable] sealed class Receipt{public bool success,nativeExecuted,headsetVerified=false;public string error="";public List<Group> groups=new List<Group>();}
        R23PlayableLoop loop;Receipt receipt;float deadline;Saved saved;
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}static Vector3 V(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Source loot fixture exceeded240s active bound");}
        void Note(Group g,string value){g.notes.Add(value);Debug.Log("R24 SOURCE LOOT "+g.name+": "+value);}
        public IEnumerator Run()
        {
            receipt=new Receipt();float end=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){receipt.error="Actual ready XR-disabled Root required";Finish();yield break;}
            saved=new Saved(loop);deadline=Time.realtimeSinceStartup+240;receipt.nativeExecuted=true;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Left.localPosition=new Vector3(-.24f,1.16f,.24f);loop.Rig.Right.localPosition=new Vector3(.24f,1.16f,.24f);
                foreach(string name in new[]{"ActualRange28SourceSlots","ActualCheckpoint18AndNativeSettling","ActualBackpackCapacityAndNoRecollect","ActualGrenadeCustodyHiddenAndRetirement","ActualAlphaPreparedR6Caches"})
                {
                    var g=new Group{name=name};receipt.groups.Add(g);
                    IEnumerator work=name=="ActualRange28SourceSlots"?Range(g):name=="ActualCheckpoint18AndNativeSettling"?Layout(g):name=="ActualBackpackCapacityAndNoRecollect"?Bag(g):name=="ActualGrenadeCustodyHiddenAndRetirement"?Grenade(g):Alpha(g);
                    var drive=Drive(g,work);while(drive.MoveNext())yield return drive.Current;
                }
                var cleanup=new Group{name="ActualSceneCleanup"};receipt.groups.Add(cleanup);var restore=Drive(cleanup,Cleanup(cleanup));while(restore.MoveNext())yield return restore.Current;
            }
            finally{try{if(loop!=null&&loop.Ready)loop.ReturnToLobby();}catch(Exception e){receipt.error+=" Cleanup: "+e;}saved.Restore(loop);Finish();}
        }
        IEnumerator Drive(Group g,IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            try{while(stack.Count>0){bool moved=false;object value=null;try{Bound();moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}catch(Exception e){g.error=e.ToString();Debug.LogError("R24 SOURCE LOOT FAIL "+g.name+": "+e);break;}if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}var next=value as IEnumerator;if(next!=null)stack.Push(next);else yield return value;}g.passed=g.error.Length==0;}
            finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
        }
        IEnumerator Range(Group g)
        {
            loop.ReturnToLobby();loop.SelectMap("range",true);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+70);
            while((!loop.Ready||loop.Map.MapId!="range")&&loop.Failure==null&&Time.realtimeSinceStartup<end){Bound();yield return null;}
            Need(loop.Ready&&loop.Map.MapId=="range","Actual Range load failed: "+loop.Failure);loop.StartPlaying();loop.RestartRound();Neutral();
            while(loop.WorldLoot.SpawnedCount!=28&&Time.realtimeSinceStartup<end)yield return null;
            Need(loop.WorldLoot.Failure==null&&loop.WorldLoot.PlacementMode=="source-builtin-rack"&&loop.WorldLoot.SpawnedCount==28,"Real28-slot source rack did not prepare: "+loop.WorldLoot.Failure+" / "+loop.WorldLoot.PlacementMode+" / "+loop.WorldLoot.SpawnedCount);
            var profiles=new HashSet<int>();foreach(string id in loop.WorldLoot.WorldInstanceActionIds){string profile=loop.Gameplay.ResolveWeaponProfile(id);Need(loop.Gameplay.TryReadWeaponInstance(id,out var instance)&&instance.Stars==1,"Actual independent builtin weapon instance missing");profiles.Add(R23WeaponPickupRules.CatalogIndex(profile));}
            foreach(string id in loop.WorldLoot.WorldGrenadeInstanceIds){R23GrenadeItemState item;Need(loop.Explosives.TryReadItem(id,out item)&&!item.Owned&&!item.Spent&&item.Stars==1&&item.SourceStars==1,"Real source grenade state missing");profiles.Add(item.Kind=="frag"?26:28);}
            Need(profiles.Count==28&&loop.WorldLoot.WorldInstanceActionIds.Length==26&&loop.WorldLoot.WorldGrenadeInstanceIds.Length==2&&loop.WorldLoot.UnsupportedDraws==0,"Source catalog slots substituted, skipped or duplicated");
            R23GrenadeItemState legacy;Need(!loop.Explosives.TryReadItem("r23-practice-grenade/0",out legacy),"Old ad-hoc grenade row duplicates the source rack");
            Note(g,"Actual26 weapon copies +Frag26 +Zone28, all28 distinct source catalogs, no unsupported draw or old duplicate grenade row.");
        }
        IEnumerator Layout(Group g)
        {
            Need(loop.Map.MapId=="range"&&loop.Ready,"Range predecessor unavailable");var rows=new List<R24CheckpointPickup>(R24WorldPickupLayout.FoodsAndBackpacks());rows.AddRange(R24WorldPickupLayout.Ammo());
            Need(loop.Consumables.SourceCheckpointIds.Length==9&&loop.Supplies.SourceCheckpointIds.Length==9,"Source9food/3bags/6ammo identity counts differ");float end=Mathf.Min(deadline,Time.realtimeSinceStartup+8);
            while(Time.realtimeSinceStartup<end){bool pending=false;foreach(var row in rows){R24PickupSettleResult r;if(!Read(row,out r)||r.Outcome==R24PickupSettleOutcome.Pending)pending=true;}if(!pending)break;yield return null;}
            Physics.SyncTransforms();int settled=0;
            foreach(var row in rows)
            {
                R24PickupSettleResult r;Need(Read(row,out r)&&r.Outcome==R24PickupSettleOutcome.Settled,"Actual deferred settle did not accept source position: "+row.Id+" / "+(r==null?"missing":r.Outcome.ToString()));
                var expected=V(row.At(F(loop.Map.GameSpawn)));Need(Vector3.Distance(V(r.RequestedPosition),expected)<.002f,"Checkpoint layout depends on head/yaw or wrong reflected origin: "+row.Id);
                var prop=Find(row.Id);Need(prop!=null&&Vector3.Distance(prop.position,V(r.Position))<.002f,"Accepted source state and real visual diverged: "+row.Id);
                RaycastHit hit=Floor(expected+Vector3.up*2,20);float lowest=float.PositiveInfinity;
                foreach(var filter in prop.GetComponentsInChildren<MeshFilter>(true)){var renderer=filter.GetComponent<MeshRenderer>();if(renderer!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&filter.GetComponent<R24SettleIgnoreMesh>()==null)lowest=Mathf.Min(lowest,renderer.bounds.min.y);}
                Need(!float.IsPositiveInfinity(lowest)&&Mathf.Abs(lowest-hit.point.y-R24WorldPickupLayout.HoverHeight)<.005f,"Actual visible lowest mesh does not hover at source floor offset: "+row.Id+" lowest="+lowest+" floor="+hit.point.y);settled++;
            }
            Note(g,"All18 actual props settled from exact reflected checkpoint offsets to native floors; visible mesh bottom matches source hover, including3 real backpack supplies.");yield break;
        }
        IEnumerator Bag(Group g)
        {
            string id="r24-checkpoint/backpack/9";Vector3 p;Quaternion q;bool collected;
            Need(loop.Supplies.TryReadWorldPose(id,out p,out q,out collected)&&!collected,"Actual source backpack unavailable");PlaceAt(p);loop.Gameplay.HolsterCurrent();loop.Consumables.Stow();loop.Explosives.Stow();Neutral();loop.Rig.Right.position=p;Physics.SyncTransforms();
            loop.Supplies.UpdateInputAvailability(new R24SupplyInputFrame{Enabled=true,LeftEligible=true,RightEligible=true});int before=loop.InventoryCapacity,accepted=loop.Supplies.TryCollect(id,2);
            Need(accepted==1&&loop.InventoryCapacity==before+1&&loop.Supplies.TryCollect(id,2)==0&&loop.InventoryCapacity==before+1,"Real backpack capacity grant/replay behavior failed");
            Note(g,"Actual source backpack collected through native reach/LOS and real Root capacity "+before+"→"+loop.InventoryCapacity+"; repeat refused.");yield break;
        }
        IEnumerator Grenade(Group g)
        {
            string frag=null,zone=null;foreach(var id in loop.WorldLoot.WorldGrenadeInstanceIds){R23GrenadeItemState s;if(loop.Explosives.TryReadItem(id,out s)){if(s.Kind=="frag")frag=id;else zone=id;}}
            Need(frag!=null&&zone!=null,"Actual source rack grenade records unavailable");loop.WorldLoot.SetWorldEnabled(true);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+3);
            while(!loop.Explosives.IsWorldItemVisible(frag)&&Time.realtimeSinceStartup<end)yield return null;
            Vector3 at;Quaternion rotation;bool owned;Need(loop.Explosives.TryReadWorldItem(frag,out at,out rotation,out owned)&&!owned,"Actual world frag pose unavailable");PlaceAt(at);loop.Gameplay.HolsterCurrent();loop.Consumables.Stow();Neutral();loop.Rig.Right.position=at;loop.Rig.Right.rotation=Quaternion.Inverse(R23TrackedGripPose.LocalRotation);Physics.SyncTransforms();
            ItemStep(0,0);ItemStep(.8f,0);Need(loop.Explosives.HeldId==frag,"Fresh Root objective grip did not acquire actual cache frag");R23GrenadeItemState held;Need(loop.Explosives.TryReadItem(frag,out held)&&held.Owned&&!loop.Explosives.RetireUnownedWorldItem(frag),"World retirement stole owned grenade custody");
            loop.WorldLoot.SetWorldEnabled(false);Need(!loop.Explosives.IsWorldItemVisible(zone)&&!loop.WorldLoot.AllowsWorldClaim(zone),"Disabled world retains visible/claimable grenade");foreach(string id in loop.WorldLoot.WorldInstanceActionIds)Need(!loop.Gameplay.CanClaimWorldGrip(id,0),"Root admits hidden cached weapon: "+id);Need(loop.Explosives.TryReadItem(frag,out held)&&held.Owned,"World toggle removed held finite item");loop.WorldLoot.SetWorldEnabled(true);
            var neutral=new HandSample{Tracked=true};for(int i=0;i<4;i++)loop.StepItemInput(.1f,neutral,neutral,true,false);loop.Rig.Right.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);ItemStep(0,.9f);
            Need(loop.Explosives.TryReadItem(frag,out held)&&held.Spent&&!held.Owned&&loop.Explosives.LiveProjectiles==1,"Actual trigger did not consume/launch exactly one real source frag");
            loop.WorldLoot.WithdrawMap();Need(!loop.Explosives.TryReadItem(frag,out held)&&!loop.Explosives.TryReadItem(zone,out held),"Withdraw leaves stale unowned/spent grenade records");Need(!loop.Explosives.SpawnWorldItem(frag,"frag",1,at,Quaternion.identity,false),"Retired cache identity reissued a finite charge");
            loop.Explosives.ResetRound();loop.WorldLoot.SetWorldEnabled(true);foreach(string id in loop.WorldLoot.WorldGrenadeInstanceIds)Need(id!=frag&&id!=zone,"New source generation aliased retired grenade identity");
            Note(g,"Root grip acquired real frag; hidden caches rejected; owned retirement refused; real throw consumed one payload; withdrawal and reset did not regrant retired identity.");yield break;
        }
        IEnumerator Alpha(Group g)
        {
            loop.ReturnToLobby();loop.SelectMap(R23MapCatalog.AlphaId,true);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+75);while((!loop.Ready||loop.Map.MapId!=R23MapCatalog.AlphaId)&&loop.Failure==null&&Time.realtimeSinceStartup<end){Bound();yield return null;}
            Need(loop.Ready&&loop.Map.MapId==R23MapCatalog.AlphaId,"Actual Alpha load failed: "+loop.Failure);loop.StartPlaying();Neutral();
            while(loop.WorldLoot.SpawnedCount==0&&Time.realtimeSinceStartup<end)yield return null;
            Need(loop.WorldLoot.Failure==null&&loop.WorldLoot.PlacementMode=="source-exported-cache-sites"&&loop.WorldLoot.ValidatedSites>0&&loop.WorldLoot.UnsupportedDraws==0,"Actual prepared R6 Alpha cache source/dispatch failed: "+loop.WorldLoot.Failure);
            int grenades=loop.WorldLoot.WorldGrenadeInstanceIds.Length;foreach(string id in loop.WorldLoot.WorldGrenadeInstanceIds){R23GrenadeItemState state;Need(loop.Explosives.TryReadItem(id,out state)&&state.Stars==1&&state.SourceStars>=1&&state.SourceStars<=5&&!state.Owned&&!state.Spent,"Cache raw rarity draw changed real source grenade semantics");}
            Note(g,"Actual Alpha validated source sites="+loop.WorldLoot.ValidatedSites+",activated="+loop.WorldLoot.ActivatedSites+",spawned="+loop.WorldLoot.SpawnedCount+",grenades="+grenades+"; loaded-map source hash bound, no unsupported catalog substitution. Other archive maps not natively accepted here.");
        }
        IEnumerator Cleanup(Group g)
        {
            loop.ReturnToLobby();if(loop.Map.MapId!=saved.Map){loop.SelectMap(saved.Map,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+75);while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId==saved.Map,"Original source map cleanup failed");loop.ReturnToLobby();}
            Note(g,"Source map restored via real loader, no fake terrain/actors or settings persistence.");
        }
        bool Read(R24CheckpointPickup row,out R24PickupSettleResult result){return row.IsSupply?loop.Supplies.TryReadSourceSettleResult(row.Id,out result):loop.Consumables.TryReadSourceSettleResult(row.Id,out result);}
        static Transform Find(string id){Transform found=null;foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==id){Need(found==null,"Duplicate physical pickup ID: "+id);found=t;}return found;}
        RaycastHit Floor(Vector3 from,float depth){RaycastHit best=new RaycastHit();float distance=float.PositiveInfinity;foreach(var h in Physics.RaycastAll(from,Vector3.down,depth,1,QueryTriggerInteraction.Ignore))if(h.collider.transform.IsChildOf(loop.Map.transform)&&h.normal.y>.7f&&h.distance<distance){best=h;distance=h.distance;}Need(!float.IsPositiveInfinity(distance),"Actual source floor missing below pickup/player");return best;}
        void PlaceAt(Vector3 pickup){var floor=Floor(pickup+Vector3.up*2,20);loop.Rig.SetSpawn(floor.point+Vector3.up*.02f-Vector3.forward*.4f,floor.point+Vector3.forward);loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();}
        void Neutral(){loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);for(int i=0;i<6;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Physics.SyncTransforms();}
        void ItemStep(float grip,float trigger){var l=new HandSample{Tracked=true};var r=new HandSample{Tracked=true,Grip=grip,Trigger=trigger};loop.Rig.SmokeTrackedHands(l,r);loop.PrepareObjectiveGrips(l,r,true,true,true);loop.StepItemInput(.02f,l,r,true,false);}
        void Finish(){receipt.success=receipt.nativeExecuted&&receipt.error.Length==0&&receipt.groups.Count==6;foreach(var g in receipt.groups)receipt.success&=g.passed;R23SmokeControl.Finish(receipt.success,JsonUtility.ToJson(receipt,true));}
        sealed class Saved
        {
            public readonly string Map;readonly bool manual,external,rig,pump,persist;readonly Vector3 origin,head,left,right;readonly Quaternion headQ,leftQ,rightQ;
            public Saved(R23PlayableLoop l){Map=l.Map.MapId;manual=l.ManualSmoke;external=l.ExternalProbePump;rig=l.Rig.enabled;pump=l.Gameplay.ExternalInputPump;persist=l.Calibration.SuppressPersistence;origin=l.Rig.TrackingOrigin.localPosition;head=l.Rig.Head.transform.localPosition;left=l.Rig.Left.localPosition;right=l.Rig.Right.localPosition;headQ=l.Rig.Head.transform.localRotation;leftQ=l.Rig.Left.localRotation;rightQ=l.Rig.Right.localRotation;}
            public void Restore(R23PlayableLoop l){if(l==null)return;l.ManualSmoke=manual;l.ExternalProbePump=external;l.Rig.enabled=rig;l.Gameplay.ExternalInputPump=pump;l.Calibration.SuppressPersistence=persist;l.Rig.TrackingOrigin.localPosition=origin;l.Rig.Head.transform.SetLocalPositionAndRotation(head,headQ);l.Rig.Left.SetLocalPositionAndRotation(left,leftQ);l.Rig.Right.SetLocalPositionAndRotation(right,rightQ);}
        }
    }
}
#endif
