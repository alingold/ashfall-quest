using System;
using Ashfall.Core;

namespace Ashfall.Modules.Modes
{
    // Coordinator supplies current authoritative roster, never client-supplied team/life data.
    public struct ModeActor
    {
        public readonly string Id, Name, Life;
        public readonly int Team;
        public readonly bool Loaded, Spectator;
        public ModeActor(string id, string name, int team, string life, bool loaded, bool spectator)
        { Id=id; Name=name; Team=team; Life=life; Loaded=loaded; Spectator=spectator; }
    }
    public interface IModeRoster
    {
        bool TryGet(string actorId, out ModeActor actor);
        ModeActor[] Snapshot();
    }
    // Applied AFTER authenticated server knockdown; HealthStates remains life-state owner.
    // LifeSequence must increase on every new alive life (spawn OR revive).
    public struct Knockdown
    {
        public readonly string VictimId, KillerId;
        public readonly long LifeSequence;
        public Knockdown(string victimId, string killerId, long lifeSequence)
        { VictimId=victimId; KillerId=killerId; LifeSequence=lifeSequence; }
    }
    public struct LiveLimits
    {
        public readonly int ScoreLimit, TimeLimit;
        public LiveLimits(int scoreLimit,int timeLimit)
        { ScoreLimit=Math.Max(0,Math.Min(500,scoreLimit)); TimeLimit=Math.Max(0,Math.Min(3600,timeLimit)); }
    }
    public enum RoundStage { Loading, Match, Results, Complete, Lobby, Countdown }
    public struct ScoreRow
    {
        public readonly string Id, Name;
        public readonly int Score;
        public ScoreRow(string id, int score) : this(id,id,score) { }
        public ScoreRow(string id,string name,int score) { Id=id; Name=name; Score=score; }
    }
}
