using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Ashfall.Modules.Audio
{
    public sealed class AudioAsset
    {
        public readonly string Route, Path, Hash, Version, Approval, License, Bus;
        public readonly int Variant, Channels, Rate, Frames;
        public readonly bool Loop;
        internal AudioAsset(string[] f, int channels, int rate, int frames)
        { Route=f[0]; Variant=int.Parse(f[1]); Path=f[2]; Hash=f[3]; Version=f[4]; Approval=f[5]; License=f[6]; Bus=f[7]; Loop=f[8]=="1"; Channels=channels; Rate=rate; Frames=frames; }
    }
    public sealed class AudioCatalog
    {
        public const string ApiVersion="ASHFALL-AUDIO/2";
        private readonly Dictionary<string,List<AudioAsset>> routes=new Dictionary<string,List<AudioAsset>>(StringComparer.Ordinal);
        public static AudioCatalog Load(string manifest)
        {
            var catalog=new AudioCatalog();
            var lines=File.ReadAllLines(AudioPaths.Canonical(manifest));
            if(lines.Length==0 || lines[0]!="ASHFALL-AUDIO/1") throw new InvalidDataException("Audio schema");
            for(int i=1;i<lines.Length;i++)
            {
                if(lines[i].Length==0) continue;
                string[] f=lines[i].Split('\t'); int variant;
                if(f.Length!=9 || !int.TryParse(f[1],out variant) || variant<0 || !Regex.IsMatch(f[0],@"^(generic|weapons|actions)/[a-zA-Z0-9_/.-]+$") || !Regex.IsMatch(f[3],"^[A-Fa-f0-9]{64}$") || string.IsNullOrWhiteSpace(f[4]) || !f[5].StartsWith("accepted:",StringComparison.Ordinal) || f[5].Length<=9 || string.IsNullOrWhiteSpace(f[6]) || !ValidBus(f[7]) || (f[8]!="0" && f[8]!="1")) throw new InvalidDataException("Invalid/unapproved asset at line "+(i+1));
                f[2]=AudioPaths.Canonical(f[2]);
                // Read once: validation and decoding use the same byte identity.
                byte[] bytes=File.ReadAllBytes(f[2]);
                using(var sha=SHA256.Create()) if(!string.Equals(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""),f[3],StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Hash mismatch: "+f[2]);
                PcmWave pcm=PcmWave.Decode(bytes);
                List<AudioAsset> list;
                if(!catalog.routes.TryGetValue(f[0],out list)){list=new List<AudioAsset>();catalog.routes.Add(f[0],list);}
                if(variant!=list.Count) throw new InvalidDataException("Variants must be contiguous and ordered");
                list.Add(new AudioAsset(f,pcm.Channels,pcm.Rate,pcm.Frames));
            }
            if(catalog.routes.Count==0) throw new InvalidDataException("Empty audio manifest");
            return catalog;
        }
        public static bool ValidBus(string bus) {return bus=="SFX"||bus=="Ambience"||bus=="Music"||bus=="Voice"||bus=="Master";}
        public AudioAsset Resolve(string kind, string subject, bool weapon, int variant, bool authoredOnly)
        {
            List<AudioAsset> list=null;
            string id=weapon && subject=="uzi"?"uzis":subject;
            string[] reload={"mag_eject","pouch","mag_insert","bolt","open","close","latch","pump","shell_insert"};
            if(!authoredOnly && !string.IsNullOrEmpty(subject))
            {
                // Quest R21 explicitly restores the authored generic bolt for AWP.
                if(weapon && id=="awp" && kind=="bolt") routes.TryGetValue("generic/bolt",out list);
                else routes.TryGetValue((weapon?"weapons/":"actions/")+id+"/"+kind,out list);
                if(list==null && weapon && Array.IndexOf(reload,kind)>=0) routes.TryGetValue("generic/"+kind,out list);
            }
            if(list==null) routes.TryGetValue((authoredOnly?"generic/authored/":"generic/")+kind,out list);
            if(list==null) routes.TryGetValue("generic/authored/"+kind,out list);
            if(list==null) throw new KeyNotFoundException("Unmapped audio cue: "+kind);
            int n=variant%list.Count; if(n<0)n+=list.Count; return list[n];
        }
    }
    public sealed class PcmWave
    {
        public readonly int Channels,Rate,Frames;
        public readonly float[] Samples;
        private PcmWave(int channels,int rate,float[] samples){Channels=channels;Rate=rate;Samples=samples;Frames=samples.Length/channels;}
        public static PcmWave Decode(byte[] bytes)
        {
            using(var r=new BinaryReader(new MemoryStream(bytes)))
            {
                if(bytes.Length<12 || Tag(r)!=0x46464952u) throw new InvalidDataException("RIFF required");
                uint size=r.ReadUInt32(); if((long)size+8!=bytes.Length || Tag(r)!=0x45564157u)throw new InvalidDataException("WAVE length");
                int channels=0,rate=0,align=0; byte[] data=null; bool fmt=false;
                while(r.BaseStream.Position<bytes.Length)
                {
                    if(bytes.Length-r.BaseStream.Position<8)throw new InvalidDataException("Truncated chunk");
                    uint tag=Tag(r);uint length=r.ReadUInt32();long end=r.BaseStream.Position+length;
                    if(end+(length%2)>bytes.Length)throw new InvalidDataException("Truncated chunk payload");
                    if(tag==0x20746D66u)
                    {
                        if(fmt||length<16)throw new InvalidDataException("Invalid fmt");fmt=true;
                        int format=r.ReadUInt16();channels=r.ReadUInt16();rate=r.ReadInt32();int byteRate=r.ReadInt32();align=r.ReadUInt16();int bits=r.ReadUInt16();
                        if(format!=1||bits!=16||(channels!=1&&channels!=2)||rate<8000||rate>192000||align!=channels*2||byteRate!=rate*align)throw new InvalidDataException("PCM16 mono/stereo required");
                    }
                    if(tag==0x61746164u){if(data!=null || length>64000000)throw new InvalidDataException("Data size/duplicate");data=r.ReadBytes((int)length);}
                    r.BaseStream.Position=end+(length%2);
                }
                if(!fmt||data==null||data.Length==0||data.Length%align!=0)throw new InvalidDataException("Incomplete PCM frames");
                var samples=new float[data.Length/2];for(int i=0;i<samples.Length;i++) samples[i]=(short)(data[i*2]|data[i*2+1]<<8)/32768f;
                return new PcmWave(channels,rate,samples);
            }
        }
        private static uint Tag(BinaryReader reader)
        {
            byte[] b=reader.ReadBytes(4);
            if(b.Length!=4)throw new InvalidDataException("Truncated FourCC");
            return (uint)(b[0]|b[1]<<8|b[2]<<16|b[3]<<24);
        }
    }
    public static class AudioPaths
    {
        // Windows deployment identity v2. Device/extended paths and ADS are unsupported.
        public static string Canonical(string path)
        {
            if(string.IsNullOrWhiteSpace(path))throw new InvalidDataException("Absolute immutable asset path required");
            string p=path.Replace('/','\\');
            bool drive=p.Length>=3 && ((p[0]>='A'&&p[0]<='Z')||(p[0]>='a'&&p[0]<='z')) && p[1]==':' && p[2]=='\\';
            bool unc=p.StartsWith("\\\\",StringComparison.Ordinal) && !p.StartsWith("\\\\?\\",StringComparison.Ordinal) && !p.StartsWith("\\\\.\\",StringComparison.Ordinal);
            if(!drive&&!unc)throw new InvalidDataException("Fully qualified asset path required");
            if(p.IndexOf(':',drive?2:0)>=0)throw new InvalidDataException("Alternate streams unsupported");
            if(unc){string[] parts=p.Substring(2).Split('\\');if(parts.Length<3||parts[0].Length==0||parts[1].Length==0)throw new InvalidDataException("UNC server/share/file required");}
            foreach(string part in p.Split('\\'))if(part.EndsWith(" ",StringComparison.Ordinal)||(part.EndsWith(".",StringComparison.Ordinal)&&part!="."&&part!=".."))throw new InvalidDataException("Ambiguous Windows path");
            try{return System.IO.Path.GetFullPath(p);}catch(ArgumentException e){throw new InvalidDataException("Invalid asset path",e);}catch(NotSupportedException e){throw new InvalidDataException("Invalid asset path",e);}
        }
    }
}
