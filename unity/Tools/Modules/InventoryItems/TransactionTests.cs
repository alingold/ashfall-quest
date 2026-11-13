using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
class TransactionTests
{
    sealed class Boundary:IInventoryTransactions
    {
        public InventoryChange Last;
        public int StageCalls, Commits, Aborts, Pending, Published;
        public string Active=""; public bool Holstered=true; public double Delay;
        public int FailMember=-1;
        public bool InvalidReservation, FailCommit, ThrowCommit, ThrowStage, DenyStage;
        public Action DuringStage, DuringCommit;
        public readonly Dictionary<string,int> Ammo=new Dictionary<string,int>();
        public readonly Dictionary<string,int> Entries=new Dictionary<string,int>();
        public readonly HashSet<string> Carried=new HashSet<string>();
        public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance n,ItemInstance[] old){return RuleResult.Accept();}
        sealed class Receipt:IInventoryReceipt
        {
            readonly Boundary owner; readonly InventoryChange change; readonly string active; readonly bool holstered; readonly double delay; readonly string[] carried;
            public Receipt(Boundary owner,InventoryChange change){this.owner=owner;this.change=change;active=owner.Active;holstered=owner.Holstered;delay=owner.Delay;carried=new List<string>(owner.Carried).ToArray();}
            public RuleResult Commit()
            {
                owner.Commits++;
                if(owner.DuringCommit!=null)owner.DuringCommit();
                // Adversarial partial publication must be undone by Abort.
                if(owner.FailCommit || owner.ThrowCommit) {
                    if(change.Removed.Length>0)owner.Carried.Remove(change.Removed[0].InstanceId);
                    owner.Active=change.SelectedId; owner.Holstered=change.Holstered;
                }
                if(owner.ThrowCommit)throw new InvalidOperationException("commit");
                if(owner.FailCommit || owner.InvalidReservation)return RuleResult.Reject("reservation-invalid-or-commit-failed");
                foreach(var item in change.Removed)owner.Carried.Remove(item.InstanceId);
                if(change.Incoming!=null) {
                    string id=change.Incoming.InstanceId; owner.Carried.Add(id);
                    owner.Entries[id]=owner.Entries.ContainsKey(id)?owner.Entries[id]+1:1;
                    // Initialize once only at materialization, never on equip/re-entry.
                    if(!owner.Ammo.ContainsKey(id))owner.Ammo[id]=30;
                }
                owner.Active=change.SelectedId;owner.Holstered=change.Holstered;owner.Delay=change.DrawDelaySeconds;
                owner.Published++;owner.Pending=0;return RuleResult.Accept();
            }
            public void Abort(){owner.Aborts++;owner.Pending=0;owner.Carried.Clear();foreach(var id in carried)owner.Carried.Add(id);owner.Active=active;owner.Holstered=holstered;owner.Delay=delay;}
        }
        public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt)
        {
            StageCalls++; Last=change;receipt=new Receipt(this,change);
            if(DuringStage!=null)DuringStage();
            if(ThrowStage){Pending++;throw new InvalidOperationException("stage");}
            if(DenyStage)return RuleResult.Reject("equip-denied");
            int member=0;
            foreach(var item in change.Removed){Pending++;if(member++==FailMember)return RuleResult.Reject("drop-member-blocked");}
            if(change.Incoming!=null)Pending++;
            return RuleResult.Accept();
        }
    }
    sealed class ValidateOnly:IInventoryBoundary
    {public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance i,ItemInstance[] o){return RuleResult.Accept();}}
    static int count; static long tick;
    static CommandContext Next(long epoch){tick++;return new CommandContext("s","a","r"+tick,tick,epoch,true);}
    static ItemInstance Item(string id,string kind,int stars){return new ItemInstance(id,kind,stars);}
    static bool Do(Inventory i,InventoryOperation op,ItemInstance n,string id){return i.Apply(Next(7),op,n,id).Accepted;}
    static void Must(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Run(string name,Action body){body();count++;Console.WriteLine("PASS "+name);}
    static Inventory Pair(Boundary b)
    {
        var i=new Inventory(SourceCatalog.Create(),b,"s","a",7,false);
        Must(Do(i,InventoryOperation.Admit,Item("f1","frag",1),null),"frag1");
        Must(Do(i,InventoryOperation.Admit,Item("f2","frag",1),null),"frag2");
        Must(Do(i,InventoryOperation.Admit,Item("k","kestrel",1),null),"k");
        Must(Do(i,InventoryOperation.Admit,Item("m","meridian",1),null),"m");
        Must(Do(i,InventoryOperation.Equip,null,"f2"),"equip");return i;
    }
    static void Unchanged(Inventory i,Boundary b)
    {Must(i.Items.Length==4&&i.SlotCount==3&&i.SelectedInstanceId=="f2"&&!i.Holstered&&b.Pending==0&&b.Active=="f2"&&!b.Holstered&&b.Carried.Contains("f1")&&b.Carried.Contains("f2")&&!b.Carried.Contains("n"),"whole-slot rollback");}
    static void Main()
    {
        Run("explosive-order-every-SMAW-rarity-and-reverse-acquisition",delegate {
            for(int star=1;star<=5;star++) {
                var i=new Inventory(SourceCatalog.Create(),new Boundary(),"s","a",7,false);
                Do(i,InventoryOperation.Admit,Item("z","zone",1),null);
                Do(i,InventoryOperation.Admit,Item("s","smaw",star),null);
                Do(i,InventoryOperation.Admit,Item("f","frag",1),null);
                Must(i.Slots[0][0].DefinitionId=="frag"&&i.Slots[1][0].DefinitionId=="smaw"&&i.Slots[2][0].DefinitionId=="zone","explosive sorting");
            }
        });
        Run("missing-transaction-port-fails-closed",delegate {
            var i=new Inventory(SourceCatalog.Create(),new ValidateOnly(),"s","a",7,false);
            Must(!Do(i,InventoryOperation.Admit,Item("k","kestrel",1),null)&&i.Items.Length==0,"required receipt");
        });
        Run("second-drop-staging-denial-aborts-entire-slot-and-retries",delegate {
            var b=new Boundary();var i=Pair(b);b.FailMember=1;var c=Next(7);
            Must(!i.Apply(c,InventoryOperation.ReplaceSlot,Item("n","soda",1),"f2").Accepted,"deny");
            Unchanged(i,b);Must(b.Aborts==1&&b.Last.Removed.Length==2&&b.Last.Removed[0].InstanceId=="f1"&&b.Last.Removed[1].InstanceId=="f2","exact members");
            var copy=b.Last.Removed;copy[0]=null;Must(b.Last.Removed[0]!=null,"receipt isolation");
            b.FailMember=-1;Must(i.Apply(c,InventoryOperation.ReplaceSlot,Item("n","soda",1),"f2").Accepted,"retry");
            Must(i.Items.Length==3&&!b.Carried.Contains("f1")&&!b.Carried.Contains("f2")&&b.Carried.Contains("n"),"whole slot commit");
        });
        Run("incoming-reservation-invalidated-at-commit-aborts",delegate {
            var b=new Boundary();var i=Pair(b);b.InvalidReservation=true;
            Must(!Do(i,InventoryOperation.ReplaceSlot,Item("n","banana",1),"f2"),"invalidated");Unchanged(i,b);Must(b.Aborts==1,"abort");
        });
        Run("partial-stage-throw-aborts-and-preserves-retry",delegate {
            var b=new Boundary();var i=Pair(b);b.ThrowStage=true;var c=Next(7);bool caught=false;
            try{i.Apply(c,InventoryOperation.ReplaceSlot,Item("n","banana",1),"f2");}catch(InvalidOperationException){caught=true;}
            Must(caught&&b.Aborts==1,"stage exception");Unchanged(i,b);b.ThrowStage=false;
            Must(i.Apply(c,InventoryOperation.ReplaceSlot,Item("n","banana",1),"f2").Accepted,"retry");
        });
        Run("commit-reject-and-throw-abort-with-old-state-visible",delegate {
            foreach(bool throws in new[]{false,true}) {
                var b=new Boundary();var i=Pair(b);b.ThrowCommit=throws;b.FailCommit=!throws;
                b.DuringCommit=delegate {Must(i.Items.Length==4&&i.SelectedInstanceId=="f2","old state until commit");};
                bool denied=false;try{denied=!Do(i,InventoryOperation.ReplaceSlot,Item("n","banana",1),"f2");}catch(InvalidOperationException){denied=throws;}
                Must(denied&&b.Aborts==1,"commit failure");Unchanged(i,b);
            }
        });
        Run("stage-and-commit-reentrancy-rejected",delegate {
            var b=new Boundary();var i=Pair(b);
            Action nested=delegate {Must(!Do(i,InventoryOperation.Remove,null,"k"),"no reentry");};
            b.DuringStage=nested;b.DuringCommit=nested;Must(Do(i,InventoryOperation.Holster,null,null)&&i.Items.Length==4,"outer");
        });
        Run("equip-holster-repeat-and-denied-equip-effects",delegate {
            var b=new Boundary();var i=Pair(b);int entries=b.Entries["k"];b.Ammo["k"]=4;
            Must(Do(i,InventoryOperation.Equip,null,"k")&&b.Last.PreviousSelectedId=="f2"&&b.Last.SelectedId=="k"&&b.Last.Removed.Length==0&&b.Last.RequireFreshTrigger&&b.Last.DrawDelaySeconds==0.25&&b.Active=="k"&&!b.Holstered&&b.Delay==0.25,"equip effects");
            Must(Do(i,InventoryOperation.Equip,null,"k")&&!b.Last.SelectionChanged&&!b.Last.RequireFreshTrigger,"same selection no reset");
            Must(Do(i,InventoryOperation.Holster,null,null)&&b.Last.SelectionChanged&&b.Last.Holstered&&!b.Last.RequireFreshTrigger,"holster");
            Must(Do(i,InventoryOperation.Equip,null,"k")&&b.Last.RequireFreshTrigger,"redraw");
            b.DenyStage=true;Must(!Do(i,InventoryOperation.Equip,null,"m")&&i.SelectedInstanceId=="k"&&b.Aborts==1,"equip denial");
            Must(b.Ammo["k"]==4&&b.Entries["k"]==entries,"no refill or lifetime recreation on selection");
        });
        Run("remove-selected-stack-member-notifies-fallback-without-creating",delegate {
            var b=new Boundary();var i=Pair(b);int entries=b.Entries["f1"];
            Must(Do(i,InventoryOperation.Remove,null,"f2")&&i.SelectedInstanceId=="f1"&&b.Last.PreviousSelectedId=="f2"&&b.Last.SelectedId=="f1"&&b.Last.Removed.Length==1&&b.Last.Removed[0].InstanceId=="f2"&&b.Entries["f1"]==entries,"stack fallback");
        });
        Run("drop-readmit-and-epoch-handoff-preserve-external-ammo-and-identity",delegate {
            var b=new Boundary();var i=Pair(b);b.Ammo["k"]=3;
            Must(Do(i,InventoryOperation.Remove,null,"k"),"drop");
            Must(!Do(i,InventoryOperation.Admit,Item("k","meridian",1),null),"identity alias denied");
            Must(Do(i,InventoryOperation.Admit,Item("k","kestrel",1),null)&&b.Ammo["k"]==3,"readmit no refill");
            int calls=b.StageCalls;var snapshot=i.RetireForHandoff();var j=new Inventory(SourceCatalog.Create(),b,"s","a",8,snapshot);
            Must(b.StageCalls==calls&&j.Items.Length==i.Items.Length&&j.SelectedInstanceId==i.SelectedInstanceId&&j.Slots.Length==i.Slots.Length&&b.Ammo["k"]==3,"handoff no lifecycle reset");
            Must(!Do(i,InventoryOperation.Remove,null,"k"),"old aggregate retired");
            Must(!j.Apply(Next(7),InventoryOperation.Equip,null,"k").Accepted,"old epoch rejected");
            Must(j.Apply(Next(8),InventoryOperation.Equip,null,"k").Accepted&&b.Ammo["k"]==3,"new epoch selection preserves ammo");
        });
        Run("free-slot-and-pair-fill-have-zero-removals",delegate {
            var b=new Boundary();var i=new Inventory(SourceCatalog.Create(),b,"s","a",7,false);
            Must(Do(i,InventoryOperation.ReplaceSlot,Item("f1","frag",1),null)&&b.Last.Removed.Length==0,"free");
            Must(Do(i,InventoryOperation.ReplaceSlot,Item("f2","frag",1),"f1")&&b.Last.Removed.Length==0&&i.SlotCount==1,"pair");
        });
        Run("unexpected-incoming-cannot-forge-lifetime-entry",delegate {
            var b=new Boundary();var i=Pair(b);int calls=b.StageCalls;
            Must(!Do(i,InventoryOperation.Equip,Item("n","kestrel",1),"k")&&b.StageCalls==calls,"extraneous incoming");
        });
        Console.WriteLine("PASS "+count+" r2 transaction/lifecycle scenarios (synthetic bridge; no runtime acceptance)");
    }
}
