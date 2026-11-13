using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23RoundState
    {
        readonly Dictionary<string,R24MeleeRuntime> knives=new Dictionary<string,R24MeleeRuntime>();
        public R24MeleeRuntime Melee{get{R24MeleeRuntime owner;return knives.TryGetValue(selectedAction,out owner)?owner:null;}}
        public R24KnifeRuntime Knife{get{return ReadKnifeInstance(selectedRifle=="knife"?selectedAction:"knife");}}
        public R24KnifeRuntime ReadKnifeInstance(string actionId){return ReadMeleeInstance(actionId) as R24KnifeRuntime;}
        public R24MeleeRuntime ReadMeleeInstance(string actionId){R24MeleeRuntime owner;return actionId!=null&&knives.TryGetValue(actionId,out owner)?owner:null;}
        public bool ApplyKnifeContact(string actionId,R24KnifeEntry entry){var owner=ReadMeleeInstance(actionId);if(retiredRound||owner==null||selectedAction!=actionId||R24MeleeProfiles.ForId(selectedRifle)==null||Health<=0)return false;return owner.ApplyContact(entry,delegate(string targetId,float amount){Target target;if(!practice.TryGetValue(targetId,out target))return false;var before=health.Read(targetId);if(before==null||before.Life!=Ashfall.Modules.HealthStates.LifeState.Alive)return false;var c=Command();var receipt=health.ApplyDamage(c,c.CommandId,targetId,before.Revision,Math.Min(before.Health,amount),0,false);if(!receipt.Result.Accepted)return false;target.Last=before.Health-receipt.Snapshot.Health;target.Total+=target.Last;target.Head=false;target.ResetLeft=1.5f;Hits++;health.DrainTransitions();return true;});}
    }
}
