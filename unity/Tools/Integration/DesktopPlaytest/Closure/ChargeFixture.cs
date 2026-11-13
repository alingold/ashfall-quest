using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.InventoryItems;
// Deliberate local synthetic entitlement and geometry ports, actual owner services.
public sealed class ChargeFixture : IAuthorityGate, IBuildGrantAuthority, IConstructionWorld
{
    public RuleResult Validate(CommandContext c) { return c.IsAuthority && c.ActorId=="local" ? RuleResult.Accept() : RuleResult.Reject("local-only"); }
    public bool Validate(CommandContext c,string receipt,string kind,int amount) { return receipt=="local-seed" && kind=="source-initial999" && amount==999; }
    public bool HasStaticContact(Panel panel,float tolerance) { return true; }
    public bool IsBuried(Panel panel) { return false; }
    public string Clearance(Panel panel,string actor) { return ""; }
    public bool HasLineOfSight(string actor,Float3 origin,Float3 target) { return true; }
    static void Check(bool ok,string name) { if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name); }
    static CommandContext C(string id,long tick) { return new CommandContext("sandbox","local",id,tick,1,true); }
    public static int Main()
    {
        var ports=new ChargeFixture();var accounts=new BuildChargeAccounts("sandbox",1,ports,ports);var ledger=accounts.ForActor("local");
        BuildChargeLedger.PreparedCharge seed;
        Check(!ledger.PrepareGrant(C("negative",-1),"local-seed","source-initial999",999,10,out seed).Accepted,"negative seed rejected");
        Check(ledger.PrepareGrant(C("seed",0),"local-seed","source-initial999",999,100,out seed).Accepted && ledger.Balance==0 && seed.Validate(0),"real grant prepare pure");
        seed.CommitValidated();seed.CommitValidated();Check(ledger.Balance==999,"seed commits once");
        var service=new CandidateService("sandbox",1,ports,ports,false);
        CandidateService.PreparedChange panel;
        var pose=new BuilderState(new Float3(1,1,1),new Float3(0,-1,0),new Float3(1,1,1),true,true);
        Check(service.PreparePlace(new ConstructionCommand(C("place",1),1),pose,out panel).Accepted,"actual candidate prepare");
        BuildChargeLedger.PreparedCharge debit;
        Check(ledger.PrepareDebit(C("debit",1),panel.Cost,100,out debit).Accepted,"actual debit prepare");
        Check(service.Snapshot().Length==0 && ledger.Balance==999,"both preparations preserve state");
        Check(panel.Validate() && debit.Validate(1),"validate both on local serialized lane");
        int cost=panel.Cost;panel.CommitValidated();debit.CommitValidated();
        Check(service.Snapshot().Length>0 && ledger.Balance==999-cost,"actual panel and charge publication");
        panel.Abort();debit.Abort();Check(ledger.Balance==999-cost,"post-commit abort preserves charge");
        Check(!ledger.PrepareDebit(C("debit",1),cost,100,out debit).Accepted,"debit replay rejected");
        Check(service.PrepareAdvance(.02f,false,out panel).Accepted,"actual advance prepare");panel.Abort();Check(!panel.Validate(),"aborted candidate invalid");
        Console.WriteLine("PASS local fixture only; production authority/world approval absent");return 0;
    }
}
