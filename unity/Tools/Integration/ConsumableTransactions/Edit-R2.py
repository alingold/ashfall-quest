from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall')
def edit(rel,old,new):
 p=R/rel;s=p.read_text();assert old in s,rel;p.write_text(s.replace(old,new))
G='unity/Assets/Ashfall/Modules/HealthStates/PreparedGains.cs'
edit(G,'gains.Count>=4096||seen.Count', 'gains.Count>=4096||gainCommands.Count>=4096||seen.Count')
edit(G,'token=new PreparedGain(this,c,logical,p,health,shield);return true;', '''PreparedPublicationLease lease;
                if(!TryReservePublication(world.Now+5,out lease))return false;
                try { token=new PreparedGain(this,c,logical,p,health,shield,lease);return true; }
                catch { lease.Abort();throw; }''')
edit(G,'private readonly HealthService owner;private readonly CommandContext', 'private readonly PreparedPublicationLease lease;private readonly HealthService owner;private readonly CommandContext')
edit(G,'object target,float h,float s)', 'object target,float h,float s,PreparedPublicationLease reservation)')
edit(G,'{ owner=service;context=c;', '{ lease=reservation;owner=service;context=c;')
edit(G,'sequence=service.eventSequence;', 'sequence=reservation.BaseSequence;')
edit(G,'sequence+1,"gain"', 'reservation.NextSequence,"gain"')
edit(G,'return !aborted&&!committed&&owner.eventSequence', 'return !aborted&&!committed&&lease.Validate()&&owner.eventSequence')
edit(G,'owner.gains.Count<4096&&owner.seen.Count', 'owner.gains.Count<4096&&owner.gainCommands.Count<4096&&owner.seen.Count')
edit(G,'committed=true;return receipt;', 'committed=true;lease.CompleteValidated();return receipt;')
edit(G,'if(!committed)aborted=true;', 'if(!committed){aborted=true;lease.Abort();}')
P='unity/Assets/Ashfall/Modules/HealthStates/PreparedPublication.cs'
edit(P,'lease=null;double now=world.Now;', '''lease=null;double now=world.Now;
                // Reclaim only an expired, unpublished reservation. Old tokens remain closed.
                if(preparedPublication!=null&&Finite(now)&&now>=preparedPublication.ExpiresAt)preparedPublication.Abort();''')
edit(P,'private bool closed;', 'internal double ExpiresAt { get { return expires; } }\n            private bool closed;')
