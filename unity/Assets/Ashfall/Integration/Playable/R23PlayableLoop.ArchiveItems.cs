#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using Ashfall.Integration.Playable.World.Loot;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public R24MountedWeaponRacks MountedRacks{get;private set;}
        public R24ArchivePickupComposition ArchivePickups{get;private set;}
        bool archivePickupReset;string reportedArchiveItemFailure;
        void InitializeArchiveItems()
        {
            WorldLoot.BindArchiveWorld(this);ArchivePickups=new R24ArchivePickupComposition(Consumables,Supplies);
            MountedRacks=new GameObject("Original source mounted red weapon racks").AddComponent<R24MountedWeaponRacks>();MountedRacks.transform.SetParent(transform,false);MountedRacks.Initialize(this);
        }
        void TickArchiveItems()
        {
            if(!ArchiveWorldReady||ArchivePickups==null)return;
            string capturedMap=Map.MapId;long generation=ArchiveWorldGeneration;
            Func<bool> collisionReady=delegate{return this!=null&&Ready&&roundStarted&&!switchingMap&&ArchiveWorldReady&&Map.MapId==capturedMap&&ArchiveWorldGeneration==generation;};
            if(ArchivePickups.Apply(capturedMap,generation,ArchiveWorldPlan,TryArchiveSourceToWorld,collisionReady,archivePickupReset)){archivePickupReset=false;reportedArchiveItemFailure=null;}
            else if(ArchivePickups.Failure!=reportedArchiveItemFailure){reportedArchiveItemFailure=ArchivePickups.Failure;Debug.LogWarning("Archive pickup composition: "+reportedArchiveItemFailure);}
        }
        void WithdrawArchiveItems()
        {PrepareArchiveWorld(false);if(MountedRacks!=null)MountedRacks.WithdrawMap();if(ArchivePickups!=null)ArchivePickups.LeaveArchive();archivePickupReset=true;}
        void ResetArchiveItems()
        {PrepareArchiveWorld(false);if(MountedRacks!=null)MountedRacks.ResetMap();archivePickupReset=true;}
    }
}
#endif
