using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.JumpPads;
class Mechanics
{
    public sealed class Fixture {public string Label;public Float3 Start,End,Center,Normal;public double Radius;public bool Expected;}
    public static Float3 V(uint x,uint y,uint z){return new Float3(F(x),F(y),F(z));}
    static float F(uint bits){return BitConverter.ToSingle(BitConverter.GetBytes(bits),0);}
    static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
    static Float3 Mul(Float3 a,float k){return new Float3(a.X*k,a.Y*k,a.Z*k);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static bool Segment(Float3 p,Float3 a,Float3 b){return p.X>=Math.Min(a.X,b.X)&&p.X<=Math.Max(a.X,b.X)&&p.Y>=Math.Min(a.Y,b.Y)&&p.Y<=Math.Max(a.Y,b.Y)&&p.Z>=Math.Min(a.Z,b.Z)&&p.Z<=Math.Max(a.Z,b.Z);}
    static void Main()
    {
        int passed=0;
        foreach(var c in SourceFixtures.Cases)
        {Float3 contact;bool found=R23PadContactMath.TrySwept(c.Center,c.Normal,c.Radius,c.Start,c.End,out contact);if(found!=c.Expected)throw new Exception(c.Label+" expected "+c.Expected+" got "+found);if(found&&(!R23PadContactMath.Contains(c.Center,c.Normal,c.Radius,contact)||!Segment(contact,c.Start,c.End)))throw new Exception(c.Label+" witness");passed++;}
        Check(passed==79,"all79 actual source rounded-contact fixtures and strict witnesses");
        var rows=SourceFixtures.Alpha;Check(rows.Length==115,"all115 genuine ALFA authored rows");int tilted=0;foreach(var p in rows)if(p.Tilted)tilted++;Check(tilted==44,"44 actual tilted pads retain source directions");
        var pad=rows[0];var center=pad.Pad.Center;var normal=pad.Pad.Normal;var feet=Add(center,Mul(normal,.3f));int calls=0;float speed=0;Float3 received=new Float3();
        Func<Float3,Float3,bool> clear=delegate(Float3 from,Float3 to){return true;};
        Func<Float3,float,bool> launch=delegate(Float3 n,float s){calls++;received=n;speed=s;return true;};
        var state=new R23PadContacts(new[]{pad});Check(state.TryLaunch(feet,0,true,false,false,false,clear,launch)&&calls==1&&speed==45&&received.Y==1,"actual current-feet source pad dispatches positive45m/s launch");
        Check(!state.TryLaunch(feet,.2,true,false,false,false,clear,launch)&&calls==1,"source1.2-second per-pad cooldown prevents duplicate dispatch");
        Check(state.TryLaunch(feet,1.21,true,false,false,false,clear,launch)&&calls==2,"cooldown expiration permits next actual contact");
        state=new R23PadContacts(new[]{pad});int before=calls;Check(!state.TryLaunch(feet,0,true,false,false,false,delegate(Float3 a,Float3 b){return false;},launch)&&calls==before,"occluded authored face-to-feet contact cannot launch");
        Check(!state.TryLaunch(feet,.01,true,false,false,false,clear,delegate(Float3 n,float s){return false;})&&state.LastPadId==null,"refused root launch spends no cooldown or launch publication");
        Check(state.TryLaunch(feet,.02,true,false,false,false,clear,launch),"rejected root launch can retry when the actual owner accepts");
        state=new R23PadContacts(new[]{pad});before=calls;
        Check(!state.TryLaunch(feet,0,false,false,false,false,clear,launch)&&!state.TryLaunch(feet,.01,true,true,false,false,clear,launch)&&!state.TryLaunch(feet,.02,true,false,true,false,clear,launch)&&!state.TryLaunch(feet,.03,true,false,false,true,clear,launch)&&calls==before,"input pause climbing mantle and ghost eligibility suppress launches");
        var start=Add(feet,new Float3(-7.5f,0,0));var end=Add(feet,new Float3(7.5f,0,0));state=new R23PadContacts(new[]{pad});
        Check(!state.TryLaunch(start,0,true,false,false,false,clear,launch)&&state.TryLaunch(end,.01,true,false,false,false,clear,launch),"nominal15m swept feet contact dispatches source pad");
        state=new R23PadContacts(new[]{pad});start=Add(feet,new Float3(-8,0,0));end=Add(feet,new Float3(8,0,0));
        Check(!state.TryLaunch(start,0,true,false,false,false,clear,launch)&&!state.TryLaunch(end,.01,true,false,false,false,clear,launch),"over15m teleport cannot trigger a swept pad");
        state=new R23PadContacts(new[]{pad});state.TryLaunch(start,0,true,false,false,false,clear,launch);state.ResetContacts();Check(!state.TryLaunch(end,.01,true,false,false,false,clear,launch),"reset clears stale feet history before relocation");
        var empty=new R23PadContacts(new R23SourcePad[0]);Check(!empty.TryLaunch(feet,0,true,false,false,false,clear,launch),"genuine zero-pad Paintball does not invent a launch");
        R23SourcePad slope=null;foreach(var p in rows)if(p.Tilted){slope=p;break;}state=new R23PadContacts(new[]{slope});feet=Add(slope.Pad.Center,Mul(slope.Pad.Normal,.3f));
        Check(state.TryLaunch(feet,0,true,false,false,false,clear,launch)&&Math.Abs(received.X-slope.Pad.Normal.X)<.000001f&&Math.Abs(received.Z-slope.Pad.Normal.Z)<.000001f,"actual tilted source pad passes its reflected normalized direction");
        Console.WriteLine("Source fixture math and launch-port dispatch only. Native engine physics, Flight root route and headset not executed.");
    }
}
