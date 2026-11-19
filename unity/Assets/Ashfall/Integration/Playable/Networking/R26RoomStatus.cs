using System;
namespace Ashfall.Integration.Playable.Networking
{
    public enum R26RoomStage { None, Joining, Lobby, Loading, Starting, FreeRoam, Match, Results }
    // One immutable picture of the real room for every menu (room panel, pause menu, Match Rules).
    // It is read from the lobby/Session owners only; no menu infers "hosting", "match running"
    // or "loading" from its own button history. Pure C#: no engine, transport or UI dependency.
    public sealed class R26RoomStatus
    {
        public static readonly R26RoomStatus Offline=new R26RoomStatus(R26RoomStage.None,false,"","",0,0,0,0,0,0,false);
        public readonly R26RoomStage Stage;public readonly bool Host,Busy;public readonly string MapId,Preset;
        public readonly int Members,Ready,Capacity,Countdown,Round,Rounds;
        public R26RoomStatus(R26RoomStage stage,bool host,string mapId,string preset,int members,int ready,int capacity,int countdown,int round,int rounds,bool busy)
        {Stage=stage;Host=host;MapId=mapId??"";Preset=preset??"";Members=Math.Max(0,members);Ready=Math.Max(0,Math.Min(ready,Members));Capacity=Math.Max(0,capacity);Countdown=Math.Max(0,countdown);Round=Math.Max(0,round);Rounds=Math.Max(0,rounds);Busy=busy;}
        public bool InRoom{get{return Stage!=R26RoomStage.None;}}
        public bool Guest{get{return InRoom&&!Host;}}
        // The room is between worlds: its members are behind the loading barrier or the start countdown.
        public bool InTransition{get{return Stage==R26RoomStage.Joining||Stage==R26RoomStage.Loading||Stage==R26RoomStage.Starting;}}
        public bool MatchRunning{get{return Stage==R26RoomStage.Starting||Stage==R26RoomStage.Match;}}
        // Host controls (start, end, restart, map, home) act only on a settled room.
        public bool HostMayAct{get{return Host&&!Busy&&!InTransition;}}
        public bool Same(R26RoomStage stage,bool host,string mapId,string preset,int members,int ready,int capacity,int countdown,int round,int rounds,bool busy)
        {return Stage==stage&&Host==host&&Busy==busy&&Members==members&&Ready==Math.Max(0,Math.Min(ready,members))&&Capacity==capacity&&Countdown==countdown&&Round==round&&Rounds==rounds&&MapId==(mapId??"")&&Preset==(preset??"");}
        // hostPreparing: the host accepted a map/match request whose manifest is still being read,
        // so the barrier shows on the same frame as the press instead of an idle lobby.
        public static R26RoomStage StageOf(R24LanLobbyState state,R24LanMapSelection map,double countdown,bool hostPreparing)
        {
            if(state==R24LanLobbyState.Offline)return R26RoomStage.None;
            if(state==R24LanLobbyState.Connecting)return R26RoomStage.Joining;
            if(hostPreparing)return R26RoomStage.Loading;
            if(map==null||map.Cancelled)return R26RoomStage.Lobby; // HOME is a cancelled world too.
            if(!map.Active)return R26RoomStage.Loading;
            if(map.Rules==null)return R26RoomStage.FreeRoam;
            if(map.Results)return R26RoomStage.Results;
            return countdown>0?R26RoomStage.Starting:R26RoomStage.Match;
        }
        public string Headline()
        {
            switch(Stage)
            {
                case R26RoomStage.Joining:return "JOINING ROOM";
                case R26RoomStage.Lobby:return "ROOM LOBBY";
                case R26RoomStage.Loading:return Preset.Length>0?"LOADING MATCH":"LOADING MAP";
                case R26RoomStage.Starting:return "MATCH STARTS IN "+Math.Max(1,Countdown);
                case R26RoomStage.FreeRoam:return "FREE ROAM";
                case R26RoomStage.Match:return Rounds>1?"MATCH RUNNING  ROUND "+Round+" / "+Rounds:"MATCH RUNNING";
                case R26RoomStage.Results:return "MATCH RESULTS";
                default:return "LOCAL OFFLINE";
            }
        }
        public string Detail()
        {
            if(!InRoom||Stage==R26RoomStage.Joining)return "";
            if(Stage==R26RoomStage.Loading)return Ready+" / "+Members+" PLAYERS READY";
            return Members+" / "+Capacity+" PLAYERS";
        }
        // Root publishes one local match line whose first field names an authority ("LOCAL OFFLINE",
        // "LAN ROUND | HOST HEALTH / COMBAT"). In a room that field is replaced by the real room.
        public static string MatchDetail(string localStatus)
        {
            string text=(localStatus??"").Trim();
            foreach(string label in new[]{"LAN ROUND | HOST HEALTH / COMBAT","LOCAL OFFLINE"})
                if(text.StartsWith(label,StringComparison.OrdinalIgnoreCase)){text=text.Substring(label.Length).TrimStart(' ','|');break;}
            return text;
        }
    }
}
