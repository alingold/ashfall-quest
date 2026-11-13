from pathlib import Path
p=Path('unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs')
s=p.read_text()
s=s.replace('using Ashfall.Core;','using Ashfall.Core;\nusing Ashfall.Modules.Sessions;\nusing Ashfall.RecoveryContracts;')
s=s.replace('private SessionWeapons weapons;', '''private SessionWeapons weapons;
        private Session sessionBinding;
        private readonly HashSet<object> outstanding=new HashSet<object>();
        private readonly Dictionary<string,Lifetime> lifetimes=new Dictionary<string,Lifetime>(StringComparer.Ordinal);
        public bool BindSession(Session session)
        {return Serialize(delegate{if(active||sessionBinding!=null||session==null||lifetimes.Count!=0)return false;sessionBinding=session;return true;});}
        private sealed class Lifetime
        {
            internal ConsumableBridge Bridge;internal string Actor,Session;internal long Epoch,Life,Connection;internal object Identity;
            internal bool Valid(){var h=Bridge.Lane.Read(Actor);var s=Bridge.sessionBinding;return h!=null&&h.Connected&&h.Life==LifeState.Alive&&h.LifeGeneration==Life&&h.ConnectionGeneration==Connection&&(s==null||(s.Id==Session&&s.Epoch==Epoch&&Identity!=null&&Object.ReferenceEquals(Identity,s.GetConnectionIdentity(Actor))));}
        }
        private Lifetime CaptureLifetime(string actor)
        {var h=Lane.Read(actor);if(h==null||!h.Connected||h.Life!=LifeState.Alive)return null;var s=sessionBinding;var token=new Lifetime{Bridge=this,Actor=actor,Session=current.SessionId,Epoch=current.AuthorityEpoch,Life=h.LifeGeneration,Connection=h.ConnectionGeneration,Identity=s==null?null:s.GetConnectionIdentity(actor)};return token.Valid()?token:null;}
        private bool CurrentLifetime(string actor){Lifetime token;return lifetimes.TryGetValue(actor,out token)&&token.Valid();}
        public byte[] CapturePickup(CheckpointBinding binding)
        {return Serialize(delegate{if(active||outstanding.Count!=0||safety.Count!=0||pendingDrop!=null||pickups==null)throw new InvalidOperationException("Outstanding consumable preparation");return PickupCheckpointCodec.Capture(pickups,binding);});}
''')
s=s.replace('Inventory inventory;if(!inventories.TryGetValue(c.ActorId,out inventory)', '''Lifetime previous;var lifetime=CaptureLifetime(c.ActorId);if(lifetime==null)return RuleResult.Reject("connection-life");
                bool changed=lifetimes.TryGetValue(c.ActorId,out previous)&&!previous.Valid();lifetimes[c.ActorId]=lifetime;
                Inventory inventory;if(!inventories.TryGetValue(c.ActorId,out inventory)''')
s=s.replace('var result=pickups.Execute(c,action,item);FinishSafety();','var result=pickups.Execute(c,changed?PickupAction.Stow:action,changed?null:item);FinishSafety();if(changed)return RuleResult.Reject("connection-life-changed-preparation-reset");')
s=s.replace('try {if(!token.Validate())return false;', 'try {if(!token.Validate()||!CurrentLifetime(actor))return false;')
s=s.replace('stage=new Consumption(request,held,removal,gain,locomotion);return true;', 'var lifetime=CaptureLifetime(request.ActorId);if(lifetime==null){removal.Abort();gain.Abort();return false;}stage=new Consumption(this,lifetime,request,held,removal,gain,locomotion);outstanding.Add(stage);return true;')
s=s.replace('internal Consumption(Transfer t,', 'private readonly ConsumableBridge bridge;private readonly Lifetime lifetime;\n            internal Consumption(ConsumableBridge owner,Lifetime identity,Transfer t,')
s=s.replace('{removal=r;gain=g;locomotion=l;', '{bridge=owner;lifetime=identity;removal=r;gain=g;locomotion=l;')
s=s.replace('if(aborted||!removal.Validate()||!gain.Validate())', 'if(aborted||!bridge.active||!removal.Validate()||!gain.Validate()||!lifetime.Valid())')
s=s.replace('committed=true;result=AtomicOutcome.Applied;', 'committed=true;bridge.outstanding.Remove(this);result=AtomicOutcome.Applied;')
s=s.replace('aborted=true;gain.Abort();removal.Abort();', 'aborted=true;gain.Abort();removal.Abort();bridge.outstanding.Remove(this);')
s=s.replace('stage=new Store(request,held,admission,members.ToArray(),batch,placements);', 'var lifetime=CaptureLifetime(request.ActorId);if(lifetime==null)return false;stage=new Store(this,lifetime,request,held,admission,members.ToArray(),batch,placements);outstanding.Add(stage);')
s=s.replace('internal Store(Transfer t,', 'private readonly ConsumableBridge bridge;private readonly Lifetime lifetime;\n            internal Store(ConsumableBridge owner,Lifetime identity,Transfer t,')
s=s.replace('{admission=prepared;', '{bridge=owner;lifetime=identity;admission=prepared;')
s=s.replace('if(aborted||!admission.Validate()||(batch!=null&&!batch.StillValid))', 'if(aborted||!bridge.active||!admission.Validate()||(batch!=null&&!batch.StillValid)||!lifetime.Valid())')
s=s.replace('admission.CommitValidated();done=true;', 'admission.CommitValidated();done=true;bridge.outstanding.Remove(this);')
s=s.replace('if(batch!=null)batch.Abort();}}', 'if(batch!=null)batch.Abort();bridge.outstanding.Remove(this);}}')
s=s.replace('if(!active||request==null||request.ActorId!=current.ActorId||!inventories', 'if(!active||request==null||!CurrentLifetime(request.ActorId)||request.ActorId!=current.ActorId||!inventories')
s=s.replace('!pendingDrop.StillValid||!pendingDrop.Commit()', '!pendingDrop.StillValid||!CurrentLifetime(request.ActorId)||!pendingDrop.Commit()')
s=s.replace('if(!charge.Validate(current.Tick))', 'if(!charge.Validate(current.Tick)||!CurrentLifetime(request.ActorId))')
s=s.replace('if(!active)return ShieldProcessing.Failed;', 'if(!active)return ShieldProcessing.Failed;var lifetime=CaptureLifetime(actor);if(lifetime==null)return ShieldProcessing.Failed;')
s=s.replace('if(!gain.Validate())return ShieldProcessing.Failed;', 'if(!gain.Validate()||!lifetime.Valid())return ShieldProcessing.Failed;')
s=s.replace('pendingPlacement=placements[0];', '''CanonicalPickupSource source;if(!pickups.TryReadCanonicalSource(item,out source)||source.Removed||source.Owner!=actor||source.Kind!=placements[0].Kind||source.SemanticOwnerId!=placements[0].SemanticOwnerId)return false;
                pendingPlacement=placements[0];''')
p.write_text(s)
p=Path('unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableInventoryCheckpoint.cs');s=p.read_text();s=s.replace('Missing owner Pickups checkpoint: items/semantic/amount/reservation/custody/tombstones, use state/soda clock, command replay; common durable Health/stamina/registry sequence and canonical reconnect adoption required','Inventory-only quarantine; compose actual PickupCustody/schema1 at common durable Health/stamina/build/reserve/registry sequence with ACK prefix and root canonical reconnect adoption');p.write_text(s)
