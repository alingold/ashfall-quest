using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.Integration.ConsumableTransactions;
sealed class ColdR4:IInventoryTransactions,IAuthorityGate {
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance i,ItemInstance[] o){return RuleResult.Accept();}
 sealed class Receipt:IInventoryReceipt {public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt r){r=new Receipt();return RuleResult.Accept();}
 static CommandContext C(string id,long tick){return new CommandContext("s","a",id,tick,1,true);}
 static void Check(bool b,string name){if(!b)throw new Exception(name);}
 static void Main(string[] args){var port=new ColdR4();var catalog=SourceCatalog.Create();var binding=new CheckpointBinding("R4-cold-fixture",ColdInventoryCheckpoint.CatalogFingerprint(catalog),"s","a",1,0,0,1);
 if(args[0]=="capture"){
  var inventory=new Inventory(catalog,port,"s","a",1,false);
  Check(inventory.Apply(C("admit-one",0),InventoryOperation.Admit,new ItemInstance("one","soda",1),null).Accepted,"admit one");
  Check(inventory.Apply(C("admit-two",0),InventoryOperation.Admit,new ItemInstance("two","soda",1),null).Accepted,"admit two");
  Inventory.PreparedRemoval token;Check(inventory.PrepareRemoval(C("remove-one",1),"one",out token)&&token.Validate(),"prepare removal");token.CommitValidated();token.Abort();
  var bytes=ConsumableInventoryCheckpoint.Capture(binding,inventory);File.WriteAllBytes(args[1],bytes);File.WriteAllText(args[1]+".trusted-fixture",ColdInventoryCheckpoint.Digest(bytes));Console.WriteLine("PASS capture actual schema1 Inventory segment; no Pickup/effect checkpoint");
 }else{
  var bytes=File.ReadAllBytes(args[1]);var receipt=new TrustedCheckpointReceipt(binding,File.ReadAllText(args[1]+".trusted-fixture"),1);QuarantinedInventory q;
  Check(ConsumableInventoryCheckpoint.TryRestore(bytes,receipt,port,port,out q),"cold restore");Check(q.Inventory.Items.Length==1&&q.Inventory.Items[0].InstanceId=="two"&&q.Inventory.SelectedInstanceId=="two","exact survivor");
  Check(!q.Inventory.Apply(C("remove-one",1),InventoryOperation.Holster,null,null).Accepted,"retained command replay");
  Check(!q.Inventory.Apply(C("conflict",2),InventoryOperation.Admit,new ItemInstance("one","shaker",1),null).Accepted,"known removed identity retained");
  var broken=(byte[])bytes.Clone();broken[broken.Length-1]^=1;Check(!ConsumableInventoryCheckpoint.TryRestore(broken,receipt,port,port,out q),"digest substitution rejection");
  var truncated=new byte[bytes.Length-1];Array.Copy(bytes,truncated,truncated.Length);Check(!ConsumableInventoryCheckpoint.TryRestore(truncated,receipt,port,port,out q),"truncation rejects");
  var wrong=new CheckpointBinding("other-build",binding.Catalog,"s","a",1,0,0,1);Check(!ConsumableInventoryCheckpoint.TryRestore(bytes,new TrustedCheckpointReceipt(wrong,receipt.Digest,1),port,port,out q),"wrong build rejects");
  Check(ConsumableInventoryCheckpoint.LiveAdoptionBlocker.Length>0,"live adoption gate remains open");Console.WriteLine("PASS cold subprocess actual Inventory replay/survivor/known identity/digest rejection; live custody unavailable");
 }
 }
}
