#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;

namespace Ashfall.Editor.Playable
{
    // XR-off callable oracle over the real scene and production paired owners.
    // API compilation is not evidence of native rays, art/audio or headset delivery.
    public static class R24DualGameplayFixture
    {
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static void Note(Action<string> note,string message){Debug.Log("R24 DUAL "+message);if(note!=null)note(message);}
        static void Check(Action<string> note,bool okay,string why)
        {Note(note,"CHECK "+why+" => "+okay);if(!okay)throw new InvalidOperationException("Dual native gameplay: "+why);}

        public static IEnumerator Run(R23PlayableLoop loop,Action<string> note,Action<string> capture=null)
        {
            Check(note,loop!=null&&loop.Ready&&loop.Gameplay.Ready&&loop.Map.IsLoaded,"actual playable scene is ready");
            Check(note,!UnityEngine.XR.XRSettings.isDeviceActive,"XR-off; never replace live headset/controller poses");
            var h=loop.Gameplay;var rig=loop.Rig;Check(note,h.PracticeTargetCount>0,"existing genuine practice targets are available");
            var saved=new Saved(loop,note);float end=Time.realtimeSinceStartup+65;
            Action bound=delegate{Check(note,Time.realtimeSinceStartup<end,"65-second bound");};
            int damageGroups=0;float damageAmount=0;
            Action<string,bool,float,float> observed=delegate(string id,bool head,float shield,float health){if(id=="target/0"){damageGroups++;damageAmount+=shield+health;}};
            h.TargetDamaged+=observed;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;rig.enabled=false;h.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                foreach(string profile in new[]{"uzis","matadors"})
                {
                    bound();Note(note,"BEGIN actual "+profile);
                    loop.StartPlaying();loop.WorldLoot.SetWorldEnabled(false);loop.RestartRound();rig.GameplayBlocked=false;
                    h.NotifyFocus(true);h.SetGameplayPaused(false);h.SetWeaponPresentationSuppressed(false);h.SetWeaponTuning(0,0);
                    damageGroups=0;damageAmount=0;var definition=new R24DualDefinition(profile,1);
                    int reserveBefore=h.ReadAmmoReserveFamily(definition.AmmoFamily),expectedReserve=profile=="uzis"?1200:480;
                    Check(note,reserveBefore==expectedReserve,"actual initial family "+definition.AmmoFamily+" reserve="+reserveBefore);
                    Bounds firstBounds;Vector3 firstDrop=Vector3.zero;Quaternion firstRotation=Quaternion.identity;
                    Check(note,h.TryGetWeaponBounds(h.ActiveWeaponActionId,out firstBounds)&&R23WorldItemPlacement.TryDrop(rig.Head.transform,rig.transform,firstBounds,out firstDrop,out firstRotation),"authored player floor supports actual full-slot outgoing drop");
                    Vector3 forward=Vector3.ProjectOnPlane(rig.Head.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
                    Vector3 across=Vector3.Cross(Vector3.up,forward),spawn=rig.Head.transform.position+forward*.8f-Vector3.up*.2f;
                    Check(note,ClearMap(loop,rig.Head.transform.position,spawn),"actual head-to-world-copy lane clear");
                    string action;bool spawned=h.SpawnWorldWeapon(profile,1,spawn,Quaternion.LookRotation(forward),out action,true);
                    Note(note,"Spawn actual "+profile+" action="+action+" accepted="+spawned);Check(note,spawned,"real independent paired world copy created");
                    string instance;Vector3 center,worldPosition;Quaternion worldRotation;
                    Check(note,h.TryReadWorldWeapon(action,out instance,out center,out worldPosition,out worldRotation),"typed actual world identity/center available");
                    string nativeInstance=instance;R23WeaponInstanceInfo metadata;
                    Check(note,h.TryReadWeaponInstance(action,out metadata)&&metadata.ProfileId==profile&&metadata.InstanceId==instance,"exact native copy/profile identity");
                    Pose(rig.Left,rig.Head.transform.position-across*.35f-Vector3.up*.3f,forward);
                    Pose(rig.Right,center,forward);Physics.SyncTransforms();Pump(loop,0);Nearest(loop,note,action,center);Pump(loop,.8f);
                    State(loop,note,"AFTER source world grip");
                    Check(note,h.ActiveWeaponActionId==action&&h.WeaponInstanceId==instance&&h.HasWeapon&&h.PrimaryHand==1,"Root accepted exact paired world action/native identity");
                    Check(note,h.DualReady&&h.DualVisuals!=null&&h.DualVisuals.Visible&&h.DualVisuals.Model(0)!=h.DualVisuals.Model(1),"two genuine independent source models visible");
                    Check(note,h.LeftHandHeld&&h.RightHandHeld&&h.LeftHandOccupied&&h.RightHandOccupied,"one owned inventory instance occupies both tracked hands");
                    R24DualHandState initialLeft,initialRight;Read(loop,note,out initialLeft,out initialRight);
                    Check(note,initialLeft.Loaded==definition.Capacity&&initialRight.Loaded==definition.Capacity&&h.ReadAmmoReserveFamily(definition.AmmoFamily)==reserveBefore,"per-hand native initial capacities without family debit");
                    Pose(rig.Right,rig.Head.transform.position+forward*.23f+across*.18f-Vector3.up*.24f,forward);
                    Pose(rig.Left,rig.Head.transform.position+forward*.23f-across*.18f-Vector3.up*.24f,forward);Physics.SyncTransforms();Tick(loop,.02f);
                    Vector3 floor;bool lane=PlaceTarget(loop,forward,out floor);Note(note,"Actual target lane found="+lane+" floor="+floor+" map="+loop.Map.MapId+" head="+rig.Head.transform.position);
                    Check(note,lane,"real source floor/target volume available; no substitute platform or target");AimBoth(loop,note);
                    R23ShotPose leftPose=new R23ShotPose(),rightPose=new R23ShotPose();Check(note,h.TryReadDualShotPose(0,out leftPose)&&h.TryReadDualShotPose(1,out rightPose),"both physical-hand shot poses valid");
                    Note(note,"Actual accepted-pose origins left="+V(leftPose.Origin)+" right="+V(rightPose.Origin));
                    Check(note,Vector3.Distance(V(leftPose.Origin),V(rightPose.Origin))>.1f,"two guns use distinct actual tracked-hand muzzle origins");
                    Preflight(loop,note,definition,0);Preflight(loop,note,definition,1);

                    FireOne(loop,note,action,definition,1,ref damageGroups,ref damageAmount);
                    FireOne(loop,note,action,definition,0,ref damageGroups,ref damageAmount);
                    R24DualHandState firedLeft,firedRight;Read(loop,note,out firedLeft,out firedRight);
                    Check(note,firedLeft.Loaded==definition.Capacity-1&&firedRight.Loaded==definition.Capacity-1&&firedLeft.Shots==1&&firedRight.Shots==1&&h.ReadAmmoReserveFamily(definition.AmmoFamily)==reserveBefore,"independent triggers debited exactly their own native magazines; no reserve per shot");
                    Check(note,damageGroups==2&&Mathf.Abs(damageAmount-definition.Body*2)<.01f,"both genuine production gun rays applied exact source body damage through native Health");
                    if(capture!=null)capture("dual-"+profile+"-independent-hands");
                    bound();yield return null;bound();

                    // Both hands stay occupied by the real paired item. No
                    // support-magazine, stick click or desktop Reload shortcut.
                    for(int i=0;i<5;i++)Tick(loop,.02f);
                    State(loop,note,"BEFORE right occupied-hand UP flick");rig.Right.position+=Vector3.up*.10f;Physics.SyncTransforms();Tick(loop,.08f);State(loop,note,"AFTER right occupied-hand UP flick");
                    R24DualHandState reloadedLeft,reloadedRight;Read(loop,note,out reloadedLeft,out reloadedRight);
                    Check(note,reloadedRight.Loaded==definition.Capacity&&reloadedLeft.Loaded==definition.Capacity-1&&reloadedRight.SpinElapsed<.001f&&h.ReadAmmoReserveFamily(definition.AmmoFamily)==reserveBefore-1,"source right UP flick refilled only right gun from same finite family");
                    State(loop,note,"BEFORE left occupied-hand UP flick");rig.Left.position+=Vector3.up*.10f;Physics.SyncTransforms();Tick(loop,.08f);State(loop,note,"AFTER left occupied-hand UP flick");Read(loop,note,out reloadedLeft,out reloadedRight);
                    Check(note,reloadedLeft.Loaded==definition.Capacity&&reloadedRight.Loaded==definition.Capacity&&reloadedLeft.SpinElapsed<.001f&&h.ReadAmmoReserveFamily(definition.AmmoFamily)==reserveBefore-2,"source left UP flick refilled only left gun; two spent rounds net finite debit2");
                    for(int i=0;i<4;i++)Tick(loop,.1f);AimBoth(loop,note);Preflight(loop,note,definition,1);
                    FireOne(loop,note,action,definition,1,ref damageGroups,ref damageAmount);
                    R24DualHandState retainedLeft,retainedRight;Read(loop,note,out retainedLeft,out retainedRight);int reserve=h.ReadAmmoReserveFamily(definition.AmmoFamily);
                    Bounds bounds;Vector3 drop=Vector3.zero;Quaternion rotation=Quaternion.identity;
                    Check(note,h.TryGetWeaponBounds(action,out bounds)&&R23WorldItemPlacement.TryDrop(rig.Head.transform,rig.transform,bounds,out drop,out rotation),"actual paired geometry has genuine reachable supported drop");
                    State(loop,note,"BEFORE actual paired drop");bool dropped=h.DropOwned(action,drop,rotation);Note(note,"Drop accepted="+dropped+" pose="+drop);Check(note,dropped,"exact paired inventory instance dropped");
                    Check(note,h.TryReadWorldWeapon(action,out instance,out center,out worldPosition,out worldRotation)&&instance==nativeInstance,"dropped copy retained native identity");
                    Pose(rig.Right,center,forward);Physics.SyncTransforms();Pump(loop,0);Nearest(loop,note,action,center);Pump(loop,.8f);State(loop,note,"AFTER exact-center recollect");
                    Check(note,h.HasWeapon&&h.ActiveWeaponActionId==action&&h.WeaponInstanceId==nativeInstance&&h.LeftHandHeld&&h.RightHandHeld,"real fresh grip recollected exact paired instance with both hands");
                    R24DualHandState collectedLeft,collectedRight;Read(loop,note,out collectedLeft,out collectedRight);
                    Check(note,Same(retainedLeft,collectedLeft)&&Same(retainedRight,collectedRight)&&h.ReadAmmoReserveFamily(definition.AmmoFamily)==reserve,"drop/recollect preserved both native magazines/shot counters and real family reserve");
                    bool holstered=h.HolsterCurrent(),equipped=holstered&&h.EquipOwned(action,1);State(loop,note,"AFTER real holster/re-equip");Check(note,holstered&&equipped,"same owned paired copy holstered/re-equipped");Read(loop,note,out collectedLeft,out collectedRight);
                    Check(note,Same(retainedLeft,collectedLeft)&&Same(retainedRight,collectedRight)&&h.ReadAmmoReserveFamily(definition.AmmoFamily)==reserve,"re-equip did not refill either gun");
                    bound();Note(note,"PASS actual "+profile+" native owner/rays/Health/occupied-hand flick/finite reserve/copy persistence; art/audio/headset comparison remains separate");
                }
            }
            finally
            {h.TargetDamaged-=observed;saved.Restore(loop);Note(note,"Actual dual fixture cleanup restored source round, target poses, settings, tracked local poses and input flags");}
        }

