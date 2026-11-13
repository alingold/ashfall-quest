using System;
using Ashfall.Modules.Avatar;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.AvatarPose
{
    public sealed class GripContact
    {
        public readonly string Role,Profile,SourceId;
        public GripContact(string role,string profile,string sourceId){if(role!="free"&&role!="firing"&&role!="support")throw new ArgumentException("role");Role=role;Profile=profile;SourceId=sourceId;}
        public static readonly GripContact Free=new GripContact("free","free","");
    }
    public interface IGripContactSource
    {
        // Actual gesture owner supplies reservation/climb and exact selected instance/hand binding.
        bool TryRead(bool left,out GripContact contact);
    }
    public static class InventoryGripContact
    {
        static readonly ItemCatalog catalog=SourceCatalog.Create();
        // Read-only snapshots, explicit source contact profile (from actual weapon scene path),
        // no mapping of every gun to rifle. Unknown/custom non-firearm IDs require descriptor.
        public static GripContact Read(InventorySnapshot inventory,HandlingSnapshot handling,string exactInstance,
            bool primary,bool reserved,string sourceContactProfile,string explicitMeleeId)
        {
            if(reserved||inventory==null||inventory.Holstered||inventory.Handoff||inventory.SelectedInstanceId!=exactInstance)return GripContact.Free;
            ItemDefinition definition=null;foreach(var item in inventory.Items)if(item.InstanceId==exactInstance)catalog.TryGet(item.DefinitionId,out definition);
            if(definition==null||!definition.Active||!definition.IsWeapon)throw new AvatarValidationException("Selected source catalog identity");
            if(!primary&&!handling.ClaimsSupport)return GripContact.Free;
            if(sourceContactProfile!="pistol"&&sourceContactProfile!="revolver"&&sourceContactProfile!="longgun"&&sourceContactProfile!="sword")throw new AvatarValidationException("Unbound source weapon profile");
            string id=definition.ProfileId;if(String.IsNullOrEmpty(id)){if(String.IsNullOrEmpty(explicitMeleeId))throw new AvatarValidationException("Missing source melee descriptor");id=explicitMeleeId;}
            return new GripContact(primary?"firing":"support",sourceContactProfile,id);
        }
    }
}
