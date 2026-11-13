using System;
using Ashfall.Integration.Playable.UI;
class Cases
{
    static int count;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static void Main()
    {
        string[] slugs={"battlefield-paintball-gods-pitdevil","alfa-aglae-souza","classic-bigbox-vr"};
        foreach(var slug in slugs)Check(new R23MapOption("archive:"+slug,slug).ThumbnailResource=="R23MapThumbnails/"+slug,"actual archive thumbnail "+slug);
        foreach(var id in new[]{"valley","range","cyber_bird"})Check(new R23MapOption(id,id).ThumbnailResource=="R23MapThumbnails/"+id,"preserved original thumbnail "+id);
        Check(new R23MapOption("archive:unknown","UNKNOWN").ThumbnailResource==null,"unknown archive gets no invented image");
        Check(new R23MapOption("archive:classic-bigbox-vr","CLASSIC","explicit/source").ThumbnailResource=="explicit/source","explicit immutable API override preserved");
        var selection=new R23MapSelection();selection.SetOptions(new[]{new R23MapOption("archive:classic-bigbox-vr","CLASSIC")});
        Check(selection.Option(0).ThumbnailResource=="R23MapThumbnails/classic-bigbox-vr","map selection retains actual thumbnail option");
        Console.WriteLine("PASS "+count+" actual map option cases");
    }
}
