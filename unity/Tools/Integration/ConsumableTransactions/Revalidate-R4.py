from pathlib import Path
R=Path.cwd();p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedConsumables.cs';s=p.read_text().replace('if(preparedBusy||executing||retired||String.IsNullOrEmpty(expectedHeld)','if(preparedBusy||executing||retired||revision==long.MaxValue||String.IsNullOrEmpty(expectedHeld)')
s=s.replace('private readonly ItemInstance[] removed;','private readonly ItemInstance[] removed;private readonly ItemInstance incoming;private readonly InventoryOperation operation;private readonly ItemInstance[] boundaryMembers;')
s=s.replace('removed=(ItemInstance[])evicted.Clone();owner=o;', '''removed=(ItemInstance[])evicted.Clone();this.incoming=incoming;operation=incoming!=null?(removed.Length>0?InventoryOperation.ReplaceSlot:InventoryOperation.Admit):(selection==null?InventoryOperation.Holster:InventoryOperation.Equip);
                boundaryMembers=incoming!=null?removed:(selection==null?new ItemInstance[0]:new ItemInstance[]{o.Find(selection)});owner=o;''')
s=s.replace('owner.revision==revision&&owner.LiveConsumable(lease);}','owner.revision==revision&&owner.LiveConsumable(lease)&&owner.boundary.Validate(context,operation,incoming,(ItemInstance[])boundaryMembers.Clone()).Accepted;}')
p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/InventoryItems/PreparedRemoval.cs';s=p.read_text().replace('owner.LiveConsumable(lease);','owner.LiveConsumable(lease)&&owner.boundary.Validate(context,InventoryOperation.Remove,null,new ItemInstance[]{item}).Accepted;');p.write_text(s)
