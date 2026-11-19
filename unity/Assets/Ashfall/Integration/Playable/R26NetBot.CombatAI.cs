#if UNITY_6000_0_OR_NEWER
using System;using System.Globalization;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        float botSkill=.5f,aiScanAt,aiReadyAt,aiBurstAt,aiBurstUntil,aiErrorAt,aiVisibleSince=-1,aiStrafeAt,aiStrafe=1;
        Vector2 aiError;string aiTarget,aiRescue;long aiEpoch=-1;int aiSpawn=-1;uint aiSeed;bool aiVisible;
        const int AiWorldMask=(1<<0)|(1<<8); // Same map/build layers as the host's ballistic trace; ignore triggers.
        bool ReadCombatAI()
        {
            string text=Arg("-ashfallNetBotSkill");if(text.Length>0&&(!RouteNumber(text,out botSkill)||botSkill<0||botSkill>1))return false;
            aiSeed=unchecked((uint)Guid.NewGuid().GetHashCode())|1;return true;
        }
        float AiRandom(){aiSeed^=aiSeed<<13;aiSeed^=aiSeed>>17;aiSeed^=aiSeed<<5;return (aiSeed&0xffff)/65535f;}
        bool AiHealth(R24LanLobby lobby,string actor,out R24LanHealthState health)
        {return lobby.TryReadHealth(actor,out health)&&health.Epoch==lobby.Epoch&&health.MapHash==lobby.MapManifestHash&&health.Connected;}
        bool AiPose(R24LanLobby lobby,string actor,float now,out R24LanPoseSnapshot pose)
        {return lobby.TryReadPresence(actor,out pose)&&pose.Pose.Tracked&&now-pose.ReceivedAt<=.5f&&pose.Pose.Scene==root.LanSession.PresenceScene;}
        bool AiEnemy(R24LanLobby lobby,string actor)
        {
            var map=lobby.SelectedMap;var rules=map==null?null:map.SourceRules;R24LanHealthState own,other;
            if(actor==lobby.OwnActor||rules==null||!AiHealth(lobby,lobby.OwnActor,out own)||!AiHealth(lobby,actor,out other)||other.Life!=LifeState.Alive)return false;
            R24LanPoseSnapshot pose;
            if(root.TeamLaunch!=null&&root.TeamLaunch.Eligible&&lobby.TryReadPresence(actor,out pose)&&root.TeamLaunch.Runtime.Layout.PlatformAt(pose.Pose.Root.Position)>=0)return false;
            if(rules.Teams==0)return true;
            // Require roster and health to agree: a partial team/epoch update must never permit friendly fire.
            int ownTeam=-1,otherTeam=-1;foreach(var member in lobby.Members){if(member.Actor==lobby.OwnActor)ownTeam=member.Team;if(member.Actor==actor)otherTeam=member.Team;}
            return ownTeam>=0&&otherTeam>=0&&ownTeam==own.Team&&otherTeam==other.Team&&ownTeam!=otherTeam;
        }
        static Vector3 AiChest(R24LanPoseSnapshot pose){return V(pose.Pose.Head.Position)-Vector3.up*.45f;}
        static bool AiClear(Vector3 from,Vector3 to){return !Physics.Linecast(from,to,AiWorldMask,QueryTriggerInteraction.Ignore);}
        R24LanPoseSnapshot SelectCombatTarget(R24LanLobby lobby,float now)
        {
            BindHitreg();R24LanHealthState own;
            if(!AiHealth(lobby,lobby.OwnActor,out own)||own.Life!=LifeState.Alive){aiTarget=null;return null;}
            if(aiEpoch!=lobby.Epoch||aiSpawn!=own.SpawnRevision){aiEpoch=lobby.Epoch;aiSpawn=own.SpawnRevision;aiTarget=null;aiScanAt=0;aiVisibleSince=-1;aiBurstAt=now+.5f;}
            R24LanPoseSnapshot current=null;
            if(now>=aiScanAt||aiTarget==null||!AiEnemy(lobby,aiTarget)||!AiPose(lobby,aiTarget,now,out current))
            {
                aiScanAt=now+.2f;string selected=null;float best=Single.MaxValue;
                foreach(var member in lobby.Members)
                {
                    R24LanPoseSnapshot pose;if(!AiEnemy(lobby,member.Actor)||!AiPose(lobby,member.Actor,now,out pose))continue;
                    Vector3 chest=AiChest(pose);float distance=Vector3.Distance(root.Rig.Head.transform.position,chest);if(distance>120)continue;
                    bool visible=AiClear(root.Rig.Head.transform.position,chest);float score=distance+(visible?0:150)-(member.Actor==aiTarget?8:0);
                    if(score<best){best=score;selected=member.Actor;current=pose;}
                }
                if(selected!=aiTarget)
                {
                    aiTarget=selected;aiVisibleSince=-1;aiReadyAt=now+Mathf.Lerp(.65f,.16f,botSkill)+AiRandom()*.12f;aiBurstAt=aiReadyAt;aiBurstUntil=0;
                    if(selected!=null){R24LanHealthState h;lobby.TryReadHealth(selected,out h);Write("ai-target","actor="+lobby.OwnActor+" ownTeam="+own.Team+" target="+selected+" targetTeam="+h.Team+" teams="+lobby.SelectedMap.SourceRules.Teams+" visible="+AiClear(root.Rig.Head.transform.position,AiChest(current))+" skill="+botSkill.ToString("F2",CultureInfo.InvariantCulture));}
                }
            }
            if(aiTarget==null||!AiEnemy(lobby,aiTarget)||!AiPose(lobby,aiTarget,now,out current))return null;
            return current;
        }
        float AiRange(string weapon)
        {R24LanBallisticProfile p;if(weapon=="smaw")return 70;if(!R24LanBallisticProfile.TryRead(weapon,out p))return 0;if(p.Pellets>1)return 18;if(weapon=="awp"||weapon=="spr"||weapon=="s85")return 95;return weapon=="px4"||weapon=="1911"||weapon=="uzis"?30:65;}
        // Reject an entire firing cone if any ally could intersect it, even with friendly-fire enabled.
        bool AiFriendlyLane(R24LanLobby lobby,Vector3 from,Vector3 direction,float range,string weapon,float now)
        {
            var rules=lobby.SelectedMap.SourceRules;if(rules.Teams==0)return false;R24LanHealthState own;if(!AiHealth(lobby,lobby.OwnActor,out own))return true;
            R24LanBallisticProfile profile;bool pellets=R24LanBallisticProfile.TryRead(weapon,out profile)&&profile.Pellets>1;
            foreach(var member in lobby.Members)
            {
                if(member.Actor==lobby.OwnActor)continue;R24LanHealthState health;
                if(!AiHealth(lobby,member.Actor,out health))return true;
                if(member.Team!=own.Team&&health.Team!=own.Team)continue;
                R24LanPoseSnapshot pose;if(!AiPose(lobby,member.Actor,now,out pose))return true;
                Vector3 chest=AiChest(pose);float along=Vector3.Dot(chest-from,direction);
                // Cover head/hips, spread, recoil, and a short replication/motion margin. Check beyond the enemy too.
                float radius=.85f+Mathf.Clamp(V(pose.Pose.Velocity).magnitude*.15f,0,1.5f)+Mathf.Max(0,along)*(pellets?.18f:.045f);
                if(along>=-.5f&&along<=range&&Vector3.Distance(chest,from+direction*Mathf.Max(0,along))<radius)return true;
                if(weapon=="smaw"&&Vector3.Distance(chest,from+direction*range)<R24SmawRuntime.Radius+3)return true;
            }
            return false;
        }
        bool AiHelpAlly(R24LanLobby lobby,float now,ref Vector2 move,ref string step)
        {
            var rules=lobby.SelectedMap.SourceRules;if(rules==null||rules.Mode!="revive"||rules.Teams==0)return false;
            R24LanHealthState own;if(!AiHealth(lobby,lobby.OwnActor,out own))return false;
            foreach(var member in lobby.Members)
            {
                R24LanHealthState health;R24LanPoseSnapshot pose;
                if(member.Actor==lobby.OwnActor||member.Team!=own.Team||!AiHealth(lobby,member.Actor,out health)||health.Team!=own.Team||health.Life!=LifeState.Downed||!AiPose(lobby,member.Actor,now,out pose))continue;
                Vector3 delta=Vector3.ProjectOnPlane(V(pose.Pose.Root.Position)-root.Rig.transform.position,Vector3.up);
                if(delta.magnitude>6||!AiClear(root.Rig.Head.transform.position,AiChest(pose)))continue;
                if(aiRescue!=member.Actor){aiRescue=member.Actor;Write("ai-rescue","approach="+aiRescue+" team="+own.Team);}
                Vector3 local=root.Rig.transform.InverseTransformDirection(delta);move=delta.magnitude>.75f?Vector2.ClampMagnitude(new Vector2(local.x,local.z),.5f):Vector2.zero;
                step="help-downed-ally";return true; // Normal proximity revive handles the hold; never mutate health or bypass paddle authority.
            }
            aiRescue=null;return false;
        }
        float deckEntered=-1,nextPodAttempt;
        void DeployFromPlatform(R24LanLobby lobby,float now,ref Vector2 move,ref float grip,ref float trigger,ref string step)
        {
            grip=trigger=0;move=Vector2.zero;step="pod-flight";
            if(root.Rig.CarrierActive||root.TeamLaunch==null||!root.TeamLaunch.Eligible)return;
            var layout=root.TeamLaunch.Runtime.Layout;var feet=root.Rig.transform.position;
            var at=new Ashfall.Core.Float3(feet.x,feet.y,feet.z);int platform=layout.PlatformAt(at);if(platform<0)return;
            if(deckEntered<0){deckEntered=now;return;}
            if(now-deckEntered<.5f)return; // release the normal spawn/focus stick gate
            var frame=layout.Station(platform);var local=frame.Local(at);
            R24LanSpawn spawn;float seatX=lobby.TryReadSpawn(lobby.OwnActor,out spawn)&&layout.PlatformAt(spawn.Feet)==platform?frame.Local(spawn.Feet).X:local.X;
            int slot=Mathf.Clamp(Mathf.RoundToInt(seatX/3.6f+5.5f),0,TeamLaunch.R24LaunchLayout.Slots-1);
            var target=layout.Pod(platform,slot).Origin;string key=layout.PodId(platform,slot);
            var rider=new TeamLaunch.R24LaunchPlayer{Id=lobby.OwnActor,Team=root.LocalMatchTeam,Alive=true,OnDeck=true,Position=at};
            int inside;TeamLaunch.R24LaunchFrame cabin;
            if(layout.TryCabin(rider,out inside,out cabin)&&now-deckEntered<8&&!root.TeamLaunch.Runtime.Used(key)){
                step="board-pod";
                if(now>=nextPodAttempt){nextPodAttempt=now+.4f;root.TryLaunchLocalPod(layout.PodId(platform,inside));}
                return;
            }
            step="approach-pod";
            if(now-deckEntered>=8||root.TeamLaunch.Runtime.Used(key)){
                // A spent or obstructed cabin still leaves the deck edge available.
                float aisle=(slot-5.5f)*3.6f+(slot==11?-1.8f:1.8f);
                target=frame.Point(new Ashfall.Core.Float3(aisle,0,Mathf.Abs(local.X-aisle)>.45f?local.Z:14));step="leave-platform";
            }
            var delta=root.Rig.transform.InverseTransformDirection(V(target)-feet);move=Vector2.ClampMagnitude(new Vector2(delta.x,delta.z),.85f);
        }
        void CombatAI(float now,float dt,R24LanPoseSnapshot targetPose,ref Vector2 move,ref Vector3 left,ref Vector3 right,ref Quaternion aim,ref float trigger,ref float grip,ref string step)
        {
            var lobby=root.LanSession.Lobby;if(lobby==null)return;trigger=0;
            R24LanHealthState own;if(!AiHealth(lobby,lobby.OwnActor,out own)||own.Life!=LifeState.Alive){move=Vector2.zero;grip=0;step="hold-for-revive-or-spawn";return;}
            if(lobby.SelectedMap==null||lobby.SelectedMap.SourceRules==null)return;
            if(AiHelpAlly(lobby,now,ref move,ref step))return;
            if(targetPose==null||aiTarget==null||!AiEnemy(lobby,aiTarget)){aiVisibleSince=-1;return;}
            Vector3 chest=AiChest(targetPose),to=chest-right;float distance=to.magnitude;string weapon=root.Gameplay.ActiveWeaponId;float range=AiRange(weapon);
            aiVisible=AiClear(root.Rig.Head.transform.position,chest)&&AiClear(right,chest);
            if(!aiVisible)aiVisibleSince=-1;else if(aiVisibleSince<0){aiVisibleSince=now;aiReadyAt=Mathf.Max(aiReadyAt,now+Mathf.Lerp(.65f,.16f,botSkill));}
            if(roam||routine=="soak")
            {
                Vector3 toward=Vector3.ProjectOnPlane(chest-root.Rig.transform.position,Vector3.up).normalized;
                if(now>=aiStrafeAt){aiStrafeAt=now+1.5f+AiRandom()*2;aiStrafe=AiRandom()<.5f?-1:1;}
                float comfortable=Mathf.Clamp(range*.28f,5,18);Vector3 desired=toward*(distance>comfortable? .65f:distance<comfortable*.55f?-.35f:0)+Vector3.Cross(Vector3.up,toward)*aiStrafe*(aiVisible?.35f:.55f);
                // Let exploration find a way around a wall when the direct approach is blocked.
                if(AiClear(root.Rig.transform.position+Vector3.up*.6f,root.Rig.transform.position+Vector3.up*.6f+desired.normalized*1.4f))
                {Vector3 local=root.Rig.transform.InverseTransformDirection(desired);move=new Vector2(local.x,local.z);}
                if(now-playBegan<1||(now-playBegan)%12<.25f)move=Vector2.zero;
                step=aiVisible?"engage-enemy":"approach-enemy";
            }
            if(!root.Gameplay.HasWeapon||range<=0||root.Gameplay.Reloading||!aiVisible||distance>range||distance<(weapon=="smaw"?8:1)||now<aiReadyAt)return;
            // LAN bullets are hitscan (tracer flight is visual only); the SMAW is the actual moving projectile.
            Vector3 velocity=Vector3.ClampMagnitude(V(targetPose.Pose.Velocity),15);Vector3 point=chest;
            if(weapon=="smaw")point+=velocity*Mathf.Min(distance/R24SmawRuntime.RocketSpeed,.65f);
            if(now>=aiErrorAt){aiErrorAt=now+.25f+AiRandom()*.2f;aiError=new Vector2(AiRandom()*2-1,AiRandom()*2-1)*Mathf.Lerp(1.8f,.18f,botSkill);}
            Quaternion desiredAim=Quaternion.LookRotation(point-right)*Quaternion.Euler(aiError.y,aiError.x,0);
            if(HitregEnabled)desiredAim=Quaternion.LookRotation(point-right);
            aim=desiredAim*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);
            bool fireWindow=routine=="soak"||(now-playBegan)%48>=27&&(now-playBegan)%48<39;
            if(!fireWindow||Arg("-ashfallNetBotFire")=="false")return;
            Vector3 direction=desiredAim*Vector3.forward;
            if(!AiClear(right,right+direction*distance)||AiFriendlyLane(lobby,right,direction,weapon=="smaw"?distance:180,weapon,now))return;
            R23ShotPose displayed;
            if(root.Gameplay.TryReadDisplayedShotPose(out displayed))
            {
                // The rendered muzzle can lag a hand turn or recoil; do not fire until it points into the safe cone.
                if(Vector3.Angle(V(displayed.Forward),direction)>5||!AiClear(root.Rig.Head.transform.position,V(displayed.Origin))||AiFriendlyLane(lobby,V(displayed.Origin),V(displayed.Forward),weapon=="smaw"?distance:180,weapon,now))return;
            }
            if(now>=aiBurstAt){aiBurstUntil=now+Mathf.Lerp(.22f,.55f,botSkill);aiBurstAt=aiBurstUntil+Mathf.Lerp(.9f,.35f,botSkill)+AiRandom()*.3f;}
            if(now<aiBurstUntil)trigger=1;
        }
        void LogCombatShot(string weapon)
        {
            var lobby=root.LanSession.Lobby;if(lobby==null)return;R24LanHealthState own,other;
            Write("ai-shot","actor="+lobby.OwnActor+" ownTeam="+(lobby.TryReadHealth(lobby.OwnActor,out own)?own.Team:-99)+" target="+(aiTarget??"none")+" targetTeam="+(aiTarget!=null&&lobby.TryReadHealth(aiTarget,out other)?other.Team:-99)+" targetEligibleAfterShot="+(aiTarget!=null&&AiEnemy(lobby,aiTarget))+" weapon="+weapon);
        }
    }
}
#endif
