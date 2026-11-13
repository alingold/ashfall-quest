using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;

class InventoryTests
{
    class Boundary : IInventoryTransactions
    {
        class Receipt : IInventoryReceipt { public RuleResult Commit(){return RuleResult.Accept();} public void Abort(){} }
        public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=new Receipt();return RuleResult.Accept();}
        public bool Allow=true; public int Calls; public ItemInstance[] Outgoing;
        public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance incoming,ItemInstance[] outgoing)
        { Calls++; Outgoing=outgoing; return Allow ? RuleResult.Accept() : RuleResult.Reject("held-reload-or-blocked-drop"); }
    }
    static int checks; static long tick;
    static void Check(bool yes,string name) { checks++; if(!yes)throw new Exception(name); }
    static CommandContext Next() { tick++; return new CommandContext("s","a","c"+tick,tick,7,true); }
    static ItemInstance Item(string id,string kind) {return new ItemInstance(id,kind,1);}
    static bool Apply(Inventory i,InventoryOperation op,ItemInstance n,string target) {return i.Apply(Next(),op,n,target).Accepted;}
    static void Main()
    {
        var c=SourceCatalog.Create();
        Check(c.Definitions.Length==35,"complete catalog"); int active=0;
        foreach(var d in c.Definitions) if(d.IsWeapon && d.Active)active++;
        Check(active==28,"reserved slots inactive");
        Check(c.Weapon(0).Id=="kestrel" && c.Weapon(0).ProfileId=="cx4","identity separate from profile");
        Check(c.Weapon(11).Stats(5).MagazineTotal==72,"dual uzis capacity");
        Check(c.Weapon(12).RoundsPerSecond==3.75,"matadors rate gain");
        Check(c.Weapon(1).Stats(4).Body==96 && c.Weapon(1).Stats(4).Pellets==8,"shotgun aggregate damage");
        Check(c.Weapon(26).Stats(1).MagazineTotal==1,"one physical frag");
        Check(c.Weapon(5).AmmoFamily=="sniper","rack family overrides old profile family");
        ItemDefinition missing; Check(!c.TryGet("Kestrel",out missing),"case sensitive");
        bool threw=false;try {c.Weapon(26).Stats(2);}catch(ArgumentOutOfRangeException){threw=true;} Check(threw,"non-tiered rejects stars");
        threw=false; try{new ItemCatalog(new[]{c.Weapon(0),c.Weapon(0)});}catch(ArgumentException){threw=true;}Check(threw,"duplicate definitions reject");
        var b=new Boundary(); var i=new Inventory(c,b,"s","a",7,true);
        Check(i.Items.Length==3 && i.Capacity==3 && i.SlotCount==3 && i.SelectedInstanceId=="weapon_0" && !i.Holstered,"starter loadout");
        Check(i.Slots[0][0].DefinitionId=="surge8" && i.Slots[1][0].DefinitionId=="meridian","category display order");
        Check(!Apply(i,InventoryOperation.Admit,Item("f","frag"),null),"full inventory rejects add");
        b.Allow=false;Check(!Apply(i,InventoryOperation.ReplaceSlot,Item("f","frag"),"weapon_0"),"failed drop rejects replacement");
        Check(i.Items.Length==3 && i.SelectedInstanceId=="weapon_0","replacement failure preserves state");
        b.Allow=true;Check(Apply(i,InventoryOperation.ReplaceSlot,Item("f","frag"),"weapon_0"),"replace selected slot");
        Check(Apply(i,InventoryOperation.Admit,Item("f2","frag"),null) && i.SlotCount==3 && i.Items.Length==4,"second frag fits full pouch");
        Check(!Apply(i,InventoryOperation.Admit,Item("f3","frag"),null),"third frag needs slot");
        Check(!Apply(i,InventoryOperation.Admit,Item("f2","frag"),null),"instance duplicate rejected");
        Check(Apply(i,InventoryOperation.Equip,null,"f2"),"equip second stack member");
        Check(Apply(i,InventoryOperation.Remove,null,"f2") && i.SelectedInstanceId=="f","depleted selects same stack");
        Check(Apply(i,InventoryOperation.Admit,Item("f2","frag"),null),"external instance can return after removal");
        b.Allow=false; Check(!Apply(i,InventoryOperation.ReplaceSlot,Item("b","banana"),"f"),"atomic whole stack placement failure");
        Check(b.Outgoing.Length==2 && i.Items.Length==4,"all drop members validated together");
        b.Allow=true;Check(Apply(i,InventoryOperation.ReplaceSlot,Item("b","banana"),"f") && i.Items.Length==3,"whole stack replaced");
        Check(!Apply(i,InventoryOperation.Admit,Item("b2","banana"),null),"banana does not stack");
        Check(!Apply(i,InventoryOperation.ReplaceSlot,Item("s1","shaker"),"b"),"full replacement requires selected slot");
        Check(Apply(i,InventoryOperation.Equip,null,"b"),"select banana to replace");
        Check(Apply(i,InventoryOperation.ReplaceSlot,Item("s1","shaker"),"b"),"shaker replaces banana");
        Check(Apply(i,InventoryOperation.Admit,Item("s2","shaker"),null),"shaker stacks");
        for(int n=0;n<3;n++)Check(Apply(i,InventoryOperation.UpgradeCapacity,null,null),"backpack capacity increment");
        Check(i.Capacity==6 && !Apply(i,InventoryOperation.UpgradeCapacity,null,null),"capacity capped");
        Check(!Apply(i,InventoryOperation.Admit,Item("inactive","furnace"),null),"inactive weapon rejects");
        Check(!Apply(i,InventoryOperation.Admit,Item("pack","backpack"),null),"backpack is upgrade not carried");
        var replay=Next();Check(i.Apply(replay,InventoryOperation.Holster,null,null).Accepted,"holster");
        Check(!i.Apply(replay,InventoryOperation.Equip,null,"s1").Accepted,"replayed command rejects");
        int calls=b.Calls;Check(!i.Apply(new CommandContext("wrong","a","bad",tick+1,7,true),InventoryOperation.Remove,null,"s1").Accepted,"wrong session");
        Check(!i.Apply(new CommandContext("s","a","bad",tick+1,7,false),InventoryOperation.Remove,null,"s1").Accepted,"client rejected");
        Check(!i.Apply(new CommandContext("s","a","bad",tick+1,8,true),InventoryOperation.Remove,null,"s1").Accepted,"wrong epoch");
        Check(b.Calls==calls,"authority failure before external port");
        for(int n=0;n<270;n++)Check(Apply(i,InventoryOperation.Holster,null,null),"replay window advance");
        Check(!i.Apply(replay,InventoryOperation.Holster,null,null).Accepted,"evicted replay cannot return");
        var copy=i.Slots;copy[0][0]=null;Check(i.Slots[0][0]!=null,"snapshot isolation");
        var e=new Inventory(c,b,"s","a",7,false);
        Check(e.Holstered && e.SelectedInstanceId=="","empty state");
        Check(Apply(e,InventoryOperation.Admit,Item("x","soda"),null),"soda accepted");
        Check(Apply(e,InventoryOperation.Equip,null,"x"),"consumable equip");
        Check(Apply(e,InventoryOperation.Remove,null,"x") && e.Holstered && e.SelectedInstanceId=="","last item removed");
        // Deterministic capacity/conservation stress through production transitions.
        for(int n=0;n<100;n++) {
            string id="t"+n;Check(Apply(e,InventoryOperation.Admit,Item(id,"frag"),null),"cycle admit");
            Check(Apply(e,InventoryOperation.Remove,null,id),"cycle remove");Check(e.Items.Length==0 && e.SlotCount==0,"no stale groups");
        }
        Console.WriteLine("PASS "+checks+" production behavior checks");
    }
}
