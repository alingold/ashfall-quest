using System;
namespace Ashfall.Integration.Playable.Match
{
    // Arena render/play bounds can exclude the authored warmup room below it.
    public static class R25OfflineFallPolicy
    {
        public static float Level(float arenaBottom, float entryHeight)
        {
            if (Single.IsNaN(arenaBottom) || Single.IsInfinity(arenaBottom) ||
                Single.IsNaN(entryHeight) || Single.IsInfinity(entryHeight))
                throw new ArgumentException("Finite world-space arena and entry heights required");
            return Math.Min(arenaBottom, entryHeight) - 12;
        }
    }
}
