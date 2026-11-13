using System;
using System.IO;

namespace Ashfall.Integration.Playable.Soundscape
{
    public struct R24MusicFrame
    {
        public float Exploration,Combat,Extraction,PauseMix,DuckMix,Level;
        public bool PauseStreams;
    }
    // music.gd's three synchronized stems. Personal volume belongs to the
    // separate Music bus; it must not be applied twice to the internal .65 mix.
    public sealed class R24MusicState
    {
        float combat,extraction,pause=1,duck=1,level;
        public double PlaybackSeconds {get;private set;}
        public R24MusicFrame Step(float dt,float intensity,bool extracting,bool paused,bool radio)
        {
            Validate(dt);if(!Finite(intensity))throw new ArgumentOutOfRangeException("intensity");
            combat=Move(combat,Clamp(intensity),dt/2.5f);extraction=Move(extraction,extracting?1:0,dt/4);
            pause=Move(pause,paused?0:1,dt/.3f);duck=Move(duck,radio?.24f:1,dt/(radio?.25f:1.4f));level=Move(level,.65f,dt/1.2f);
            if(!paused||pause>0)PlaybackSeconds+=dt;
            float master=level*pause*duck;
            return new R24MusicFrame{Exploration=master*(.85f+(.65f-.85f)*combat),Combat=master*combat*.9f,Extraction=master*extraction*.85f,PauseMix=pause,DuckMix=duck,Level=level,PauseStreams=paused&&pause<=0};
        }
        public static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
        public static float Clamp(float v){return Math.Max(0,Math.Min(1,v));}
        public static float Move(float a,float b,float max){return a<b?Math.Min(b,a+max):Math.Max(b,a-max);}
        public static void Validate(float dt){if(!Finite(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");}
    }
    public struct R24AmbienceFrame
    {
        public bool QueryShelter,Creak;
        public float WindDb;
    }
    // Other movement feedback remains in the existing traversal owner. This
    // adds only the missing environmental wind/creak clocks from ambience.gd.
    public sealed class R24AmbienceState
    {
        float environmentTimer,nextCreak=13,windDb=-25;
        readonly Random random=new Random(71731);
        public bool ShelterQueryDue(float dt){R24MusicState.Validate(dt);return environmentTimer-dt<=0;}
        public R24AmbienceFrame Step(float dt,bool active,float sourceHeight,bool sheltered)
        {
            R24MusicState.Validate(dt);if(!R24MusicState.Finite(sourceHeight))throw new ArgumentOutOfRangeException("sourceHeight");
            if(!active)return new R24AmbienceFrame{WindDb=windDb};
            environmentTimer-=dt;bool query=environmentTimer<=0;if(query)environmentTimer=.4f;
            float target=sheltered?-34:-26+Math.Max(0,Math.Min(4,sourceHeight/12));windDb=R24MusicState.Move(windDb,target,dt*3);
            nextCreak-=dt;bool creak=nextCreak<=0;if(creak)nextCreak=14+(float)random.NextDouble()*12;
            return new R24AmbienceFrame{QueryShelter=query,Creak=creak,WindDb=windDb};
        }
    }

    public sealed class R24Pcm16Wave
    {
        public readonly int Channels,Rate,Frames,DataOffset,DataBytes;
        public readonly float[] Samples;
        R24Pcm16Wave(int channels,int rate,int frames,int offset,int length,float[] samples)
        {Channels=channels;Rate=rate;Frames=frames;DataOffset=offset;DataBytes=length;Samples=samples;}
        public static R24Pcm16Wave Decode(byte[] bytes,bool decodeSamples=true)
        {
            if(bytes==null||bytes.Length<44||bytes.Length>64*1024*1024||Tag(bytes,0)!="RIFF"||Tag(bytes,8)!="WAVE")throw new InvalidDataException("Source WAV RIFF bounds");
            long end=8L+U32(bytes,4);if(end>bytes.Length||end<44)throw new InvalidDataException("Truncated source WAV");
            int channels=0,rate=0,offset=-1,length=0;bool format=false;
            for(long at=12;at+8<=end;){string name=Tag(bytes,(int)at);long size=U32(bytes,(int)at+4),start=at+8;if(start+size>end)throw new InvalidDataException("Truncated source WAV chunk");
                if(name=="fmt "){
                    if(format||size<16||U16(bytes,(int)start)!=1||U16(bytes,(int)start+14)!=16)throw new InvalidDataException("Expected source PCM16 format");
                    channels=U16(bytes,(int)start+2);rate=(int)U32(bytes,(int)start+4);if(channels<1||channels>2||rate<8000||rate>192000||U16(bytes,(int)start+12)!=channels*2||U32(bytes,(int)start+8)!=(uint)(rate*channels*2))throw new InvalidDataException("Source PCM16 alignment/rate");format=true;
                }else if(name=="data"){if(offset>=0||size>int.MaxValue)throw new InvalidDataException("Source WAV data bounds");offset=(int)start;length=(int)size;}
                at=start+size+(size&1);
            }
            if(!format||offset<0||length<=0||length%(channels*2)!=0)throw new InvalidDataException("Missing/partial source PCM16 frames");
            float[] samples=decodeSamples?new float[length/2]:null;if(samples!=null)Fill(bytes,offset,0,samples);
            return new R24Pcm16Wave(channels,rate,length/(channels*2),offset,length,samples);
        }
        public void ReadFrames(byte[] bytes,int firstFrame,float[] destination)
        {if(bytes==null||destination==null||firstFrame<0||destination.Length%Channels!=0||(long)firstFrame*Channels+destination.Length>(long)Frames*Channels||DataOffset+DataBytes>bytes.Length)throw new ArgumentException("Source PCM frame bounds");Fill(bytes,DataOffset,firstFrame*Channels,destination);}
        static void Fill(byte[] bytes,int offset,int firstSample,float[] destination)
        {for(int i=0;i<destination.Length;i++){int b=offset+(firstSample+i)*2;short sample=unchecked((short)(bytes[b]|(bytes[b+1]<<8)));destination[i]=sample/32768f;}}
        static int U16(byte[] b,int i){return b[i]|(b[i+1]<<8);}
        static uint U32(byte[] b,int i){return (uint)(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24));}
        static string Tag(byte[] b,int i){return new string(new[]{(char)b[i],(char)b[i+1],(char)b[i+2],(char)b[i+3]});}
    }
}
