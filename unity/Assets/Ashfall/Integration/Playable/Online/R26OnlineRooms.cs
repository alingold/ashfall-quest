using System;
using System.Text;
using System.Threading.Tasks;
namespace Ashfall.Integration.Playable.Online
{
    public enum OnlineRoomState { Idle, SigningIn, CreatingRoom, RoomCode, Joining, Connected, Failed }
    public sealed class OnlineRoom : IDisposable
    {
        public readonly string Code;
        // Opaque engine transport parameters; never serialized onto the lobby wire.
        public readonly object TransportData;
        public bool Disposed { get; private set; }
        public OnlineRoom(string code, object data) { Code=code; TransportData=data; }
        public void Dispose() { Disposed=true; }
    }
    public interface IOnlineRooms
    {
        bool IsConfigured { get; }
        Task SignIn();
        Task<OnlineRoom> CreateRoom(int maxPlayers);
        Task<OnlineRoom> JoinRoom(string code);
    }
    public sealed class OnlineRoomFailure : Exception
    {
        public OnlineRoomFailure(string reason) : base(reason) { }
    }
    // Pump only from Update. Tasks are polled, so no completion can mutate Unity off-thread.
    public sealed class R26OnlineRoomFlow
    {
        public const string NotSetUp="Online play is not set up in this build";
        readonly IOnlineRooms service;
        Task signing;
        Task<OnlineRoom> allocating;
        double deadline;
        bool hosting;
        string requestedCode;
        public OnlineRoomState State { get; private set; }
        public string Reason { get; private set; }
        public OnlineRoom Room { get; private set; }
        public bool Hosting { get { return hosting; } }
        public event Action Changed;
        public R26OnlineRoomFlow(IOnlineRooms service) { this.service=service; }
        public static string NormalizeCode(string code)
        {
            var b=new StringBuilder();
            foreach(char c in code??"") if(!Char.IsWhiteSpace(c)) b.Append(Char.ToUpperInvariant(c));
            return b.ToString();
        }
        public bool Begin(bool host,string code,double now)
        {
            if(State!=OnlineRoomState.Idle && State!=OnlineRoomState.Failed) return false;
            hosting=host; requestedCode=NormalizeCode(code); Reason=null;
            if(!service.IsConfigured) { Fail(NotSetUp); return false; }
            if(!host && (requestedCode.Length!=6 || !ValidCode(requestedCode))) { Fail("That room code was not found"); return false; }
            deadline=now+30;
            try { signing=service.SignIn(); Set(OnlineRoomState.SigningIn); }
            catch(Exception e) { Fail(ReasonFor(e)); }
            return State!=OnlineRoomState.Failed;
        }
        static bool ValidCode(string code) { foreach(char c in code) if(!(c>='A'&&c<='Z') && !(c>='0'&&c<='9')) return false; return true; }
        public void Pump(double now)
        {
            if(State!=OnlineRoomState.SigningIn && State!=OnlineRoomState.CreatingRoom && State!=OnlineRoomState.Joining) return;
            if(now>=deadline) { Fail("The connection timed out. Please try again"); return; }
            try
            {
                if(signing!=null && signing.IsCompleted)
                {
                    signing.GetAwaiter().GetResult(); signing=null;
                    allocating=hosting?service.CreateRoom(8):service.JoinRoom(requestedCode);
                    Set(hosting?OnlineRoomState.CreatingRoom:OnlineRoomState.Joining);
                }
                if(allocating!=null && allocating.IsCompleted)
                {
                    Room=allocating.GetAwaiter().GetResult(); allocating=null;
                    Set(hosting?OnlineRoomState.RoomCode:OnlineRoomState.Joining);
                }
            }
            catch(Exception e) { Fail(ReasonFor(e)); }
        }
        public void MarkConnected() { if(Room!=null) Set(hosting?OnlineRoomState.RoomCode:OnlineRoomState.Connected); }
        public void Fail(string reason) { Clear(); Reason=reason; Set(OnlineRoomState.Failed); }
        public void Leave() { Clear(); Reason=null; Set(OnlineRoomState.Idle); }
        void Clear()
        {
            // UGS requests cannot be cancelled. Observe faults and discard late results.
            if(signing!=null) signing.ContinueWith(delegate(Task t) { if(t.IsFaulted) { var ignored=t.Exception; } },TaskContinuationOptions.ExecuteSynchronously);
            if(allocating!=null) allocating.ContinueWith(delegate(Task<OnlineRoom> t) { if(t.Status==TaskStatus.RanToCompletion) t.Result.Dispose(); else if(t.IsFaulted) { var ignored=t.Exception; } },TaskContinuationOptions.ExecuteSynchronously);
            signing=null; allocating=null;
            if(Room!=null) Room.Dispose(); Room=null;
        }
        static string ReasonFor(Exception e) { return e is OnlineRoomFailure?e.Message:"Cannot connect to online play. Please try again"; }
        void Set(OnlineRoomState state) { State=state; if(Changed!=null) Changed(); }
    }
}
