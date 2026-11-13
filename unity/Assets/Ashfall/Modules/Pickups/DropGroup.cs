using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Pickups
{
    // Source inventory.drop_slot stages the whole selected stack before any member
    // leaves custody. One command and one transaction avoid partial two-item drops.
    public interface IStagedPickupDropGroup
    {
        EvictedInstance[] Members {get;}
        bool TryCommit();
        void Abort();
    }
    public interface IPickupDropGroups
    {
        bool TryStageDropGroup(string actor,EvictedInstance[] members,out IStagedPickupDropGroup stage);
    }
    public sealed partial class PickupRules
    {
        public RuleResult DropGroup(CommandContext context,EvictedInstance[] members)
        {
            lock(sync)
            {
                if(quarantined)return No("quarantine");if(pendingSupply!=null)return No("supply-pending");
                if(revision==long.MaxValue)return No("revision-exhausted");if(executing)return No("reentry");
                executing=true;IStagedPickupDropGroup stage=null;
                try
                {
                    ActorFrame frame;UseState user;var begun=BeginCommand(context,out frame,out user);if(!begun.Accepted)return begun;
                    var port=transactions as IPickupDropGroups;
                    if(port==null||members==null||members.Length<1||members.Length>2||!frame.TransferSafe)return No("group-drop");
                    var frozen=(EvictedInstance[])members.Clone();var ids=new HashSet<string>(StringComparer.Ordinal);
                    foreach(var member in frozen)
                    {
                        Item item;if(member==null||!Id(member.ItemId)||!ids.Add(member.ItemId)||!items.TryGetValue(member.ItemId,out item)||item.Removed||item.Owner!=context.ActorId||!IsConsumable(item.Kind)||member.Kind!=item.Kind||member.SemanticOwnerId!=item.Semantic||!member.Position.IsFinite||Distance(frame.Head,member.Position)>2.2f||!query.HasLineOfSight(context.ActorId,item.Semantic,frame.Head,member.Position,2.2f))return No("group-drop-member");
                    }
                    if(!port.TryStageDropGroup(context.ActorId,(EvictedInstance[])frozen.Clone(),out stage)||stage==null)return No("group-drop-stage");
                    var receipt=stage.Members;if(receipt==null||receipt.Length!=frozen.Length)return No("group-drop-receipt");
                    for(int i=0;i<frozen.Length;i++)if(!Object.ReferenceEquals(receipt[i],frozen[i]))return No("group-drop-receipt");
                    if(!stage.TryCommit())return No("group-drop-refused");
                    foreach(var member in frozen){var item=items[member.ItemId];item.Owner=null;item.Position=member.Position;item.Reserved=null;item.Until=0;if(user.Held==item.Id)user.Stow();}
                    return Yes();
                }
                finally{if(stage!=null)stage.Abort();revision++;executing=false;}
            }
        }
    }
}
