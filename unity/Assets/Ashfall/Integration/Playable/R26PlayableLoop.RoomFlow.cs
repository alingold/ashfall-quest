#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using UnityEngine;using Ashfall.Integration.Playable.World;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        bool startupLogoAnchored;
        void LateUpdate()
        {
            if(startupLogoAnchored||UI==null||Lobby==null||Rig==null)return;
            // Start builds the presenter at the origin, then places the rig in the menu
            // city. Anchor after that placement, before the first camera render.
            startupLogoAnchored=true;
            if(!Ready&&Failure==null){UI.BeginLoading("MENU");Debug.Log("R26 ROOMFLOW startup-logo anchored after menu placement; head="+Rig.Head.transform.position);}
        }
        IEnumerator ChangeRoomMap(string id)
        {
            if(R23MapCatalog.Find(id)==null)yield break;
            string previous=Map.MapId;var room=LanSession.Lobby;long epoch=room.Epoch;int members=room.Members.Length;
            CancelSourceArchiveEntry();if(ValleyMission!=null)ValleyMission.Withdraw();if(ValleyBots!=null)ValleyBots.Clear();EndTeamLaunch();EndMatch();
            if(Soundscape!=null)Soundscape.SuspendEnvironment();ClearLocalEffects();if(Explosives!=null)Explosives.ResetRound();
            if(WorldLoot!=null)WorldLoot.WithdrawMap();WithdrawArchiveItems();PrepareArchiveWorld(false);
            if(MountedRacks!=null)MountedRacks.SetWorldEnabled(false,false);if(WorldLoot!=null)WorldLoot.SetWorldEnabled(false);
            if(WeaponWheel!=null)WeaponWheel.Cancel();switchingMap=true;roundStarted=false;Ready=false;
            if(Gallery!=null)Gallery.Hide();if(Preview!=null)Preview.Hide();Rig.GameplayBlocked=true;Gameplay.SetGameplayPaused(true);Gameplay.HolsterCurrent();
            Gameplay.SetPracticeTargetsVisible(false);
            Consumables.CancelGestures();Consumables.SetWorldVisible(false);if(Explosives!=null){Explosives.Stow();Explosives.SetWorldVisible(false);}if(Supplies!=null)Supplies.SetWorldVisible(false);
            Activities.ResetRound();Climbing.ResetMotion();Lobby.Hide();Map.SetWorldVisible(false);UI.ClearMenuAnchor();UI.BeginLoading(R23MapCatalog.Find(id).Label);
            Debug.Log("R26 ROOMFLOW direct-map begin from="+previous+" to="+id+" epoch="+epoch+" members="+members);
            // Publish Ready=false synchronously, so ApplySharedMap's legacy SelectMap
            // call cannot start the solo/menu-loading coroutine in parallel.
            yield return null;
            if(Map.MapId!=id){Map.Load(id);while(Map.IsLoading)yield return null;}
            if(!Map.IsLoaded){switchingMap=false;Fail(new Exception(Map.LoadError));yield break;}
            mapHealthResetPending=true;activeSpawn=Map.WarmupSpawn;Rig.Head.farClipPlane=Map.CameraFarDistance;Map.SetWorldVisible(false);
            Gameplay.SetPickupHome(activeSpawn+Vector3.up*1.3f,Quaternion.identity);
            UI.SetSelectedMap(id);SpawnConsumables();Consumables.SetWorldVisible(false);if(Explosives!=null)Explosives.SetWorldVisible(false);if(Supplies!=null)Supplies.SetWorldVisible(false);
            JumpPads.SetMap(id);if(HealthStations!=null)HealthStations.SetMap(id,true);
            switchingMap=false;Ready=true;UI.SetLoadingProgress("WAITING FOR ROOM",.95f);
            Debug.Log("R26 ROOMFLOW direct-map loaded map="+id+" menuVisible="+Lobby.Visible+" epoch="+epoch+" members="+members);
            // ApplySharedMap owns hash validation, loaded acknowledgements, warmup or
            // round binding and the authoritative spawn. Only that path reveals play.
        }
    }
}
#endif
