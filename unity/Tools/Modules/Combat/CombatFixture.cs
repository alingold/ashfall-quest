using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Combat;
// Test-only trusted host fixture. No production fallback for missing capabilities.
class CombatFixture : ICombatApplication
{
    public string Instance="sword-A";
    public long Generation=1;
    public bool AutoCommit=true;
    public long Revision;
    public int Applied;
    public bool RejectApply=false;
    readonly Dictionary<string,WeaponLease> leases=new Dictionary<string,WeaponLease>();
    readonly Dictionary<string,HostHit> identities=new Dictionary<string,HostHit>();
    long serial;
    public RuleResult TryApply(DamageDecision d)
    {
        if(RejectApply||d.ExpectedRevision!=Revision)return RuleResult.Reject("revision");
        Applied++;Revision++;return RuleResult.Accept();
    }
    public CommandContext Host(CommandContext c,long tick)
    {return new CommandContext(c.SessionId,"host","publication",tick,c.AuthorityEpoch,true);}
    public DamageDecision Evaluate(CombatRules r,CommandContext c,string id,long tick,HitEvidence h)
    {
        if(tick<0||c.Tick<0||c.Tick>tick||tick-c.Tick>10||!c.IsAuthority||
            c.SessionId!="session"&&c.SessionId!="s"||c.AuthorityEpoch!=3&&c.AuthorityEpoch!=2)
            return r.Evaluate(c,id,tick);
        try
        {
            var source=h.Kind==DamageKind.Zone?DamageSource.HostEnvironment:
                h.Kind==DamageKind.Area?DamageSource.HostProjectile:DamageSource.CurrentActorIntent;
            string key=id+"|"+h.Kind+"|"+h.ContactEntry+"|"+h.TargetId+"|"+Instance+"|"+Generation;
            HostHit identity;
            if(!identities.TryGetValue(key,out identity))
            {
                WeaponLease lease=null;
                string weaponKey=Instance+"|"+Generation;
                if(source!=DamageSource.HostEnvironment&&!leases.TryGetValue(weaponKey,out lease))
                {lease=r.Equip(Host(c,tick),Instance,"a",h.WeaponId,Generation,tick);leases.Add(weaponKey,lease);}
                var ev=r.PublishEvent(Host(c,tick),++serial,tick+10,source,h.Kind,lease,tick);
                identity=r.PublishHit(Host(c,tick),ev,"0",h.TargetId,h.ContactEntry,tick);identities.Add(key,identity);
            }
            h.Identity=identity;h.HealthRevision=Revision;
            if(source!=DamageSource.CurrentActorIntent)c=new CommandContext(c.SessionId,"host",c.CommandId,c.Tick,c.AuthorityEpoch,c.IsAuthority);
            var d=r.Evaluate(c,id,tick);
            if(d.Accepted&&AutoCommit)
            {var committed=r.Commit(d,this,tick);if(!committed.Accepted)return DamageDecision.Reject(committed.Reason);}
            return d;
        }
        catch(InvalidOperationException e){return DamageDecision.Reject(e.Message);}
        catch(ArgumentException e){return DamageDecision.Reject(e.Message);}
    }
}

