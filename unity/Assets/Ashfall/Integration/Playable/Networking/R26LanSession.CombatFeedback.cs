#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Modules.HealthStates;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        double lastLocalVolley=double.NegativeInfinity; // WPN-CX4-SEMI ammo settle clock (SendVolley)
        public int HitConfirmsPresented{get;private set;}
        readonly R24LanRemoteAvatar[] aimAvatars=new R24LanRemoteAvatar[R26RemoteAimVolumes.Capacity];readonly Transform[] aimHeads=new Transform[R26RemoteAimVolumes.Capacity];
        // Every session frame (from SyncAmmo): host-confirmed hit markers, then remote aim volumes.
        void SyncCombatFeedback()
        {
            if(peer==null||root==null||root.Gameplay==null)return;var lobby=peer.Lobby;R26HitConfirm hit;int budget=16;
            while(budget-->0&&lobby.TryDequeueHitConfirm(out hit))if(root.Ready&&(root.Lobby==null||!root.Lobby.Visible))PresentHitConfirm(hit);
            SyncRemoteAim(lobby);
        }
        // MP-HITMARKER: the local practice/bot hit presentation (source hit cue + one damage
        // number). Blades use the admitted contact. Guns use the displayed or host victim head.
        void PresentHitConfirm(R26HitConfirm hit)
        {
            root.Gameplay.PlayConfirmedHitCue();HitConfirmsPresented++;if(root.CombatDamageNumbers==null)return;
            if(hit.BladeContact){root.CombatDamageNumbers.EmitBlade(hit.Total,new Vector3(hit.Point.X,hit.Point.Y,hit.Point.Z),hit.Shield>0);return;}
            R24LanRemoteAvatar view;Transform head;Vector3 anchor;
            if(remoteAvatars.TryGetValue(hit.Victim,out view)&&view!=null&&view.Visible&&(head=AimHead(view))!=null)anchor=head.position;
            else if(hit.Located)anchor=new Vector3(hit.Point.X,hit.Point.Y,hit.Point.Z);else return;
            root.CombatDamageNumbers.Emit(hit.Total,anchor+Vector3.up*.15f,hit.Shield>0);
        }
        // MP-RETICLE: visible, living remote avatars become reticle aim volumes this frame.
        void SyncRemoteAim(R24LanLobby lobby)
        {
            var gameplay=root.Gameplay;gameplay.BeginRemoteAim();if(!root.Ready||lobby.SelectedMap!=null&&lobby.SelectedMap.Rules==null)return;
            foreach(var entry in remoteAvatars){var view=entry.Value;if(view==null||!view.Visible)continue;R24LanHealthState health;if(lobby.TryReadHealth(entry.Key,out health)&&health.Life!=LifeState.Alive)continue;var head=AimHead(view);if(head!=null&&!gameplay.AddRemoteAim(head))break;}
        }
        // R24LanRemoteAvatar keeps its head transform private; resolve the named child once per avatar.
        Transform AimHead(R24LanRemoteAvatar view)
        {
            for(int i=0;i<aimAvatars.Length;i++)if(ReferenceEquals(aimAvatars[i],view)&&aimHeads[i]!=null)return aimHeads[i];
            var head=view.transform.Find("Remote head");if(head==null)return null;
            for(int i=0;i<aimAvatars.Length;i++)if(aimAvatars[i]==null){aimAvatars[i]=view;aimHeads[i]=head;break;}
            return head;
        }
    }
}
#endif
