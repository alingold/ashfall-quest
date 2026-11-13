using System;
using Ashfall.Modules.Player;
namespace Ashfall.Integration.PersonalSettings
{
    // Immutable port for generation/revision-authorized PlayerR6 publication.
    // Later personal edits cannot mutate an already published capability.
    public sealed class TypedPlayerSettingsPort : IPlayerSprintSettingsPort
    {
        private readonly EffectiveSettings snapshot;
        public TypedPlayerSettingsPort(EffectiveSettings snapshot)
        {if(snapshot==null)throw new ArgumentNullException("snapshot");this.snapshot=snapshot;}
        public float StaminaSeconds {get{return snapshot.Number("sprint_stamina_seconds");}}
        public float RecoverySeconds {get{return snapshot.Number("sprint_recovery_seconds");}}
        public ForwardReference ForwardReference {get {return snapshot.Text("forward_reference")=="hand"?ForwardReference.Hand:ForwardReference.Head;}}
        public float GroundSpeedGain(float sourceGain)
        {
            float gain=sourceGain;
            if(Math.Abs(sourceGain-7.5f/6.5f)<.00001f)gain=snapshot.Number("sprint_slow_gain");
            else if(Math.Abs(sourceGain-9.5f/6.5f)<.00001f)gain=snapshot.Number("sprint_fast_gain");
            return gain*snapshot.WalkBaseMetersPerSecond/PlayerTuning.WalkSpeed;
        }
    }
}
