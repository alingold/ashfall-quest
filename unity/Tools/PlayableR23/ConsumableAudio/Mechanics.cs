using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Consumables;
class Mechanics
{
    static int count;static void Check(bool v,string why){count++;if(!v)throw new Exception(why);}
    static void Near(float a,float b,string why){Check(Math.Abs(a-b)<.000001f,why);}
    static Float3 P(float x){return new Float3(x,0,0);}
    static void Main()
    {
        var s=new R23ConsumableAudioPolicy();R23ConsumableCue c;
        foreach(string kind in new[]{"soda","shaker","banana"})
        {
            Check(s.TryCue(R23ConsumableAudioEvent.Equip,kind,out c)&&!c.Spatial&&c.Action=="item_equip"&&c.Variant==0&&c.StopShake,"equip "+kind);
            Check(s.TryCue(R23ConsumableAudioEvent.Open,kind,out c)&&c.Spatial&&c.Action=="consume_open"&&!c.StopShake,"open "+kind);
            Check(s.TryCue(R23ConsumableAudioEvent.Consume,kind,out c)&&c.Spatial&&c.Action=="consume_drink"&&c.StopShake,"consume "+kind);
            Check(s.TryCue(R23ConsumableAudioEvent.Stow,kind,out c)&&!c.Spatial&&c.Action=="item_stow"&&c.StopShake,"stow "+kind);
        }
        int before=s.EventCount;Check(!s.TryCue(R23ConsumableAudioEvent.Open,"fake",out c)&&s.EventCount==before,"invalid kind");
        Check(!s.TryCue(R23ConsumableAudioEvent.Peel,"soda",out c)&&!s.TryCue(R23ConsumableAudioEvent.Ready,"banana",out c),"kind constraints");
        s.Reset();for(int peel=0;peel<4;peel++)Check(s.TryCue(R23ConsumableAudioEvent.Peel,"banana",out c)&&c.Action=="consume_open"&&c.Variant==peel+1,"each real peel");
        Check(s.TryCue(R23ConsumableAudioEvent.Open,"banana",out c)&&c.Variant==5,"source fourth peel then open interrupts same voice");
        for(int i=0;i<100;i++)s.TryCue(R23ConsumableAudioEvent.Open,"banana",out c);Check(c.Variant==64&&s.EventCount==64,"bounded original event-history variant");
        s.Reset();Check(!s.ShakeActive&&s.EventCount==0,"round reset");
        Check(!s.SampleShake(P(0),.1f,true),"first sample silent");Check(s.SampleShake(P(.05f),.1f,true),"real .5m/s sample qualifies");Near(s.ShakeSecondsLeft,.2f,"motion refresh");
        s.Tick(.19f);Check(s.ShakeActive,"still playing before timeout");s.Tick(.010001f);Check(!s.ShakeActive,"pause after .20");
        s.ShakeMotion();s.Tick(.1f);s.ShakeMotion();Near(s.ShakeSecondsLeft,.2f,"refresh rather than restarting clip");
        s.TryCue(R23ConsumableAudioEvent.Ready,"shaker",out c);Check(!s.ShakeActive&&c.StopShake&&c.Action=="consume_ready","ready stops shake");
        Check(!s.SampleShake(P(.1f),.1f,true),"ready invalidates previous sample");
        s.ShakeMotion();s.TryCue(R23ConsumableAudioEvent.Stow,"shaker",out c);Check(!s.ShakeActive,"stow stops shake");
        s.ShakeMotion();s.TryCue(R23ConsumableAudioEvent.Equip,"soda",out c);Check(!s.ShakeActive,"equip stops shake");
        s.ShakeMotion();s.TryCue(R23ConsumableAudioEvent.Consume,"shaker",out c);Check(!s.ShakeActive,"drink stops shake");
        s.SampleShake(P(0),.1f,false);Check(!s.SampleShake(P(100),.1f,true),"new gesture no teleport sound");
        Check(!s.SampleShake(P(101),.1f,true),"travel outlier");Check(s.SampleShake(P(101.05f),.1f,true),"source previous updates after outlier");
        Check(!s.SampleShake(P(float.NaN),.1f,true),"invalid hand");Check(!s.SampleShake(P(2),.1f,true),"recovery prime");
        float[] travel={0,.0449f,.045f,.299f,.3f,.8f,float.NaN,float.PositiveInfinity,-1};
        float[] delta={.1f,.01f,0,-1,float.NaN,float.PositiveInfinity};
        foreach(float t in travel)foreach(float dt in delta)
        {float speed=t/dt;bool expected=!float.IsNaN(t)&&!float.IsInfinity(t)&&!float.IsNaN(dt)&&!float.IsInfinity(dt)&&dt>0&&t>=0&&t<.3f&&speed>=.45f&&speed<=8;Check(R23ConsumableAudioPolicy.QualifiesShake(t,dt)==expected,"source speed/travel boundaries");}
        Near(R23ConsumableAudioPolicy.UiGain,(float)Math.Pow(10,-8/20.0),"UI source dB");Near(R23ConsumableAudioPolicy.PhysicalGain,(float)Math.Pow(10,-12/20.0),"physical source dB");
        s.ShakeMotion();s.Tick(float.NaN);Check(!s.ShakeActive,"invalid clock clears");
        Console.WriteLine("PASS "+count+" real consumable cue/gesture/timing policy cases");
    }
}
