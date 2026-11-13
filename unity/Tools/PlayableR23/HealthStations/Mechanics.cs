using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.JumpPads;
using Ashfall.Integration.Playable.HandsWeapons;
static class HealthStationCases
{
    static int cases;
    sealed class Sight:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return true;}}
    static void Check(bool ok,string name){cases++;if(!ok)throw new Exception(name);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.00001f;}
    static bool Clear(Float3 a,Float3 b){return true;}
    static R23SourceHealthStation Row(string id,float rate){return new R23SourceHealthStation(id,new Float3(0,0,0),new Float3(0,1,0),1,rate);}
    static void Main()
    {
        var row=Row("source-form-fixture",10);
        Check(R23HealthContactMath.Contains(row,new Float3(0,0,0)),"face center");
        Check(R23HealthContactMath.Contains(row,new Float3(0,.75f,0)),"inclusive front height");
        Check(!R23HealthContactMath.Contains(row,new Float3(0,.75001f,0)),"outside front height");
        Check(R23HealthContactMath.Contains(row,new Float3(0,-.19999999f,0)),"inside rear height");
        Check(!R23HealthContactMath.Contains(row,new Float3(0,-.20001f,0)),"outside rear height");
        Check(!R23HealthContactMath.Contains(row,new Float3(0,-.20f,0)),"source float32 -.20 lies just below script double bound");
        Check(R23HealthContactMath.Contains(row,new Float3(1,0,0)),"inclusive radial extent");
        Check(!R23HealthContactMath.Contains(row,new Float3(1.001f,0,0)),"outside radius");
        Check(!R23HealthContactMath.Contains(row,new Float3(0,0,1.001f)),"circle rather than box");
        Check(!R23HealthContactMath.Contains(row,new Float3(float.NaN,0,0)),"invalid feet cannot heal");
        var side=new R23SourceHealthStation("side-fixture",new Float3(4,5,6),new Float3(2,0,0),1,10);
        Check(R23HealthContactMath.Contains(side,new Float3(4.5f,5.5f,6.5f)),"normalized authored tilted normal");
        Check(!R23HealthContactMath.Contains(side,new Float3(4.76f,5,6)),"height along normal, not world up");
        Float3 from,to;R23HealthContactMath.ContactSegment(row,new Float3(.5f,.1f,0),out from,out to);
        Check(Near(from.Y,.20f)&&Near(to.X,.5f)&&Near(to.Y,.22f),"source start .20/end .12 normal offsets");
        Check(R23HealthContactMath.NeedsContactRay(from,to),"clear contact has physical ray");
        R23HealthContactMath.ContactSegment(row,new Float3(0,0,0),out from,out to);Check(!R23HealthContactMath.NeedsContactRay(from,to),"source sub .10m contact short circuit");
        var contacts=new R23HealthContacts(new[]{row,Row("overlap-high",20)});var feet=new Float3(.5f,.1f,0);int queries=0;
        Check(contacts.RateAt(feet,delegate(Float3 a,Float3 b){queries++;return true;})==20&&queries==2,"max overlap rate, not summed");
        Check(contacts.RateAt(feet,delegate(Float3 a,Float3 b){return false;})==0,"wall contact denies healing");
        queries=0;Check(contacts.RateAt(new Float3(0,0,0),delegate(Float3 a,Float3 b){queries++;return false;})==20&&queries==0,"actual source near-contact shortcut");
        Check(contacts.RateAt(feet,null)==0,"missing physical query owner cannot heal");
        contacts=new R23HealthContacts(new[]{row});int callbacks=0;float rate,gain;
        Check(!contacts.Step(feet,.1f,false,Clear,delegate(float amount){callbacks++;return true;},out rate,out gain)&&callbacks==0&&rate==0&&gain==0,"pause has no health callback");
        foreach(float dt in new float[]{0,-1,float.NaN,float.PositiveInfinity,.1001f})Check(!contacts.Step(feet,dt,true,Clear,delegate(float amount){callbacks++;return true;},out rate,out gain)&&callbacks==0,"invalid dt no gain");
        Check(!contacts.Step(feet,.1f,true,delegate(Float3 a,Float3 b){return false;},delegate(float amount){callbacks++;return true;},out rate,out gain)&&callbacks==0,"blocked wall has no effect callback");
        Check(!contacts.Step(feet,.1f,true,Clear,delegate(float amount){callbacks++;return false;},out rate,out gain)&&rate==10&&gain==1&&callbacks==1,"actual health rejection not reported as applied");
        var real=new R23RoundState(new string[0],new Sight());real.Damage(40);Check(real.Health==60,"real HealthService damaged setup");
        for(int i=0;i<10;i++)Check(contacts.Step(feet,.1f,true,Clear,delegate(float amount){return real.ApplyPlayerGain(amount,0);},out rate,out gain),"real health accepts station tick");
        Check(real.Health==70,"10 HP/s reaches actual health owner");
        contacts.Step(feet,.1f,false,Clear,delegate(float amount){return real.ApplyPlayerGain(amount,0);},out rate,out gain);Check(real.Health==70,"pause preserves actual health");
        contacts.Step(new Float3(10,0,0),.1f,true,Clear,delegate(float amount){return real.ApplyPlayerGain(amount,0);},out rate,out gain);Check(real.Health==70,"exit station preserves actual health");
        real.ApplyPlayerGain(100,0);Check(!contacts.Step(feet,.1f,true,Clear,delegate(float amount){return real.ApplyPlayerGain(amount,0);},out rate,out gain)&&real.Health==100,"real full-health rejection without fake reserve");
        real.Damage(100);Check(!contacts.Step(feet,.1f,true,Clear,delegate(float amount){return real.ApplyPlayerGain(amount,0);},out rate,out gain)&&real.Health==0,"station cannot resurrect actual dead owner");
        foreach(var source in SourceRows.All)
        {
            Check(R23HealthContactMath.Contains(source,source.Center),"original authored center contains");
            Check(new R23HealthContacts(new[]{source}).RateAt(source.Center,Clear)==10,"original source rate at face");
        }
        Console.WriteLine("PASS "+cases+" actual health/contact/math cases, including all "+SourceRows.All.Length+" original health rows and real HealthService gains. CPU only.");
    }
}
