using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Combat;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Persistence;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.Integration.CombatPublication;
using Handler=Ashfall.Modules.WeaponHandling.WeaponHandling;
namespace Ashfall.Integration.HostEpochAuthority
{
    public sealed class EpochBinding
    {
        public readonly string Session,Source,Content,Registry;
        public readonly long Epoch;
        public EpochBinding(string session,long epoch,string source,string content,string registry)
        {
            if(String.IsNullOrEmpty(session)||session.Length>128||epoch<0||!Hash(source)||!Hash(content)||!Hash(registry))throw new ArgumentException("epoch binding");
            Session=session;Epoch=epoch;Source=source;Content=content;Registry=registry;
        }
        static bool Hash(string s){if(s==null||s.Length!=64)return false;foreach(char c in s)if(!Uri.IsHexDigit(c))return false;return true;}
        internal string Key {get{return Session.Length+":"+Session+":"+Epoch+":"+Source+":"+Content+":"+Registry;}}
    }
    // Trusted root composition object. No arbitrary participant registration or gate getter.
    // This subset NEVER returns a global retirement token, including with zero owners.
    public sealed class OwnerRegistry : IDisposable
    {
        public const string ApiVersion="HostEpochHealthRegistrationR2/subset2";
        readonly EpochBinding binding;readonly ClosureJournal journal;
        readonly int thread;readonly object lane=new object();
        bool inside,closed,disposed,everOpened,closureFault,terminalClosed;long revision;
        HealthService.EpochClosure healthClosure;
        HealthParticipantKey healthKey;
        HealthEpochAccounting healthBeforeClose,healthAfterClose;
        HealthService health;CombatRules combat;Session session;
        HealthPublicationLane producer;
        readonly Dictionary<string,InventoryOwner> inventories=new Dictionary<string,InventoryOwner>(StringComparer.Ordinal);
        readonly Dictionary<string,WeaponOwner> weapons=new Dictionary<string,WeaponOwner>(StringComparer.Ordinal);
        readonly bool reopened;
        sealed class InventoryOwner {internal Inventory Value;internal ItemCatalog Catalog;internal string Actor;}
        sealed class WeaponOwner {internal Handler Value;internal string Actor,Provenance;}
        public EpochBinding Binding {get{return binding;}}
        public long Revision {get{return revision;}}
        public bool IngressClosed {get{return closed;}}
        public OwnerRegistry(EpochBinding binding,string tombstonePath,bool createNew)
        {
            if(binding==null||binding.Source!=SourceProfiles.Build)throw new ArgumentException("unsupported compiled source profile");this.binding=binding;thread=Thread.CurrentThread.ManagedThreadId;
            closed=true;journal=new ClosureJournal(tombstonePath,binding.Key,createNew);reopened=!createNew;terminalClosed=reopened;
        }
        void Lane(){if(disposed||!inside||Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("exclusive-host-lane");}
        // Scheduling only. The callback is never evidence of quiescence or retirement.
        public T Serialize<T>(Func<T> work)
        {
            if(work==null||Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("host-thread");
            lock(lane){if(inside||disposed)throw new InvalidOperationException("host-reentrance");inside=true;try{return health==null?work():health.Serialize(work);}finally{inside=false;}}
        }
        void Register(){Lane();if(!closed||everOpened||reopened||terminalClosed||revision==Int64.MaxValue)throw new InvalidOperationException("registry-frozen");}
        sealed class Gate : IAuthorityGate
        {
            readonly OwnerRegistry owner;readonly string actor;
            internal Gate(OwnerRegistry owner,string actor){this.owner=owner;this.actor=actor;}
            public RuleResult Validate(CommandContext c)
            {
                if(owner.disposed||owner.closed||!owner.inside||Thread.CurrentThread.ManagedThreadId!=owner.thread)return RuleResult.Reject("epoch-ingress-closed-or-off-lane");
                if(!c.IsAuthority||c.SessionId!=owner.binding.Session||c.AuthorityEpoch!=owner.binding.Epoch||String.IsNullOrEmpty(c.ActorId)||String.IsNullOrEmpty(c.CommandId)||c.Tick<0||actor!=null&&c.ActorId!=actor)return RuleResult.Reject("epoch-binding");
                return RuleResult.Accept();
            }
        }
        sealed class InventoryGate : IInventoryTransactions
        {
            readonly Gate gate;readonly IInventoryTransactions target;
            internal InventoryGate(Gate gate,IInventoryTransactions target){this.gate=gate;this.target=target;}
            public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance incoming,ItemInstance[] outgoing){var r=gate.Validate(c);return r.Accepted?target.Validate(c,op,incoming,outgoing):r;}
            public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=null;var r=gate.Validate(c);return r.Accepted?target.Stage(c,change,out receipt):r;}
        }
        public HealthService CreateHealth(int teams,bool respawn,IHealthWorld world,IInteractionQuery query)
        {HealthParticipantKey key;return CreateHealth(teams,respawn,world,query,out key);}
        public HealthService CreateHealth(int teams,bool respawn,IHealthWorld world,IInteractionQuery query,out HealthParticipantKey key)
        {Register();key=null;if(health!=null)throw new InvalidOperationException("duplicate health");var identity=new HealthParticipantKey();HealthService.EpochClosure closure;var h=HealthService.CreateEpochParticipant(binding.Session,binding.Epoch,teams,respawn,new Gate(this,null),world,query,out closure);healthClosure=closure;healthKey=identity;health=h;revision++;key=identity;return h;}
        // Key is a local exact-factory identity, never serialized authority or an adoption receipt.
        public HealthParticipantDisposition InspectHealthParticipant(HealthParticipantKey key)
        {Lane();if(key==null||!Object.ReferenceEquals(key,healthKey))throw new InvalidOperationException("health-participant-key");return new HealthParticipantDisposition(key,binding,revision,health.EpochAccounting,healthBeforeClose,healthAfterClose);}
        public CombatRules CreateCombat(ICombatEvidence evidence,IInteractionQuery query,int capacity)
        {Register();if(combat!=null)throw new InvalidOperationException("duplicate combat");combat=new CombatRules(binding.Session,binding.Epoch,256,capacity,new Gate(this,null),evidence,query);revision++;return combat;}
        public Inventory CreateInventory(string actor,ItemCatalog catalog,IInventoryTransactions boundary)
        {Register();if(String.IsNullOrEmpty(actor)||boundary==null||inventories.ContainsKey(actor)||inventories.Count>=64)throw new ArgumentException("inventory identity");var v=new Inventory(catalog,new InventoryGate(new Gate(this,actor),boundary),binding.Session,actor,binding.Epoch,false);inventories.Add(actor,new InventoryOwner{Value=v,Catalog=catalog,Actor=actor});revision++;return v;}
        public Handler CreateWeapon(string actor,string instance,string reserveProvenance,WeaponMechanism mechanism,int entitlement,int initialReserve)
        {Register();if(!inventories.ContainsKey(actor)||String.IsNullOrEmpty(instance)||String.IsNullOrEmpty(reserveProvenance)||weapons.ContainsKey(instance)||weapons.Count>=64)throw new ArgumentException("weapon identity");foreach(var old in weapons.Values)if(old.Provenance==reserveProvenance)throw new ArgumentException("reserve identity");var v=new Handler(binding.Session,actor,binding.Epoch,mechanism,new FiniteAmmoReserve(entitlement,initialReserve),new Gate(this,actor));weapons.Add(instance,new WeaponOwner{Value=v,Actor=actor,Provenance=reserveProvenance});revision++;return v;}
        public Session CreateSession(SessionRules rules,ISessionClock clock,IReconnectTokens tokens,ISpawnWorld world,ISessionManagement management)
        {Register();if(session!=null||binding.Epoch!=0)throw new InvalidOperationException("Session constructor supports epoch zero only; restore port absent");session=new Session(binding.Session,binding.Content,rules,new Gate(this,null),clock,tokens,world,management);revision++;return session;}
        public HealthPublicationLane CreateHealthProducer(AtomicSettingsStore store,IHealthWorld world,ICombatEvidence facts,IInteractionQuery query)
        {Lane();if(closed||reopened||health==null||producer!=null||revision==Int64.MaxValue)throw new InvalidOperationException("producer ownership");var bridge=new Ashfall.Integration.CombatHealthBridge(health,world,facts,new Gate(this,null),query,binding.Session,binding.Epoch);producer=new HealthPublicationLane(health,bridge,store,binding.Session,binding.Epoch,"host",65,binding.Source);revision++;return producer;}
        public RuleResult OpenFreshSubset()
        {Lane();if(reopened||everOpened||disposed||terminalClosed||!closed||health==null)return RuleResult.Reject("quarantined-or-incomplete");everOpened=true;closed=false;return RuleResult.Accept();}
        public RuleResult CloseIngress()
        {Lane();if(closureFault)return RuleResult.Reject("closure-io-recovery-required");if(terminalClosed)return RuleResult.Accept();closed=true;terminalClosed=true;if(healthClosure!=null){healthBeforeClose=health.EpochAccounting;healthAfterClose=healthClosure.Close();}try{journal.Close(revision);return RuleResult.Accept();}catch(IOException){closureFault=true;return RuleResult.Reject("closure-io-recovery-required");}catch(UnauthorizedAccessException){closureFault=true;return RuleResult.Reject("closure-io-recovery-required");}}
        public HealthEpochAccounting InspectHealthEpoch()
        {Lane();if(health==null)throw new InvalidOperationException("health-required");return health.EpochAccounting;}
        public byte[] CaptureClosedHealthCheckpoint(CommandContext host)
        {Lane();if(!terminalClosed||closureFault||healthClosure==null)throw new InvalidOperationException("closed-health-recovery-required");return healthClosure.CaptureClosedCheckpoint(host,binding.Source);}
        // Actual Health capture rejects active gain/advance/respawn reservations.
        // Actual InventoryRecovery checks executing/preparedBusy/FireReservation and fields.
        public byte[] CaptureInventory(string actor,CheckpointBinding checkpoint)
        {
            Lane();InventoryOwner owner;if(!inventories.TryGetValue(actor,out owner)||checkpoint==null||checkpoint.Session!=binding.Session||checkpoint.Epoch!=binding.Epoch||checkpoint.Owner!=actor||checkpoint.Build!=binding.Source)throw new InvalidOperationException("checkpoint-binding");
            var members=new List<WeaponCheckpointMember>();foreach(var pair in weapons)if(pair.Value.Actor==actor)members.Add(new WeaponCheckpointMember(pair.Key,pair.Value.Provenance,pair.Value.Value));
            var expected=new HashSet<string>(StringComparer.Ordinal);foreach(var item in owner.Value.Items)if(owner.Catalog.Get(item.DefinitionId).IsWeapon)expected.Add(item.InstanceId);
            if(expected.Count!=members.Count)throw new InvalidOperationException("registry-inventory-membership");foreach(var member in members)if(!expected.Contains(member.InstanceId))throw new InvalidOperationException("registry-inventory-membership");
            return ColdInventoryCheckpoint.Capture(checkpoint,owner.Catalog,owner.Value,members.ToArray());
        }
        public RuleResult InspectHealthPrefix(CommandContext host)
        {
            Lane();var publication=producer;if(health==null||publication==null)return RuleResult.Reject("actual-health-producer-required");
            // No adoption of caller's watermark. Receipt comes from actual producer.
            var receipt=publication.SuccessfulCheckpoint;
            if(receipt==null||publication.RecoveryRequired||receipt.SourceBinding!=binding.Source)return RuleResult.Reject("producer-checkpoint-required");
            try {
                byte[] bytes=health.CreateCheckpoint(host,binding.Source);
                using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m)) {
                    if(r.ReadInt32()!=2||r.ReadString()!=binding.Source||r.ReadString()!=binding.Session||r.ReadInt64()!=binding.Epoch)return RuleResult.Reject("health-schema");
                    r.ReadInt32();r.ReadBoolean();r.ReadDouble();r.ReadDouble();r.ReadDouble();long sequence=r.ReadInt64();
                    if(sequence!=receipt.HealthSequence||receipt.AcknowledgedThrough!=sequence||health.PendingTransitionCount!=0||publication.Offer(host).Length!=0)return RuleResult.Reject("health-unacknowledged-prefix");
                }
                // LOCAL producer prefix only, never a retirement receipt.
                return RuleResult.Accept();
            }catch(InvalidOperationException){return RuleResult.Reject("health-pending-or-closed");}catch(IOException){return RuleResult.Reject("health-checkpoint-io");}
        }
        public RuleResult PrepareGlobalRetirement()
        {Lane();return RuleResult.Reject("global-retirement-unsupported: Session/Combat/WeaponLifetimes/suspension/consumable/projectile/stamina/runtime reservation registries and common durable adoption absent");}
        public void Dispose(){if(disposed)return;if(Thread.CurrentThread.ManagedThreadId!=thread||inside)throw new InvalidOperationException("host-thread");Serialize(delegate{CloseIngress();return true;});closed=true;disposed=true;journal.Dispose();}
    }
}
