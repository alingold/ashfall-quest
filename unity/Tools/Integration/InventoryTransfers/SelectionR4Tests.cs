using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryTransfers;

static class SelectionR4Tests
{
    static int checks,sequence;static long tick;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    static CommandContext C(string actor){return new CommandContext("s",actor,"r4-"+Interlocked.Increment(ref sequence),tick,1,true);}
    sealed class Host:IAuthorityGate,ITransferEligibility,IPickupActors,IInteractionQuery,IWeaponLifetimeAuthority
    {
        public bool Allow=true;
        public RuleResult Validate(CommandContext c){return Allow&&c.IsAuthority&&c.SessionId=="s"&&c.AuthorityEpoch==1?RuleResult.Accept():RuleResult.Reject("host");}
        public bool TryRead(string actor,out ActorFrame f){f=new ActorFrame{Allowed=Allow,HandEligible=true,TransferSafe=true,Desktop=true,Forward=new Float3(0,0,1),MaxStamina=6};return true;}
        public bool HasLineOfSight(string actor,string owner,Float3 a,Float3 b,float range){return true;}
        public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int rounds){return Validate(c);}
        public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
    }
    sealed class Reserve:IAmmoReserve {public int Available{get{return 10;}}public int Take(int n){return n;}public int Refund(int n){return n;}}
    sealed class Planner:IDropPlanner
    {
        public bool Prepare(CommandContext c,ItemInstance[] items,out IDropBatch batch){batch=new Batch();return items.Length==0;}
        sealed class Batch:IDropBatch{public EvictedInstance[] Placements{get{return new EvictedInstance[0];}}public bool StillValid{get{return true;}}public bool Commit(){return true;}public void Abort(){}}
    }
    sealed class Fixture
    {
        public Host Host=new Host();public TransferBridge Bridge;public SessionWeapons Weapons;
        public Fixture(bool twoActors=false)
        {
            tick=0;Bridge=new TransferBridge("s",1,100,Host,Host,Host,Host,new Planner());
            Check(Bridge.AddActor("a"),"add a");Check(Bridge.AddActor("b"),"add b");Weapons=new SessionWeapons("s",100,Host,Host);Check(Bridge.BindWeapons(Weapons),"bind one registry");
            foreach(string actor in twoActors?new[]{"a","b"}:new[]{"a"})
            {
                var gun=new ItemInstance("rifle-"+actor,"longwatch",1);
                Check(Weapons.Grant(C(actor),gun,WeaponMechanism.PrecisionRifle,3,new Reserve()).Accepted,"grant actual gun");
                Check(Bridge.AdmitExistingWeapon(C(actor),gun,Weapons).Accepted,"admit actual gun");
                string food="food-"+actor;Check(Bridge.Register(food,"banana","sem-"+food,new Float3(0,0,1)),"register banana");
                Check(Bridge.Execute(C(actor),PickupAction.Reserve,food).Accepted,"reserve banana");Check(Bridge.Execute(C(actor),PickupAction.Collect,food).Accepted,"collect banana");
                Check(Bridge.Equip(C(actor),gun.InstanceId,false).Accepted,"draw rifle");
            }
            At(25);
            foreach(string actor in twoActors?new[]{"a","b"}:new[]{"a"})Check(Bridge.ApplyWeapon(C(actor),"rifle-"+actor,HandlingAction.Sample,Input(1,false)).Accepted,"fresh released sample");
        }
        public void At(long value){tick=value;Bridge.Advance(value);}
        public void Food(string actor){Check(Bridge.Execute(C(actor),PickupAction.Equip,"food-"+actor).Accepted,"equip actual banana");}
    }
    static HandlingInput Input(long sequence,bool trigger){return new HandlingInput{Sequence=sequence,Enabled=true,Desktop=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger};}
    static void Main()
    {
        var f=new Fixture();var old=f.Bridge.Inventory("a");f.Food("a");f.At(26);
        Check(!f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(2,true)).Accepted,"banana blocks bridge rifle dispatch");
        Check(!f.Weapons.Apply(old,C("a"),"rifle-a",HandlingAction.Fire,Input(2,true)).Accepted,"old inventory snapshot cannot bypass live registry action owner");
        Check(f.Weapons.Inspect("rifle-a").Loaded==3,"blocked actions consume no rounds");
        Check(f.Bridge.Execute(C("a"),PickupAction.Stow,null).Accepted,"explicit consumable stow");
        Check(!f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(3,true)).Accepted,"stow does not implicitly redraw old rifle");
        Check(f.Bridge.Equip(C("a"),"rifle-a",false).Accepted,"explicit return to rifle");
        f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Sample,Input(4,false));f.At(50);
        Check(!f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(5,true)).Accepted,"return draw denies .24 seconds");f.At(51);
        Check(f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(6,true)).Accepted&&f.Weapons.Inspect("rifle-a").Loaded==2,"return draw accepts .25 with real ammo conservation");

        f=new Fixture();f.Food("a");
        for(int i=0;i<4;i++){Check(f.Bridge.Execute(C("a"),PickupAction.DesktopRelease,"food-a").Accepted,"peel release");Check(f.Bridge.Execute(C("a"),PickupAction.DesktopPress,"food-a").Accepted,"peel press");}
        Check(f.Bridge.ConsumablePhase("a")==Preparation.Consume,"actual banana prepared");
        Check(!f.Bridge.Equip(C("a"),"missing",false).Accepted&&f.Bridge.HeldConsumable("a")=="food-a"&&f.Bridge.ConsumablePhase("a")==Preparation.Consume,"invalid inventory selection preserves preparation");
        f.Host.Allow=false;Check(!f.Bridge.Equip(C("a"),"rifle-a",false).Accepted&&f.Bridge.HeldConsumable("a")=="food-a","denied selection preserves actual held item");f.Host.Allow=true;
        Check(f.Bridge.Equip(C("a"),"rifle-a",false).Accepted&&f.Bridge.HeldConsumable("a")==null&&f.Bridge.ConsumablePhase("a")==Preparation.Idle,"valid reverse selection clears real consumable preparation");
        f.At(50);Check(!f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(2,true)).Accepted,"return selection requires fresh trigger release");
        Check(f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Sample,Input(3,false)).Accepted,"new release after return");
        Check(f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(4,true)).Accepted,"fire after explicit release and draw");

        f=new Fixture();f.Food("a");Check(f.Bridge.Equip(C("a"),"rifle-a",true).Accepted,"holster while food active");
        Check(f.Bridge.HeldConsumable("a")==null&&f.Bridge.ConsumablePhase("a")==Preparation.Idle&&f.Bridge.Inventory("a").Holstered,"holster clears preparation but leaves gun inactive");
        Check(!f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(2,true)).Accepted,"holstered gun cannot fire");
        f=new Fixture();Check(f.Bridge.Equip(C("a"),"rifle-a",false).Accepted,"source same-item reselect");
        Check(!f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(2,true)).Accepted,"same-item reselect resets trigger/draw gate");

        f=new Fixture(true);f.Food("a");
        Check(f.Bridge.ApplyWeapon(C("b"),"rifle-b",HandlingAction.Fire,Input(2,true)).Accepted,"other actor independent action owner");
        Check(!f.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(2,true)).Accepted&&f.Weapons.Inspect("rifle-a").Loaded==3,"food actor remains denied with unchanged ammo");
        Check(!f.Bridge.Execute(C("a"),PickupAction.Equip,"food-b").Accepted&&f.Bridge.HeldConsumable("a")=="food-a","foreign food selection denied without replacing owner");
        var other=new TransferBridge("s",1,100,f.Host,f.Host,f.Host,f.Host,new Planner());Check(!other.BindWeapons(f.Weapons),"registry cannot bind another action lane");
        for(int i=0;i<16;i++)
        {
            var race=new Fixture();var start=new ManualResetEvent(false);bool equip=false,fire=false;Exception error=null;
            var a=new Thread(delegate(){try{start.WaitOne();equip=race.Bridge.Execute(C("a"),PickupAction.Equip,"food-a").Accepted;}catch(Exception e){error=e;}});
            var b=new Thread(delegate(){try{start.WaitOne();fire=race.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(2,true)).Accepted;}catch(Exception e){error=e;}});
            a.Start();b.Start();start.Set();a.Join();b.Join();start.Close();
            Check(error==null&&equip&&race.Bridge.HeldConsumable("a")=="food-a"&&race.Weapons.Inspect("rifle-a").Loaded==(fire?2:3),"equip/fire race respects serialized outcome "+i);
            Check(!race.Bridge.ApplyWeapon(C("a"),"rifle-a",HandlingAction.Fire,Input(3,true)).Accepted,"post-equip shot denied "+i);
        }
        Console.WriteLine("PASS "+checks+" selection checks; prepared weapon migration/physical gesture suspension remain OPEN");
    }
}
