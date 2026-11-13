using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Modules.InventoryItems;
using Ashfall.Integration.ConstructionRuntime;
namespace Ashfall.Integration.ConstructionTransactions
{
    public sealed class BuildChargePreparation:IBuildChargePreparation
    {
        readonly BuildChargeAccounts accounts;readonly IConstructionHostState host;
        public BuildChargePreparation(BuildChargeAccounts accounts,IConstructionHostState host){if(accounts==null||host==null)throw new ArgumentException("charge binding");this.accounts=accounts;this.host=host;}
        public RuleResult Prepare(CommandContext context,int cost,long expiry,out IPreparedBuildCharge prepared)
        {
            prepared=null;HostBuilderStamp stamp;if(!host.TryRead(context.ActorId,out stamp)||!Current(stamp,context)||host.Tick<context.Tick||host.Tick>expiry)return RuleResult.Reject("charge lifecycle");
            BuildChargeLedger.PreparedCharge charge;var r=accounts.ForActor(context.ActorId).PrepareDebit(context,cost,expiry,out charge);
            if(r.Accepted)prepared=new Charge(charge,host,stamp,context,accounts.ForActor(context.ActorId),cost);return r;
        }
        internal static bool Current(HostBuilderStamp s,CommandContext c){return s!=null&&s.Active&&s.Actor==c.ActorId&&s.Session==c.SessionId&&s.Epoch==c.AuthorityEpoch&&s.Life>0&&s.Connection>0;}
        internal static bool Same(HostBuilderStamp a,HostBuilderStamp b){return a!=null&&b!=null&&a.Active&&b.Active&&a.Session==b.Session&&a.Actor==b.Actor&&a.Epoch==b.Epoch&&a.AuthorityLease==b.AuthorityLease&&a.Life==b.Life&&a.Connection==b.Connection&&a.RootRevision==b.RootRevision&&a.PoseRevision==b.PoseRevision&&a.InputGeneration==b.InputGeneration;}
        internal sealed class Charge:IPreparedBuildCharge
        {
            readonly BuildChargeLedger accountsLedger;readonly int debit;readonly BuildChargeLedger.PreparedCharge charge;readonly IConstructionHostState host;readonly HostBuilderStamp stamp;readonly CommandContext context;
            internal Charge(BuildChargeLedger.PreparedCharge charge,IConstructionHostState host,HostBuilderStamp stamp,CommandContext context,BuildChargeLedger ledger,int cost){accountsLedger=ledger;debit=cost;this.charge=charge;this.host=host;this.stamp=stamp;this.context=context;}
            internal bool Binds(BuildChargeLedger ledger,CommandContext c,int cost){return Object.ReferenceEquals(accountsLedger,ledger)&&context.CommandId==c.CommandId&&context.ActorId==c.ActorId&&context.SessionId==c.SessionId&&context.AuthorityEpoch==c.AuthorityEpoch&&context.Tick==c.Tick&&debit==cost;}
            public bool Validate(){HostBuilderStamp now;return host.TryRead(context.ActorId,out now)&&Same(stamp,now)&&charge.Validate(host.Tick);}
            public void CommitValidated(){charge.CommitValidated();}public void Abort(){charge.Abort();}
        }
    }
    // Deliberate candidate adoption; root binds the legacy host solely as opaque ingress lease owner.
    // World is an exclusive NEW candidate binding, never the legacy host's mutable geometry.
    public sealed class PreparedConstruction:IStructurePreparation
    {
        public const string ApiVersion="PreparedConstruction/4";
        readonly CandidateService service;readonly RuntimeHost ingress;readonly AttestedWorld world;readonly IConstructionHostState clock;
        readonly BuildChargeAccounts accounts;readonly string session,instance;readonly long epoch;
        Structure pending;long serial;
        public PreparedConstruction(string session,long epoch,string instance,IAuthorityGate authority,RuntimeHost ingress,AttestedWorld world,IConstructionHostState clock,BuildChargeAccounts accounts,bool infiniteBuilds)
        {
            if(ingress==null||world==null||clock==null||accounts==null||String.IsNullOrEmpty(instance))throw new ArgumentException("prepared binding");
            this.session=session;this.epoch=epoch;this.instance=instance;this.ingress=ingress;this.world=world;this.clock=clock;this.accounts=accounts;
            world.Attach(session,epoch,instance);service=new CandidateService(session,epoch,authority,world,infiniteBuilds);
        }
        public long Revision {get{return service.Revision;}}public PlacedPanel[] Snapshot(){return service.Snapshot();}
        bool Begin(long expiry,out long generation)
        {generation=world.PreparedGeneration;return pending==null&&serial<long.MaxValue&&clock.Tick>=0&&clock.Tick<=expiry&&world.PrepareSnapshot(service.Snapshot())&&world.PreparedCurrent(generation);}
        RuleResult Reserve(CandidateService.PreparedChange change,string cause,CommandContext context,ConstructionLease lease,long expiry,long generation,out IPreparedStructure prepared)
        {
            prepared=null;HostBuilderStamp stamp=null;
            if(!String.IsNullOrEmpty(context.ActorId)&&(!clock.TryRead(context.ActorId,out stamp)||!BuildChargePreparation.Current(stamp,context)||(lease!=null&&!BuildChargePreparation.Same(lease.Stamp,stamp)))){change.Abort();return RuleResult.Reject("structure lifecycle");}
            BuildChargeLedger ledger=String.IsNullOrEmpty(context.ActorId)?null:accounts.ForActor(context.ActorId);
            int balance=ledger==null?0:ledger.Balance-change.Cost;
            if(balance<0){change.Abort();return RuleResult.Reject("build_charges");}
            var publication=new ConstructionPublication(serial+1,cause,context.ActorId,context.CommandId,world.Identity.PayloadSha256,balance,change.Panels,session,epoch,instance);
            pending=new Structure(this,change,publication,context,lease,expiry,generation,ledger,stamp);prepared=pending;return RuleResult.Accept();
        }
        public RuleResult PreparePlace(ConstructionCommand command,ConstructionLease lease,long expiry,out IPreparedStructure prepared)
        {
            prepared=null;BuilderState pose;long generation;
            if(command.Context.SessionId!=session||command.Context.AuthorityEpoch!=epoch||command.Context.Tick>clock.Tick||!ingress.TryReadPreparedLease(command.Context.ActorId,lease,out pose)||!Begin(expiry,out generation))return RuleResult.Reject("place binding");
            CandidateService.PreparedChange change;var r=service.PreparePlace(command,pose,out change);return r.Accepted?Reserve(change,"place",command.Context,lease,expiry,generation,out prepared):r;
        }
        public RuleResult PrepareDamage(CommandContext context,string body,float damage,long expectedRevision,long expiry,out IPreparedStructure prepared)
        {
            prepared=null;long generation;string piece=null;
            foreach(var p in service.Snapshot())if(BodyIdentity.Constructed(BodyIdentity.Part(BodyIdentity.ConstructedScope(session,epoch,instance))+BodyIdentity.Part(p.Id))==body)piece=p.Id;
            if(piece==null||context.Tick>clock.Tick||!Begin(expiry,out generation))return RuleResult.Reject("typed body/binding");
            CandidateService.PreparedChange change;var r=service.PrepareDamage(context,piece,damage,expectedRevision,out change);return r.Accepted?Reserve(change,"damage",context,null,expiry,generation,out prepared):r;
        }
        // Coarse service revision is deliberately conservative: ANY panel mutation stales damage.
        public bool TryReadBody(string body,out PlacedPanel panel,out long revision)
        {revision=service.Revision;panel=null;foreach(var p in service.Snapshot())if(Body(p.Id)==body){panel=p;return true;}return false;}
        public string Body(string piece){return BodyIdentity.Constructed(BodyIdentity.Part(BodyIdentity.ConstructedScope(session,epoch,instance))+BodyIdentity.Part(piece));}
        public RuleResult PrepareAdvance(float seconds,bool paused,long expiry,out IPreparedStructure prepared)
        {
            prepared=null;long generation;if(!Begin(expiry,out generation))return RuleResult.Reject("advance binding");
            CandidateService.PreparedChange change;var r=service.PrepareAdvance(seconds,paused,out change);
            // Explicit disposition: every accepted step publishes a snapshot, including pause/no-op.
            return r.Accepted?Reserve(change,"advance",new CommandContext(session,"","",clock.Tick,epoch,true),null,expiry,generation,out prepared):r;
        }
        sealed class Structure:IPreparedPlacement,IPreparedChargeBinding
        {
            readonly HostBuilderStamp stamp;readonly PlacedPanel[] boundState;readonly PreparedConstruction owner;readonly CandidateService.PreparedChange change;readonly ConstructionPublication publication;
            readonly CommandContext context;readonly ConstructionLease lease;readonly long expiry,generation,ledgerRevision;readonly BuildChargeLedger ledger;readonly long preparedAt;
            bool committed,aborted;
            internal Structure(PreparedConstruction owner,CandidateService.PreparedChange change,ConstructionPublication publication,CommandContext context,ConstructionLease lease,long expiry,long generation,BuildChargeLedger ledger,HostBuilderStamp stamp)
            {this.stamp=stamp;boundState=change.Panels;this.owner=owner;this.change=change;this.publication=publication;this.context=context;this.lease=lease;this.expiry=expiry;this.generation=generation;this.ledger=ledger;ledgerRevision=ledger==null?0:ledger.Revision;preparedAt=owner.clock.Tick;}
            public bool Accepts(IPreparedBuildCharge charge){if(lease==null)return charge==null;var actual=charge as BuildChargePreparation.Charge;return actual!=null&&actual.Binds(ledger,context,change.Cost);}
            public int Cost {get{return change.Cost;}}public ConstructionPublication Publication {get{return publication;}}
            bool Lifecycle(){HostBuilderStamp now;return stamp==null||owner.clock.TryRead(context.ActorId,out now)&&BuildChargePreparation.Same(stamp,now);}
            public bool Validate()
            {
                BuilderState pose;
                return !committed&&!aborted&&Lifecycle()&&Object.ReferenceEquals(owner.pending,this)&&owner.clock.Tick>=preparedAt&&owner.clock.Tick<=expiry&&owner.world.PreparedCurrent(generation)&&change.Validate()&&
                    (ledger==null||ledger.Revision==ledgerRevision)&&(lease==null||owner.ingress.TryReadPreparedLease(context.ActorId,lease,out pose))&&
                    (context.ActorId.Length==0||owner.service.Authorize(context));
            }
            public void CommitValidated(){if(committed||aborted)return;change.CommitValidated();owner.world.PublishPreparedPanels(boundState);owner.serial=publication.Serial;owner.pending=null;committed=true;}
            public void Abort(){if(committed||aborted)return;change.Abort();aborted=true;if(Object.ReferenceEquals(owner.pending,this))owner.pending=null;}
        }
    }
}
