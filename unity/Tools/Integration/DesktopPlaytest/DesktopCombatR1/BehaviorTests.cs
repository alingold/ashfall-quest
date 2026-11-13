using System;
using Ashfall.Core;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Modules.HealthStates;
class BehaviorTests : IInteractionQuery
{
    bool visible=true;static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    public bool HasLineOfSight(string actor,string target,Float3 a,Float3 b,float range){return visible;}
    static int Main(){var query=new BehaviorTests();var rifle=new LocalRifleSandbox(new[]{"target"},query);var point=new Float3(0,0,10);var origin=new Float3();
        Check(rifle.Snapshot.Loaded==24&&rifle.Reserve==96,"initial entitlement");
        Check(!rifle.Hit("target",origin,point).Accepted,"cannot fabricate shot");
        Check(rifle.Step(.016f,true,false,false),"first shot");Check(rifle.Snapshot.Loaded==23&&rifle.Reserve==96,"one debit");
        Check(rifle.Hit("target",origin,point).Accepted,"combat health commit");Check(rifle.Read("target").Shield==75,"real shield loss");
        Check(!rifle.Hit("target",origin,point).Accepted&&rifle.Read("target").Shield==75,"no duplicate hit");
        Check(!rifle.Step(.016f,true,false,false)&&rifle.Snapshot.Loaded==23,"cadence denial preserves ammo");
        Check(!rifle.Step(.25f,true,true,true)&&rifle.Snapshot.Loaded==23&&!rifle.Reloading,"pause denial");
        Check(!rifle.Step(.2f,true,false,false)&&rifle.Snapshot.Loaded==23,"held fire remains disarmed after pause");
        rifle.Step(.016f,false,false,false);
        query.visible=false;Check(rifle.Step(.2f,true,false,false),"second shot");Check(!rifle.Hit("target",origin,point).Accepted&&rifle.Read("target").Shield==75,"occlusion preserves health");query.visible=true;
        rifle.Step(.016f,false,true,false);for(int i=0;i<20;i++)rifle.Step(.1f,false,false,false);
        Check(!rifle.Reloading&&rifle.Snapshot.Loaded==24&&rifle.Reserve==94,"actual reload conserves 118 rounds");
        int applied=0;for(int i=0;i<20&&rifle.Read("target").Life==LifeState.Alive;i++){if(rifle.Step(.2f,true,false,false)&&rifle.Hit("target",origin,point).Accepted)applied++;}
        Check(applied==7&&rifle.Read("target").Life==LifeState.Dead,"actual target death");
        var reset=new LocalRifleSandbox(new[]{"target"},query);Check(reset.Read("target").Life==LifeState.Alive&&reset.Snapshot.Shots==0&&reset.Reserve==96,"fresh reset state");
        Console.WriteLine("PASS DesktopCombat "+checks+" checks");return 0;
    }
}

