from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall')
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedRemoval.cs';s=p.read_text().replace('''        {
            token=null;if(!ConsumablePreparationAllowed(c))''','''        {return PrepareRemoval(c,id,false,out token);}
        public bool PrepareDropRemoval(CommandContext c,string id,out PreparedRemoval token)
        {return PrepareRemoval(c,id,true,out token);}
        private bool PrepareRemoval(CommandContext c,string id,bool drop,out PreparedRemoval token)
        {
            token=null;if(!ConsumablePreparationAllowed(c))''',1)
s=s.replace('new PreparedRemoval(this,c,item,members,next,revision)', 'new PreparedRemoval(this,c,item,members,next,revision,drop)')
s=s.replace('private readonly long revision;private bool done,aborted;', 'private readonly long revision;private readonly bool holstered;private bool done,aborted;')
s=s.replace('string next,long rev)', 'string next,long rev,bool drop)')
s=s.replace('Selected=next;members=', 'Selected=drop&&o.SelectedInstanceId!=i.InstanceId?o.SelectedInstanceId:next;holstered=drop?(o.SelectedInstanceId==i.InstanceId||o.Holstered):Selected==null;members=')
s=s.replace('owner.Holstered=Selected==null;', 'owner.Holstered=holstered;')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs';s=p.read_text();s=s.replace('private IDropPlanner dropPlanner;', '''private IDropPlanner dropPlanner;private IDropBatch pendingDrop;private EvictedInstance pendingPlacement;
        private void AbortDrop(){if(pendingDrop!=null)pendingDrop.Abort();pendingDrop=null;pendingPlacement=null;}''')
s=s.replace('finally{AbortSafety();active=false;}', 'finally{AbortDrop();AbortSafety();active=false;}')
s=s.replace('if(request.Operation==TransferKind.Supply)', '''if(request.Operation==TransferKind.Drop) {
                if(pendingDrop==null||pendingPlacement==null||pendingPlacement.ItemId!=request.ItemId||pendingPlacement.Kind!=request.Kind||pendingPlacement.Position.X!=request.Position.X||pendingPlacement.Position.Y!=request.Position.Y||pendingPlacement.Position.Z!=request.Position.Z)return AtomicOutcome.Failed;
                Inventory.PreparedRemoval removal;if(!inventory.PrepareDropRemoval(current,request.ItemId,out removal))return AtomicOutcome.Failed;
                try{if(!removal.Validate()||!pendingDrop.StillValid||!pendingDrop.Commit())return AtomicOutcome.Failed;removal.CommitValidated();return AtomicOutcome.Applied;}finally{removal.Abort();}
            }
            if(request.Operation==TransferKind.Supply)''')
s=s.replace('public bool TryPlaceDrop(string actor,string item,out Float3 position){position=new Float3();return false;}', '''public bool TryPlaceDrop(string actor,string item,out Float3 position)
        {
            position=new Float3();Inventory inventory;
            if(!active||actor!=current.ActorId||dropPlanner==null||pendingDrop!=null||!inventories.TryGetValue(actor,out inventory))return false;
            ItemInstance member=Array.Find(inventory.Items,delegate(ItemInstance m){return m.InstanceId==item&&PickupRules.IsConsumable(m.DefinitionId);});if(member==null)return false;
            IDropBatch batch=null;
            try{if(!dropPlanner.Prepare(current,new ItemInstance[]{member},out batch)||batch==null)return false;
                var placements=batch.Placements;if(placements==null||placements.Length!=1||placements[0]==null||placements[0].ItemId!=item||placements[0].Kind!=member.DefinitionId||!placements[0].Position.IsFinite)return false;
                pendingPlacement=placements[0];pendingDrop=batch;batch=null;position=pendingPlacement.Position;return true;
            }finally{if(batch!=null)batch.Abort();}
        }''')
s=s.replace('// Supply/weapon drop ports deliberately remain closed; no synthetic backing state.', '// External geometry and canonical reserve bindings are trusted host-only ports.\n    // Mixed weapon inventories stay closed pending registry-owner composition.')
p.write_text(s)
