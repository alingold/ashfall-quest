using System;
using Ashfall.Integration.Playable.UI;
class MenuAudioChecks
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        var edges=new R23MenuAudioEdges();
        Check(!edges.Hover(0,1,-1,true),"No target is silent");
        Check(edges.Hover(0,1,0,true),"New target emits hover");
        for(int i=0;i<300;i++)if(edges.Hover(0,1,0,true))throw new Exception("Per-frame hover replay");
        Check(!edges.Hover(1,1,0,true),"Second hand same control does not double cue");
        Check(edges.Hover(1,1,1,true),"Second hand different control emits once");
        Check(!edges.Hover(1,1,1,true),"Second hand stable target silent");
        Check(!edges.Hover(0,1,0,false),"Lost tracking/focus clears silently");
        Check(edges.Hover(0,1,0,true),"Tracked reacquisition is one hover edge");
        Check(!edges.Hover(0,1,-1,true)&&edges.Hover(0,1,0,true),"Leave/reenter is a new edge");
        Check(edges.Hover(0,2,0,true),"Same button index on a different page is a new target");
        edges.ResetHover();Check(edges.Hover(0,2,0,true),"Reenabled UI reacquires target");
        Check(!edges.Initialize(false,true,true,true),"Inactive page cannot initialize");
        Check(!edges.Initialize(true,false,true,true),"Initialization waits tracking");
        Check(!edges.Initialize(true,true,false,true),"Initialization waits focus");
        Check(!edges.Initialize(true,true,true,false),"Initialization waits real clip; no fallback sound");
        Check(edges.Initialize(true,true,true,true),"First active tracked menu initializes");
        Check(!edges.Initialize(true,true,true,true),"Initialization never repeats on frame/page change");
        edges.ResetHover();Check(!edges.Initialize(true,true,true,true),"Hover reset does not reset boot cue");
        Check(Math.Abs(R23MenuAudioEdges.Gain(1)-(float)Math.Pow(10,-8.0/20))<.00001f,"Exact source -8dB gain");
        Check(R23MenuAudioEdges.Gain(0)==0,"Persisted SFX zero mutes");
        Check(Math.Abs(R23MenuAudioEdges.Gain(.25f)-R23MenuAudioEdges.SourceGain*.25f)<.000001f,"Only SFX applied locally; Master belongs to listener");
        Check(R23MenuAudioEdges.Gain(-1)==0&&R23MenuAudioEdges.Gain(2)==R23MenuAudioEdges.SourceGain,"Bounded local volume");
        Check(R23MenuAudioEdges.Gain(float.NaN)==0&&R23MenuAudioEdges.Gain(float.PositiveInfinity)==0,"Malformed direct audio volume is silent");
        Console.WriteLine("PASS "+checks+" menu audio checks plus 300 stable-hover frames: changed targets, two hands, tracking/focus loss, one tracked boot, delayed/missing clip and source SFX gain");
    }
}
