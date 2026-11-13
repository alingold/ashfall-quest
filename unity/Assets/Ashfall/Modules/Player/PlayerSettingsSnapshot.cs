using System;
namespace Ashfall.Modules.Player
{
    // Locomotion requests exactly these three source tiers. Capture their values,
    // rather than retaining a caller-controlled function in a committed frame.
    public sealed class PlayerSettingsSnapshot : IPlayerSprintSettingsPort
    {
        readonly float walk,slow,fast,stamina,recovery;
        readonly ForwardReference forward;
        PlayerSettingsSnapshot(IPlayerSettingsPort source)
        {
            forward=source.ForwardReference;
            walk=source.GroundSpeedGain(1);
            slow=source.GroundSpeedGain(7.5f/6.5f);
            fast=source.GroundSpeedGain(9.5f/6.5f);
            var sprint=source as IPlayerSprintSettingsPort;
            stamina=sprint==null?6:sprint.StaminaSeconds;recovery=sprint==null?4:sprint.RecoverySeconds;
            if(!Enum.IsDefined(typeof(ForwardReference),forward)||!Gain(walk)||!Gain(slow)||!Gain(fast)||
                !PlayerInput.Finite(stamina)||stamina<2||stamina>20||!PlayerInput.Finite(recovery)||recovery<1||recovery>20)
                throw new ArgumentException("Effective settings values");
        }
        static bool Gain(float value){return PlayerInput.Finite(value)&&value>=0&&value<=100;}
        public static PlayerSettingsSnapshot Capture(IPlayerSettingsPort source)
        {if(source==null)throw new ArgumentNullException("source");return source as PlayerSettingsSnapshot??new PlayerSettingsSnapshot(source);}
        public ForwardReference ForwardReference {get{return forward;}}
        public float StaminaSeconds {get{return stamina;}}
        public float RecoverySeconds {get{return recovery;}}
        public float GroundSpeedGain(float sourceGain)
        {
            if(sourceGain==1)return walk;
            if(sourceGain==7.5f/6.5f)return slow;
            if(sourceGain==9.5f/6.5f)return fast;
            throw new ArgumentOutOfRangeException("sourceGain","Unsupported source locomotion tier");
        }
    }
}
