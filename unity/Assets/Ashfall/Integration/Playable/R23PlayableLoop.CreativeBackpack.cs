#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.Playable.UI;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        R24CreativeRequest pendingCreative;long creativeSerial;int creativeRequestFrame;
        bool queuedBackpackDrop;int backpackDropRequestFrame;
        void QueueBackpackDrop(){if(Ready&&BackpackOpen){queuedBackpackDrop=true;backpackDropRequestFrame=Time.frameCount;}}
        void ApplyBackpackDrop()
        {
            if(!queuedBackpackDrop)return;queuedBackpackDrop=false;
            if(backpackDropRequestFrame!=Time.frameCount||!Ready||!roundStarted||switchingMap||Gameplay.Health<=0||!BackpackOpen||creativeSerial==long.MaxValue)return;
            int used=Gameplay.OwnedWeaponActionIds.Length+ConsumableSlotCount+ExplosiveSlotCount;
            if(InventoryCapacity<=3){UI.SetLoadoutStatus("NO EXTRA BACKPACK");return;}
            if(used>=InventoryCapacity){UI.SetLoadoutStatus("FREE ONE SLOT BEFORE DROPPING A BACKPACK");return;}
            Vector3 at;Quaternion rotation;
            if(Supplies==null||!Supplies.Ready||!R23WorldItemPlacement.TryDrop(Rig.Head.transform,Rig.transform,Supplies.GetDropBounds("backpack"),out at,out rotation))
            {UI.SetLoadoutStatus("DROP REFUSED / NO REACHABLE CLEAR GROUND");return;}
            if(LanSession!=null&&LanSession.Lobby!=null){UI.SetLoadoutStatus(LanSession.QueueReserveDrop("backpack",at)?"DROP REQUESTED / WAITING FOR HOST":"BACKPACK DROP REFUSED");return;}
            string id="backpack/drop/"+(++creativeSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(!Supplies.Spawn(id,"backpack",at,rotation,1)){UI.SetLoadoutStatus("BACKPACK DROP REFUSED");return;}
            // Publish the actual world item before withdrawing its one capacity
            // unit; the occupancy guard prevents discarding any retained item.
            inventoryCapacity--;RefreshSlotBudget();RefreshLoadout();UI.SetLoadoutStatus("BACKPACK DROPPED");
        }
        bool CreativeCatalogAllowed {get{return R24CreativeCatalog.Enabled(ActualMatchRules!=null,ActualMatchRules!=null&&ActualMatchRules.Catalog);}}
        public string LastCreativeResult {get;private set;}
        public int CreativeRequestCount {get;private set;}
        void QueueCreativeItem(R24CreativeRequest request){pendingCreative=request;creativeRequestFrame=Time.frameCount;CreativeRequestCount++;LastCreativeResult="queued "+request.Kind+"/"+request.Value;}
        Vector3 CatalogDropPoint()
        {
            Vector3 forward=Vector3.ProjectOnPlane(Rig.Head.transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;else forward.Normalize();
            Vector3 desired=Rig.transform.position+forward*1.65f+Vector3.up*.75f;
            Vector3 delta=desired-Rig.Head.transform.position;float nearest=float.PositiveInfinity;Vector3 result=desired;
            foreach(var hit in Physics.RaycastAll(Rig.Head.transform.position,delta.normalized,delta.magnitude,(1<<0)|(1<<8)|(1<<19),QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(Rig.transform)&&hit.distance<nearest){nearest=hit.distance;result=hit.point+hit.normal*.2f;}
            return result;
        }
        void ApplyCreativeItem()
        {
            var request=pendingCreative;pendingCreative=null;if(request==null)return;
            if(creativeRequestFrame!=Time.frameCount){LastCreativeResult="expired request after input frame";return;}
            if(!CreativeCatalogAllowed||!Ready||!roundStarted||switchingMap||Gameplay.Health<=0||!Rig.Tracking||!Rig.SessionFocused||PageBlocksGameplay||creativeSerial==long.MaxValue){LastCreativeResult="unavailable: catalog="+CreativeCatalogAllowed+" ready="+Ready+" round="+roundStarted+" switching="+switchingMap+" hp="+Gameplay.Health+" tracking="+Rig.Tracking+" focus="+Rig.SessionFocused+" page="+UI.Page;return;}
            // Either pointer may operate the board; actual equipment belongs to
            // the selected weapon hand, mirrored for left-handed players.
            bool drop=request.Drop;int hand=Gameplay.PrimaryHand;if(!(hand==0?Rig.LeftInput.Tracked:Rig.RightInput.Tracked)){LastCreativeResult="requested hand not tracked";return;}
            LastCreativeResult="dispatch "+request.Kind+"/"+request.Value;
            if(request.Kind!="utility"&&LanSession!=null&&LanSession.Lobby!=null){bool queued=LanSession.QueueCatalog(request,hand,request.Drop||request.Kind=="supply"?CatalogDropPoint():(hand==0?Rig.Left:Rig.Right).position);LastCreativeResult=queued?"waiting for host custody":"host request refused";UI.SetLoadoutStatus(queued?"CATALOG REQUESTED / WAITING FOR HOST":"CATALOG REQUEST REFUSED");return;}
            catalogPickup=true;try{var frame=hand==0?Rig.Left:Rig.Right;
            Vector3 at=CatalogDropPoint();Quaternion rotation=Quaternion.identity;
            string serial="creative/"+(++creativeSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);bool okay=false;
            if(request.Kind=="supply")okay=Supplies!=null&&Supplies.Spawn(serial,request.Value,at);
            else if(request.Kind=="utility"){if(!PaddleHands)TogglePaddles();okay=PaddleHands;}
            else if(request.Kind=="weapon"&&Ashfall.Integration.Playable.Explosives.R23GrenadeState.ValidKind(request.Value))
            {
                if(drop)okay=Explosives!=null&&Explosives.SpawnWorldItem(serial,request.Value,request.Stars,at,rotation);
                else if(Explosives!=null&&MakeRoomForPickup(request.Value,false,true))
                {StowPaddles();Consumables.Stow();Explosives.Stow();if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();okay=Explosives.GrantOwned(serial,request.Value,hand+1,!(hand==0?Climbing.LeftAnchored:Climbing.RightAnchored));}
            }
            else if(request.Kind=="weapon")
            {
                string id;if(Gameplay.SpawnWorldWeapon(request.Value,request.Stars,at,rotation,out id))
                {LastCreativeResult="created "+id;if(drop)okay=true;else{StowPaddles();Consumables.Stow();if(Explosives!=null)Explosives.Stow();Gameplay.UpdateInputAvailability(AvailabilityFrame());okay=Gameplay.CollectCatalogWorldWeapon(id,hand);if(!okay)LastCreativeResult="pickup refused "+id+" owned="+Gameplay.OwnedWeaponActionIds.Length+" capacity="+InventoryCapacity;}}
                else LastCreativeResult="instance creation refused";
            }
            else if(request.Kind=="item"&&request.Value=="backpack")
            {
                if(Supplies!=null&&Supplies.Ready&&(drop||InventoryCapacity<6)&&Supplies.Spawn(serial,"backpack",drop?at:frame.position,rotation,1))
                {
                    if(drop)okay=true;
                    else{Supplies.UpdateInputAvailability(SupplyInput(true,Rig.LeftInput,Rig.RightInput));okay=Supplies.TryCollect(serial,hand+1)==1;}
                }
            }
            else if(request.Kind=="item")
            {
                // A menu equip inserts directly at the real receiving hand; the
                // normal Pickup/Health/stack/capacity owner still accepts it.
                if(Consumables.Spawn(serial,request.Value,drop?at:frame.position))
                {if(drop)okay=true;else{StowPaddles();if(Explosives!=null)Explosives.Stow();if(Gameplay.HasWeapon)Gameplay.HolsterCurrent();SampleConsumableAvailability();okay=Consumables.TryPickup(serial,hand+1);}}
            }
            if(okay)LastCreativeResult=(drop?"placed ":"selected ")+request.Kind+"/"+request.Value;
            RefreshSlotBudget();RefreshLoadout();UI.SetLoadoutStatus(okay?(drop?"PLACED ":"SELECTED ")+request.Value.ToUpperInvariant():"UNAVAILABLE / COMPLETE CURRENT INTERACTION FIRST");}finally{catalogPickup=false;}
        }
    }
}
#endif
