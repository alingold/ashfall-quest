#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;

namespace Ashfall.Editor.Playable
{
    // A callable native oracle, not an automatic route. Compilation does not
    // establish PhysX ray hits, rendered art/audio or headset behavior.
    public static class R24Dt11GameplayFixture
    {
        static void Need(bool value,string why)
        {if(!value)throw new InvalidOperationException("DT11 native gameplay: "+why);}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
        static Vector3 V(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
        static void Note(Action<string> note,string value)
        {Debug.Log("R24 DT11 "+value);if(note!=null)note(value);}

        public static IEnumerator Run(R23PlayableLoop loop,Action<string> note,Action<string> capture=null)
        {
            Need(loop!=null&&loop.Ready&&loop.Map.IsLoaded&&loop.Gameplay.Ready,"actual loaded playable scene unavailable");
            Need(!UnityEngine.XR.XRSettings.isDeviceActive,"XR-off required; never replace live headset poses");
            var h=loop.Gameplay;var rig=loop.Rig;
            Need(h.Dt11Ready&&h.PracticeTargetCount>0,"real DT11/source practice presentation unavailable");
            var saved=new Saved(loop);float deadline=Time.realtimeSinceStartup+55;
            Action checkTime=delegate{Need(Time.realtimeSinceStartup<deadline,"bounded55s fixture deadline exceeded");};
            string action=null,instance=null;int observedGroups=0;float observedDamage=0;
            Action<string,bool,float,float> damaged=delegate(string id,bool head,float shield,float health){if(id=="target/0"){observedGroups++;observedDamage+=shield+health;}};
            h.TargetDamaged+=damaged;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;rig.enabled=false;h.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);loop.RestartRound();rig.GameplayBlocked=false;
                h.NotifyFocus(true);h.SetGameplayPaused(false);h.SetWeaponPresentationSuppressed(false);
                // Source settings are restored below. Disable aim filtering only
                // for deterministic simulated tracked poses, never alter bore fit.
                h.SetWeaponTuning(0,0);
                Vector3 forward=Vector3.ProjectOnPlane(rig.Head.transform.forward,Vector3.up).normalized;
                if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
                Bounds initialBounds;Vector3 initialDrop=Vector3.zero;Quaternion initialRotation=Quaternion.identity;
                Need(h.TryGetWeaponBounds(h.ActiveWeaponActionId,out initialBounds)&&R23WorldItemPlacement.TryDrop(rig.Head.transform,rig.transform,initialBounds,out initialDrop,out initialRotation),"actual player site cannot support source full-slot replacement drop; no replacement floor supplied");
                int initialReserve=h.ReadAmmoReserveFamily("shells");
                Vector3 spawn=rig.Head.transform.position+forward*.85f-Vector3.up*.22f;
                Need(ClearMap(loop,rig.Head.transform.position,spawn),"actual head-to-pickup lane is map-obstructed");
                Need(h.SpawnWorldWeapon("dt11",1,spawn,Quaternion.LookRotation(forward),out action,true),"real independent DT11 construction refused");
                string worldInstance;Vector3 center,position;Quaternion rotation;
                Need(h.TryReadWorldWeapon(action,out worldInstance,out center,out position,out rotation),"world action/instance absent");instance=worldInstance;
                R23WeaponInstanceInfo info;
                Need(h.TryReadWeaponInstance(action,out info)&&info.InstanceId==instance&&info.ProfileId=="dt11"&&info.Capacity==2&&info.Stars==1,"actual world metadata differs from source DT11 instance");
                Pose(rig.Right,center-forward*.25f,forward);rig.Left.position=rig.Head.transform.position-Vector3.Cross(Vector3.up,forward)*.4f-Vector3.up*.35f;Physics.SyncTransforms();
                PumpPickup(loop,0);string aimed;Vector3 aimedPoint;float score;
                Need(h.TryReadAimedWorldWeapon(1,out aimed,out aimedPoint,out score)&&aimed==action,"actual source aim selected another/no world pickup: "+aimed);
                PumpPickup(loop,.8f);
                Need(h.HasWeapon&&h.ActiveWeaponActionId==action&&h.WeaponInstanceId==instance&&h.Ammo==2&&h.ReadAmmoReserveFamily("shells")==initialReserve,"Root accepted pickup did not preserve exact native2-shell identity/reserve");
                Note(note,"Actual fresh-grip world DT11 "+action+" / "+instance+"; source finite shells "+initialReserve+"; full-slot replacement used authored floor "+initialDrop);
                checkTime();

                Pose(rig.Right,rig.Head.transform.position+forward*.23f-Vector3.up*.24f,forward);Tick(loop,.02f);
                Vector3 floor;Need(PlaceClearTarget(loop,forward,out floor),"no real authored floor/clear existing target volume and source eight-pellet lane; no fabricated target/geometry supplied");
                Note(note,"Actual target source floor "+floor+" / map "+loop.Map.MapId+" / head "+rig.Head.transform.position);Aim(loop);PreflightPellets(loop);
                var target=h.ReadPracticeTarget(0);float beforeVital=target.Health+target.Shield;long shots=h.ReadWeaponState(action).Shots;
                Need(target.Health==100&&target.Shield==100,"actual target did not start100 health/100 shield");
                bool fired=Tick(loop,.02f,0,.8f,1);State(loop,note,"first trigger");Need(fired&&h.Ammo==1&&h.ReadWeaponState(action).Shots==shots+1,"first genuine tracked trigger failed one-shell native debit");
                AssertPellets(loop,note);target=h.ReadPracticeTarget(0);
                Need(Mathf.Abs(beforeVital-target.Health-target.Shield-96)<.01f&&observedGroups==1&&Mathf.Abs(observedDamage-96)<.01f,"actual eight12-damage pellet rays did not produce one96 Health group");
                Note(note,"Shot1 actual target100/100 -> "+target.Health+"/"+target.Shield+"; native loaded1, eight collider-clipped rays, one Health group96");
                if(capture!=null)capture("dt11-native-first-shot");
                for(int i=0;i<4;i++)Need(!Tick(loop,.1f,0,.8f,1)&&h.Ammo==1&&h.ReadWeaponState(action).Shots==shots+1,"semi held trigger discharged second barrel");
                Tick(loop,.2f);Aim(loop);PreflightPellets(loop);
                fired=Tick(loop,.02f,0,.8f,1);State(loop,note,"second trigger");Need(fired&&h.Ammo==0&&h.ReadWeaponState(action).Shots==shots+2&&h.ReloadStage==ReloadPhase.NeedsMagazine,"fresh second trigger failed real final-shell auto break-open");
                AssertPellets(loop,note);Need(observedGroups==2,"second accepted volley did not apply actual target Health");
                Need(h.Dt11LiveMask==0&&h.Dt11SpentMask==0&&h.Reserve==initialReserve,"empty automatic break changed finite reserve/live chambers");
                checkTime();yield return null;checkTime();

                // Tracked support path only: no Reload(), stickclick or desktop
                // ReloadStep. Source physical stroke is scaled local DOWN then UP.
                Tick(loop,.02f);rig.Left.position=h.ReloadOfferPosition;Physics.SyncTransforms();Tick(loop,.02f,.8f,.8f);
                State(loop,note,"support grabbed offered pair");Need(h.ReloadHandOccupied&&h.ReloadStage==ReloadPhase.NeedsMagazine&&h.Reserve==initialReserve,"actual source offered pair grip failed or debited early");
                Quaternion gun=rig.Right.rotation*R23TrackedGripPose.LocalRotation;
                rig.Left.position-=gun*Vector3.up*.04f;Physics.SyncTransforms();Tick(loop,.02f,.8f,.8f);
                State(loop,note,"physical DOWN");Need(h.ReloadStage==ReloadPhase.NeedsRack&&h.ReadWeaponState(action).Seated==2&&h.Dt11LiveMask==3&&h.Reserve==initialReserve-2,"actual physical DOWN did not seat exactly two finite shared shells");
                rig.Left.position+=gun*Vector3.up*.044f;Physics.SyncTransforms();Tick(loop,.02f,.8f,.8f);
                State(loop,note,"physical UP");Need(h.ReloadStage==ReloadPhase.Ready&&h.Ammo==2&&h.Reserve==initialReserve-2,"same-grip source UP failed to close actual breech");
                Note(note,"Actual support grip DOWN.04m / UP.044m closes native2; shared reserve "+initialReserve+" -> "+h.Reserve+"; no desktop reload input");
                Tick(loop,.2f);Aim(loop);PreflightPellets(loop);var beforeThird=h.ReadWeaponState(action);target=h.ReadPracticeTarget(0);float thirdVital=target.Health+target.Shield;int groupsBeforeThird=observedGroups;State(loop,note,"post-reload BEFORE trigger");
                fired=Tick(loop,.02f,0,.8f,1);State(loop,note,"post-reload AFTER trigger / returned="+fired);var afterThird=h.ReadWeaponState(action);
                Need(h.ActiveWeaponActionId==action&&h.WeaponInstanceId==instance&&afterThird.Shots==beforeThird.Shots+1&&afterThird.Loaded==1&&h.Reserve==initialReserve-2&&h.LastAcceptedShotActionId==action&&h.LastAcceptedNativeShot==afterThird.Shots,"post-reload same native owner did not accept exactly one chamber");
                AssertPellets(loop,note);target=h.ReadPracticeTarget(0);Need(thirdVital>0&&Mathf.Abs(thirdVital-target.Health-target.Shield-Mathf.Min(96,thirdVital))<.01f&&observedGroups==groupsBeforeThird+1,"post-reload actual pellet volley did not apply native target Health");Tick(loop,.02f);

                var spent=h.ReadWeaponState(action);int reserve=h.Reserve,live=h.Dt11LiveMask,spentMask=h.Dt11SpentMask;
                Bounds bounds;Vector3 drop=Vector3.zero;Quaternion dropRotation=Quaternion.identity;
                Need(h.TryGetWeaponBounds(action,out bounds)&&R23WorldItemPlacement.TryDrop(rig.Head.transform,rig.transform,bounds,out drop,out dropRotation),"actual spent DT11 has no source reachable supported drop");
                Need(h.DropOwned(action,drop,dropRotation),"actual owned DT11 drop refused");
                Need(h.TryReadWorldWeapon(action,out worldInstance,out center,out position,out rotation)&&worldInstance==instance&&h.ReadWeaponState(action).Loaded==1&&h.ReadReserve(action)==reserve,"drop changed exact native chamber/reserve/identity");
                // The full-slot pickup left the outgoing CX4 on this same authored
                // drop site. Reach the retained copy itself; a hand .25m behind
                // it can legitimately score the other gun as nearer in source rules.
                Pose(rig.Right,center,(center-rig.Head.transform.position).normalized);Physics.SyncTransforms();PumpPickup(loop,0);
                string recollectAimed;Vector3 recollectPoint;float recollectScore;
                bool recollectFound=h.TryReadAimedWorldWeapon(1,out recollectAimed,out recollectPoint,out recollectScore);
                Note(note,"BEFORE recollect grip expected="+action+" aimed="+recollectAimed+" found="+recollectFound+" hand="+rig.Right.position+" copyCenter="+center+" selectedCenter="+recollectPoint+" score="+recollectScore);
                Need(recollectFound&&recollectAimed==action,"retained copy is not the actual source nearest pickup at its body center");PumpPickup(loop,.8f);
                State(loop,note,"AFTER recollect grip");
                Need(h.HasWeapon&&h.ActiveWeaponActionId==action&&h.WeaponInstanceId==instance&&h.Ammo==1&&h.Reserve==reserve&&h.Dt11LiveMask==live&&h.Dt11SpentMask==spentMask,"real fresh-grip recollect reset native barrels/reserve/identity");
                Need(h.HolsterCurrent()&&h.EquipOwned(action,1)&&h.Ammo==1&&h.Reserve==reserve&&h.ReadWeaponState(action).Shots==spent.Shots,"holster/re-equip refilled or reset actual native state");
                Note(note,"Actual supported drop/recollect/re-equip retained "+action+" / "+instance+" loaded1/live"+live+"/spent"+spentMask+"/reserve"+reserve);
                checkTime();loop.RestartRound();
                Need(!h.TryReadWeaponInstance(action,out info)&&h.ReadWeaponState("dt11").Loaded==2&&!h.OwnsWeapon("dt11")&&h.ReadAmmoReserveFamily("shells")==initialReserve,"actual restart did not retire dynamic copy/reset unowned default2/finite shell reserve");
                target=h.ReadPracticeTarget(0);Need(target.Health==100&&target.Shield==100,"actual restart did not reset native target Health");
                Note(note,"PASS actual DT11 pickup/finite semi/eight-ray Health/physical DOWN-UP/drop identity/reset; native art/audio/headset delivery remain separate");
            }
            finally
            {
                h.TargetDamaged-=damaged;saved.Restore(loop);Note(note,"DT11 fixture cleanup restored actual round, target poses, simulated tracking/input flags and source settings");
            }
        }

