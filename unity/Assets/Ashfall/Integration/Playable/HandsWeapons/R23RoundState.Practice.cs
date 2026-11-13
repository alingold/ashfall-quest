using System;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23RoundState
    {
        // The authored dummy has its own _process, independent of the held gun,
        // player life, menu input and weapon reload clock. Standalone mechanics
        // consumers retain their old internal clock until a real scene binds it.
        public bool ExternalPracticeClock{get;set;}
        public void AdvancePracticeTargets(float dt)
        {
            if(Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}
        }
        void AdvanceLegacyPracticeTargets(float dt){if(!ExternalPracticeClock)AdvancePracticeTargets(dt);}
    }
}
