using System;
using System.Threading.Tasks;
using Ashfall.Integration.Playable.Online;
partial class LobbyCases
{
    sealed class FakeOnlineRooms : IOnlineRooms
    {
        public bool Configured=true;
        public int Signs,Creates,Joins,Capacity;
        public string Code;
        public TaskCompletionSource<bool> Sign=new TaskCompletionSource<bool>();
        public TaskCompletionSource<OnlineRoom> Allocation=new TaskCompletionSource<OnlineRoom>();
        public bool IsConfigured { get { return Configured; } }
        public Task SignIn() { Signs++; return Sign.Task; }
        public Task<OnlineRoom> CreateRoom(int maxPlayers) { Creates++; Capacity=maxPlayers; return Allocation.Task; }
        public Task<OnlineRoom> JoinRoom(string code) { Joins++; Code=code; return Allocation.Task; }
    }
    static void OnlineRoomsCases()
    {
        var service=new FakeOnlineRooms(); var flow=new R26OnlineRoomFlow(service);
        Need(flow.Begin(true,"",0)&&flow.State==OnlineRoomState.SigningIn,"Online starts sign-in");
        Need(!flow.Begin(true,"",0)&&service.Signs==1,"Double press ignored");
        service.Sign.SetResult(true); flow.Pump(1);
        Need(flow.State==OnlineRoomState.CreatingRoom&&service.Capacity==8,"Host reserves seven joiners");
        var room=new OnlineRoom("ABC123",null);service.Allocation.SetResult(room);flow.Pump(2);
        Need(flow.State==OnlineRoomState.RoomCode&&flow.Room.Code=="ABC123","Host gets code");
        flow.Leave();Need(room.Disposed&&flow.State==OnlineRoomState.Idle,"Leave releases room");
        service=new FakeOnlineRooms();flow=new R26OnlineRoomFlow(service);flow.Begin(false," a b c 1 2 3 ",0);service.Sign.SetResult(true);flow.Pump(1);
        Need(service.Code=="ABC123"&&service.Joins==1,"Code normalization");service.Allocation.SetResult(new OnlineRoom(service.Code,null));flow.Pump(2);flow.MarkConnected();Need(flow.State==OnlineRoomState.Connected,"Join connects");flow.Leave();
        foreach(string reason in new[]{"That room code was not found","The room is full","No internet connection"})
        {
            service=new FakeOnlineRooms();flow=new R26OnlineRoomFlow(service);flow.Begin(false,"ABC123",0);service.Sign.SetResult(true);flow.Pump(1);service.Allocation.SetException(new OnlineRoomFailure(reason));flow.Pump(2);
            Need(flow.State==OnlineRoomState.Failed&&flow.Reason==reason,"Plain service failure "+reason);
        }
        service=new FakeOnlineRooms();flow=new R26OnlineRoomFlow(service);flow.Begin(true,"",0);flow.Leave();service.Sign.SetResult(true);flow.Pump(1);Need(service.Creates==0&&flow.State==OnlineRoomState.Idle,"Leave during sign-in cannot host late");
        service=new FakeOnlineRooms();flow=new R26OnlineRoomFlow(service);flow.Begin(true,"",0);flow.Pump(31);Need(flow.State==OnlineRoomState.Failed&&flow.Reason.Contains("timed out"),"Bounded sign-in timeout");service.Sign.SetResult(true);flow.Pump(32);Need(service.Creates==0,"Late timeout completion ignored");
        service=new FakeOnlineRooms();flow=new R26OnlineRoomFlow(service);flow.Begin(true,"",0);service.Sign.SetResult(true);flow.Pump(1);flow.Pump(31);Need(flow.State==OnlineRoomState.Failed,"Bounded allocation timeout");
        service=new FakeOnlineRooms();flow=new R26OnlineRoomFlow(service);flow.Begin(true,"",0);service.Sign.SetResult(true);flow.Pump(1);flow.Leave();room=new OnlineRoom("ABC123",null);service.Allocation.SetResult(room);flow.Pump(2);Need(room.Disposed&&flow.Room==null&&flow.State==OnlineRoomState.Idle,"Leave during allocation releases late result without reconnecting");
        service=new FakeOnlineRooms();service.Configured=false;flow=new R26OnlineRoomFlow(service);flow.Begin(true,"",0);Need(flow.Reason==R26OnlineRoomFlow.NotSetUp&&service.Signs==0,"Missing project fails without network");
        service=new FakeOnlineRooms();flow=new R26OnlineRoomFlow(service);flow.Begin(false,"wrong!",0);Need(flow.State==OnlineRoomState.Failed&&service.Signs==0,"Malformed code rejected locally");
    }
}
