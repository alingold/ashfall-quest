#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
namespace Ashfall.Integration.Playable.Online
{
    // Public room list on Unity Lobby (same cloud project and anonymous sign-in as Relay).
    // The Lobby entry is only a directory row owned by the room host: guests never join the
    // Lobby itself, they read the room code from it and join over Relay exactly as with a typed code.
    public sealed class R26UnityRoomDirectory : IRoomDirectory
    {
        const string CodeKey="code",MapKey="map",PlayersKey="players",MatchKey="match",BuildKey="build";
        static Dictionary<string,DataObject> Data(PublicRoom room,string build)
        {
            return new Dictionary<string,DataObject>{
                {CodeKey,new DataObject(DataObject.VisibilityOptions.Public,room.Code)},
                {MapKey,new DataObject(DataObject.VisibilityOptions.Public,room.MapId)},
                {PlayersKey,new DataObject(DataObject.VisibilityOptions.Public,room.Players.ToString(System.Globalization.CultureInfo.InvariantCulture))},
                {MatchKey,new DataObject(DataObject.VisibilityOptions.Public,room.MatchRunning?"1":"0")},
                // Indexed so the query only returns rooms of this exact build.
                {BuildKey,new DataObject(DataObject.VisibilityOptions.Public,build,DataObject.IndexOptions.S1)}};
        }
        public async Task<string> Publish(PublicRoom room,string build)
        {
            try
            {
                await new R26UnityOnlineRooms().SignIn();
                var lobby=await LobbyService.Instance.CreateLobbyAsync(room.Name,room.MaxPlayers,new CreateLobbyOptions{IsPrivate=false,Data=Data(room,build)});
                return lobby.Id;
            }
            catch(Exception e){throw Friendly(e);}
        }
        public async Task Update(string id,PublicRoom room,string build)
        {
            try{await LobbyService.Instance.UpdateLobbyAsync(id,new UpdateLobbyOptions{Name=room.Name,MaxPlayers=room.MaxPlayers,Data=Data(room,build)});}
            catch(Exception e){throw Friendly(e);}
        }
        public async Task Heartbeat(string id)
        {
            try{await LobbyService.Instance.SendHeartbeatPingAsync(id);}
            catch(Exception e){throw Friendly(e);}
        }
        public async Task Remove(string id)
        {
            try{await LobbyService.Instance.DeleteLobbyAsync(id);}
            catch(Exception e){throw Friendly(e);}
        }
        public async Task<PublicRoom[]> List(string build,int count)
        {
            try
            {
                await new R26UnityOnlineRooms().SignIn();
                var response=await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions{
                    Count=Mathf.Clamp(count,1,100),
                    Filters=new List<QueryFilter>{new QueryFilter(QueryFilter.FieldOptions.S1,build,QueryFilter.OpOptions.EQ)},
                    Order=new List<QueryOrder>{new QueryOrder(false,QueryOrder.FieldOptions.Created)}});
                var rooms=new List<PublicRoom>();
                if(response!=null&&response.Results!=null)foreach(var lobby in response.Results)
                {
                    string code=Read(lobby,CodeKey);if(code.Length==0)continue;int players;
                    if(!Int32.TryParse(Read(lobby,PlayersKey),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out players))players=1;
                    rooms.Add(new PublicRoom(lobby.Id,lobby.Name,code,Read(lobby,MapKey),players,lobby.MaxPlayers,Read(lobby,MatchKey)=="1"));
                }
                return rooms.ToArray();
            }
            catch(Exception e){throw Friendly(e);}
        }
        static string Read(Unity.Services.Lobbies.Models.Lobby lobby,string key){DataObject value;return lobby.Data!=null&&lobby.Data.TryGetValue(key,out value)&&value!=null&&value.Value!=null?value.Value:"";}
        static Exception Friendly(Exception e)
        {
            if(e is OnlineRoomFailure)return e;
            var lobby=e as LobbyServiceException;
            Debug.LogWarning("ASHFALL ROOM LIST service error: "+e.GetType().FullName+(lobby!=null?" reason="+lobby.Reason:"")+" | "+e.Message);
            if(lobby!=null&&lobby.Reason==LobbyExceptionReason.RateLimited)return new OnlineRoomFailure("The room list is busy. Try again in a moment");
            if(lobby!=null&&lobby.Reason==LobbyExceptionReason.NetworkError)return new OnlineRoomFailure("No internet connection");
            return new OnlineRoomFailure("Cannot reach the room list. Please try again");
        }
    }
}
#endif
