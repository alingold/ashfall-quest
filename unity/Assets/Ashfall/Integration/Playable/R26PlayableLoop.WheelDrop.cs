#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        R24LanLobby wheelDropLobby;long wheelDropEpoch;int wheelDropKind;
        // WHEELDROP2 (r15): a held A drops a gun, food (shield, shaker, banana) or grenade alike, as Godot weapon_rack.gd.
        bool WheelItemDroppable(string id){return Gameplay.OwnsWeapon(id)||OwnsConsumable(id)||OwnsExplosive(id);}
        // Consumable input is disabled while A holds the wheel (Consumables.Allowed), so a food drop waits for the release.
        string wheelItemDrop;float wheelItemDropUntil;
        void PumpWheelItemDrop(){if(wheelItemDrop==null)return;if(!roundStarted||Time.unscaledTime>wheelItemDropUntil||!OwnsConsumable(wheelItemDrop)){wheelItemDrop=null;return;}if(queuedDrop!=null||WeaponWheel!=null&&WeaponWheel.Active||R23InputProjection.WheelHeld(Rig.LeftInput,Rig.RightInput,Gameplay.PrimaryHand))return;string id=wheelItemDrop;wheelItemDrop=null;QueueDrop(id);}
        bool OwnsConsumable(string id){if(Consumables!=null&&id!=null)foreach(var item in Consumables.OwnedItems)if(item.Id==id)return true;return false;}
        void ConfigureWheelDrop(){WeaponWheel.ConfigureDrop(QueueWheelWeaponDrop,WheelWeaponDropConfirmed);}
        bool QueueWheelWeaponDrop(string id,int hand)
        {
            if(!Ready||!roundStarted||Gameplay==null||Gameplay.Health<=0||!Rig.SessionFocused||PageBlocksGameplay||BackpackOpen||hand!=Gameplay.PrimaryHand||!WheelItemDroppable(id)||queuedDrop!=null)return false;
            wheelDropLobby=LanSession==null?null:LanSession.Lobby;wheelDropEpoch=wheelDropLobby==null?-1:wheelDropLobby.Epoch;
            wheelDropKind=Gameplay.OwnsWeapon(id)?0:1;if(wheelDropKind!=0&&OwnsConsumable(id)){wheelItemDrop=id;wheelItemDropUntil=Time.unscaledTime+10;return true;}QueueDrop(id);return true; // Same safe placement, ammo preservation and host gate as backpack drop.
        }
        bool WheelWeaponDropConfirmed(string id)
        {
            var lobby=LanSession==null?null:LanSession.Lobby;if(lobby!=wheelDropLobby||lobby!=null&&lobby.Epoch!=wheelDropEpoch||!Ready||!roundStarted||Gameplay==null||Gameplay.Health<=0)return false;
            if(OwnsExplosive(id)||OwnsConsumable(id))return false;if(!Gameplay.OwnsWeapon(id)&&wheelDropKind!=0)return true;
            Vector3 position;Quaternion rotation;return !Gameplay.OwnsWeapon(id)&&Gameplay.TryGetDroppedPose(id,out position,out rotation);
        }
    }
}
#endif
