#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Integration.Playable.Lobby;
using UnityEngine;
using UnityEngine.XR;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        R23PlayableLoop menuLoop;
        readonly List<XRInputSubsystem> menuInputs=new List<XRInputSubsystem>();
        readonly List<XRInputSubsystem> observedMenuInputs=new List<XRInputSubsystem>();
        bool menuSpawnActive,menuFloorReady,menuTracked,menuRecenterPending;
        string menuSession,menuActor;int menuSlot;
        R23Lobby MenuWorld
        {
            get{if(menuLoop==null)menuLoop=GetComponentInParent<R23PlayableLoop>();return menuLoop==null?null:menuLoop.Lobby;}
        }
        // All existing boot/HOME/map-load/result callers converge at SetSpawn. Gameplay map
        // spawns retain their normal path, even while a loading menu is visible.
        bool TryEnterMenuSpawn(Vector3 location)
        {
            var lobby=MenuWorld;if(lobby==null||!lobby.Visible||(location-lobby.Spawn).sqrMagnitude>.000001f)return false;
            EnterMenuPlacement();return true;
        }
        public static Vector3 MenuMemberOffset(int slot)
        {
            if(slot>=8&&slot<24)return new Vector3(((slot-8)%5-2)*.8f,0,-1.8f-((slot-8)/5)*.9f);
            switch(slot){case 1:return new Vector3(-.8f,0,0);case 2:return new Vector3(.8f,0,0);case 3:return new Vector3(-1.6f,0,0);case 4:return new Vector3(1.6f,0,0);case 5:return new Vector3(-.8f,0,-.9f);case 6:return new Vector3(0,0,-.9f);case 7:return new Vector3(.8f,0,-.9f);default:return Vector3.zero;}
        }
        // One placement owner. Only the rig root moves: native head/controller poses and the
        // calibrated tracking-origin transform survive. A stage-space horizontal offset must
        // be subtracted from the root so the measured player's feet, not the stage origin,
        // land on the anchor. The normal source feet-recentering subsequently follows the head.
        public void EnterMenuPlacement()
        {
            var lobby=MenuWorld;if(lobby==null||!lobby.Visible||Head==null||body==null)return;
            var room=menuLoop.LanSession==null?null:menuLoop.LanSession.Lobby;
            menuSession=room==null?null:room.SessionId;menuActor=room==null?null:room.OwnActor;menuSlot=room==null?0:room.MenuSpawnSlot;
            Vector3 feet=lobby.Spawn+MenuMemberOffset(menuSlot);feet.y=lobby.Origin.y;
            Physics.SyncTransforms();RaycastHit floor;
            if(lobby.Ready&&Physics.Raycast(feet+Vector3.up,Vector3.down,out floor,2,Physics.DefaultRaycastLayers&~(1<<10),QueryTriggerInteraction.Ignore)&&floor.normal.y>.65f)feet.y=floor.point.y;
            Vector3 toward=lobby.MenuAnchor.position-feet;toward.y=0;
            Vector3 localForward=transform.InverseTransformDirection(Head.transform.forward);localForward.y=0;
            // Looking straight up/down has no forward azimuth; the horizontal right axis supplies it.
            if(localForward.sqrMagnitude<.0001f){Vector3 right=transform.InverseTransformDirection(Head.transform.right);localForward=Vector3.Cross(right,Vector3.up);localForward.y=0;}
            Quaternion rotation=Quaternion.LookRotation(toward,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(localForward,Vector3.up));
            Vector3 offset=rotation*transform.InverseTransformPoint(Head.transform.position);offset.y=0;
            DisarmMotion();if(Climbing!=null)Climbing.ResetMotion();
            bool enabledBefore=body.enabled;body.enabled=false;
            transform.SetPositionAndRotation(feet-offset,rotation);body.enabled=enabledBefore;
            spawn=transform.position;spawnRotation=rotation;artificialOriginOffset=Vector3.zero;
            grounded=true;ceiling=false;Paused=false;CrouchFloorValid=false;haveSourceHead=false;
            havePhysicalNeutral=false;physicalSupportHold=false;physicalNeutralSince=-1;lastPhysicalBlock=-1;headJumpInProgress=false;headMotionOwned=false;
            standableCount=0;nextStandableSample=0;headRecovery.ClearHistory();headRecovery.ResetOpaque();HeadClearanceBlocked=false;
            if(viewFade!=null)viewFade.NotifyRootReset();
            if(Calibration!=null)Calibration.NotifyRootPoseReset();
            menuSpawnActive=true;menuFloorReady=lobby.Ready;menuTracked=Tracking;menuRecenterPending=false;
            Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,this,"R26 MENU SPAWN slot={0} feet={1} root={2} head={3} yaw={4:F2} tracked={5} floorReady={6}",menuSlot,feet.ToString("F3"),transform.position.ToString("F3"),Head.transform.position.ToString("F3"),Head.transform.eulerAngles.y,Tracking,lobby.Ready);
        }
        // Run after sampling tracking, BEFORE any sweep against the previous map's head.
        // Reapply only at boundaries, never continuously lock the player to the menu.
        void TickMenuPlacement()
        {
            RefreshMenuInputSubscriptions();var lobby=MenuWorld;
            if(lobby==null||!lobby.Visible){menuSpawnActive=false;menuRecenterPending=false;return;}
            if(!menuSpawnActive)return;
            var room=menuLoop.LanSession==null?null:menuLoop.LanSession.Lobby;
            string session=room==null?null:room.SessionId,actor=room==null?null:room.OwnActor;int slot=room==null?0:room.MenuSpawnSlot;
            if(menuRecenterPending||!menuFloorReady&&lobby.Ready||!menuTracked&&Tracking||session!=menuSession||actor!=menuActor||slot!=menuSlot)EnterMenuPlacement();
        }
        void RefreshMenuInputSubscriptions()
        {
            menuInputs.Clear();SubsystemManager.GetSubsystems(menuInputs);
            foreach(var inputSystem in menuInputs)if(!observedMenuInputs.Contains(inputSystem)){observedMenuInputs.Add(inputSystem);inputSystem.trackingOriginUpdated+=MenuTrackingOriginUpdated;}
            for(int i=observedMenuInputs.Count-1;i>=0;i--)if(!menuInputs.Contains(observedMenuInputs[i])){observedMenuInputs[i].trackingOriginUpdated-=MenuTrackingOriginUpdated;observedMenuInputs.RemoveAt(i);}
        }
        void MenuTrackingOriginUpdated(XRInputSubsystem inputSystem)
        {var lobby=MenuWorld;if(lobby==null||!lobby.Visible||!menuSpawnActive)return;menuRecenterPending=true;haveSourceHead=false;}
        void DisposeMenuPlacement()
        {foreach(var inputSystem in observedMenuInputs)inputSystem.trackingOriginUpdated-=MenuTrackingOriginUpdated;observedMenuInputs.Clear();}
    }
}
#endif
