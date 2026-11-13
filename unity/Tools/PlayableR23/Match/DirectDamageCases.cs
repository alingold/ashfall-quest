using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
class DirectDamageCases
{
    sealed class World:IInteractionQuery{public bool HasLineOfSight(string actor,string target,Float3 from,Float3 to,float range){return false;}}
    static int count;static void Need(bool value){if(!value)throw new Exception("Actual shared Health zone contract");count++;}
    static void Main()
    {
        var round=new R23RoundState(new string[0],new World(),true);
        Need(round.Health==100&&round.Shield==100);
        Need(round.ApplyDirectPlayerDamage(5)&&round.Health==95&&round.Shield==100);
        round.Damage(5);Need(round.Health==95&&round.Shield==95);
        Need(round.ApplyPlayerGain(5,0)&&round.Health==100&&round.Shield==95);
        foreach(float invalid in new[]{0f,-1,float.NaN,float.PositiveInfinity,float.NegativeInfinity})Need(!round.ApplyDirectPlayerDamage(invalid)&&round.Health==100&&round.Shield==95);
        Need(round.ApplyDirectPlayerDamage(.25f)&&round.Health==99.75f&&round.Shield==95);
        Need(round.ApplyDirectPlayerDamage(1000)&&round.Health==0&&round.Shield==0);
        Need(!round.ApplyDirectPlayerDamage(1)&&!round.ApplyPlayerGain(100,100));
        round.Reset();Need(round.Health==100&&round.Shield==100);
        Console.WriteLine(count+" actual shared Health direct-zone/ordinary-shield/gain/death/reset contracts PASS");
    }
}
