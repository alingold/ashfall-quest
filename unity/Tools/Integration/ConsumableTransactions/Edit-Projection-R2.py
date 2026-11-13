from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall')
def edit(rel,old,new):
 p=R/rel;s=p.read_text();assert old in s,rel;p.write_text(s.replace(old,new))
P='unity/Assets/Ashfall/Modules/Pickups/PickupRules.cs'
edit(P,'if(action==PickupAction.Stow) { u.Stow();return Yes(); }', '''if(action==PickupAction.Stow) {
                var projection=transactions as IPreparedPickupSelection;
                if(projection!=null&&!projection.PublishSelection(c.ActorId,u.Held,null))return No("selection-rejected");
                u.Stow();return Yes(); }''')
edit(P,'u.Stow();u.Held=id;u.Phase=', '''var projection=transactions as IPreparedPickupSelection;
                if(projection!=null&&!projection.PublishSelection(c.ActorId,u.Held,id))return No("selection-rejected");
                u.Stow();u.Held=id;u.Phase=''')
B='unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs'
edit(B,'ISelectedPickupTransactions,IAtomicPickupEffects', 'ISelectedPickupTransactions,IAtomicPickupEffects,IPreparedPickupSelection')
edit(B,'public bool TryStageConsume(', '''public bool PublishSelection(string actor,string beforeHeld,string selected)
        {
            Inventory inventory;Inventory.PreparedConsumables token;
            if(!active||current.ActorId!=actor||pickups.HeldInstance(actor)!=beforeHeld||!inventories.TryGetValue(actor,out inventory)||!inventory.PrepareConsumableSelection(current,selected,out token))return false;
            try {if(!token.Validate())return false;token.CommitValidated();return true;}finally{token.Abort();}
        }
        public bool TryStageConsume(''')
edit(B,'members.Add(request.ItemId);stage=new Store(request,held,inventory,current,members.ToArray());return true;', '''members.Add(request.ItemId);Inventory.PreparedConsumables admission;
            if(!inventory.PrepareConsumableAdmission(current,new ItemInstance(request.ItemId,request.Kind,1),out admission))return false;
            try {stage=new Store(request,held,admission,members.ToArray());return true;}catch{admission.Abort();throw;}''')
start='''            private readonly Inventory inventory;private readonly CommandContext context;private readonly long revision;private bool done,aborted;
            internal Store(Transfer t,string held,Inventory i,CommandContext c,string[] members){inventory=i;context=c;revision=i.Revision;Receipt=new StoreReceipt(t,new EvictedInstance[0],new StackSelection(held,null,members));}
            public bool TryCommit(){if(done)return true;if(aborted||revision!=inventory.Revision)return false;done=inventory.Apply(context,InventoryOperation.Admit,new ItemInstance(Receipt.Incoming.ItemId,Receipt.Incoming.Kind,1),null).Accepted;return done;}
            public void Abort(){if(!done)aborted=true;}'''
edit(B,start,'''            private readonly Inventory.PreparedConsumables admission;private bool done,aborted;
            internal Store(Transfer t,string held,Inventory.PreparedConsumables prepared,string[] members){admission=prepared;Receipt=new StoreReceipt(t,new EvictedInstance[0],new StackSelection(held,null,members));}
            public bool TryCommit(){if(done)return true;if(aborted||!admission.Validate())return false;admission.CommitValidated();done=true;return true;}
            public void Abort(){if(!done){aborted=true;admission.Abort();}}''')
edit('unity/Assets/Ashfall/Modules/InventoryItems/PreparedRemoval.cs','token=null;ItemInstance item=Find(id);','token=null;if(!ConsumablePreparationAllowed(c))return false;ItemInstance item=Find(id);')
