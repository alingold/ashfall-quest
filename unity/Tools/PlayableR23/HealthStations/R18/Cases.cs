using System;
using Ashfall.Integration.Playable.JumpPads;
class R18Cases
{
    static int n;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);n++;Console.WriteLine("PASS "+message);}
    static void Main()
    {
        var station=new R23HealthStationRecharge(2);int calls=0;float health=40,shield=0;
        Func<bool> restore=delegate{calls++;if(health<=0||(health>=100&&shield>=100))return false;health=shield=100;return true;};
        Check(station.TryRestore(0,2,restore)&&health==100&&shield==100,"source restores health+shield together");
        Check(station.Remaining(0,2)==15,"source starts shared15 seconds");
        Check(!station.TryRestore(0,16.999,restore)&&calls==1,"cooldown does not reapply or call owner");
        Check(station.Remaining(1,2)==0,"per-station timers independent");
        Check(!station.TryRestore(1,3,restore)&&station.Remaining(1,3)==0,"full owner refuses without consuming timer");
        health=100;shield=12;Check(station.TryRestore(1,3,restore)&&shield==100,"shield-only deficit restores");
        health=20;Check(station.TryRestore(0,17,restore),"exact15s expiry accepts");
        Check(station.Remaining(0,17)==15,"new cooldown belongs to same station");
        health=0;Check(!station.TryRestore(0,32,restore)&&station.Remaining(0,32)==0,"dead owner refuses without cooldown");
        health=10;Check(!station.TryRestore(0,32,delegate{return false;})&&station.Remaining(0,32)==0,"explicit refusal no consumption");
        Check(!station.TryRestore(0,32,null),"unbound callback never consumes");
        Check(!station.TryRestore(-1,32,restore)&&!station.TryRestore(2,32,restore),"invalid station denied");
        Check(!station.TryRestore(0,Double.NaN,restore)&&!station.TryRestore(0,Double.PositiveInfinity,restore)&&!station.TryRestore(0,-1,restore),"invalid time denied");
        Check(station.TryRestore(0,33,restore),"refused ready station remains usable");
        Check(station.Remaining(0,38)==10,"wall clock continues independently of contact or pause");
        var replacement=new R23HealthStationRecharge(2);Check(replacement.Remaining(0,38)==0,"actual map replacement gets fresh stations");
        Console.WriteLine("PASS "+n+" R18 restore/refusal/per-station recharge production cases");
    }
}
