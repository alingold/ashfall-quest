using Ashfall.Core;

namespace Ashfall.Modules.Audio
{
    // Orthogonal to spatialization: a local weapon is still a spatial emitter.
    public enum AudioEmitterRole { Remote = 0, Local = 1 }
    // Presentation only: callers authorize gameplay/network events before submitting them.
    public interface IAudioVoice
    {
        void Set(float gain, float pitch, Float3 position);
        void Pause(bool paused);
        void Stop();
    }
    public interface IAudioPlayback
    {
        IAudioVoice Start(AudioAsset asset, bool spatial, float gain, float pitch, Float3 position);
        void SetBus(string bus, float gain);
        void StopTransients();
    }
}
