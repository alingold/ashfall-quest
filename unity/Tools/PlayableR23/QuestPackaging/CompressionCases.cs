using System;
using System.IO;
using Ashfall.Editor.Playable;
class CompressionCases
{
    static int n;
    static void Need(bool yes,string why){n++;if(!yes)throw new Exception(why);}
    static void Refuses(string input){bool refused=false;try{R24QuestStreamingCompressionPolicy.Rewrite(input);}catch(InvalidDataException){refused=true;}Need(refused,"Changed Gradle contract must refuse");}
    static void Main(string[] args)
    {
        string actual=File.ReadAllText(args[0]),changed=R24QuestStreamingCompressionPolicy.Rewrite(actual);
        Need(R24QuestStreamingCompressionPolicy.Rewrite(changed)==changed,"Actual incremental Gradle second callback is idempotent");
        Need(!changed.Contains(".bytes")&&!changed.Contains(".meshbin"),"Actual raw stream extensions are compressible");
        foreach(string keep in new[]{".png",".json",".wav",".jpg",".rgb9e5"})Need(actual.Contains(keep)&&changed.Contains(keep),"Other current extensions retained: "+keep);
        string example="# preserve\r\nother=value\r\nunityStreamingAssets=.png, .bytes, .meshbin, .wav\r\nlast=true\r\n";
        Need(R24QuestStreamingCompressionPolicy.Rewrite(example)=="# preserve\r\nother=value\r\nunityStreamingAssets=.png, .wav\r\nlast=true\r\n","Only target property changes; adjacent settings and CRLF preserved");
        Need(R24QuestStreamingCompressionPolicy.Rewrite("unityStreamingAssets=.png,.wav\n")=="unityStreamingAssets=.png,.wav\n","Already-configured property preserves bytes on a repeated callback");
        Refuses("unityStreamingAssets=.png,.meshbin\n");
        Refuses("other=true\n");Refuses("unityStreamingAssets=.png,.bytes\n");
        Refuses("unityStreamingAssets=.png,.bytes,.meshbin,\n");
        Refuses("unityStreamingAssets=.bytes,.meshbin\nunityStreamingAssets=.bytes,.meshbin\n");
        Need(File.ReadAllText(args[0])==actual,"Check never mutates the generated project");
        Console.WriteLine("PASS "+n+" real Gradle compression-policy cases; no build/runtime/Quest execution");
    }
}
