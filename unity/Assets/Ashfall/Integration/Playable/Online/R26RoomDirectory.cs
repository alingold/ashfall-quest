using System;
using System.Threading.Tasks;
namespace Ashfall.Integration.Playable.Online
{
    // One public room as the browser shows it. Code is the room code friends could also type.
    public sealed class PublicRoom
    {
        public readonly string Id,Name,Code,MapId;public readonly int Players,MaxPlayers;public readonly bool MatchRunning;
        public PublicRoom(string id,string name,string code,string mapId,int players,int maxPlayers,bool matchRunning)
        {Id=id??"";Name=String.IsNullOrEmpty(name)?"ASHFALL ROOM":name;Code=code??"";MapId=mapId??"";MaxPlayers=Math.Max(1,maxPlayers);Players=Math.Max(0,Math.Min(players,MaxPlayers));MatchRunning=matchRunning;}
        public bool Full{get{return Players>=MaxPlayers;}}
        public bool SameListing(string name,string code,string mapId,int players,int maxPlayers,bool matchRunning)
        {return Name==(String.IsNullOrEmpty(name)?"ASHFALL ROOM":name)&&Code==(code??"")&&MapId==(mapId??"")&&Players==Math.Max(0,Math.Min(players,Math.Max(1,maxPlayers)))&&MaxPlayers==Math.Max(1,maxPlayers)&&MatchRunning==matchRunning;}
        public bool SameListing(PublicRoom other){return other!=null&&SameListing(other.Name,other.Code,other.MapId,other.Players,other.MaxPlayers,other.MatchRunning);}
    }
    // The public room list service (Unity Lobby in the game, a fake in CPU cases). build keeps
    // rooms of an incompatible game version out of the list.
    public interface IRoomDirectory
    {
        Task<string> Publish(PublicRoom room,string build);
        Task Update(string id,PublicRoom room,string build);
        Task Heartbeat(string id);
        Task Remove(string id);
        Task<PublicRoom[]> List(string build,int count);
    }
    public enum RoomListState { Idle, Loading, Ready, Failed }
    // Pump only from Update; tasks are polled, so no completion mutates Unity off-thread.
    // One request is in flight at a time per direction (listing / own room), and each kind has
    // its own minimum spacing, so the service limits (heartbeat 5 per 30 s, update 5 per 5 s,
    // query 1 per s) cannot be exceeded whatever the callers do.
    public sealed class R26RoomDirectoryFlow
    {
        public const double HeartbeatSeconds=15,UpdateSeconds=3,RetrySeconds=10,RefreshSeconds=2;public const int ListCount=20;
        readonly IRoomDirectory service;readonly string build;
        PublicRoom desired,listed,publishingRoom;string id;Task<string> publishing;Task writing,removing;Task<PublicRoom[]> listing;
        double nextHeartbeat,nextWrite,nextRetry,nextRefresh;
        public PublicRoom[] Rooms{get;private set;}
        public RoomListState ListState{get;private set;}
        public string ListError{get;private set;}
        // True once the service holds this room's listing.
        public bool Listed{get{return id!=null;}}
        public bool Busy{get{return publishing!=null||writing!=null||removing!=null;}}
        public string PublishError{get;private set;}
        public event Action Changed;
        public R26RoomDirectoryFlow(IRoomDirectory service,string build){if(service==null||String.IsNullOrEmpty(build))throw new ArgumentException("Room directory configuration");this.service=service;this.build=build;Rooms=new PublicRoom[0];}
        // The listing this host wants: null for no room or a private (code only) room.
        public void Offer(PublicRoom room){desired=room;}
        public bool Refresh(double now)
        {
            if(listing!=null||now<nextRefresh)return false;nextRefresh=now+RefreshSeconds;
            try{listing=service.List(build,ListCount);ListState=RoomListState.Loading;ListError=null;}
            catch(Exception e){listing=null;ListState=RoomListState.Failed;ListError=Reason(e);}
            Raise();return listing!=null;
        }
        public void Pump(double now)
        {
            if(listing!=null&&listing.IsCompleted)
            {
                var done=listing;listing=null;
                if(done.Status==TaskStatus.RanToCompletion){Rooms=done.Result??new PublicRoom[0];ListState=RoomListState.Ready;ListError=null;}
                else{ListState=RoomListState.Failed;ListError=Reason(done.Exception);}
                Raise();
            }
            if(removing!=null){if(!removing.IsCompleted)return;Observe(removing);removing=null;}
            if(publishing!=null)
            {
                if(!publishing.IsCompleted)return;var done=publishing;publishing=null;
                if(done.Status==TaskStatus.RanToCompletion&&!String.IsNullOrEmpty(done.Result)){id=done.Result;listed=publishingRoom;PublishError=null;nextHeartbeat=now+HeartbeatSeconds;nextWrite=now+UpdateSeconds;}
                else{PublishError=Reason(done.Exception);nextRetry=now+RetrySeconds;}
                publishingRoom=null;Raise();
            }
            if(writing!=null)
            {
                if(!writing.IsCompleted)return;var done=writing;writing=null;
                // A failed heartbeat or update means the listing expired or was removed: list again later.
                if(done.Status!=TaskStatus.RanToCompletion){Observe(done);id=null;listed=null;nextRetry=now+RetrySeconds;Raise();}
            }
            if(desired==null)
            {
                if(id==null)return;string gone=id;id=null;listed=null;
                try{removing=service.Remove(gone);}catch(Exception){removing=null;}
                Raise();return;
            }
            if(id==null)
            {
                if(now<nextRetry)return;publishingRoom=desired;
                try{publishing=service.Publish(desired,build);}catch(Exception e){publishing=null;publishingRoom=null;PublishError=Reason(e);nextRetry=now+RetrySeconds;Raise();}
                return;
            }
            if(!desired.SameListing(listed)&&now>=nextWrite)
            {
                nextWrite=now+UpdateSeconds;var next=desired;
                try{writing=service.Update(id,next,build);listed=next;}catch(Exception){writing=null;}
                return;
            }
            if(now>=nextHeartbeat)
            {
                nextHeartbeat=now+HeartbeatSeconds;
                try{writing=service.Heartbeat(id);}catch(Exception){writing=null;}
            }
        }
        static void Observe(Task task){if(task!=null&&task.IsFaulted){var ignored=task.Exception;}}
        static string Reason(Exception e)
        {
            var aggregate=e as AggregateException;if(aggregate!=null&&aggregate.InnerException!=null)e=aggregate.InnerException;
            return e is OnlineRoomFailure?e.Message:"Cannot reach the room list. Please try again";
        }
        void Raise(){if(Changed!=null)Changed();}
    }
}
