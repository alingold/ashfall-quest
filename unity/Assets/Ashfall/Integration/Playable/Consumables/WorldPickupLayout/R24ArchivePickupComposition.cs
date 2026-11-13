#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using UnityEngine;
namespace Ashfall.Integration.Playable.Consumables.WorldPickupLayout
{
    public delegate bool R24ArchivePointToWorld(Float3 source,out Vector3 world);
    // Consumes the ONE existing validated source plan; owns no floor/grid query.
    public sealed class R24ArchivePickupComposition
    {
        readonly R23VRConsumables foods;readonly R24VRSupplies supplies;
        public string AppliedMapId{get;private set;}public long AppliedGeneration{get;private set;}public string Failure{get;private set;}
        public R24PickupLayoutResult FoodResult{get;private set;}public R24PickupLayoutResult SupplyResult{get;private set;}
        public R24ArchivePickupComposition(R23VRConsumables actualFoods,R24VRSupplies actualSupplies)
        {if(actualFoods==null||actualSupplies==null)throw new ArgumentNullException();foods=actualFoods;supplies=actualSupplies;}
        public bool Apply(string mapId,long generation,R24ArchiveSpawnPlan actualPlan,R24ArchivePointToWorld sourceToWorld,Func<bool> collisionReady,bool forceSourceReset=false)
        {
            R24ArchivePickupLayout.AmmoId(mapId,0);
            if(AppliedMapId==mapId&&AppliedGeneration==generation&&!forceSourceReset)return true;
            Failure=null;if(actualPlan==null||!actualPlan.Ready||sourceToWorld==null||collisionReady==null||!foods.Ready||!supplies.Ready){Failure="Actual archive plan/item owners not ready";return false;}
            if(!collisionReady()){Failure="Actual archive collision not active";return false;}
            var source=actualPlan.Loot;if(source.Length==0){Failure="Actual archive plan has no validated loot";return false;}
            // Verify EVERY conversion before touching current world presentation.
            var world=new Vector3[source.Length];for(int i=0;i<source.Length;i++)if(!sourceToWorld(source[i],out world[i])||!new Float3(world[i].x,world[i].y,world[i].z).IsFinite){Failure="Actual archive source/world binding failed";return false;}
            FoodResult=foods.AssignArchiveFoodPositions(world);SupplyResult=supplies.ApplyArchivePickupLayout(mapId,world,collisionReady);
            AppliedMapId=mapId;AppliedGeneration=generation;return true;
        }
        public void LeaveArchive()
        {supplies.LeaveArchivePickupLayout();AppliedMapId=null;AppliedGeneration=0;Failure=null;}
    }
}
#endif
