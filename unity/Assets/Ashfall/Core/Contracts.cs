using System;

namespace Ashfall.Core
{
    // Engine-neutral SI data. Unity adapters convert only at the engine boundary.
    public struct Float3
    {
        public readonly float X, Y, Z;
        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool IsFinite { get { return Finite(X) && Finite(Y) && Finite(Z); } }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        public static Float3 FromGodot(float x, float y, float z) { return new Float3(x, y, -z); }
    }
    public struct CommandContext
    {
        public readonly string SessionId, ActorId, CommandId;
        public readonly long Tick, AuthorityEpoch;
        public readonly bool IsAuthority;
        public CommandContext(string sessionId, string actorId, string commandId, long tick, long authorityEpoch, bool isAuthority)
        { SessionId = sessionId; ActorId = actorId; CommandId = commandId; Tick = tick; AuthorityEpoch = authorityEpoch; IsAuthority = isAuthority; }
    }
    public struct RuleResult
    {
        public readonly bool Accepted;
        public readonly string Reason;
        public RuleResult(bool accepted, string reason) { Accepted = accepted; Reason = reason; }
        public static RuleResult Reject(string reason) { return new RuleResult(false, reason); }
        public static RuleResult Accept() { return new RuleResult(true, String.Empty); }
    }
    public struct InputFrame
    {
        public long Tick, Sequence;
        public float MoveX, MoveY, TurnX, TurnY;
        public bool Tracked, Paused, Jump, Sprint, GripLeft, GripRight, TriggerLeft, TriggerRight, Reload, Menu, Ping;
        public Float3 HeadPosition, LeftHandPosition, RightHandPosition;
    }
    public interface IInputSource { InputFrame ReadFrame(long tick); }
    public interface IAuthorityGate { RuleResult Validate(CommandContext context); }
    public interface IInteractionQuery
    {
        bool HasLineOfSight(string actorId, string semanticOwnerId, Float3 origin, Float3 target, float range);
    }
    public interface ISettingsStore
    {
        bool TryRead(string key, out string value);
        void Write(string key, string value);
        RuleResult Flush();
    }
    public interface INetworkTransport : IDisposable
    {
        void Send(string peerId, byte[] payload, bool reliable);
        bool TryReceive(out string peerId, out byte[] payload);
    }
    public static class Protocol
    {
        public const string Name = "ASHFALL-UNITY/1";
        public const int Schema = 1;
    }
}
