using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.MatchDirector
{
    // One instance per HealthService/epoch. Host captures after the damage commit and
    // before publishing its Health transition. No attacker field is accepted from RPCs.
    public sealed partial class CommittedAttribution : IKnockdownAttribution
    {
        private readonly HealthService health;
        private readonly Dictionary<string,string> killers=new Dictionary<string,string>(StringComparer.Ordinal);
        public CommittedAttribution(HealthService health)
        { if(health==null)throw new ArgumentNullException("health");this.health=health; }
        private static string Key(string session,long epoch,string victim,long revision)
        { return session.Length+":"+session+":"+epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+victim.Length+":"+victim+":"+revision.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        public RuleResult Capture(CommandContext authenticatedDamageActor,string logicalId,string target)
        {
            DamageReceipt receipt=health.FindDamage(authenticatedDamageActor,logicalId,target);
            if(receipt==null||!receipt.Result.Accepted||receipt.Retryable||receipt.Snapshot==null)return RuleResult.Reject("uncommitted-damage");
            HealthSnapshot state=receipt.Snapshot;
            if(state.Life!=LifeState.Downed&&state.Life!=LifeState.Dead)return RuleResult.Accept();
            string key=Key(authenticatedDamageActor.SessionId,authenticatedDamageActor.AuthorityEpoch,state.Id,state.Revision),existing;
            if(killers.TryGetValue(key,out existing))return existing==authenticatedDamageActor.ActorId?RuleResult.Accept():RuleResult.Reject("attribution-conflict");
            if(killers.Count>=8192)return RuleResult.Reject("attribution-capacity");
            killers.Add(key,authenticatedDamageActor.ActorId);return RuleResult.Accept();
        }
        public string KillerFor(HealthTransition transition)
        {
            if(transition==null||transition.Kind!="damage"||transition.After==null)return null;
            string killer;return killers.TryGetValue(Key(transition.SessionId,transition.AuthorityEpoch,transition.After.Id,transition.After.Revision),out killer)?killer:null;
        }
        // Bound host capability + actual Bridge damage receipt, never a latest-attacker field.
        public RuleResult CapturePublication(CommandContext host,Ashfall.Modules.Combat.HostHit hit,Ashfall.Integration.CombatHealthBridge.Receipt publication)
        {
            if(hit==null||publication==null||publication.Damage==null||!publication.Result.Accepted||host.ActorId!="host"||
                hit.Event.SessionId!=host.SessionId||hit.Event.AuthorityEpoch!=host.AuthorityEpoch)return RuleResult.Reject("uncommitted-damage");
            string logical;
            using(var m=new System.IO.MemoryStream())using(var w=new System.IO.BinaryWriter(m,new System.Text.UTF8Encoding(false,true)))
            {
                foreach(string field in new[]{hit.Event.SessionId,hit.Event.AuthorityEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture),hit.Event.Serial.ToString(System.Globalization.CultureInfo.InvariantCulture),hit.SubhitId,hit.TargetId})
                {byte[] bytes=System.Text.Encoding.UTF8.GetBytes(field);w.Write(bytes.Length);w.Write(bytes);}
                w.Flush();using(var sha=System.Security.Cryptography.SHA256.Create())logical="combat/1:"+BitConverter.ToString(sha.ComputeHash(m.ToArray())).Replace("-","").ToLowerInvariant();
            }
            DamageReceipt receipt=health.FindDamage(host,logical,hit.TargetId);
            if(!Object.ReferenceEquals(receipt,publication.Damage)||!receipt.Result.Accepted||receipt.Retryable||receipt.Snapshot==null)return RuleResult.Reject("uncommitted-damage");
            var state=receipt.Snapshot;if(state.Life!=LifeState.Downed&&state.Life!=LifeState.Dead)return RuleResult.Accept();
            string key=Key(host.SessionId,host.AuthorityEpoch,state.Id,state.Revision),old;
            if(killers.TryGetValue(key,out old))return old==hit.Event.OwnerId?RuleResult.Accept():RuleResult.Reject("attribution-conflict");
            if(killers.Count>=8192)return RuleResult.Reject("attribution-capacity");
            killers.Add(key,hit.Event.OwnerId);return RuleResult.Accept();
        }
    }
}

