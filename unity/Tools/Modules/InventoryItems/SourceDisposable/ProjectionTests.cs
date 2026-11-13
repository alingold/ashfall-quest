using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.RecoveryContracts;
class ProjectionTests
{
    sealed class Boundary:IInventoryTransactions
    {
        public bool Allowed=true;
        public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance n,ItemInstance[] old)
        {return Allowed?RuleResult.Accept():RuleResult.Reject("revoked");}
        sealed class Receipt:IInventoryReceipt {public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
        public RuleResult Stage(CommandContext c,InventoryChange x,out IInventoryReceipt r){r=new Receipt();return RuleResult.Accept();}
    }
    static int checks; static long tick;
    static CommandContext C(){return At(++tick);}
    static CommandContext At(long t){return new CommandContext("s","a","c"+t,t,1,true);}
    static void Check(bool b,string text){if(!b)throw new Exception(text);checks++;}
    static ItemCatalog Catalog(){return SourceCatalog.Create();}
    static Inventory Make(Boundary b,params string[] kinds)
    {
        var i=new Inventory(Catalog(),b,"s","a",1,false);
        for(int n=0;n<kinds.Length;n++)Check(i.Apply(C(),InventoryOperation.Admit,new ItemInstance("i"+n,kinds[n],1),null).Accepted,"admit");
        if(kinds.Length>0)Check(i.Apply(C(),InventoryOperation.Equip,null,"i0").Accepted,"equip");
        return i;
    }
    static Inventory.SourceDisposableProjection Prepare(Inventory i,string id,out CommandContext c)
    {c=C();Inventory.SourceDisposableProjection p;Check(i.PrepareSourceDisposableProjection(c,id,c.Tick+10,out p),"prepare");return p;}
    static void Main()
    {
        var b=new Boundary();var i=Make(b,"frag","frag","zone");CommandContext c;
        var p=Prepare(i,"i0",out c);
        Check(i.Items.Length==3&&i.SelectedInstanceId=="i0","no predebit");
        Check(p.RemovedId=="i0"&&p.SelectedId=="i1"&&!p.Holstered,"same-stack survivor");
        Check(!i.Apply(C(),InventoryOperation.Holster,null,null).Accepted,"exclude ordinary");
        Inventory.PreparedWeaponHandoff handoff;
        Check(!i.PrepareWeaponHandoff(C(),new[]{"i0","i1"},new ItemInstance[0],out handoff),"exclude handoff");
        Check(p.Validate(At(c.Tick+1)),"validate");p.CommitValidated();p.CommitValidated();p.Abort();
        Check(i.Items.Length==2&&i.Items[0].InstanceId=="i1"&&i.SelectedInstanceId=="i1"&&!i.Holstered,"exact removal and no survivor holster");
        Check(i.Slots[0][0].DefinitionId=="frag"&&i.Slots[1][0].DefinitionId=="zone","deterministic sort");
        Check(!i.PrepareSourceDisposableProjection(c,"i1",c.Tick+10,out p),"committed replay");
        i=Make(b,"zone","frag","frag");Check(i.Apply(C(),InventoryOperation.Equip,null,"i1").Accepted,"select later stack");
        p=Prepare(i,"i1",out c);Check(p.SelectedId=="i2","same-stack before earlier unrelated member");p.Abort();
        i=Make(b,"frag","frag");p=Prepare(i,"i1",out c);
        Check(p.SelectedId=="i0"&&!p.Holstered,"unselected member");p.Abort();p.Abort();
        Check(i.Items.Length==2,"abort preserves");
        Check(i.PrepareSourceDisposableProjection(c,"i1",c.Tick+10,out p),"same-command retry");
        Check(p.Validate(c),"retry validate");p.CommitValidated();Check(i.Items.Length==1&&i.SelectedInstanceId=="i0","retry exact member");
        i=Make(b,"zone");p=Prepare(i,"i0",out c);Check(p.Validate(c),"zone singleton validate");p.CommitValidated();
        Check(i.Items.Length==0&&i.SlotCount==0&&i.SelectedInstanceId==""&&i.Holstered,"singleton");
        i=Make(b,"frag","zone");p=Prepare(i,"i0",out c);Check(p.Validate(c),"expiry initial validate");
        Check(i.AdvanceConsumableReservations(At(c.Tick+10)),"advance");tick=Math.Max(tick,c.Tick+10);
        bool threw=false;try{p.CommitValidated();}catch(InvalidOperationException){threw=true;}Check(threw&&i.Items.Length==2,"expired retained token cannot commit");
        Check(!p.Validate(At(c.Tick+10)),"expiry exact edge");
        p.Abort();p=Prepare(i,"i0",out c);Check(p.Validate(c),"revocation initial");b.Allowed=false;
        Check(!p.Validate(At(c.Tick+1)),"revalidate revoked eligibility");threw=false;try{p.CommitValidated();}catch(InvalidOperationException){threw=true;}Check(threw&&i.Items.Length==2,"revocation refuses publication");p.Abort();b.Allowed=true;
        p=Prepare(i,"i0",out c);Check(!p.Validate(new CommandContext("s","a","x",c.Tick,2,true)),"epoch mismatch");
        Check(!p.Validate(new CommandContext("s","b","x",c.Tick,1,true)),"actor mismatch");
        Check(!p.Validate(new CommandContext("s","a","x",c.Tick,1,false)),"client mismatch");
        var binding=new CheckpointBinding("build","catalog","s","a",1,1,1,1);
        threw=false;try{InventoryCheckpointCodec.Capture(i,binding);}catch(System.IO.InvalidDataException){threw=true;}Check(threw,"capture pending refuses");
        p.Abort();var bytes=InventoryCheckpointCodec.Capture(i,binding);
        var restored=InventoryCheckpointCodec.Restore(bytes,Catalog(),b,binding);
        Check(restored.Items.Length==2&&restored.SelectedInstanceId==i.SelectedInstanceId,"schema1 field guard roundtrip");
        Inventory.PreparedSourceDisposableDepletion live;long rev=i.Revision;
        var refused=i.PrepareSourceDisposableDepletion(C(),"i0",out live);
        Check(!refused.Accepted&&live==null&&refused.Reason.Contains("missing-owner-contract"),"live missing mechanics fails closed");
        Check(i.Revision==rev&&i.Items.Length==2,"live refusal unchanged");
        Check(i.Apply(C(),InventoryOperation.Holster,null,null).Accepted,"refusal holds no lease");
        p=Prepare(i,"i0",out c);Check(p.Holstered,"already holstered preserved");p.Abort();
        Check(!i.PrepareSourceDisposableProjection(C(),"missing",long.MaxValue,out p),"invalid id expiry");
        Console.WriteLine("PASS "+checks+" inventory projection observations; mechanics/life/connection composition UNIMPLEMENTED");
    }
}
