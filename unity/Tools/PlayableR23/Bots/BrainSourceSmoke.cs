using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Bots;

// Executes the live engine-neutral brain, not a second AI implementation.
static class BrainSourceSmoke
{
    static int checks;
    static void Need(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static Float3 P(float x,float y,float z){return new Float3(x,y,z);}
    static R24KestrelBrain New(int variant=0,int slot=0){return new R24KestrelBrain(variant,slot,P(0,0,0),P(0,0,10),.5f,1901+slot);}
    static void Main()
    {
        var lit=New();var shade=New();for(int i=0;i<20;i++){lit.Observe(.15f,P(0,0,0),P(0,0,20),P(0,1.7f,20),0,1,true);shade.Observe(.15f,P(0,0,0),P(0,0,20),P(0,1.7f,20),0,.12f,true);}
        Need(lit.HasSight&&!shade.HasSight,"Actual source exposure must affect detection without flattening shade");
        Need(lit.InFieldOfView(P(0,0,0),P(0,0,1),P(0,1.7f,20)),"Forward visible");Need(!lit.InFieldOfView(P(0,0,0),P(0,0,1),P(0,1.7f,-20)),"Rear outside source FOV");
        Need(lit.InFieldOfView(P(0,0,0),P(0,0,1),P(0,1.7f,-2)),"Source near-field awareness");
        lit.Observe(.15f,P(0,0,0),P(30,0,20),P(30,1.7f,20),0,1,false);Need(!lit.HasSight&&lit.LastFeet.X==0,"Hidden actual target must not overwrite last observed location");
        var heard=New();heard.HearShot(P(0,1.7f,200),P(0,0,0),1);Need(!heard.HasLastKnown,"Finite source hearing radius");heard.HearShot(P(0,1.7f,20),P(0,0,0),1);Need(heard.State==R24KestrelState.Pursue&&!heard.HasSight&&heard.Detection==.55f,"Sound cues investigation without granting sight");
        heard.Observe(.02f,P(0,0,0),P(0,0,20),P(0,1.7f,20),0,1,true);Need(heard.HasSight,"Visible alert-memory reacquisition");
        for(int i=0;i<250&&heard.State!=R24KestrelState.Windup;i++)heard.Tick(.02f,P(0,0,0));Need(heard.State==R24KestrelState.Windup,"Source finite attack cooldown reaches windup");
        while(heard.StateTime<.66f)heard.Tick(.02f,P(0,0,0));var locked=heard.LockedAim;
        heard.Observe(.02f,P(0,0,0),P(2,0,20),P(2,1.7f,20),6,1,true);heard.Tick(.02f,P(0,0,0));Need(R24KestrelBrain.Length(R24KestrelBrain.Sub(locked,heard.LockedAim))<.000001f,"Final .42-second source dodge window stays locked");
        while(!heard.ShotRequested)heard.Tick(.02f,P(0,0,0));Need(heard.StateTime>1.04f,"Source reaction telegraph is not skipped");heard.CompleteShot(true);Need(heard.State==R24KestrelState.Recover&&!heard.ShotRequested,"Exactly one discharge enters recovery");
        for(int i=0;i<24;i++)heard.Tick(.02f,P(0,0,0));Need(heard.State==R24KestrelState.Pursue,"Source .45-second recovery returns pursuit");
        var radio=New();radio.ReceiveRadio(P(2,0,20),P(2,1.7f,20));Need(radio.HasLastKnown&&!radio.HasSight&&radio.State==R24KestrelState.Pursue,"Radio shares memory, not hidden target sight");Need(!radio.TryRadio(),"Normal source difficulty has no radio broadcast");
        foreach(int variant in new[]{0,1,2}){var b=New(variant);float health=variant==0?85:variant==1?60:150;Need(b.Health==health,"Source per-variant health");Need(b.TakeDamage(health-1,P(0,0,10),P(0,1.7f,10))&&b.Health==1,"Live shared bot health takes exact body damage");Need(b.TakeDamage(1,P(0,0,10),P(0,1.7f,10))&&b.State==R24KestrelState.Dead,"Lethal damage terminal");Need(!b.TakeDamage(10,P(0,0,10),P(0,1.7f,10)),"Dead target cannot re-enter damage");}
        var left=New(0,0);var right=New(0,1);left.ReceiveRadio(P(0,0,20),P(0,1.7f,20));right.ReceiveRadio(P(0,0,20),P(0,1.7f,20));Need(left.TacticalDestination().X==-right.TacticalDestination().X&&Math.Abs(left.TacticalDestination().X)==4,"Reflected source tactical lanes remain opposite/stable");
        bool rejected=false;try{left.Tick(float.NaN,P(0,0,0));}catch(ArgumentOutOfRangeException){rejected=true;}Need(rejected,"Non-finite clock rejected");
        Console.WriteLine("Live R6 first-encounter brain: "+checks+" checks PASS. Actual Unity physics/render/headset NOT exercised.");
    }
}
