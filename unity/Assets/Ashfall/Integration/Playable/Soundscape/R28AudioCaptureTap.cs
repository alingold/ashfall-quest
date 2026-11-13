#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public sealed class R28AudioCaptureTap:MonoBehaviour
    {
        readonly object sync=new object();
        readonly List<float> samples=new List<float>();
        readonly List<string> marks=new List<string>();
        int channels=2;
        public int Count {get{lock(sync)return samples.Count;}}
        public float Peak {get{lock(sync){float peak=0;foreach(float v in samples)peak=Math.Max(peak,Math.Abs(v));return peak;}}}
        void OnAudioFilterRead(float[] data,int count)
        {lock(sync){channels=count;if(samples.Count<48000*2*100)samples.AddRange(data);}}
        public void Mark(string label){lock(sync)marks.Add(label+","+samples.Count);}
        public void Save(string output)
        {
            lock(sync)
            {
                File.WriteAllLines(Path.Combine(output,"events.csv"),marks.ToArray());
                using(var w=new BinaryWriter(File.Create(Path.Combine(output,"listener.wav"))))
                {
                    int bytes=samples.Count*4;int rate=AudioSettings.outputSampleRate;
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(bytes+36);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)3);w.Write((short)channels);w.Write(rate);w.Write(rate*channels*4);w.Write((short)(channels*4));w.Write((short)32);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(bytes);foreach(float v in samples)w.Write(v);
                }
            }
        }
    }
}
#endif
