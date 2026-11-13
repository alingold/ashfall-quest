#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;

namespace Ashfall.Integration.Playable.Soundscape
{
    // One final mix gain preserves the established shot/reverb/glide/pod balance.
    // A stereo-linked block peak detector attenuates overloads without clipping
    // the waveform. Attack anticipates the current DSP block; release is 80 ms.
    [DisallowMultipleComponent]
    public sealed class R28AudioOutput : MonoBehaviour
    {
        public const float MixGain = 1.4125375f; // +3 dB
        public const float Ceiling = .84139514f; // -1.5 dBFS, true-peak headroom
        float envelope = 1;
        int rate;
        void OnEnable() { rate = UnityEngine.AudioSettings.outputSampleRate; envelope = 1; }
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels < 1 || data.Length == 0) return;
            float peak = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample)) sample = 0;
                data[i] = sample * MixGain;
                peak = Math.Max(peak, Math.Abs(data[i]));
            }
            float target = peak > Ceiling ? Ceiling / peak : 1;
            float release = (float)Math.Exp(-(double)data.Length / channels / Math.Max(1, rate) / .08);
            envelope = Math.Min(target, 1 - (1 - envelope) * release);
            for (int i = 0; i < data.Length; i++) data[i] *= envelope;
        }
    }
}
#endif
