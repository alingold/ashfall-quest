from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall')
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedConsumables.cs';s=p.read_text();pos=s.index('        // This bounded path')
s=s[:pos]+'''        // Trusted host safety cancellation, reserved before PickupRules reads actor state.
        // It never grants an action, spends a command, or calls gameplay eligibility.
        public bool PrepareSafetyHolster(string expectedHeld,out PreparedSafetyHolster token)
        {
            token=null;
            if(preparedBusy||executing||retired||String.IsNullOrEmpty(expectedHeld)||SelectedInstanceId!=expectedHeld||Holstered)return false;
            var item=Find(expectedHeld);if(item==null||catalog.Get(item.DefinitionId).IsWeapon)return false;
            token=new PreparedSafetyHolster(this,revision,expectedHeld);preparedBusy=true;return true;
        }
        public sealed class PreparedSafetyHolster
        {
            private readonly Inventory owner;private readonly long revision;private readonly string held;private bool done,aborted;
            internal PreparedSafetyHolster(Inventory o,long r,string id){owner=o;revision=r;held=id;}
            public bool Validate(){return !done&&!aborted&&owner.preparedBusy&&!owner.retired&&owner.revision==revision&&owner.SelectedInstanceId==held&&!owner.Holstered;}
            // Only the reserving host lane calls this after validate-all. No callbacks,
            // allocation, eligibility or other fallible operation after PickupRules stows.
            public void CommitValidated(){if(done)return;owner.Holstered=true;owner.revision++;owner.preparedBusy=false;done=true;}
            public void Abort(){if(done||aborted)return;aborted=true;owner.preparedBusy=false;}
        }
''' +s[pos:]
s=s.replace('afterItems.Add(incoming);afterKnown[incoming.InstanceId]=incoming;', 'selected=incoming.InstanceId;afterItems.Add(incoming);afterKnown[incoming.InstanceId]=incoming;')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs';s=p.read_text();s=s.replace('private PickupRules pickups;', '''private readonly Dictionary<string,Inventory.PreparedSafetyHolster> safety=new Dictionary<string,Inventory.PreparedSafetyHolster>(StringComparer.Ordinal);
        private bool PrepareSafety(string actor)
        {
            string held=pickups.HeldInstance(actor);if(held==null)return true;
            Inventory inventory;Inventory.PreparedSafetyHolster token;
            if(!inventories.TryGetValue(actor,out inventory)||!inventory.PrepareSafetyHolster(held,out token))return false;
            safety.Add(actor,token);return token.Validate();
        }
        private void ReleaseSafety(string actor)
        {Inventory.PreparedSafetyHolster token;if(safety.TryGetValue(actor,out token)){token.Abort();safety.Remove(actor);}}
        private void FinishSafety()
        {
            foreach(var pair in safety) {if(pickups.HeldInstance(pair.Key)==null)pair.Value.CommitValidated();else pair.Value.Abort();}
            safety.Clear();
        }
        private void AbortSafety(){foreach(var pair in safety)pair.Value.Abort();safety.Clear();}
        private PickupRules pickups;''')
s=s.replace('try{return pickups.Execute(c,action,item);}finally{active=false;}', 'try{if(!PrepareSafety(c.ActorId))return RuleResult.Reject("safety-reservation");var result=pickups.Execute(c,action,item);FinishSafety();return result;}finally{AbortSafety();active=false;}')
s=s.replace('try{pickups.Advance(c.Tick);}finally{active=false;}', 'try{foreach(var pair in inventories)if(!PrepareSafety(pair.Key))throw new InvalidOperationException("safety-reservation");pickups.Advance(c.Tick);FinishSafety();}finally{AbortSafety();active=false;}')
s=s.replace('Inventory inventory;Inventory.PreparedConsumables token;\n            if(!active', 'Inventory inventory;Inventory.PreparedConsumables token;\n            if(active&&current.ActorId==actor)ReleaseSafety(actor);\n            if(!active')
s=s.replace('stage=null;Inventory inventory;Locomotion locomotion;', 'stage=null;Inventory inventory;Locomotion locomotion;\n            if(active&&request!=null&&request.ActorId==current.ActorId)ReleaseSafety(request.ActorId);')
s=s.replace('if(kind!=request.Kind||!PickupRules.IsConsumable(kind))return false;', 'if(kind!=request.Kind||!PickupRules.IsConsumable(kind)||pickups.HeldInstance(request.ActorId)!=held||inventory.Holstered||inventory.SelectedInstanceId!=held)return false;')
s=s.replace('stage=null;Inventory inventory;\n            if(!active', 'stage=null;Inventory inventory;\n            if(active&&request!=null&&request.ActorId==current.ActorId)ReleaseSafety(request.ActorId);\n            if(!active')
s=s.replace('Inventory inventory;\n            if(!active||request==null', 'Inventory inventory;\n            if(active&&request!=null&&request.ActorId==current.ActorId)ReleaseSafety(request.ActorId);\n            if(!active||request==null')
p.write_text(s)
print('Safety reservation, canonical consume ownership, source incoming selection implemented')