        static R23HandsInputFrame Frame(float left=0,float right=0,float trigger=0)
        {return new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftGrip=left,RightGrip=right,RightTrigger=trigger};}
        // Keep the already-held primary grip down. Trigger-neutral must not create
        // repeated fresh world-pickup edges beside the source full-slot drop.
        static bool Tick(R23PlayableLoop loop,float dt,float left=0,float right=.8f,float trigger=0)
        {return loop.Gameplay.TickInput(dt,Frame(left,right,trigger));}
        static void PumpPickup(R23PlayableLoop loop,float grip)
        {var left=new HandSample{Tracked=true,Position=F(loop.Rig.Left.position)};var right=new HandSample{Tracked=true,Grip=grip,Position=F(loop.Rig.Right.position)};loop.PrepareObjectiveGrips(left,right,true,true,true);loop.StepItemInput(.02f,left,right,true,false);}
        static void Pose(Transform hand,Vector3 position,Vector3 forward)
        {hand.SetPositionAndRotation(position,Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation));}
        static bool ClearMap(R23PlayableLoop loop,Vector3 a,Vector3 b)
        {Vector3 d=b-a;if(d.sqrMagnitude<.000001f)return true;foreach(var hit in Physics.RaycastAll(a,d.normalized,d.magnitude,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform))return false;return true;}
        static bool PlaceClearTarget(R23PlayableLoop loop,Vector3 forward,out Vector3 floor)
        {
            floor=Vector3.zero;var h=loop.Gameplay;Vector3 across=Vector3.Cross(Vector3.up,forward);
            foreach(float distance in new[]{2.5f,3f,2f})foreach(float side in new[]{0f,-1f,1f})
            {
                Vector3 probe=loop.Rig.Head.transform.position+forward*distance+across*side+Vector3.up;RaycastHit best=new RaycastHit();float nearest=float.PositiveInfinity;
                foreach(var hit in Physics.RaycastAll(probe,Vector3.down,8,1<<0,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.normal.y>.65f&&hit.distance<nearest){best=hit;nearest=hit.distance;}
                if(float.IsPositiveInfinity(nearest))continue;Quaternion rotation=Quaternion.LookRotation(-forward,Vector3.up);bool blocked=false;
                foreach(var c in Physics.OverlapBox(best.point+Vector3.up*.86f,new Vector3(.30f,.72f,.16f),rotation,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore))if(c.transform.IsChildOf(loop.Map.transform)){blocked=true;break;}
                if(blocked||!ClearMap(loop,loop.Rig.Head.transform.position,best.point+Vector3.up*1.16f))continue;
                var positions=new Vector3[h.PracticeTargetCount];for(int i=0;i<positions.Length;i++)positions[i]=best.point+Vector3.up*.02f+across*(i*2);h.RepositionTrainingTargets(positions,rotation);Physics.SyncTransforms();floor=best.point;return true;
            }
            return false;
        }
        static void Aim(R23PlayableLoop loop)
        {
            for(int i=0;i<8;i++){R23ShotPose pose;Need(loop.Gameplay.TryReadDisplayedShotPose(out pose),"actual displayed bore unavailable");Vector3 point=loop.Gameplay.PracticeAimPoint(0,false);loop.Rig.Right.rotation=Quaternion.FromToRotation(V(pose.Forward),(point-V(pose.Origin)).normalized)*loop.Rig.Right.rotation;Physics.SyncTransforms();Tick(loop,.025f);}
        }
        static void PreflightPellets(R23PlayableLoop loop)
        {
            var h=loop.Gameplay;R23ShotPose p;Need(h.TryReadDisplayedShotPose(out p),"actual accepted-pose predictor unavailable");Vector3 start=V(p.Origin);Need(ClearMap(loop,loop.Rig.Head.transform.position,start),"actual head-to-muzzle obstruction before shot");
            var directions=R24Dt11Volley.DirectionsFor(new R23M1014Pose{Origin=p.Origin,Forward=p.Forward,Right=p.Right,Up=p.Up});Collider body;Need(h.TryReadMeleeTargetCollider(0,out body),"actual training body collider unavailable");
            foreach(var d in directions){RaycastHit hit;Need(body.Raycast(new Ray(start,V(d)),out hit,8)&&ClearMap(loop,start,hit.point),"actual source pellet cone is not a clear <=8m body lane; refusing fixture placement");}
        }
        static void State(R23PlayableLoop loop,Action<string> note,string stage)
        {var h=loop.Gameplay;var target=h.ReadPracticeTarget(0);var native=h.ReadWeaponState(h.ActiveWeaponActionId);Note(note,stage+": action="+h.ActiveWeaponActionId+" instance="+h.WeaponInstanceId+" nativeShots="+native.Shots+" cooldown="+native.CooldownSeconds+" triggerRearm="+native.TriggerReleaseRequired+" loaded="+h.Ammo+" phase="+h.ReloadStage+" reserve="+h.Reserve+" live="+h.Dt11LiveMask+" spent="+h.Dt11SpentMask+" pellets="+h.LastShotgunPelletCount+" tracers="+h.ShotgunTracerCount+" groups="+h.LastShotgunTargetGroups+" target="+target.Health+"/"+target.Shield+" support="+loop.Rig.Left.position+" inputs="+h.Dt11ControlStatus);}
        static void AssertPellets(R23PlayableLoop loop,Action<string> note)
        {
            var h=loop.Gameplay;Need(h.LastShotgunPelletCount==8&&h.ShotgunTracerCount==8&&h.LastShotgunTargetGroups==1,"actual eight pellet rays/tracers/one Health group missing");Collider body;Need(h.TryReadMeleeTargetCollider(0,out body),"native target collider absent after discharge");
            R23ShotPose pose;Need(h.TryReadLastAcceptedShotPose(out pose),"last accepted real muzzle absent");
            for(int pellet=0;pellet<8;pellet++){Vector3 start,end;Need(h.TryReadShotgunTracer(pellet,out start,out end)&&Vector3.Distance(start,V(pose.Origin))<.0001f&&Vector3.Distance(body.ClosestPoint(end),end)<.003f,"actual pellet endpoint not on genuine target collider: "+pellet);}
            Note(note,"Accepted action "+h.LastAcceptedShotActionId+" nativeShot"+h.LastAcceptedNativeShot+" eight source rays from "+V(pose.Origin)+" on actual target bounds "+body.bounds);
        }
        sealed class Saved
        {
            readonly bool manual,external,enabled,pump,persist,lobby;readonly Vector3 root,origin,head,left,right;readonly Quaternion rootQ,headQ,leftQ,rightQ,targetQ;readonly Vector3[] targets;
            public Saved(R23PlayableLoop loop)
            {var rig=loop.Rig;manual=loop.ManualSmoke;external=loop.ExternalProbePump;enabled=rig.enabled;pump=loop.Gameplay.ExternalInputPump;persist=loop.Calibration.SuppressPersistence;lobby=loop.Lobby.Visible;root=rig.transform.position;rootQ=rig.transform.rotation;origin=rig.TrackingOrigin.localPosition;head=rig.Head.transform.localPosition;headQ=rig.Head.transform.localRotation;left=rig.Left.localPosition;leftQ=rig.Left.localRotation;right=rig.Right.localPosition;rightQ=rig.Right.localRotation;targets=new Vector3[loop.Gameplay.PracticeTargetCount];targetQ=Quaternion.identity;for(int i=0;i<targets.Length;i++){Collider body;Need(loop.Gameplay.TryReadMeleeTargetCollider(i,out body),"existing target absent before fixture writes");targets[i]=body.transform.position;if(i==0)targetQ=body.transform.rotation;}}
            public void Restore(R23PlayableLoop loop)
            {var h=loop.Gameplay;var rig=loop.Rig;h.NotifyFocus(true);h.SetGameplayPaused(false);loop.RestartRound();h.RepositionTrainingTargets(targets,targetQ);rig.transform.SetPositionAndRotation(root,rootQ);rig.TrackingOrigin.localPosition=origin;rig.Head.transform.SetLocalPositionAndRotation(head,headQ);rig.Left.SetLocalPositionAndRotation(left,leftQ);rig.Right.SetLocalPositionAndRotation(right,rightQ);h.SetWeaponTuning(loop.UI.Settings.MotionDamping,loop.UI.Settings.OffhandInfluence);if(lobby)loop.ReturnToLobby();loop.Calibration.SuppressPersistence=persist;h.ExternalInputPump=pump;rig.enabled=enabled;loop.ExternalProbePump=external;loop.ManualSmoke=manual;Physics.SyncTransforms();}
        }
    }
}
#endif
