using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Integration.MapNative;
public static class FormatTests
{
    static int count;
    static void Need(bool b){count++;if(!b)throw new Exception("format assertion "+count);}
    static void Reject(Action a){bool failed=false;try{a();}catch(InvalidDataException){failed=true;}Need(failed);}
    // Structural JPEG probe, intentionally not represented as decodable pixels.
    static byte[] Probe(){var b=new List<byte>();b.AddRange(new byte[]{255,216});Segment(b,219,new byte[65]);Segment(b,196,new byte[18]);Segment(b,192,new byte[]{8,0,3,0,5,1,1,17,0});Segment(b,218,new byte[]{1,1,0,0,63,0});b.AddRange(new byte[]{42,255,0,13,255,217});return b.ToArray();}
    static void Segment(List<byte> b,int tag,byte[] data){b.Add(255);b.Add((byte)tag);b.Add((byte)((data.Length+2)>>8));b.Add((byte)(data.Length+2));b.AddRange(data);}
    public static int Main(){
        int w,h;var b=Probe();Need(BoundedJpeg.Inspect(b,4096,out w,out h)==60&&w==5&&h==3);
        Reject(()=>BoundedJpeg.Inspect(b,4,out w,out h));Reject(()=>BoundedJpeg.Inspect(b,0,out w,out h));
        for(int n=0;n<b.Length;n++){var t=new byte[n];Array.Copy(b,t,n);Reject(()=>BoundedJpeg.Inspect(t,4096,out w,out h));}
        var extra=new byte[b.Length+1];Array.Copy(b,extra,b.Length);Reject(()=>BoundedJpeg.Inspect(extra,4096,out w,out h));
        foreach(int marker in new[]{193,194,195,225,238}){var t=(byte[])b.Clone();t[3]=(byte)marker;Reject(()=>BoundedJpeg.Inspect(t,4096,out w,out h));}
        var dimension=(byte[])b.Clone();int sof=2+69+22;dimension[sof+7]=0;dimension[sof+8]=0;Reject(()=>BoundedJpeg.Inspect(dimension,4096,out w,out h));
        foreach(int wrap in new[]{10497,33071,33648})foreach(int min in new[]{9729,9987}){var s=new SourceSamplerR4(9729,min,wrap,wrap);Need(s.WrapS==wrap&&s.WrapT==wrap&&s.Mipmaps==(min==9987));Need(s.DecodedBytes(5,3)==(min==9987?72:60));}
        Need(new SourceSamplerR4(9728,9984,10497,33071).DecodedBytes(1,8)==60);
        Need(new SourceSamplerR4(9729,9987,10497,10497).DecodedBytes(4096,4096)==89478484);
        foreach(int min in new[]{9728,9984,9985,9986,0})Reject(()=>new SourceSamplerR4(9729,min,10497,10497));
        Reject(()=>new SourceSamplerR4(9728,9987,10497,10497));Reject(()=>new SourceSamplerR4(9729,9729,0,10497));Reject(()=>new SourceSamplerR4(9729,9987,10497,10497).DecodedBytes(4097,1));
        Console.WriteLine("PASS external public DLL JPEG structural/adversarial and sampler assertions="+count);return 0;
    }
}
