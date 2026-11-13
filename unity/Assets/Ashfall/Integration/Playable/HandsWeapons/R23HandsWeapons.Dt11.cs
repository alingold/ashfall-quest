#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.WeaponHandling;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons {public sealed partial class R23HandsWeapons {
 R24Dt11View dt11View;readonly R23MagazineControls dt11Controls=new R23MagazineControls();bool IsDt11{get{return ActiveWeaponId=="dt11";}}bool IsPelletGun{get{return IsShotgun||IsDt11;}}
 public bool Dt11Ready{get{return dt11View!=null&&dt11View.Ready;}}public int Dt11LiveMask{get{return round==null||round.Dt11==null?0:round.Dt11.LiveMask;}}public int Dt11SpentMask{get{return round==null||round.Dt11==null?0:round.Dt11.SpentMask;}}
 // Read-only actual input/gate state for the bounded native fixture. No new authority or counters.
 public string Dt11ControlStatus{get{var primary=haveInputSnapshot?lastInput.Hand(primaryHand):new R23HandInput();return "ready="+Ready+" held="+HasWeapon+" focused="+hadFocus+" paused="+gameplayPaused+" suppressed="+presentationSuppressed+" active="+isActiveAndEnabled+" allowed="+FeedbackAllowed+" primaryHand="+primaryHand+" tracked="+primary.Tracked+" reserved="+primary.Reserved+" grip="+primary.Grip+" trigger="+primary.Trigger+" projectedPrimary="+trackedControls.PrimaryTracked+" projectedClimb="+trackedControls.PrimaryClimbing+" projectedSupport="+trackedControls.SupportTracked+" projectedReserved="+trackedControls.SupportReserved+" projectedGrip="+trackedControls.Grip+" desktop="+trackedControls.Desktop;}}
 IEnumerator LoadDt11Presentation(){dt11View=gameObject.AddComponent<R24Dt11View>();dt11View.InitializeModel(weaponModels[21]);while(!dt11View.Ready){if(dt11View.Failure!=null)throw new InvalidOperationException(dt11View.Failure);yield return null;}feedbackRearSights[21]=new GameObject("Source DT11 rear reference").transform;feedbackRearSights[21].SetParent(weaponModels[21],false);feedbackRearSights[21].localPosition=new Vector3(0,.095f,.02f);feedbackFrontSights[21]=new GameObject("Source DT11 front reference").transform;feedbackFrontSights[21].SetParent(weaponModels[21],false);feedbackFrontSights[21].localPosition=new Vector3(0,.095f+(.04f-.095f)*(-.62f+.02f)/(-50.65f+.02f),.62f);}
 void PresentDt11(float seconds){if(!Dt11Ready||round==null||round.Dt11==null)return;if(!IsDt11||!FeedbackAllowed){dt11View.ClearTransient();return;}dt11View.SetNeutralRotation(SourceAimRotation);dt11View.Present(round.Dt11,SupportTransform,true,seconds);}
 bool TickDt11(float dt,bool fire,bool reload){bool suspended=!FeedbackAllowed;ActiveFeedback.Advance(dt);feedbackAdvancedFrame=Time.frameCount;ApplySourceAimTuning(dt,suspended);RenderDisplayedPose();var pose=displayedShot;var input=haveTrackedControls?trackedControls:new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true};input.Trigger=fire;input.Desktop|=desktopReloadRequest;var action=dt11Controls.Project(!suspended,haveTrackedControls&&reload,true,0,0,(desktopReloadRequest||!haveTrackedControls)&&reload);if(action==HandlingAction.ReloadStep)input.Desktop=true;var before=round.ActiveSnapshot;bool blockedFlick=haveInputSnapshot?(lastInput.Hand(primaryHand).Trigger>.2f||lastInput.Hand(primaryHand).TriggerPressed):fire;bool shot=round.StepDt11(dt,input,action,new R23M1014Pose{Origin=pose.Origin,Forward=pose.Forward,Right=pose.Right,Up=pose.Up},blockedFlick,suspended);var after=round.ActiveSnapshot;if(shot){lastAcceptedShot=pose;lastAcceptedPoseValid=true;LastAcceptedShotActionId=activeActionId;LastAcceptedNativeShot=after.Shots;ActiveFeedback.AcceptedShot(after.Shots,round.LastDt11Volley.Supported);Pulse(primaryHand,.8f,.085f);}if(!suspended){if(!string.IsNullOrEmpty(round.Dt11.ReloadCue)){if(audioCues!=null)audioCues.Play("dt11",round.Dt11.ReloadCue,false);Pulse(SupportHand,.45f,.06f);}if((after.Supported||shot&&round.LastDt11Volley.Supported)&&!before.Supported)Pulse(SupportHand,.16f,.035f);}RenderDisplayedPose();PresentDt11(dt);UpdateWeaponPresentation();foreach(var target in targets)target.Refresh(round);previousTrigger=fire;if(!shot)return false;if(audioCues!=null)audioCues.Play("dt11","shot",true);return TraceDt11(round.LastDt11Volley);}
        bool TraceDt11(R24Dt11Volley volley){
            if(volley==null)return false;PublishBallistic(volley.Origin,volley.Directions);LastShotgunPelletCount=LastShotgunTargetGroups=0;Vector3 origin=From(volley.Origin),segment=origin-head.transform.position;RaycastHit blocked;
            muzzleFlashUntil=Time.unscaledTime+.05f;flashUntil=Time.unscaledTime+R23WeaponFeedbackState.TracerSeconds;
            if(Nearest(head.transform.position,segment.normalized,segment.magnitude,out blocked)){
                if(gunFeedback!=null)gunFeedback.Impact(blocked.point,blocked.normal,Time.unscaledTime);PresentSurfaceImpact(blocked,true,true);
                tracer.enabled=false;foreach(var line in pelletTracers)line.enabled=false;round.ApplyDt11Contacts(volley,new R23M1014Contact[0],true);return true;
            }
            var contacts=new List<R23M1014Contact>();var before=new Dictionary<string,HealthSnapshot>();var touched=new Dictionary<string,R23PracticeTarget>();var worldDamage=new Dictionary<Collider,float>();var directions=volley.Directions;
            for(int pellet=0;pellet<directions.Length;pellet++){
                Vector3 direction=From(directions[pellet]),end=origin+direction*volley.Range;RaycastHit hit;LastShotgunPelletCount++;
                if(Nearest(origin,direction,volley.Range,out hit)){
                    end=hit.point;if(gunFeedback!=null)gunFeedback.Impact(hit.point,hit.normal,Time.unscaledTime);PresentSurfaceImpact(hit,true);
                    R23PracticeTarget target=null;foreach(var candidate in targets)if(candidate.Owns(hit.collider)){target=candidate;break;}
                    if(target!=null){if(!before.ContainsKey(target.TargetId))before.Add(target.TargetId,round.ReadTarget(target.TargetId));touched[target.TargetId]=target;contacts.Add(new R23M1014Contact(pellet,target.TargetId,To(hit.point),hit.collider==target.Head));}
                    else{float amount;worldDamage.TryGetValue(hit.collider,out amount);worldDamage[hit.collider]=amount+R24Dt11Definition.DamageAtDistance(hit.distance);}
                }
                var line=pellet==0?tracer:pelletTracers[pellet-1];PresentLocalTracer(line,origin,end);
            }
            LastShotgunTargetGroups=round.ApplyDt11Contacts(volley,contacts.ToArray());
            foreach(var pair in touched){var after=round.ReadTarget(pair.Key);var prior=before[pair.Key];pair.Value.Refresh(round);float shield=prior.Shield-after.Shield,health=prior.Health-after.Health;if(shield+health>0&&TargetDamaged!=null)TargetDamaged(pair.Key,round.LastTargetHeadshot(pair.Key),shield,health);}
            if(LastShotgunTargetGroups>0&&audioCues!=null)audioCues.Play("dt11","hit",false);
            if(WorldHit!=null)foreach(var pair in worldDamage)if(pair.Key!=null&&pair.Value>0)WorldHit(pair.Key,pair.Value);
            return true;
        }
}}
#endif
