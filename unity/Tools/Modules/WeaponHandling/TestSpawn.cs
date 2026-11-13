using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
public sealed class TestLifetimeApproval : IWeaponLifetimeAuthority
{
 public bool Allow=true;
 public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Allow?RuleResult.Accept():RuleResult.Reject("spawn denied");}
 public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Allow?RuleResult.Accept():RuleResult.Reject("transition denied");}
}
public static class TestSpawn
{
 public static WeaponHandling New(string s,string a,long e,WeaponMechanism m,IAmmoReserve r,IAuthorityGate g)
 { WeaponHandling w;var ledger=new WeaponLifetimes(s,new TestLifetimeApproval());if(!ledger.CreateNew(new CommandContext(s,a,"spawn",0,e,true),"test-instance",m,m==WeaponMechanism.ServiceRifle?24:3,r,g,out w).Accepted)throw new Exception("spawn");return w; }
}
