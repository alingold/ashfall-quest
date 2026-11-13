using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.WeaponHandling;
sealed class PracticeClockCases:IInteractionQuery
{
    public bool HasLineOfSight(string actor,string target,Float3 from,Float3 to,float range){return true;}
    static int count;static void Need(bool value,string why){count++;if(!value)throw new Exception(why);}
    public static void Main()
    {
        var round=new R23RoundState(new[]{"target/0"},new PracticeClockCases(),true);round.ExternalPracticeClock=true;
        Need(round.ApplyTargetDamage("target/0",25)&&round.ReadTarget("target/0").Shield==75,"Real Health target damaged");
        for(int i=0;i<12;i++)round.Step(.25f,new HandlingInput{Enabled=true},false,false);
        Need(round.ReadTarget("target/0").Shield==75,"External source process is not double-ticked by gun mechanics");
        for(int i=0;i<5;i++)round.AdvancePracticeTargets(.25f);Need(round.ReadTarget("target/0").Shield==75,"Source1.25seconds does not reset");round.AdvancePracticeTargets(.25f);
        Need(round.ReadTarget("target/0").Shield==100&&round.ReadTarget("target/0").Health==100&&round.LastTargetDamage("target/0")==0&&round.TotalTargetDamage("target/0")==0,"Independent source1.5seconds resets actual Health/readout counters");
        round.ExternalPracticeClock=false;Need(round.ApplyTargetDamage("target/0",15),"Legacy consumer damage");for(int i=0;i<6;i++)round.Step(.25f,new HandlingInput{Enabled=true},false,false);Need(round.ReadTarget("target/0").Shield==100,"Standalone mechanics retain internal target clock");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-.1f,.26f}){bool refused=false;try{round.AdvancePracticeTargets(invalid);}catch(ArgumentOutOfRangeException){refused=true;}Need(refused,"Invalid source clock sample refused");}
        Console.WriteLine(count+" practice Health/independent-clock cases PASS");
    }
}
