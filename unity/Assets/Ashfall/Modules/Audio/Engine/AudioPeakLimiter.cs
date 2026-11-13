#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Modules.Audio
{
    // Put on each source (automatic) and the final AudioListener (coordinator wiring).
    [DisallowMultipleComponent]
    public sealed class AudioPeakLimiter : MonoBehaviour
    {
        private readonly PeakLimiter limiter=new PeakLimiter();
        private int sampleRate=48000;
        private void Awake(){sampleRate=AudioSettings.outputSampleRate;}
        private void OnAudioConfigurationChanged(bool changed){sampleRate=AudioSettings.outputSampleRate;}
        private void OnAudioFilterRead(float[] data,int channels){limiter.Process(data,channels,sampleRate);}
    }
}
#endif
