#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Networking;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R25NetBot
    {
        bool RemoteEvidenceEnabled{get{return Arg("-ashfallNetBotRemoteEvidence")=="true";}}
        int evidenceShots,evidenceLife=-1,evidenceCount;bool evidenceHeld;float nextEvidence;int evidenceBuilds=-1;
        void CaptureRemoteEvidence()
        {
            if(!RemoteEvidenceEnabled||root.LanSession.Lobby==null||evidenceCount>=24)return;var lobby=root.LanSession.Lobby;
            foreach(var member in lobby.Members){if(member.Actor==lobby.OwnActor)continue;R24LanRemoteAvatar view;if(!root.LanSession.TryReadRemoteAvatar(member.Actor,out view)||!view.Visible)continue;
                R24LanHealthState health;if(!lobby.TryReadHealth(member.Actor,out health))continue;int shots=root.LanSession.RemoteShotsPresented;int life=(int)health.Life;
                bool held=view.WeaponVisible;bool capture=held&&!evidenceHeld||life!=evidenceLife&&health.Life!=LifeState.Alive||shots>evidenceShots&&UnityEngine.Time.unscaledTime>=nextEvidence;
                if(capture){Capture(100+evidenceCount++);nextEvidence=UnityEngine.Time.unscaledTime+1;Write("remote-evidence","actor="+member.Actor+" weapon="+view.HeldProfile+" held="+held+" ghost="+view.GhostVisible+" life="+health.Life+" volleys="+shots+" sounds="+root.LanSession.RemoteSoundsPresented+" builds="+view.EquipmentBuilds);}
                if(evidenceBuilds!=view.EquipmentBuilds){evidenceBuilds=view.EquipmentBuilds;Write("remote-equipment-builds",evidenceBuilds.ToString());}evidenceHeld=held;evidenceLife=life;evidenceShots=shots;
            }
        }
    }
}
#endif
