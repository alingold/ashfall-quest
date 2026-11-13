using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.InventoryItems;
namespace Ashfall.Integration.WorldWeaponTransfers
{
    public interface IWeaponHostClock { long Tick {get;} }
    // Root provisions from authenticated server state. Must validate canonical actor,
    // session/epoch, connection AND life generation, alive/connected, expiry and scope.
    public interface IWeaponPrincipal
    {
        string CanonicalActor {get;} long ConnectionLife {get;} long Life {get;}
        bool Validate(CommandContext context,long deadline);
    }
    public sealed class WeaponPlacement
    {
        public readonly string InstanceId,BodyId,SemanticOwner,LeaseId;
        public readonly long Generation;public readonly Float3 Position;
        public WeaponPlacement(string instance,string body,string semantic,string lease,long generation,Float3 position)
        {if(String.IsNullOrEmpty(instance)||String.IsNullOrEmpty(body)||String.IsNullOrEmpty(semantic)||String.IsNullOrEmpty(lease)||generation<0||!position.IsFinite)throw new ArgumentException();InstanceId=instance;BodyId=body;SemanticOwner=semantic;LeaseId=lease;Generation=generation;Position=position;}
    }
    // All members, exact records, and target principal must be bound by the provider.
    // Prepare/Validate/Abort mutate no gameplay. CommitValidated is preallocated,
    // nonthrowing and cannot fail under the same trusted root host lane.
    public interface IWeaponGeometryLease
    {
        WeaponPlacement[] Placements {get;}
        bool Validate();void CommitValidated();void Abort();
    }
    public interface IWeaponGeometry
    {
        bool Prepare(CommandContext c,IWeaponPrincipal principal,long deadline,ItemInstance[] drop,WorldWeaponRecord[] collect,out IWeaponGeometryLease lease);
    }
    public sealed class WorldWeaponRecord
    {
        public readonly ItemInstance Item;public readonly WeaponPlacement Placement;
        public readonly HandlingSnapshot Mechanics;public readonly int Reserve;
        public readonly string Session,Actor;public readonly long Epoch,ConnectionLife,Life,Deadline,Generation,ReserveRevision;
        internal WorldWeaponRecord(ItemInstance item,WeaponPlacement place,HandlingSnapshot mechanics,int reserve,string session,string actor,long epoch,long connection,long life,long deadline,long generation,long reserveRevision)
        {Item=item;Placement=place;Mechanics=mechanics;Reserve=reserve;Session=session;Actor=actor;Epoch=epoch;ConnectionLife=connection;Life=life;Deadline=deadline;Generation=generation;ReserveRevision=reserveRevision;}
        internal WorldWeaponRecord(ItemInstance item,WeaponPlacement place,HandlingSnapshot mechanics,int reserve,CommandContext c,IWeaponPrincipal p,long deadline,long generation,long reserveRevision)
        {Item=item;Placement=place;Mechanics=mechanics;Reserve=reserve;Session=c.SessionId;Actor=p.CanonicalActor;Epoch=c.AuthorityEpoch;ConnectionLife=p.ConnectionLife;Life=p.Life;Deadline=deadline;Generation=generation;ReserveRevision=reserveRevision;}
    }
    public sealed class WeaponWorldEvent
    {
        public readonly long Sequence;public readonly bool Dropped;readonly WorldWeaponRecord[] records;
        public WorldWeaponRecord[] Records{get{return (WorldWeaponRecord[])records.Clone();}}
        internal WeaponWorldEvent(long seq,bool dropped,WorldWeaponRecord[] values){Sequence=seq;Dropped=dropped;records=(WorldWeaponRecord[])values.Clone();}
    }
}
