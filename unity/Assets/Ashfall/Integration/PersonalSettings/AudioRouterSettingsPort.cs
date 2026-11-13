using System;
using Ashfall.Modules.Audio;
namespace Ashfall.Integration.PersonalSettings
{
    // Source bus dB floors are applied to the real Audio owner. Only Master is
    // source-muted at zero; non-master zero retains its source -60 dB floor.
    public sealed class AudioRouterSettingsPort : IPersonalAudioPort
    {
        private readonly AudioRouter router;
        public AudioRouterSettingsPort(AudioRouter router)
        { if(router==null)throw new ArgumentNullException("router");this.router=router; }
        public void ApplyBus(string bus,float linearGain,float sourceDecibels,bool muted)
        { router.SetBus(bus,bus=="Master"&&muted?0:(float)Math.Pow(10,sourceDecibels/20)); }
    }
}
