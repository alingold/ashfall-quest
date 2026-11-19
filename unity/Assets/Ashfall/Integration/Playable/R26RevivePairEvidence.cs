#if UNITY_6000_0_OR_NEWER
using System;using System.IO;using UnityEngine;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable
{
    // Two real peers share one team. Only initial placement and knockdown are authority fixtures.
    [DefaultExecutionOrder(-120)]
    public sealed class R26RevivePairEvidence:MonoBehaviour
    {
        R23PlayableLoop root;R25NetBot bot;StreamWriter log;int phase,captures;double changed,downSince=-1,next;bool requested,opened,sawDown,sawReturn;string role;
        void Note(string text){if(log==null)log=new StreamWriter(R25NetBot.Arg("-ashfallNetBotLog")+".revive.log"){AutoFlush=true};log.WriteLine(Time.realtimeSinceStartup.ToString("F3")+" "+text);}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
        static Vector3 V(Float3 v){return new Vector3(v.X,v.Y,v.Z);}
        bool TouchTest{get{return R25NetBot.Arg("-ashfallPaddleTouch")=="true";}}
        int observerStage;double contactAt=-1,cueSeen,readyAt=-1;bool unchargedTouch,unchargedPassed,heardCharge,heardReady;float mixPeak;readonly float[] audioSamples=new float[512];
        void Update()
        {
            if(root==null)root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root==null||!root.Ready||root.LanSession==null)return;
            var lobby=root.LanSession.Lobby;if(lobby==null)return;
            bool host=lobby.State==R24LanLobbyState.Hosting;role=host?"host":"guest";
            if(host&&!opened&&lobby.Members.Length==2){
                root.UI.SelectMatchPreset("team_revive");
                bool switches=R25NetBot.Arg("-ashfallReviveSwitches")=="true";
                if(!root.UI.SetMatchRule("friendly_fire",switches)||!root.UI.SetMatchRule("enemy_revive",switches))throw new InvalidOperationException("Host cannot edit revive switches");
                var rules=root.UI.SelectedMatchRules.WithMap(root.Map.MapId).With("teams",2);
                if(!lobby.HostSetRoomTeamCount(rules))throw new InvalidOperationException("Cannot configure fixture team count");
                string guest=null;int hostTeam=-1;foreach(var member in lobby.Members){if(member.Actor==lobby.OwnActor)hostTeam=member.Team;else guest=member.Actor;}
                if(!lobby.HostAssignRoomTeam(guest,hostTeam))throw new InvalidOperationException("Cannot set up the same-team revive pair");
                opened=true;root.LanSession.RouteMapEntry(root.Map.MapId);Note("open warm-up with pair assigned to team "+hostTeam);
            }
            if(host&&opened&&!requested&&lobby.WarmupPlayersPlaced&&!root.LanSession.LoadingSharedMap&&!root.LanSession.MapEntryBlocked){
                requested=true;root.LanSession.RouteRoundRequest(root.UI.SelectedMatchRules.WithMap(root.Map.MapId).With("zone_enabled",false).With("protection",0f));
                Note("request two-peer Team Revive");
            }
            if(lobby.SelectedMap==null||lobby.SelectedMap.Rules==null||lobby.RoundCountdown>0||root.LanSession.MapEntryBlocked)return;
            if(bot==null)bot=root.GetComponent<R25NetBot>();
            R24LanHealthState own,ally=null;string other=null;double now=lobby.EstimatedHostTime;
            foreach(var member in lobby.Members)if(member.Actor!=lobby.OwnActor){other=member.Actor;break;}
            bool ready=lobby.TryReadHealth(lobby.OwnActor,out own)&&other!=null&&lobby.TryReadHealth(other,out ally);
            bool approach=false,spin=false;Vector3? touch=null;Vector3 forward=Vector3.zero;float distance=-1;
            R24LanPoseSnapshot pose;
            if(ready&&lobby.TryReadPresence(other,out pose)&&pose.Pose.Scene==root.LanSession.PresenceScene){
                forward=Vector3.ProjectOnPlane(V(pose.Pose.Root.Position)-root.Rig.transform.position,Vector3.up);distance=forward.magnitude;
                if(own.Life==LifeState.Alive&&ally.Life==LifeState.Downed){
                    if(downSince<0){downSince=now;captures=0;}
                    approach=!TouchTest&&now-downSince>2;
                    if(TouchTest){
                        if(!root.Paddles.State.Active)root.EquipPaddles();
                        double elapsed=now-downSince;
                        float remaining=root.Paddles.PresentedRemaining;
                        if(remaining>0&&readyAt<0&&root.Paddles.CoronaVisible){readyAt=now;Note("ready remaining="+remaining+" local lightning visible");}
                        bool testBefore=elapsed>=1.5&&elapsed<2.0, testReady=readyAt>=0&&now-readyAt>.7&&remaining>0;
                        if(root.Paddles.State.Active&&(testBefore||testReady)){
                            touch=V(pose.Pose.Head.Position)-Vector3.up*.45f-forward.normalized*.22f;
                            if(testBefore&&!unchargedTouch){unchargedTouch=true;Note("uncharged stroke deadline="+own.ReadyUntil+" distance="+distance);}
                            if(testReady&&contactAt<0){contactAt=now;Note("charged stroke remaining="+remaining+" distance="+distance);}
                        }
                        if(unchargedTouch&&!unchargedPassed&&elapsed>=2.2){
                            unchargedPassed=true;Note("PASS uncharged touch left target Downed");
                        }
                        spin=elapsed>=2.3&&!testReady;
                    }
                    string target;float fraction;bool holding=lobby.TryNearbyReviveProgress(out target,out fraction)&&target==other;
                    if(captures==0&&distance>1&&now-downSince>.8||captures==1&&holding&&fraction>.45f||captures==2&&holding&&fraction>.9f){Capture(++captures);Note("view stage="+captures+" hold="+holding+" progress="+fraction+" bodyDistanceXZ="+distance);}
                }else downSince=-1;
                if(TouchTest&&contactAt>=0&&ally.Life==LifeState.Alive){Note("contact to replicated Alive seconds="+(now-contactAt));contactAt=-1;}
                {
                    R24LanRemoteAvatar avatar;
                    if(lobby.TryReadHealth(lobby.OwnActor,out own)&&root.LanSession.TryReadRemoteAvatar(other,out avatar)){
                        if(observerStage==0&&own.Life==LifeState.Downed&&avatar.RescuePaddlesVisible){Capture(11);observerStage++;Note("observer paddles out");}
                        if(TouchTest&&avatar.RescueChargeCues>0&&!heardCharge){heardCharge=true;Note("observer charge sound");}
                        if(TouchTest&&avatar.RescueReadyCues>0&&!heardReady){heardReady=true;Note("observer ready sound");}
                        if(TouchTest&&observerStage==1&&avatar.RescueCoronaVisible){Capture(14);observerStage++;Note("observer ready lightning");}
                        if(observerStage==(TouchTest?2:1)&&avatar.RescueSounds>0&&avatar.RescuePaddlesVisible){Capture(12);observerStage=3;cueSeen=now;Note("observer discharge count="+avatar.RescueSounds);}
                        if(observerStage==3){foreach(var voice in avatar.GetComponentsInChildren<AudioSource>())if(voice.clip!=null&&voice.clip.name.Contains("revive-discharge")){
                            voice.GetOutputData(audioSamples,0);foreach(float value in audioSamples)mixPeak=Mathf.Max(mixPeak,Mathf.Abs(value));}}
                        if(observerStage==3&&now-cueSeen>.38){Capture(13);observerStage++;Note("observer after discharge peak="+mixPeak);}
                    }
                }
            }
            bot.SampleRevivePair(approach,forward,touch,spin);
            if(!ready)return;
            if(own.Life==LifeState.Downed&&!sawDown){sawDown=true;Note("own Downed actor="+own.Actor);}
            if(sawDown&&!sawReturn&&own.Life==LifeState.Alive){sawReturn=true;Note("own revived health="+own.Health+" shield="+own.Shield);}
            if(now>=next){next=now+2;string target;float progress;lobby.TryNearbyReviveProgress(out target,out progress);Note("phase="+phase+" own="+own.Life+" other="+ally.Life+" teams="+own.Team+"/"+ally.Team+" dist="+distance+" hold="+target+" progress="+progress+" clock="+root.ReadWorldScoreState().ClockText+" rules="+lobby.SelectedMap.Rules.FriendlyFire+"/"+lobby.SelectedMap.Rules.EnemyRevive+" rawHead="+root.Rig.Head.transform.localPosition.y+" originLift="+root.Rig.TrackingOrigin.localPosition.y+" reject="+lobby.LastRawTrackingRejection);}
            if(TouchTest&&sawReturn&&root.Paddles.State.Active&&ally.Life!=LifeState.Downed)root.StowPaddles();
            if(!host){if(root.UI.IsMatchRuleAvailable("enemy_revive")||root.UI.IsMatchRuleAvailable("friendly_fire"))throw new InvalidOperationException("Guest could edit shared rules");return;}
            if(own.Team!=ally.Team)throw new InvalidOperationException("Expected same team");
            if((phase==0||phase==3)&&now-changed>2&&root.HealthStations!=null&&root.HealthStations.Ready){
                var rows=root.HealthStations.AuthoredStations;if(rows.Length==0)throw new InvalidOperationException("Expected Paintball stations");
                Vector3 spot=V(rows[0].Center)+Vector3.right*4;RaycastHit floor;if(!Physics.Raycast(spot+Vector3.up*3,Vector3.down,out floor,8,1<<0))return;spot.y=floor.point.y+.02f;
                lobby.ReviveEvidencePlace(lobby.OwnActor,F(spot));lobby.ReviveEvidencePlace(other,F(spot+Vector3.right*(TouchTest?1.16f:1.9f)));phase++;changed=now;Note("placed pair "+spot);return;
            }
            if((phase==1||phase==4)&&now-changed>3&&distance>1&&distance<2.6f){
                var victim=phase==1?ally:own;var loss=lobby.ApplyHostHealthLoss(victim.Actor,victim.Revision,victim.Health,victim.Shield,true);if(!loss.Accepted)return;
                phase++;changed=now;Note("downed "+victim.Actor);return;
            }
            if(phase==2||phase==5){var victim=phase==2?ally:own;if(victim.Life==LifeState.Alive&&victim.Health==50&&victim.Shield==0){Note("PASS "+(phase==2?"host revives guest":"guest revives host")+" secondsSinceDown="+(now-changed)+" total="+lobby.Revives);phase++;changed=now;}}
        }
        void Capture(int stage)
        {
            var camera=root.Rig.Head;var rt=RenderTexture.GetTemporary(960,540,24);var previous=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels=new Texture2D(960,540,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(R25NetBot.Arg("-ashfallNetBotLog")),role+"-view-"+stage+".png"),pixels.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Destroy(pixels);}
        }
        void OnDestroy(){if(log!=null)log.Dispose();}
    }
}
#endif
