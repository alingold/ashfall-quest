#if UNITY_EDITOR
using System;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Consumables;
namespace Ashfall.Editor.Playable
{
    public static class R23LoadoutSmoke
    {
        static void Need(bool value,string why){if(!value)throw new Exception(why);}
        public static void Run(R23PlayableLoop loop)
        {
            var game=loop.Gameplay;game.ResetRound();game.NotifyFocus(true);game.SetGameplayPaused(false);
            loop.Rig.Left.position=game.WeaponGripPosition;
            game.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true});
            game.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftGrip=.7f});
            Need(game.HasWeapon&&game.PrimaryHand==0&&game.LeftHandHeld,"Actual left analog grip did not acquire weapon");
            for(int i=0;i<5;i++)game.TickInput(.05f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true});
            int ammo=game.Ammo;
            game.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftTrigger=.9f});
            Need(game.Ammo<ammo,"Left-hand trigger did not debit actual source ammunition");
            for(int i=0;i<12;i++)game.TickInput(.05f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true});
            int droppedAmmo=game.Ammo;int reserve=game.Reserve;Bounds bounds;Vector3 position;Quaternion rotation;
            Need(game.TryGetWeaponBounds(game.ActiveWeaponId,out bounds),"Source weapon drop bounds missing");
            Need(R23WorldItemPlacement.TryDrop(loop.Rig.Head.transform,loop.Rig.transform,bounds,out position,out rotation),"No reachable native map drop pose");
            string id=game.ActiveWeaponId;Need(game.DropOwned(id,position,rotation)&&!game.OwnsWeapon(id),"Exact owned weapon drop failed");
            game.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true});
            loop.Rig.Right.position=position;loop.Rig.Right.rotation=rotation;
            // Exact source grip offset is inside the 30cm pickup range.
            Need(game.TryPickup(1)&&game.OwnsWeapon(id)&&game.PrimaryHand==1,"Right hand did not recover left-dropped source instance");
            Need(game.Ammo==droppedAmmo&&game.Reserve==reserve,"Drop/recollect granted ammunition");
            Need(game.HolsterCurrent()&&game.EquipOwned(id,0)&&game.Ammo==droppedAmmo,"Owned left-hand re-equip changed ammunition");
            Need(loop.Consumables.Ready&&loop.Consumables.Failure==null,"Actual source consumable geometry failed loading");
            game.ResetRound();
            // The next source catalog adds a third weapon: free a real slot before
            // collecting a consumable instead of bypassing the shared capacity.
            if(game.OwnedWeaponIds.Length>=3){string spare=game.OwnedWeaponIds[game.OwnedWeaponIds.Length-1];Need(game.TryGetWeaponBounds(spare,out bounds)&&R23WorldItemPlacement.TryDrop(loop.Rig.Head.transform,loop.Rig.transform,bounds,out position,out rotation)&&game.DropOwned(spare,position,rotation),"Could not free a genuine owned slot for source soda");}
            loop.Consumables.SetOtherSlotsUsed(game.OwnedWeaponIds.Length);game.ApplyPlayerDamage(80);float shield=game.Shield;
            var sample=new R23ConsumableSample{Enabled=true,DeltaSeconds=.02f,LeftReady=true,RightReady=true,LeftFree=true,RightFree=true};
            loop.Rig.Right.position=loop.Rig.Head.transform.position+loop.Rig.Head.transform.forward*.25f-Vector3.up*.2f;loop.Rig.Right.rotation=Quaternion.identity;
            Need(loop.Consumables.RepositionWorldItem("r23-starter/soda",loop.Rig.Right.position),"Available soda relocation failed");
            loop.Consumables.Sample(sample);Need(loop.Consumables.TryPickup("r23-starter/soda",2),"Actual soda proximity pickup failed");
            loop.Rig.Left.position=loop.Consumables.ConsumptionAnchor;sample.LeftGrip=1;loop.Consumables.Sample(sample);
            Need(loop.Consumables.Phase==Preparation.Consume,"Actual free-hand soda opening failed");
            loop.Rig.Right.rotation=Quaternion.Euler(0,0,90);loop.Rig.Right.position=loop.Rig.Head.transform.position+loop.Rig.Head.transform.forward*.2f;
            loop.Consumables.Sample(sample);Need(loop.Consumables.HeldId==null&&loop.Consumables.PendingSoda>49,"Actual tilted soda did not enter timed dose");
            sample.DeltaSeconds=.1f;for(int i=0;i<100;i++)loop.Consumables.Sample(sample);
            Need(Mathf.Abs(game.Shield-shield-50)<.01f,"Source timed soda failed to restore actual Health owner shield");
            Need(!loop.Consumables.TryPickup("r23-starter/soda",2),"Consumed soda was re-collected");
            game.ResetRound();loop.Consumables.ResetRound();
            Debug.Log("R23 native loadout PASS: left pickup/fire, reachable map drop, opposite-hand recollect, ammo conservation and source timed soda into actual Health");
        }
    }
}
#endif
