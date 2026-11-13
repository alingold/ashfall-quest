using System;
using System.IO;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Handler=Ashfall.Modules.WeaponHandling.WeaponHandling;
class ReservationTests
{
 sealed class Gate:IAuthorityGate,IWeaponLifetimeAuthority,IWeaponFireBinding
 {
  public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string a,long e){return Validate(c);}
  public RuleResult Validate(CommandContext c,string id,long e,long l,long g,long f){return Validate(c);}
 }
 sealed class Boundary:IInventoryBoundary{public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] a){return RuleResult.Accept();}}
 static readonly Gate gate=new Gate();static readonly ItemCatalog cat=SourceCatalog.Create();
 static CommandContext C(string id,long tick){return new CommandContext("s","a",id,tick,0,true);}
 static HandlingInput Input(long n){return new HandlingInput{Sequence=n,Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=true,DeltaSeconds=.25f};}
 static void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static byte[] Capture(Handler w){return ColdInventoryCheckpoint.Capture(new CheckpointBinding("build",ColdInventoryCheckpoint.CatalogFingerprint(cat),"s","a",0,0,0,1),cat,new Inventory(cat,new Boundary(),"s","a",0,false),new[]{new WeaponCheckpointMember("w","entitlement",w)});}
 static void Busy(Handler w,string s){bool rejected=false;try{Capture(w);}catch(InvalidDataException){rejected=true;}Check(rejected,s);}
 static void Main()
 {
  for(int order=0;order<2;order++){
   var ledger=new WeaponLifetimes("s",gate);Handler w;Check(ledger.CreateNew(C("grant",0),"w",WeaponMechanism.ServiceRifle,2,new FiniteAmmoReserve(10,5),gate,out w).Accepted,"grant");
   IPreparedWeaponSuspension suspension;IPreparedWeaponTransfer transfer;IPreparedWeaponFire fire;
   Check(w.PrepareSuspension(out suspension).Accepted,"suspension prepare");Busy(w,"suspension prevalidation capture refused");Check(suspension.Validate().Accepted,"suspension validate");Busy(w,"validated suspension capture refused");
   IPreparedWeaponSuspension duplicateSuspension;Check(!w.PrepareSuspension(out duplicateSuspension).Accepted,"suspension excludes suspension");
   Check(!ledger.PrepareTransfer(C("transfer",1),"w","b",1,out transfer).Accepted,"suspension excludes transfer");Check(!ledger.PrepareFire(C("shot",1),"w",Input(1),0,0,0,3,gate,out fire).Accepted,"suspension excludes fire");
   suspension.Abort();suspension.Abort();Check(Capture(w).Length>0,"abort releases capture");
   Check(w.PrepareSuspension(out suspension).Accepted&&suspension.Validate().Accepted,"suspension commit fixture");suspension.CommitValidated();suspension.CommitValidated();suspension.Abort();Check(Capture(w).Length>0,"suspension consumption releases capture");
   // The committed suspension disarms the trigger; use a fresh owner for fire.
   ledger=new WeaponLifetimes("s",gate);Check(ledger.CreateNew(C("grant",0),"w",WeaponMechanism.ServiceRifle,2,new FiniteAmmoReserve(10,5),gate,out w).Accepted,"fresh owner after suspension");
   Check(ledger.PrepareTransfer(C("abort-transfer",1),"w","b",1,out transfer).Accepted&&transfer.Validate().Accepted,"transfer abort fixture");transfer.Abort();transfer.Abort();transfer.CommitValidated();Check(transfer.Result==null&&Capture(w).Length>0,"transfer abort releases capture without publication");
   if(order==0){
    Check(ledger.PrepareFire(C("shot",1),"w",Input(1),0,0,0,3,gate,out fire).Accepted,"fire first prepare");Check(fire.Validate(1).Accepted,"fire first validate");Busy(w,"validated fire capture refused");
    Check(!ledger.PrepareTransfer(C("transfer",1),"w","b",1,out transfer).Accepted,"validated fire excludes transfer");Check(!w.PrepareSuspension(out suspension).Accepted,"validated fire excludes suspension");
    WeaponTransfer old;Check(!ledger.Capture(C("capture",1),"w",out old).Accepted,"fire excludes legacy retirement");
    Check(!fire.Validate(4).Accepted,"expired validation rejects");Check(Capture(w).Length>0,"expiry releases capture");fire.CommitValidated();Check(w.Snapshot.Loaded==2&&fire.Result==null,"expired token cannot publish");
    Check(ledger.PrepareFire(C("shot",4),"w",Input(1),0,0,0,6,gate,out fire).Accepted,"retry after expiry");Check(fire.Validate(4).Accepted,"retry validates");fire.CommitValidated();var receipt=fire.Result;fire.CommitValidated();fire.Abort();Check(receipt!=null&&Object.ReferenceEquals(receipt,fire.Result)&&w.Snapshot.Loaded==1&&w.Snapshot.Shots==1,"fire consumption once");Check(Capture(w).Length>0,"fire commit releases capture");
   }else{
    Check(ledger.PrepareTransfer(C("transfer",1),"w","b",1,out transfer).Accepted,"transfer first prepare");Busy(w,"transfer prevalidation capture refused");Check(transfer.Validate().Accepted,"transfer first validate");Busy(w,"validated transfer capture refused");
    IPreparedWeaponTransfer duplicateTransfer;Check(!ledger.PrepareTransfer(C("other-transfer",1),"w","c",1,out duplicateTransfer).Accepted,"transfer excludes transfer");
    Check(!ledger.PrepareFire(C("shot",1),"w",Input(1),0,0,0,3,gate,out fire).Accepted,"validated transfer excludes fire");Check(!w.PrepareSuspension(out suspension).Accepted,"validated transfer excludes suspension");
    transfer.CommitValidated();var result=transfer.Result;transfer.CommitValidated();transfer.Abort();Check(result!=null&&Object.ReferenceEquals(result,transfer.Result)&&result.Handler.Snapshot.Loaded==2,"transfer once retains both rounds without shot");Check(Capture(w).Length>0,"transfer commit releases old capture");
    Check(!w.Apply(C("obsolete",2),HandlingAction.Fire,Input(1)).Accepted,"old owner remains retired");
   }
  }
 }
}
