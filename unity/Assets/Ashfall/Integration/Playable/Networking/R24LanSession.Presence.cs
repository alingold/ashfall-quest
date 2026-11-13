#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        readonly Dictionary<string,R24LanRemoteAvatar> remoteAvatars=new Dictionary<string,R24LanRemoteAvatar>(StringComparer.Ordinal);readonly List<string> retiredAvatars=new List<string>(8);
        R24LanMember[] presenceMembers=new R24LanMember[0];double nextPose;string ownStyle;int ownStyleIndex;
        public int RemoteAvatarCount{get{return remoteAvatars.Count;}}
        public bool TryReadRemoteAvatar(string actor,out R24LanRemoteAvatar avatar){return remoteAvatars.TryGetValue(actor,out avatar);}
        public string PresenceScene{get{return root==null||!root.Ready?null:root.Lobby!=null&&root.Lobby.Visible?"menu":root.Map!=null&&Ashfall.Modules.Networking.Wire.IsHash(root.Map.ManifestSha256)?"map:"+root.Map.ManifestSha256:null;}}
        static Float3 V(Vector3 v){return new Float3(v.x,v.y,v.z);}static R24LanRotation Q(Quaternion q){return new R24LanRotation(q.x,q.y,q.z,q.w);}static R24LanTransform T(Transform t){return new R24LanTransform(V(t.position),Q(t.rotation));}
        R24LanTransform ArtHand(int hand,bool tracked)
        {
            Vector3 position;Quaternion rotation;var raw=hand==0?root.Rig.Left:root.Rig.Right;
            if(tracked&&root.Gameplay.TryReadHandArtPose(hand,out position,out rotation)&&V(position).IsFinite&&(position-root.Rig.transform.position).sqrMagnitude<=25)return new R24LanTransform(V(position),Q(rotation));
            if(tracked&&V(raw.position).IsFinite&&(raw.position-root.Rig.transform.position).sqrMagnitude<=25)return new R24LanTransform(V(raw.position),Q(raw.rotation*R23TrackedGripPose.GloveLocalRotation(hand)));
            return new R24LanTransform(V(root.Rig.Head.transform.position+root.Rig.transform.rotation*new Vector3(hand==0?-.3f:.3f,-.35f,.25f)),Q(root.Rig.transform.rotation*R23TrackedGripPose.GloveLocalRotation(hand)));
        }
        void LateUpdate()
        {
            if(peer==null||root==null||!root.Ready||Time.realtimeSinceStartupAsDouble<nextPose)return;string scene=PresenceScene;if(scene==null)return;nextPose=Time.realtimeSinceStartupAsDouble+.04;
            if(ownStyle!=root.Appearance.SelectedStyleId){ownStyle=root.Appearance.SelectedStyleId;ownStyleIndex=0;foreach(var style in root.Appearance.AvailableStyles)if(style.Id==ownStyle)ownStyleIndex=style.SourceIndex;}
            bool tracked=root.Rig.Tracking&&root.Rig.SessionFocused,lt=tracked&&root.Rig.LeftInput.Tracked,rt=tracked&&root.Rig.RightInput.Tracked;
            bool gliding=root.Activities!=null&&root.Activities.Mode==Ashfall.Modules.Flight.FlightMode.Glide&&!root.Rig.CarrierActive&&!root.Climbing.LeftAnchored&&!root.Climbing.RightAnchored&&!root.Climbing.Mantling&&scene!="menu";
            byte flags=(byte)((tracked?1:0)|(lt?2:0)|(rt?4:0)|(root.Rig.Supported?8:0)|(gliding?16:0)|(root.Rig.Jetting?32:0)|(root.Rig.Riding?64:0));
            peer.Lobby.PublishPresence(new R24LanPose(scene,ownStyleIndex,flags,T(root.Rig.transform),T(root.Rig.Head.transform),ArtHand(0,lt),ArtHand(1,rt),V(root.Rig.MotionVelocity),lt?Mathf.Clamp01(root.Rig.LeftInput.Grip):0,rt?Mathf.Clamp01(root.Rig.RightInput.Grip):0));
            PublishRawTracking();peer.Lobby.PublishWeb(root.Rig.WebState,root.Rig.TakeGrappleShot());
        }
        bool CurrentMember(string actor){foreach(var member in presenceMembers)if(member.Actor==actor)return true;return false;}
        void SyncPresence()
        {
            if(peer==null)return;retiredAvatars.Clear();foreach(string actor in remoteAvatars.Keys)if(!CurrentMember(actor))retiredAvatars.Add(actor);foreach(string actor in retiredAvatars){var retired=remoteAvatars[actor];retired.SetVisible(false);Destroy(retired.gameObject);remoteAvatars.Remove(actor);}
            string scene=PresenceScene;double now=Time.realtimeSinceStartupAsDouble;
            foreach(var member in presenceMembers)if(member.Actor!=peer.Lobby.OwnActor)
            {
                R24LanPoseSnapshot snapshot=null;bool valid=scene!=null&&peer.Lobby.TryReadPresence(member.Actor,out snapshot)&&snapshot.Pose.Scene==scene&&snapshot.Pose.Tracked&&now-snapshot.ReceivedAt<=1;
                R24LanRemoteAvatar view;if(!valid){if(remoteAvatars.TryGetValue(member.Actor,out view))view.SetVisible(false);continue;}
                if(!remoteAvatars.TryGetValue(member.Actor,out view)){view=new GameObject("LAN remote / "+member.Actor).AddComponent<R24LanRemoteAvatar>();view.transform.SetParent(transform,false);remoteAvatars.Add(member.Actor,view);view.Initialize(member.Actor,member.Name,root.Rig.Head,snapshot.Pose);}
                view.Receive(snapshot.Pose,snapshot.ReceivedAt);
                Ashfall.Integration.Playable.Traversal.R25WebState web;peer.Lobby.TryReadWeb(member.Actor,out web);view.SetWeb(web);
                view.SetVisible(view.Failure==null);
            }
        }
        void ClearPresenceViews(){foreach(var view in remoteAvatars.Values)if(view!=null){view.SetVisible(false);Destroy(view.gameObject);}remoteAvatars.Clear();}
    }
}
#endif
