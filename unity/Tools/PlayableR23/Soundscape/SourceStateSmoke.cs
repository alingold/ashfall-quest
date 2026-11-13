using System;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Integration.Playable.Soundscape;

static class SourceStateSmoke
{
    static int checks;static void Need(bool v,string why){checks++;if(!v)throw new Exception(why);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.00002f;}
    static void Main(string[] args)
    {
        var music=new R24MusicState();R24MusicFrame frame=new R24MusicFrame();for(int i=0;i<200;i++)frame=music.Step(.02f,0,false,false,false);
        Need(Near(frame.Level,.65f)&&Near(frame.Exploration,.65f*.85f)&&frame.Combat==0&&frame.Extraction==0,"Source exploration mix/internal .65");
        for(int i=0;i<125;i++)frame=music.Step(.02f,1,false,false,false);Need(Near(frame.Combat,.65f*.9f)&&Near(frame.Exploration,.65f*.65f),"Source2.5s combat crossfade");
        for(int i=0;i<200;i++)frame=music.Step(.02f,1,true,false,false);Need(Near(frame.Extraction,.65f*.85f),"Source4s extraction layer capability");
        for(int i=0;i<13;i++)frame=music.Step(.02f,1,true,false,true);Need(Near(frame.DuckMix,.24f),"Source radio duck capability");
        for(int i=0;i<16;i++)frame=music.Step(.02f,1,false,true,false);Need(frame.PauseStreams&&frame.Exploration==0&&frame.Combat==0,"Source .3s pause fade");double paused=music.PlaybackSeconds;for(int i=0;i<100;i++)music.Step(.02f,0,false,true,false);Need(music.PlaybackSeconds==paused,"Paused virtual sample clock does not advance");
        frame=music.Step(.02f,0,false,false,false);Need(!frame.PauseStreams&&music.PlaybackSeconds>paused,"Resume retains source clock");
        var environment=new R24AmbienceState();int queries=0,creaks=0;for(int i=0;i<649;i++){var a=environment.Step(.02f,true,0,false);if(a.QueryShelter)queries++;if(a.Creak)creaks++;}
        Need(creaks==0,"No environmental creak before13s");var cue=environment.Step(.02f,true,0,false);if(cue.Creak)creaks++;cue=environment.Step(.02f,true,0,false);if(cue.Creak)creaks++;Need(creaks==1,"Source first creak at13s");Need(queries>=30&&queries<=34,"Source .4s shelter cadence");
        for(int i=0;i<200;i++)cue=environment.Step(.02f,true,0,true);Need(Near(cue.WindDb,-34),"Sheltered source wind target");for(int i=0;i<201;i++)cue=environment.Step(.02f,true,48,false);Need(Near(cue.WindDb,-22),"Altitude source wind target cap");
        float held=cue.WindDb;for(int i=0;i<100;i++)cue=environment.Step(.02f,false,0,true);Need(cue.WindDb==held&&!cue.Creak&&!cue.QueryShelter,"Paused ambient state retains clocks");
        foreach(string file in Directory.GetFiles(args[0],"*.wav")){
            byte[] bytes=File.ReadAllBytes(file);var pcm=R24Pcm16Wave.Decode(bytes,false);Need(pcm.Samples==null,"Runtime header parsing does not allocate entire score float buffer");
            using(var sha=SHA256.Create())using(var output=new MemoryStream()){
                for(int at=0;at<pcm.Frames;){int count=Math.Min(32768,pcm.Frames-at);var samples=new float[count*pcm.Channels];pcm.ReadFrames(bytes,at,samples);foreach(float v in samples){if(float.IsNaN(v)||v<-1||v>=1)throw new Exception("Nonfinite/out-of-domain actual PCM");short s=(short)Math.Round(v*32768);output.WriteByte((byte)s);output.WriteByte((byte)(s>>8));}at+=count;}
                string decoded=BitConverter.ToString(sha.ComputeHash(output.ToArray()));string source=BitConverter.ToString(sha.ComputeHash(bytes,pcm.DataOffset,pcm.DataBytes));Need(decoded==source,"Actual chunk decoder exact PCM roundtrip "+Path.GetFileName(file));
            }
        }
        bool rejected=false;try{R24Pcm16Wave.Decode(new byte[44]);}catch(InvalidDataException){rejected=true;}Need(rejected,"Malformed WAV fails closed");
        Console.WriteLine("Live source music/ambience/PCM: "+checks+" checks PASS. Extraction/radio inputs are policy capabilities, not live campaign/network proof. Native audio/listening UNRUN.");
    }
}
