using Ashfall.Core;
namespace Ashfall.Integration.ConsumableTransactions
{
    // Needed integration signature: existing ammo Refund/AwardSupply are immediate,
    // lack source-instance binding, and cannot establish prepare-all semantics.
    // This is an explicit future port, not an implemented reserve or test substitute.
    public interface IPreparedSupplyPort
    {
        bool Prepare(CommandContext context,string sourceInstance,string family,int requested,out IPreparedSupply receipt);
    }
    public interface IPreparedSupply
    {
        int AcceptedAmount { get; }
        bool Validate();
        void CommitValidated();
        void Abort();
    }
}
