#if UNITY_6000_0_OR_NEWER
using System;using Ashfall.Integration.Playable.Online;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    // ROOM-PUBLIC-LIST: a code room is listed publicly unless its host makes it private.
    // The listing follows the real room (host name, map, players, match running) and is
    // removed when the room ends; the service also drops it 30 s after heartbeats stop.
    public sealed partial class R24LanSession
    {
        R26RoomDirectoryFlow directory;PublicRoom offered;bool roomPrivate,publicRoomsBound;string listedHostName;
        public bool RoomPrivate{get{return roomPrivate;}}
        public bool RoomListed{get{return directory!=null&&directory.Listed;}}
        public PublicRoom[] PublicRooms{get{return directory==null?Array.Empty<PublicRoom>():Array.FindAll(directory.Rooms,room=>World.R23MapCatalog.Find(room.MapId)!=null);}}
        public RoomListState PublicRoomsState{get{return directory==null?RoomListState.Idle:directory.ListState;}}
        public void SetRoomPrivate(bool value){roomPrivate=value;PublishPublicRooms();}
        public void RefreshPublicRooms(){if(!EnsureDirectory()){if(root!=null&&root.UI!=null)root.UI.SetPublicRooms(new PublicRoom[0],RoomListState.Failed,R26OnlineRoomFlow.NotSetUp);return;}directory.Refresh(Time.realtimeSinceStartupAsDouble);}
        bool EnsureDirectory()
        {
            if(directory!=null)return true;if(!new R26UnityOnlineRooms().IsConfigured)return false;
            directory=new R26RoomDirectoryFlow(new R26UnityRoomDirectory(),R24LanPeer.LobbyContent);directory.Changed+=PublishPublicRooms;return true;
        }
        void PublishPublicRooms()
        {if(root==null||root.UI==null)return;root.UI.SetRoomPrivacy(roomPrivate,RoomListed);if(directory!=null)root.UI.SetPublicRooms(PublicRooms,directory.ListState,directory.ListError);}
        void BindPublicRooms()
        {if(publicRoomsBound||root==null||root.UI==null)return;publicRoomsBound=true;root.UI.PublicRoomsRequested+=RefreshPublicRooms;root.UI.RoomPrivacyRequested+=SetRoomPrivate;}
        void UnbindPublicRooms()
        {if(!publicRoomsBound)return;publicRoomsBound=false;if(root!=null&&root.UI!=null){root.UI.PublicRoomsRequested-=RefreshPublicRooms;root.UI.RoomPrivacyRequested-=SetRoomPrivate;}}
        // Best effort when the session object goes away; the service drops an unremoved listing 30 s after its last heartbeat.
        void ClosePublicRoom(){offered=null;if(directory!=null){directory.Offer(null);directory.Pump(Time.realtimeSinceStartupAsDouble);}}
        // Every frame, allocation-free unless the listing itself changes.
        void PumpPublicRooms()
        {
            BindPublicRooms();
            bool list=Hosting&&!roomPrivate&&OnlineState==OnlineRoomState.RoomCode&&OnlineCode.Length>0;
            if(directory==null&&(!list||!EnsureDirectory()))return;
            if(!list){offered=null;listedHostName=null;}
            else
            {
                var lobby=peer.Lobby;bool running=room.Stage==R26RoomStage.Match||room.Stage==R26RoomStage.Starting;
                // member.Name is the single display-name source; read it once per room (Members clones).
                if(listedHostName==null){listedHostName="";foreach(var member in lobby.Members)if(member.Host)listedHostName=member.Name;}
                if(offered==null||!offered.SameListing(listedHostName,OnlineCode,lobby.RoomMapId,lobby.MemberCount,lobby.RoomCapacity,running))
                    offered=new PublicRoom(null,listedHostName,OnlineCode,lobby.RoomMapId,lobby.MemberCount,lobby.RoomCapacity,running);
            }
            directory.Offer(offered);directory.Pump(Time.realtimeSinceStartupAsDouble);
        }
    }
}
#endif
