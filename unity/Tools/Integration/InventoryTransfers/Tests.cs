using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryTransfers;
class Tests
{
    static int checks,serial;static long tick;
    static void Check(bool v,string name){checks++;if(!v)throw new Exception(name);}
    static CommandContext C(string actor){return new CommandContext("s",actor,"c"+(++serial),tick,1,true);}
    sealed class Host:IAuthorityGate,ITransferEligibility,IPickupActors,IInteractionQuery,IWeaponLifetimeAuthority
    {
        public bool Allow=true;
        public RuleResult Validate(CommandContext c){return Allow&&c.IsAuthority&&c.SessionId=="s"&&c.AuthorityEpoch>=1?RuleResult.Accept():RuleResult.Reject("host");}
        public bool TryRead(string actor,out ActorFrame f){f=new ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,Desktop=true,Head=new Float3(),Hand=new Float3(),Forward=new Float3(0,0,1),MaxStamina=100};return true;}
        public bool HasLineOfSight(string a,string s,Float3 o,Float3 t,float r){return s!="blocked";}
        public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int rounds){return Validate(c);}
        public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
    }
    sealed class Planner:IDropPlanner
    {
        public bool FailSecond,Stale,FailCommit,BadSemantic;public int Commits,Aborts,Prepared;
        public bool Prepare(CommandContext c,ItemInstance[] members,out IDropBatch b){Prepared++;b=new Batch(this,members);return !(FailSecond&&members.Length==2);}
        sealed class Batch:IDropBatch
        {
            readonly Planner p;readonly EvictedInstance[] poses;bool committed,aborted;
            public Batch(Planner p,ItemInstance[] members){this.p=p;poses=new EvictedInstance[members.Length];for(int n=0;n<members.Length;n++)poses[n]=new EvictedInstance(members[n].InstanceId,members[n].DefinitionId,p.BadSemantic?"wrong":"sem-"+members[n].InstanceId,new Float3(.2f*n,0,1));}
            public EvictedInstance[] Placements{get{return (EvictedInstance[])poses.Clone();}}
            public bool StillValid{get{return !p.Stale;}}
            public bool Commit(){if(committed||aborted||p.FailCommit||p.Stale)return false;committed=true;p.Commits++;return true;}
            public void Abort(){if(!aborted){aborted=true;p.Aborts++;}}
        }
    }
    sealed class Reserve:IAmmoReserve {public int Available{get{return 100;}}public int Take(int n){return n;}public int Refund(int n){return n;}}
    static TransferBridge New(Host h,Planner p){tick=0;var b=new TransferBridge("s",1,100,h,h,h,h,p);Check(b.AddActor("a"),"actor a");Check(b.AddActor("b"),"actor b");return b;}
    static void Register(TransferBridge b,string id,string kind){Check(b.Register(id,kind,"sem-"+id,new Float3(0,0,1)),"register "+id);}
    static void Collect(TransferBridge b,string id){Check(b.Execute(C("a"),PickupAction.Reserve,id).Accepted,"reserve "+id);Check(b.Execute(C("a"),PickupAction.Collect,id).Accepted,"collect "+id);}
    static TransferBridge Full(Host h,Planner p){var b=New(h,p);foreach(string id in new[]{"s1","s2","b1","k1","k2","incoming"})Register(b,id,id[0]=='s'?"soda":id[0]=='b'?"banana":"shaker");foreach(string id in new[]{"s1","s2","b1","k1","k2"})Collect(b,id);var equip=b.Equip(C("a"),"s1",false);Check(equip.Accepted,"equip pair "+equip.Reason+" ids "+String.Join(",",Array.ConvertAll(b.Inventory("a").Items,delegate(ItemInstance x){return x.InstanceId;})));return b;}
    static void Main()
    {
        var h=new Host();var p=new Planner();var b=Full(h,p);
        Check(b.Inventory("a").Items.Length==5,"full actual inventory");
        Check(b.Execute(C("a"),PickupAction.Reserve,"incoming").Accepted,"incoming reserved");
        p.FailSecond=true;int commits=p.Commits,aborts=p.Aborts;
        Check(!b.Execute(C("a"),PickupAction.Collect,"incoming").Accepted,"second placement fail");
        Check(p.Commits==commits&&p.Aborts==aborts+1,"all partial batch aborted");
        Check(b.Inventory("a").Items.Length==5&&b.Inspect("s1").Owner=="a"&&b.Inspect("s2").Owner=="a"&&b.Inspect("incoming").Owner==null,"failure preserves both modules");
        p.FailSecond=false;p.BadSemantic=true;Check(!b.Execute(C("a"),PickupAction.Collect,"incoming").Accepted,"semantic substitution refused");p.BadSemantic=false;
        p.Stale=true;Check(!b.Execute(C("a"),PickupAction.Collect,"incoming").Accepted,"stale batch refused");p.Stale=false;
        p.FailCommit=true;Check(!b.Execute(C("a"),PickupAction.Collect,"incoming").Accepted,"commit refusal");Check(b.Inventory("a").Items.Length==5,"commit refusal inventory intact");p.FailCommit=false;
        CommandContext collect=C("a");Check(b.Execute(collect,PickupAction.Collect,"incoming").Accepted,"paired commit");
        Check(!b.Execute(collect,PickupAction.Collect,"incoming").Accepted,"replay");
        Check(b.Inventory("a").Items.Length==4&&b.Inspect("incoming").Owner=="a","incoming agreement");
        Check(b.Inspect("s1").Owner==null&&b.Inspect("s2").Owner==null&&b.Inspect("s2").Position.X==.2f,"exact pair positions");
        Check(b.Execute(C("b"),PickupAction.Reserve,"s2").Accepted&&b.Execute(C("b"),PickupAction.Collect,"s2").Accepted,"other actor recollect exact member");
        Check(b.Inventory("b").Items[0].InstanceId=="s2"&&b.Inspect("s2").Owner=="b","recollection agreement");
        int publications=0;while(b.TakePublication()!=null)publications++;Check(publications==7,"exactly once committed collection publication");
        Register(b,"expiry","banana");Check(b.Execute(C("a"),PickupAction.Reserve,"expiry").Accepted,"reserve expiry");tick=100;b.Advance(tick);commits=p.Commits;
        Check(!b.Execute(C("a"),PickupAction.Collect,"expiry").Accepted&&p.Commits==commits,"lease expiry no commit");
        // Concurrent ingress uses the bridge lane and real pickup reservation arbitration.
        var race=New(h,new Planner());Register(race,"race","soda");int wins=0;
        var threads=new List<Thread>();for(int n=0;n<8;n++){int i=n;threads.Add(new Thread(delegate(){string actor=i%2==0?"a":"b";var r=new CommandContext("s",actor,"race-reserve-"+i,0,1,true);var c=new CommandContext("s",actor,"race-collect-"+i,0,1,true);race.Execute(r,PickupAction.Reserve,"race");if(race.Execute(c,PickupAction.Collect,"race").Accepted)Interlocked.Increment(ref wins);}));}
        foreach(Thread t in threads)t.Start();foreach(Thread t in threads)t.Join();Check(wins==1,"one concurrent collector");Check(race.Inventory("a").Items.Length+race.Inventory("b").Items.Length==1,"one inventory member");
        // Actual weapon lifetime and inventory bridge; no integer ammo surrogate.
        tick=200;b.Advance(tick);var weapons=new SessionWeapons("s",100,h,h);var reserve=new Reserve();Check(b.BindWeapons(weapons),"bind session registry");
        Check(weapons.Grant(C("b"),new ItemInstance("rifle","longwatch",1),WeaponMechanism.PrecisionRifle,3,reserve).Accepted,"approved grant");
        Check(!weapons.Grant(C("b"),new ItemInstance("rifle","longwatch",1),WeaponMechanism.PrecisionRifle,3,reserve).Accepted,"no repeat grant");
        Check(b.AdmitExistingWeapon(C("b"),new ItemInstance("rifle","longwatch",1),weapons).Accepted,"actual inventory weapon admission");
        Check(b.Equip(C("b"),"rifle",false).Accepted,"weapon equip");weapons.Selection(b.Inventory("b"),tick);
        var input=new HandlingInput{Sequence=1,Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=true};
        Check(!b.ApplyWeapon(C("b"),"rifle",HandlingAction.Fire,input).Accepted,"draw gate");
        input.Trigger=false;input.Sequence++;b.ApplyWeapon(C("b"),"rifle",HandlingAction.Sample,input);
        tick=224;input.Trigger=true;input.Sequence++;Check(!b.ApplyWeapon(C("b"),"rifle",HandlingAction.Fire,input).Accepted,".24 draw blocked");
        tick=225;input.Sequence++;Check(b.ApplyWeapon(C("b"),"rifle",HandlingAction.Fire,input).Accepted,".25 draw fires");
        Check(weapons.Inspect("rifle").Loaded==2,"actual loaded decremented");
        Check(b.Equip(C("b"),"s2",false).Accepted,"switch slot");weapons.Selection(b.Inventory("b"),tick);
        Check(!b.ApplyWeapon(C("b"),"rifle",HandlingAction.Fire,input).Accepted,"unselected refuses");
                Check(b.Equip(C("b"),"rifle",false).Accepted,"re-equip same stable instance");Check(weapons.Inspect("rifle").Loaded==2,"slot changes never mint");
        b.Advance(tick);Register(b,"bbanana","banana");Check(b.Execute(C("b"),PickupAction.Reserve,"bbanana").Accepted&&b.Execute(C("b"),PickupAction.Collect,"bbanana").Accepted,"fill weapon actor");
        Register(b,"weapon-replace","shaker");Check(b.Execute(C("b"),PickupAction.Reserve,"weapon-replace").Accepted,"reserve weapon replacement");
        Check(!b.Execute(C("b"),PickupAction.Collect,"weapon-replace").Accepted,"atomic weapon drop fails closed");
        Check(weapons.IsActive(new ItemInstance("rifle","longwatch",1),"b",1)&&weapons.Inspect("rifle").Loaded==2&&b.Inspect("weapon-replace").Owner==null,"failed weapon drop keeps active lifetime and ownership");
Check(weapons.Capture(C("b"),"rifle").Accepted,"capture actual lifetime");
        Check(weapons.Restore(new CommandContext("s","b","epoch-restore",226,2,true),"rifle").Accepted,"epoch restore");
        Check(weapons.Inspect("rifle").Loaded==2&&weapons.Inspect("rifle").SpentChamber,"epoch preserves actual mechanical state");
        Check(!weapons.Restore(new CommandContext("s","b","epoch-replay",227,2,true),"rifle").Accepted,"one use receipt");
        Console.WriteLine("PASS "+checks+" assertions; actual InventoryItems + Pickups + WeaponHandling. Full atomic weapon transfer BLOCKED.");
    }
}






