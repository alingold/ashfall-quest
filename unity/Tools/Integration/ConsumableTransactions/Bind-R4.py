from pathlib import Path
R=Path.cwd();p=R/'unity/Assets/Ashfall/Integration/InventoryTransfers/TransferBridge.cs';s=p.read_text(encoding='utf-8-sig');needle='        sealed class Actor'
s=s.replace(needle,'''        // Concrete same-assembly adapter to the real registry; no grants/restores.
        public static bool PrepareConsumableWeapons(SessionWeapons registry,Inventory inventory,out System.Collections.Generic.List<Ashfall.Modules.WeaponHandling.IPreparedWeaponSuspension> tokens)
        {
            tokens=null;if(registry==null||inventory==null)return false;
            var snapshot=inventory.Capture();var catalog=SourceCatalog.Create();
            foreach(var item in inventory.Items)if(catalog.Get(item.DefinitionId).IsWeapon&&!registry.IsActive(item,snapshot.ActorId,snapshot.Epoch))return false;
            if(!registry.PrepareWorldSuspensions(snapshot.ActorId,new HashSet<string>(),out tokens))return false;
            foreach(var token in tokens)if(!token.Validate().Accepted){foreach(var prior in tokens)prior.Abort();tokens=null;return false;}
            return true;
        }
        public static void PublishConsumableHolster(SessionWeapons registry,string actor)
        {registry.PublishWorldHolster(actor);}
''' +needle);p.write_text(s)
p=R/'unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('Mixed weapon inventories stay closed pending registry-owner composition.','Mixed inventories require the actual session registry; weapon eviction remains closed.')
s=s.replace('        public readonly HealthService Lane;','''        public readonly HealthService Lane;
        private readonly object weaponLane=new object();
        private SessionWeapons weapons;
        private T Serialize<T>(Func<T> action){return Lane.Serialize(delegate{lock(weaponLane){return action();}});}
        public bool BindWeapons(SessionWeapons registry)
        {return Serialize(delegate{if(active||weapons!=null||registry==null||!registry.BindPreparedWorldActionOwner(weaponLane,delegate(string actor){Inventory i;return inventories.TryGetValue(actor,out i)?i:null;}))return false;weapons=registry;return true;});}
        public RuleResult ApplyWeapon(CommandContext c,string id,HandlingAction action,HandlingInput input)
        {return Serialize(delegate{Inventory i;if(active||weapons==null||!inventories.TryGetValue(c.ActorId,out i)||!Lane.Authorize(c).Accepted)return RuleResult.Reject("weapon-binding");return weapons.Apply(i.Capture(),c,id,action,input);});}
        private bool PrepareWeapons(string actor,out List<IPreparedWeaponSuspension> tokens)
        {
            tokens=null;Inventory i;if(!inventories.TryGetValue(actor,out i))return false;
            bool mixed=false;var catalog=SourceCatalog.Create();foreach(var item in i.Items)if(catalog.Get(item.DefinitionId).IsWeapon)mixed=true;
            return !mixed||TransferBridge.PrepareConsumableWeapons(weapons,i,out tokens);
        }
''')
# use both health serialization and weapon action lane everywhere, preventing external Apply during composition
s=s.replace('return Lane.Serialize(delegate','return Serialize(delegate').replace('Lane.Serialize(delegate','Serialize(delegate')
# repair helper recursion
s=s.replace('private T Serialize<T>(Func<T> action){return Serialize(delegate','private T Serialize<T>(Func<T> action){return Lane.Serialize(delegate')
old='try{if(!PrepareSafety(c.ActorId))return RuleResult.Reject("safety-reservation");var result=pickups.Execute(c,action,item);FinishSafety();return result;}finally{AbortDrop();AbortSafety();active=false;}'
new='''List<IPreparedWeaponSuspension> tokens=null;try{
                Inventory inventory;if(!inventories.TryGetValue(c.ActorId,out inventory)||!inventory.AdvanceConsumableReservations(c)||!PrepareWeapons(c.ActorId,out tokens))return RuleResult.Reject("inventory-or-registry");
                if(!PrepareSafety(c.ActorId))return RuleResult.Reject("safety-reservation");
                var result=pickups.Execute(c,action,item);FinishSafety();
                if(result.Accepted&&action!=PickupAction.Reserve&&action!=PickupAction.Release&&tokens!=null){foreach(var token in tokens)token.CommitValidated();TransferBridge.PublishConsumableHolster(weapons,c.ActorId);}
                return result;
            }finally{if(tokens!=null)foreach(var token in tokens)token.Abort();AbortDrop();AbortSafety();active=false;}'''
assert old in s;s=s.replace(old,new)
# clock each bound inventory before safety, uses canonical host actor in generated envelope (not client identity)
s=s.replace('foreach(var pair in inventories)if(!PrepareSafety(pair.Key))','foreach(var pair in inventories)if(!pair.Value.AdvanceConsumableReservations(new CommandContext(c.SessionId,pair.Key,c.CommandId,c.Tick,c.AuthorityEpoch,c.IsAuthority))||!PrepareSafety(pair.Key))')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedConsumables.cs';s=p.read_text().replace('internal long Tick=-1','public ConsumableReservation() {}\n        internal long Tick=-1').replace('// This bounded path cannot suspend weapon mechanics. Mixed inventories reject\n        // until the actual weapon owner supplies a prepared selection/suspension port.','// Inventory projection only: mixed callers must reserve real weapon suspension\n        // on the same host lane. Weapon eviction is deliberately unsupported.')
p.write_text(s)
