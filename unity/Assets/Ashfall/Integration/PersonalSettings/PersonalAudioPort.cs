using System;
namespace Ashfall.Integration.PersonalSettings
{
    // Coordinator adapts this to the existing Audio owner; no mixer/router or save
    // implementation is duplicated here. Linear gains and source dB are distinct.
    public interface IPersonalAudioPort
    {
        void ApplyBus(string bus,float linearGain,float sourceDecibels,bool muted);
    }
    public static class PersonalAudioPublication
    {
        // Explicit, idempotent whole-snapshot replay. A thrown assignment means
        // publication is incomplete; durable personal settings are never rolled back.
        public static void Apply(EffectiveSettings snapshot,IPersonalAudioPort port)
        {
            if(snapshot==null||port==null)throw new ArgumentNullException();
            foreach(string bus in new[]{"Master","SFX","Music","Voice","Ambience"})
                port.ApplyBus(bus,snapshot.AudioLinear(bus),snapshot.AudioDecibels(bus),snapshot.AudioMuted(bus));
        }
    }
}
