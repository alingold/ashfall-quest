using System;
using Ashfall.Integration.Playable.Input;
class Cases
{
    static int count;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.000001;}
    static void Main()
    {
        var s=new R23TeamMarksState();
        Check(s.Selected==2&&s.Preferred==2,"source default blue location mark");
        Check(s.Sample(1,true,true,0,0)==-1&&!s.Held,"held-at-boot input cannot arm");
        s.Sample(0,false,true,0,0);Check(s.Armed,"neutral arms");
        s.Sample(.1f,true,true,0,0);Check(s.Began&&s.Held&&!s.WheelOpen,"exact .10 s keeps wheel closed");
        s.Sample(.00001f,true,true,0,0);Check(s.WheelOpen&&!s.Began,"strict greater hold opens once");
        s.Point(6);Check(s.Selected==6&&s.ExplicitChoice,"ray selects help");
        s.Point(-1);Check(s.Selected==6,"source center does not erase last explicit ray choice");
        Check(s.Sample(0,false,true,0,0)==6&&!s.WheelOpen&&s.Preferred==6,"release emits chosen mark and remembers preference");
        s.Sample(.01f,true,true,0,0);Check(s.Selected==6&&!s.WheelOpen,"next quick press inherits preference");
        Check(s.Sample(0,false,true,0,0)==6,"quick release uses preference without wheel");
        s.Sample(.11f,true,true,1,0);Check(s.StickChoice&&s.Selected==2,"right stick selects ping");
        s.Point(1);Check(s.Selected==2,"stick choice overrides wheel ray selection");
        s.Sample(0,false,true,0,0);Check(s.Preferred==2,"stick release commits preference");
        s.Sample(.11f,true,true,0,.25f);Check(!s.StickChoice,"exact stick deadzone stays neutral");
        s.Sample(0,true,true,0,.2501f);Check(s.Selected==0&&s.StickChoice,"above stick deadzone chooses hold");
        s.Sample(0,true,false,0,0);Check(!s.Armed&&!s.Held&&!s.WheelOpen,"lost tracking cancels without release request");
        Check(s.Sample(1,true,true,1,1)==-1&&!s.Held,"still-held button after focus does not restart");
        s.Sample(0,false,true,0,0);s.Sample(.01f,true,true,0,0);Check(s.Sample(0,false,false,0,0)==-1,"ineligible release cannot create a mark");
        for(int i=0;i<8;i++){double a=Math.PI/2-i*Math.PI/4;Check(R23TeamMarksState.Sector((float)Math.Cos(a),(float)Math.Sin(a))==i,"clockwise source sector "+i);}
        Check(R23TeamMarksState.Paint(3)&&R23TeamMarksState.Paint(4)&&R23TeamMarksState.Paint(5)&&!R23TeamMarksState.Paint(2),"source decal membership");
        Check(R23TeamMarksState.Action(5)=="emote_alt"&&R23TeamMarksState.Title(5)=="EMOTE 2","alt smile source action, never old gun silhouette");
        Check(Near(R23TeamMarksState.AngularWidth(2),(float)(2*Math.Tan(2*Math.PI/180))),"4 degree source normal angular width");
        Check(Near(R23TeamMarksState.AngularWidth(4),(float)(2*Math.Tan(2.5*Math.PI/180))),"5 degree target angular width");
        Check(R23TeamMarksState.ColumnCount(.0001f,0,2)==0,"behind or near camera column hidden");
        Check(R23TeamMarksState.ColumnCount(.002f,0,2)==512,"close source column max512 bound");
        Check(R23TeamMarksState.ColumnCount(10,81,2)==0,"source sky limit80 produces no dots above sky");
        float w=R23TeamMarksState.AngularWidth(2)*10;
        Check(R23TeamMarksState.ColumnCount(10,4,2)==(int)Math.Floor((76-(.5+w*1.25))/(w*1.37))+1,"column source width/start/spacing formula");
        Check(R23TeamMarksState.WorldLayer(0)&&R23TeamMarksState.WorldLayer(8)&&R23TeamMarksState.WorldLayer(19)&&R23TeamMarksState.WorldLayer(20)&&!R23TeamMarksState.WorldLayer(5),"source mapped world/target/panel mask");
        var life=new R23TeamMarkLifetime();life.Place();life.Tick(29);Check(life.Active&&Near(life.Remaining,1),"local mark remains before30 even without input eligibility");
        life.Tick(1);Check(life.Active,"source strict expiry keeps exact30 s record");life.Tick(.01f);Check(!life.Active,"source expires after30 s");
        life.Place();life.Tick(20);life.Place();Check(life.Active&&Near(life.Remaining,30),"replacement renews single-current lifetime");
        life.Tick(float.NaN);life.Tick(-5);Check(Near(life.Remaining,30),"malformed negative delta cannot corrupt lifetime");
        life.Clear();Check(!life.Active,"map/reset clear");
        Console.WriteLine("PASS "+count+" production state/math cases; native physics/render/audio playback not executed here.");
    }
}
