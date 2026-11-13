using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
public class ColdBindingTests
{
    static int checks;
    static void A(bool b,string s){checks++;if(!b)throw new Exception(s);}
    sealed class Host:IAuthorityGate,IWeaponLifetimeAuthority,IInventoryTransactions
    {
        public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("authority");}
        public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}
        public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
        public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] removed){return Validate(c);}
        public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=new Receipt();return Validate(c);}
        sealed class Receipt:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
    }
    public static void Main()
    {
        var h=new Host();var c=new CommandContext("s","a","create",10,1,true);
        var lifetimes=new WeaponLifetimes("s",h);var reserve=new FiniteAmmoReserve(20,11);WeaponHandling weapon;
        A(lifetimes.CreateNew(c,"stable",WeaponMechanism.PrecisionRifle,3,reserve,h,out weapon).Accepted,"actual initial lifetime");
        WeaponTransfer receipt;var capture=new CommandContext("s","a","capture",11,1,true);
        A(lifetimes.Capture(capture,"stable",out receipt).Accepted,"actual opaque transfer");
        var resume=new CommandContext("s","b","resume",12,2,true);
        var cold=new WeaponLifetimes("s",h);WeaponHandling restored;
        A(!cold.Restore(resume,"stable",receipt,out restored).Accepted&&restored==null,"cold ledger cannot restore another ledger's receipt despite actual authority");
        A(lifetimes.Restore(resume,"stable",receipt,out restored).Accepted,"same actual ledger supports in-process owner handoff");
        A(restored.Snapshot.Loaded==3&&reserve.Available==11,"handoff mechanics and actual reserve preserved");
        A(!lifetimes.Restore(resume,"stable",receipt,out weapon).Accepted,"receipt remains once-only");
        var inventory=new Inventory(SourceCatalog.Create(),h,"s","a",1,false);
        A(inventory.Apply(c,InventoryOperation.Admit,new ItemInstance("stable","longwatch",1),null).Accepted,"actual inventory admit");
        bool rejected=false;
        try{new Inventory(SourceCatalog.Create(),h,"s","a",2,inventory.Capture());}catch(ArgumentException){rejected=true;}
        A(rejected,"ordinary snapshot is not a checkpoint constructor contract");
        var handoff=inventory.RetireForHandoff();var next=new Inventory(SourceCatalog.Create(),h,"s","a",2,handoff);
        A(next.Items.Length==1&&next.Items[0].InstanceId=="stable","actual epoch handoff constructor exists for live opaque snapshot");
        Console.WriteLine("PASS "+checks+" actual authority/constructor boundaries; cold playable restoration remains BLOCKED");
    }
}
