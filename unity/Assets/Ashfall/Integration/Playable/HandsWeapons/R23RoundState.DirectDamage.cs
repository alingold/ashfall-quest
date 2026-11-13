using System;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23RoundState
    {
        // net_session1000.hurt(direct=true,cause="zone"): the authoritative
        // zone has already checked stage/loading/life/spawn protection. Use the
        // same actual Health owner while bypassing shields and weapon scaling.
        public bool ApplyDirectPlayerDamage(float amount)
        {
            if(playerHealthView!=null)return playerHealthView.ApplyDamage(amount,true,"zone");
            if(localMatchHealth)return ApplyLocalMatchDamage(amount,true,"zone");
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||health==null||Health<=0)return false;
            var before=health.Read("player");var command=Command();
            var receipt=health.ApplyDamage(command,command.CommandId,"player",before.Revision,Math.Min(before.Health,amount),0,false);
            health.DrainTransitions();return receipt.Result.Accepted&&receipt.Snapshot.Health<before.Health;
        }
    }
}
