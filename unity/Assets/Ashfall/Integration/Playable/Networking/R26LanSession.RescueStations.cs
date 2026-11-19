#if UNITY_6000_0_OR_NEWER
using UnityEngine;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        long stationEpoch=-1;R24LanLobby stationLobby;JumpPads.R23HealthStations stationView;TextMesh rescueHint;Sprite rescuePixel;Material rescueBack,rescueInk;Transform rescueFill;
        double ownCue=-1;
        const float BarWidth=.48f;
        void ClearRescuePresentation(){if(stationView!=null)stationView.SharedCooldown=null;stationView=null;stationLobby=null;stationEpoch=-1;ownCue=-1;if(rescueHint!=null)Object.Destroy(rescueHint.gameObject);rescueHint=null;if(rescuePixel!=null)Object.Destroy(rescuePixel);rescuePixel=null;Font.textureRebuilt-=RefreshRescueFont;if(rescueBack!=null)Object.Destroy(rescueBack);if(rescueInk!=null)Object.Destroy(rescueInk);}
        void SyncRescueStations()
        {
            var lobby=peer==null?null:peer.Lobby;var stations=root==null?null:root.HealthStations;
            bool active=lobby!=null&&lobby.SelectedMap!=null&&lobby.SelectedMap.Active&&!lobby.SelectedMap.Cancelled&&root.Ready&&root.Map.ManifestSha256==lobby.MapManifestHash;
            if(stationView!=null&&(!active||stations!=stationView)){stationView.SharedCooldown=null;stationView=null;stationLobby=null;stationEpoch=-1;}
            if(stations!=null&&stations.Ready&&active){
                if(stationView!=stations||stationLobby!=lobby||stationEpoch!=lobby.Epoch){stationView=stations;stationLobby=lobby;stationEpoch=lobby.Epoch;stations.SharedCooldown=lobby.StationCooldownRemaining;if(lobby.State==R24LanLobbyState.Hosting)lobby.BindHealthStations(lobby.MapManifestHash,stations.AuthoredStations,stations.ClearContact);}
            }
            if(root!=null&&root.Paddles!=null&&root.Paddles.Ready){
                float charge=0,remaining=0;
                if(active){
                    ReadPaddleCharge(lobby,lobby.OwnActor,out charge,out remaining);
                    double cue=lobby.RescueCue(lobby.OwnActor);if(cue>=0&&cue!=ownCue){ownCue=cue;root.Paddles.PlayOwnRescue(root.Rig.Right.position);}
                }
                root.Paddles.PresentSharedCharge(charge,remaining);
            }
            SyncRescueHint(active?lobby:null);
        }

        static void ReadPaddleCharge(R24LanLobby lobby,string actor,out float charge,out float remaining)
        {
            charge=remaining=0;R24LanHealthState health;var map=lobby.SelectedMap;
            if(map==null||map.Results||map.Rules==null||map.Rules.Mode!="revive"||!lobby.TryReadHealth(actor,out health)||!health.Connected||health.Life!=LifeState.Alive)return;
            charge=health.Charge;remaining=Mathf.Clamp((float)(health.ReadyUntil-lobby.EstimatedHostTime),0,HandsWeapons.R24RevivePaddlesState.ReadyWindow);
        }

        void SyncRescueHint(R24LanLobby lobby)
        {
            var rules=lobby==null||lobby.SelectedMap.Results?null:lobby.SelectedMap.Rules;
            R24LanHealthState own;string held=null;float progress=0;R24LanPoseSnapshot chosen=null;
            if(rules!=null&&rules.Mode=="revive"&&rules.Teams>0&&lobby.TryReadHealth(lobby.OwnActor,out own)&&own.Life==LifeState.Alive){
                lobby.TryNearbyReviveProgress(out held,out progress);float nearest=64;bool foundHold=false;
                foreach(var member in presenceMembers){
                    R24LanHealthState h;R24LanPoseSnapshot pose;
                    if(member.Actor==lobby.OwnActor||!rules.EnemyRevive&&member.Team!=own.Team||!lobby.TryReadHealth(member.Actor,out h)||!rules.EnemyRevive&&h.Team!=own.Team||h.Life!=LifeState.Downed||!lobby.TryReadPresence(member.Actor,out pose)||!pose.Pose.Tracked||pose.Pose.Scene!=PresenceScene||Time.realtimeSinceStartupAsDouble-pose.ReceivedAt>1)continue;
                    float d=(RescuePosition(pose.Pose.Root.Position)-root.Rig.transform.position).sqrMagnitude;
                    if(member.Actor==held){chosen=pose;foundHold=true;break;}
                    if(d<=nearest){nearest=d;chosen=pose;}
                }
                if(!foundHold)held=null;
            }
            // HUD19 PADDLE-TEXT: no instruction text near a downed ally; the paddles' own lightning, sound and haptics are the cue. Only the hold-progress meter shows, once a revive is actually under way.
            if(chosen==null||held==null){if(rescueHint!=null)rescueHint.gameObject.SetActive(false);return;}
            if(rescueHint==null)CreateRescueHint();
            var panel=rescueHint.transform;var eye=root.Rig.Head.transform.position;
            Vector3 center=RescuePosition(chosen.Pose.Head.Position)-Vector3.up*.20f,delta=center-eye;
            // Follow the teammate during approach. Once in contact, keep the meter legible even
            // when the seated viewer looks down, turns away, or stands inside the ghost's mesh.
            if(held!=null){
                var view=root.Rig.Head.transform;if(panel.parent!=view)panel.SetParent(view,false);
                panel.localPosition=new Vector3(0,-.18f,1);panel.localRotation=Quaternion.identity;panel.localScale=Vector3.one*.63f;
            }else{
                if(panel.parent!=transform)panel.SetParent(transform,false);
                panel.position=center-Vector3.ClampMagnitude(delta,.20f);
                if(delta.sqrMagnitude>.0001f)panel.rotation=Quaternion.LookRotation(delta);
                panel.localScale=Vector3.one*Mathf.Clamp(delta.magnitude*.68f,.35f,2.8f);
            }
            int layer=held==null?0:5;
            panel.gameObject.layer=layer;
            for(int i=0;i<panel.childCount;i++)panel.GetChild(i).gameObject.layer=layer;
            rescueBack.renderQueue=held==null?3050:4080;rescueInk.renderQueue=held==null?3051:4081;
            rescueBack.SetFloat("_ZTest",held==null?4:8);rescueInk.SetFloat("_ZTest",held==null?4:8);
            rescueHint.gameObject.SetActive(true);
            string label="REVIVING";
            if(rescueHint.text!=label)rescueHint.text=label;
            float width=held==null?0:BarWidth*Mathf.Clamp01(progress);
            rescueFill.localScale=new Vector3(width,.018f,1);
            rescueFill.localPosition=new Vector3((width-BarWidth)*.5f,-.079f,-.005f);
        }
        static Vector3 RescuePosition(Ashfall.Core.Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        void RefreshRescueFont(Font font){if(rescueHint!=null&&font==rescueHint.font)rescueInk.mainTexture=font.material.mainTexture;}
        void CreateRescueHint()
        {
            var go=new GameObject("R26 nearby rescue progress");go.transform.SetParent(transform,false);
            rescueHint=go.AddComponent<TextMesh>();rescueHint.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rescueHint.fontSize=64;rescueHint.characterSize=.006f;rescueHint.anchor=TextAnchor.MiddleCenter;rescueHint.alignment=TextAlignment.Center;
            var shader=Resources.Load<Shader>("R23VrUi");rescueBack=new Material(shader){renderQueue=4080};rescueInk=new Material(shader){renderQueue=4081};
            rescueInk.SetFloat("_Font",1);RefreshRescueFont(rescueHint.font);Font.textureRebuilt+=RefreshRescueFont;
            go.GetComponent<MeshRenderer>().sharedMaterial=rescueInk;go.GetComponent<MeshRenderer>().sortingOrder=2;
            var tex=Texture2D.whiteTexture;rescuePixel=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,.5f),tex.width);
            HintRect("Backdrop",.92f,.20f,0,.006f,new Color(.035f,.08f,.11f,.94f),0);
            HintRect("Revive track",BarWidth,.018f,-.079f,0,new Color(.23f,.33f,.36f),1);
            rescueFill=HintRect("Revive progress",0,.018f,-.079f,-.005f,new Color(.36f,1,.68f),2);
        }
        Transform HintRect(string label,float width,float height,float y,float z,Color color,int order)
        {
            var t=new GameObject(label).transform;t.SetParent(rescueHint.transform,false);t.localPosition=new Vector3(0,y,z);t.localScale=new Vector3(width,height,1);
            var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=rescuePixel;r.sharedMaterial=rescueBack;r.color=color;r.sortingOrder=order;return t;
        }
    }
}
#endif
