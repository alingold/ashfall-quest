using System;
using Ashfall.Integration.Playable.UI;
partial class LobbyCases
{
    static void TitleMomentTiming()
    {
        var clock=new R25TitleMoment();
        if(!clock.Enter(true,false)||clock.Enter(true,false))throw new Exception("one entry only");
        clock.Advance(.3f,false);if(Math.Abs(clock.LogoAlpha-.5f)>.001||clock.EmberCount!=0)throw new Exception("logo fade/ember delay");
        clock.Advance(.7f,false);if(clock.EmberCount!=36||Math.Abs(clock.LogoAlpha-1)>.001)throw new Exception("wordmark fully visible at one second");
        clock.Advance(3,false);if(clock.Settle!=0)throw new Exception("hold");
        clock.Advance(.4f,false);if(Math.Abs(clock.Settle-.5f)>.001)throw new Exception("settle ease");
        clock.Advance(.4f,false);if(Math.Abs(clock.Scale-.42f)>.001||clock.EmberCount!=10||clock.AshCount!=0)throw new Exception("quiet banner");
        var early=new R25TitleMoment();early.Enter(true,false);early.Advance(.1f,true);early.Advance(.7f,false);if(early.Settle<.999f)throw new Exception("first input settles");
        float time=early.Elapsed;early.Advance(float.NaN,false);early.Advance(-1,false);early.Advance(float.PositiveInfinity,false);if(early.Elapsed!=time)throw new Exception("invalid time");
        if(new R25TitleMoment().Enter(false,false)||new R25TitleMoment().Enter(true,true))throw new Exception("kill/return skip");
    }
}
