#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed class R25NetDiagnostics : MonoBehaviour
    {
        float next;
        R23PlayableLoop root;
        void Update()
        {
            if (Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + 2;
            if (root == null) root = GetComponent<R23PlayableLoop>();
            var session = root == null ? null : root.LanSession;
            var lobby = session == null ? null : session.Lobby;
            // Online state and room code let the PC test bot find a Quest-hosted room from logcat.
            string online = session == null ? "" : " online=" + session.OnlineState +
                (string.IsNullOrEmpty(session.OnlineCode) ? "" : " code=" + session.OnlineCode);
            if (lobby == null)
            {
                if (session != null && session.OnlineState != Ashfall.Integration.Playable.Online.OnlineRoomState.Idle)
                    Debug.Log("ASHFALL NET role=none state=Offline" + online + " reason=" + session.OnlineReason);
                return;
            }
            Debug.Log("ASHFALL NET role=" + (lobby.State == R24LanLobbyState.Hosting ? "host" : "client") +
                " state=" + lobby.State + " members=" + lobby.Members.Length + online +
                " messagesIn=" + session.MessagesIn + " messagesOut=" + session.MessagesOut +
                " lastError=" + (lobby.HealthStorageFailure ?? lobby.LastShotRejection ?? lobby.Status));
        }
    }
}
#endif
