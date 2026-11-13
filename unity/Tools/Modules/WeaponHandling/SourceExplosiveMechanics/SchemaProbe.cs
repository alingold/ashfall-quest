using System;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.WeaponHandling;
class SchemaProbe
{
 sealed class Host:IAuthorityGate,IWeaponLifetimeAuthority
 {
  public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return RuleResult.Accept();}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return RuleResult.Accept();}
 }
 static void Main()
 {
  var host=new Host();var ledger=new WeaponLifetimes("s",host);WeaponHandling weapon;
  if(!ledger.CreateNew(new CommandContext("s","a","grant",1,1,true),"rifle",WeaponMechanism.ServiceRifle,4,new FiniteAmmoReserve(8,8),host,out weapon).Accepted)throw new Exception();
  var b=new CheckpointBinding("build","catalog","s","a",1,1,1,1);
  Console.WriteLine(RecoveryWire.Hash(WeaponCheckpointCodec.Capture(weapon,b,"reserve")));
 }
}
