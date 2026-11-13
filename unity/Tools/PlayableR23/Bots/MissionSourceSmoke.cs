using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Bots;
static class MissionSourceSmoke
{
    static int checks;static void Need(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static readonly Float3 body=new Float3(0,0,0),feet=new Float3(0,0,10),head=new Float3(0,1.65f,10);
    static void Tick(R24KestrelBrain brain){brain.Observe(.02f,body,feet,head,0,1,true);brain.Tick(.02f,body);}
    static void Windup(R24KestrelBrain brain){for(int i=0;i<1000&&brain.State!=R24KestrelState.Windup;i++)Tick(brain);Need(brain.State==R24KestrelState.Windup,"Real source perception/cooldown reaches Windup");}
    static void Main()
    {
        var mission=new R24ValleyMissionState();int total=0;int[] counts={4,6,8,6};
        for(int chapter=0;chapter<4;chapter++){
            Need(mission.Chapter==chapter&&mission.EnemyCount==counts[chapter],"Four authored encounter counts");Need(mission.BeginsAlerted==(chapter>0),"Only relay waits for gate");
            Need(!mission.Advance(1,true)&&!mission.Advance(0,false),"Live security/ineligible grip cannot advance");
            for(int i=0;i<mission.EnemyCount;i++){Need(mission.SpawnAt(i).IsFinite,"Every authored spawn finite");Need(mission.VariantAt(i)==(chapter==3&&i==5?3:i%3),"Source actor sequence/Warden final slot");total++;}
            Need(Math.Abs(mission.Screen.Z-(mission.Objective.Z-.37f))<.00001,"Screen exposed source-front Z reflection once");mission.EnemyDefeated();mission.Step(.02f,true);Need(mission.Advance(0,true),"Cleared real station advances");
        }
        Need(total==24&&mission.Complete&&mission.MissionId=="complete"&&!mission.Advance(0,true),"Four missions end without duplicate/extraneous fifth wave");Need(mission.Kills==4,"Kill accounting independent of wave generation");
        mission.Restore(2,12,61.5);Need(mission.Checkpoint.X==-18&&mission.Checkpoint.Y==10&&mission.Checkpoint.Z==41,"Checkpoint uses previous objective+source(0,1,3)");bool rejected=false;try{mission.Restore(5,0,0);}catch(ArgumentException){rejected=true;}Need(rejected&&mission.Chapter==2,"Invalid checkpoint fail closed without state mutation");
        var boss=new R24KestrelBrain(3,5,body,feet,.5f,13);Need(boss.Health==350&&boss.EngageRange==100&&boss.SightRange==120,"Source Warden health/ranges");boss.TakeDamage(40,feet,head);Need(boss.Health==340&&boss.LastDamageMultiplier==.25f&&boss.SightRange==200,"Closed armor quarter damage +12s alert");
        Windup(boss);Need(!boss.AreaAttack&&!boss.CoreOpen,"First Warden attack direct with closed core");for(int i=0;i<52;i++)Tick(boss);Need(!boss.CoreOpen,"Direct core closed before1.05s");Tick(boss);Need(boss.CoreOpen,"Direct core opens at1.05s");boss.TakeDamage(40,feet,head);Need(boss.Health==300&&boss.LastDamageMultiplier==1,"Exposed core full source damage");
        for(int i=0;i<100&&!boss.ShotRequested;i++)Tick(boss);Need(boss.ShotRequested,"Direct fire after1.3s");boss.CompleteShot(true);Need(boss.State==R24KestrelState.Recover&&boss.CoreOpen,"Core stays open throughout recovery");for(int i=0;i<73;i++)Tick(boss);Need(boss.State==R24KestrelState.Pursue&&!boss.CoreOpen,"1.45s recovery closes core");
        Windup(boss);Need(boss.AreaAttack&&boss.NeedsGroundMark,"Second Warden attack requests actual ground ray");boss.ResolveGroundMark(true,new Float3(0,.06f,10));Need(!boss.NeedsGroundMark&&boss.ImpactPoint.Y==.06f,"Ground marker uses supplied actual world support");
        for(int i=0;i<77;i++)Tick(boss);Need(!boss.CoreOpen&&!boss.ShotRequested,"Area core remains closed before1.55s/fire1.85");Tick(boss);Need(boss.CoreOpen,"Area core opens at1.55s");for(int i=0;i<20&&!boss.ShotRequested;i++)Tick(boss);Need(boss.ShotRequested,"Area fire after1.85s");boss.CompleteShot(true);
        var armored=new R24KestrelBrain(3,0,body,feet,.5f,9);armored.TakeDamage(700,feet,head);Need(armored.Health==175&&armored.BossPhase==2&&armored.MoveSpeed==3.2f,"Source half-health phase transition includes armored damage");armored.TakeDamage(1000,feet,head);Need(armored.State==R24KestrelState.Dead&&!armored.CoreOpen,"Death ends core vulnerability");
        Console.WriteLine("Mission/Warden source policy: "+checks+" checks PASS. Actual station grips/physics/blast/visual/checkpoint UI remain native-unrun.");
    }
}