        static bool Same(R24DualHandState a,R24DualHandState b){return a.Loaded==b.Loaded&&a.Capacity==b.Capacity&&a.Shots==b.Shots&&a.PhysicalHand==b.PhysicalHand;}
        static void Read(R23PlayableLoop loop,Action<string> note,out R24DualHandState left,out R24DualHandState right)
        {bool l=loop.Gameplay.TryReadDualHandState(0,out left),r=loop.Gameplay.TryReadDualHandState(1,out right);Check(note,l&&r,"actual physical left/right native state readable");}
        static void State(R23PlayableLoop loop,Action<string> note,string phase)
        {var h=loop.Gameplay;R24DualHandState left,right;bool l=h.TryReadDualHandState(0,out left),r=h.TryReadDualHandState(1,out right);var target=h.ReadPracticeTarget(0);Note(note,phase+" action="+h.ActiveWeaponActionId+" instance="+h.WeaponInstanceId+" held="+h.HasWeapon+" leftValid="+l+" left="+JsonUtility.ToJson(left)+" rightValid="+r+" right="+JsonUtility.ToJson(right)+" target="+target.Health+"/"+target.Shield+" smg="+h.ReadAmmoReserveFamily("smg")+" shells="+h.ReadAmmoReserveFamily("shells")+" rawLeft="+loop.Rig.Left.position+" rawRight="+loop.Rig.Right.position);}
        static void FireOne(R23PlayableLoop loop,Action<string> note,string action,R24DualDefinition definition,int hand,ref int groups,ref float amount)
        {
            var h=loop.Gameplay;Tick(loop,.02f);Preflight(loop,note,definition,hand);R24DualHandState left,right;Read(loop,note,out left,out right);var own=hand==0?left:right;var other=hand==0?right:left;
            var health=h.ReadPracticeTarget(0);float vital=health.Health+health.Shield;int oldGroups=groups;float oldAmount=amount;R23ShotPose before;Check(note,h.TryReadDualShotPose(hand,out before),"actual firing hand pose available");State(loop,note,"BEFORE physical "+hand+" trigger");
            bool returned=Tick(loop,.005f,hand==0?1:0,hand==1?1:0);State(loop,note,"AFTER physical "+hand+" trigger returned="+returned);Read(loop,note,out left,out right);var after=hand==0?left:right;var unchanged=hand==0?right:left;
            Check(note,h.ActiveWeaponActionId==action&&after.Loaded==own.Loaded-1&&after.Shots==own.Shots+1&&Same(other,unchanged),"trigger changed exactly its own real native gun");
            R23ShotPose accepted;Check(note,h.TryReadLastAcceptedShotPose(out accepted)&&h.LastAcceptedShotActionId==action&&Vector3.Distance(V(accepted.Origin),V(before.Origin))<.002f,"accepted volley used actual independent hand muzzle");
            health=h.ReadPracticeTarget(0);float expected=Mathf.Min(definition.Body,vital);
            Check(note,vital>0&&groups==oldGroups+1&&Mathf.Abs(amount-oldAmount-expected)<.01f&&Mathf.Abs(vital-health.Health-health.Shield-expected)<.01f,"real accepted gun/pellet rays applied exact native Health body loss");Tick(loop,.02f);
        }
        static R23HandsInputFrame Frame(float leftTrigger=0,float rightTrigger=0)
        {return new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftGrip=.8f,RightGrip=.8f,LeftTrigger=leftTrigger,RightTrigger=rightTrigger};}
        static bool Tick(R23PlayableLoop loop,float dt,float leftTrigger=0,float rightTrigger=0){return loop.Gameplay.TickInput(dt,Frame(leftTrigger,rightTrigger));}
        static void Pump(R23PlayableLoop loop,float rightGrip)
        {var left=new HandSample{Tracked=true,Position=F(loop.Rig.Left.position)};var right=new HandSample{Tracked=true,Grip=rightGrip,Position=F(loop.Rig.Right.position)};loop.PrepareObjectiveGrips(left,right,true,true,true);loop.StepItemInput(.02f,left,right,true,false);}
        static void Nearest(R23PlayableLoop loop,Action<string> note,string expected,Vector3 center)
        {string aimed;Vector3 point;float score;bool found=loop.Gameplay.TryReadAimedWorldWeapon(1,out aimed,out point,out score);Note(note,"BEFORE fresh pickup expected="+expected+" found="+found+" nearest="+aimed+" hand="+loop.Rig.Right.position+" copyCenter="+center+" nearestCenter="+point+" score="+score);Check(note,found&&aimed==expected&&Vector3.Distance(loop.Rig.Right.position,center)<.001f,"actual nearest ID at the retained copy center");}
        static void Pose(Transform hand,Vector3 point,Vector3 forward){hand.SetPositionAndRotation(point,Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation));}
        static bool ClearMap(R23PlayableLoop loop,Vector3 a,Vector3 b)
        {Vector3 ray=b-a;if(ray.sqrMagnitude<.000001f)return true;foreach(var hit in Physics.RaycastAll(a,ray.normalized,ray.magnitude,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform))return false;return true;}
        static bool PlaceTarget(R23PlayableLoop loop,Vector3 forward,out Vector3 floor)
        {
            floor=Vector3.zero;var h=loop.Gameplay;Vector3 across=Vector3.Cross(Vector3.up,forward);
            foreach(float distance in new[]{2.5f,3f,2f})foreach(float side in new[]{0f,-1f,1f})
            {
                RaycastHit best=new RaycastHit();float nearest=float.PositiveInfinity;Vector3 probe=loop.Rig.Head.transform.position+forward*distance+across*side+Vector3.up;
                foreach(var hit in Physics.RaycastAll(probe,Vector3.down,8,1<<0,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.normal.y>.65f&&hit.distance<nearest){best=hit;nearest=hit.distance;}
                if(float.IsPositiveInfinity(nearest))continue;bool blocked=false;Quaternion rotation=Quaternion.LookRotation(-forward,Vector3.up);
                foreach(var c in Physics.OverlapBox(best.point+Vector3.up*.86f,new Vector3(.30f,.72f,.16f),rotation,(1<<0)|(1<<19),QueryTriggerInteraction.Ignore))if(c.transform.IsChildOf(loop.Map.transform)){blocked=true;break;}
                if(blocked||!ClearMap(loop,loop.Rig.Head.transform.position,best.point+Vector3.up*1.16f))continue;
                var positions=new Vector3[h.PracticeTargetCount];for(int i=0;i<positions.Length;i++)positions[i]=best.point+Vector3.up*.02f+across*(i*2);h.RepositionTrainingTargets(positions,rotation);Physics.SyncTransforms();floor=best.point;return true;
            }return false;
        }
        static void AimBoth(R23PlayableLoop loop,Action<string> note)
        {for(int iteration=0;iteration<6;iteration++){for(int hand=0;hand<2;hand++){R23ShotPose pose;Check(note,loop.Gameplay.TryReadDualShotPose(hand,out pose),"source hand "+hand+" aiming pose valid");var tracked=hand==0?loop.Rig.Left:loop.Rig.Right;tracked.rotation=Quaternion.FromToRotation(V(pose.Forward),(loop.Gameplay.PracticeAimPoint(0,false)-V(pose.Origin)).normalized)*tracked.rotation;}Physics.SyncTransforms();Tick(loop,.02f);}}
        static void Preflight(R23PlayableLoop loop,Action<string> note,R24DualDefinition definition,int hand)
        {
            R23ShotPose pose;var h=loop.Gameplay;Check(note,h.TryReadDualShotPose(hand,out pose),"source physical hand "+hand+" muzzle is valid");Collider body;Check(note,h.TryReadMeleeTargetCollider(0,out body),"existing actual target body collider registered");Vector3 origin=V(pose.Origin);Check(note,ClearMap(loop,loop.Rig.Head.transform.position,origin),"actual head-to-independent-muzzle path clear");
            for(int pellet=0;pellet<definition.Pellets;pellet++){double angle=Math.PI*2*pellet/8,r=definition.Automatic?0:Math.Tan(5*Math.PI/180)*(pellet%2==0?.4:1);Vector3 direction=(V(pose.Forward)+V(pose.Right)*(float)(Math.Cos(angle)*r)+V(pose.Up)*(float)(Math.Sin(angle)*r)).normalized;RaycastHit hit;bool actual=body.Raycast(new Ray(origin,direction),out hit,8);Check(note,actual&&ClearMap(loop,origin,hit.point),"actual hand "+hand+" source pellet "+pellet+" has clear <=8m body lane");}
        }
        sealed class Saved
        {
            readonly bool manual,external,enabled,pump,persist,lobby;readonly Vector3 root,origin,head,left,right;readonly Quaternion rootQ,headQ,leftQ,rightQ,targetQ;readonly Vector3[] targets;
            public Saved(R23PlayableLoop loop,Action<string> note)
            {var r=loop.Rig;manual=loop.ManualSmoke;external=loop.ExternalProbePump;enabled=r.enabled;pump=loop.Gameplay.ExternalInputPump;persist=loop.Calibration.SuppressPersistence;lobby=loop.Lobby.Visible;root=r.transform.position;rootQ=r.transform.rotation;origin=r.TrackingOrigin.localPosition;head=r.Head.transform.localPosition;headQ=r.Head.transform.localRotation;left=r.Left.localPosition;leftQ=r.Left.localRotation;right=r.Right.localPosition;rightQ=r.Right.localRotation;targets=new Vector3[loop.Gameplay.PracticeTargetCount];targetQ=Quaternion.identity;for(int i=0;i<targets.Length;i++){Collider body;Check(note,loop.Gameplay.TryReadMeleeTargetCollider(i,out body),"save genuine existing target "+i);targets[i]=body.transform.position;if(i==0)targetQ=body.transform.rotation;}}
            public void Restore(R23PlayableLoop loop)
            {var h=loop.Gameplay;var r=loop.Rig;h.NotifyFocus(true);h.SetGameplayPaused(false);loop.RestartRound();h.RepositionTrainingTargets(targets,targetQ);r.transform.SetPositionAndRotation(root,rootQ);r.TrackingOrigin.localPosition=origin;r.Head.transform.SetLocalPositionAndRotation(head,headQ);r.Left.SetLocalPositionAndRotation(left,leftQ);r.Right.SetLocalPositionAndRotation(right,rightQ);h.SetWeaponTuning(loop.UI.Settings.MotionDamping,loop.UI.Settings.OffhandInfluence);if(lobby)loop.ReturnToLobby();loop.Calibration.SuppressPersistence=persist;h.ExternalInputPump=pump;r.enabled=enabled;loop.ExternalProbePump=external;loop.ManualSmoke=manual;Physics.SyncTransforms();}
        }
    }
}
#endif
