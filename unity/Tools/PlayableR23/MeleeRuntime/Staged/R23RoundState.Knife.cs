using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23RoundState
    {
        readonly Dictionary<string,R24KnifeRuntime> knives=new Dictionary<string,R24KnifeRuntime>();
        public R24KnifeRuntime Knife{get{R24KnifeRuntime owner;return knives.TryGetValue(selectedRifle=="knife"?selectedAction:"knife",out owner)?owner:null;}}
        public R24KnifeRuntime ReadKnifeInstance(string actionId){R24KnifeRuntime owner;return actionId!=null&&knives.TryGetValue(actionId,out owner)?owner:null;}
        public bool ApplyKnifeContact(string actionId,R24KnifeEntry entry){var owner=ReadKnifeInstance(actionId);if(retiredRound||owner==null||selectedAction!=actionId||selectedRifle!="knife"||Health<=0)return false;return owner.ApplyContact(entry,delegate(string targetId,float amount){Target target;if(!practice.TryGetValue(targetId,out target))return false;var before=health.Read(targetId);if(before==null||before.Life!=Ashfall.Modules.HealthStates.LifeState.Alive)return false;var c=Command();var receipt=health.ApplyDamage(c,c.CommandId,targetId,before.Revision,Math.Min(before.Health,amount),0,false);if(!receipt.Result.Accepted)return false;target.Last=before.Health-receipt.Snapshot.Health;target.Total+=target.Last;target.Head=false;target.ResetLeft=1.5f;Hits++;health.DrainTransitions();return true;});}
    }
}
