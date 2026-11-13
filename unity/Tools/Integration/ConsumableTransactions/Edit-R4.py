from pathlib import Path
R=Path.cwd(); p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedConsumables.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('using System.Collections.Generic;','using System.Collections.Generic;\nusing System.Runtime.CompilerServices;')
s=s.replace('    public sealed partial class Inventory','''    // Ephemeral reservations are not durable Inventory state. The owner clock must
    // advance on the trusted host lane, including while input is paused.
    internal sealed class ConsumableReservation
    {
        internal long Tick=-1, Expires; internal object Active;
        internal static readonly ConditionalWeakTable<Inventory,ConsumableReservation> States=new ConditionalWeakTable<Inventory,ConsumableReservation>();
    }
    public sealed partial class Inventory''',1)
pos=s.index('        // Trusted host safety')
s=s[:pos]+'''        public bool AdvanceConsumableReservations(CommandContext host)
        {
            var state=ConsumableReservation.States.GetOrCreateValue(this);
            if(executing||!host.IsAuthority||host.SessionId!=session||host.ActorId!=actor||host.AuthorityEpoch!=epoch||host.Tick<0||host.Tick<state.Tick)return false;
            state.Tick=host.Tick;
            if(state.Active!=null&&state.Tick>=state.Expires){state.Active=null;preparedBusy=false;}
            return true;
        }
        private object ReserveConsumable(long tick)
        {
            var state=ConsumableReservation.States.GetOrCreateValue(this);
            if(tick<state.Tick||tick>long.MaxValue-120)return null;
            state.Tick=tick;state.Expires=tick+120;state.Active=new object();return state.Active;
        }
        private bool LiveConsumable(object lease)
        {var s=ConsumableReservation.States.GetOrCreateValue(this);return lease!=null&&Object.ReferenceEquals(s.Active,lease)&&s.Tick<s.Expires;}
        private void ReleaseConsumable(object lease)
        {var s=ConsumableReservation.States.GetOrCreateValue(this);if(Object.ReferenceEquals(s.Active,lease)){s.Active=null;preparedBusy=false;}}
''' +s[pos:]
s=s.replace('            foreach(var member in items)if(catalog.Get(member.DefinitionId).IsWeapon)return false;','            if(c.Tick>long.MaxValue-120||c.Tick<ConsumableReservation.States.GetOrCreateValue(this).Tick||revision==long.MaxValue)return false;')
s=s.replace('if(incoming==null&&selected!=null&&target==null)','if(incoming==null&&selected!=null&&(target==null||catalog.Get(target.DefinitionId).IsWeapon))')
s=s.replace('removed=Group(old).ToArray();','removed=Group(old).ToArray();foreach(var member in removed)if(catalog.Get(member.DefinitionId).IsWeapon)return false;')
s=s.replace('private readonly Inventory owner;private readonly CommandContext context;private readonly long revision;','private readonly Inventory owner;private readonly CommandContext context;private readonly long revision;private readonly object lease;')
s=s.replace('owner=o;context=c;revision=o.revision;','owner=o;context=c;revision=o.revision;lease=o.ReserveConsumable(c.Tick);')
s=s.replace('owner.revision==revision;}','owner.revision==revision&&owner.LiveConsumable(lease);}')
s=s.replace('committed=true;owner.preparedBusy=false;','committed=true;owner.ReleaseConsumable(lease);')
s=s.replace('aborted=true;owner.preparedBusy=false;}','aborted=true;owner.ReleaseConsumable(lease);}') # safety separate lacks lease; add safety lease
s=s.replace('private readonly string held;private bool','private readonly string held;private readonly object lease;private bool')
s=s.replace('owner=o;revision=r;held=id;','owner=o;revision=r;held=id;lease=o.ReserveConsumable(Math.Max(o.lastTick,ConsumableReservation.States.GetOrCreateValue(o).Tick)<0?0:Math.Max(o.lastTick,ConsumableReservation.States.GetOrCreateValue(o).Tick));')
s=s.replace('&&!owner.Holstered;}','&&!owner.Holstered&&owner.LiveConsumable(lease);}')
s=s.replace('owner.preparedBusy=false;done=true;','owner.ReleaseConsumable(lease);done=true;')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedRemoval.cs';s=p.read_text(encoding='utf-8-sig').replace('private readonly long revision;','private readonly object lease;private readonly long revision;').replace('owner=o;context=c;item=i;revision=rev;','owner=o;context=c;item=i;revision=rev;lease=o.ReserveConsumable(c.Tick);').replace('owner.Find(item.InstanceId)==item;','owner.Find(item.InstanceId)==item&&owner.LiveConsumable(lease);').replace('if(aborted||!owner.preparedBusy||owner.revision!=revision)','if(!Validate())').replace('done=true;owner.preparedBusy=false;','done=true;owner.ReleaseConsumable(lease);').replace('aborted=true;owner.preparedBusy=false;','aborted=true;owner.ReleaseConsumable(lease);');p.write_text(s)
