#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
namespace Ashfall.Integration.Playable.Consumables
{
    public sealed partial class R24VRSupplies
    {
        readonly List<string> archiveSupplyIds=new List<string>();bool archiveBaseHidden;
        public string[] ArchiveSupplyIds {get{return archiveSupplyIds.ToArray();}}
        // Native diagnostic reads actual presentation rather than desired visibility.
        public bool TryReadWorldVisibility(string id,out bool actualVisible)
        {actualVisible=false;Box box;if(id==null||!boxes.TryGetValue(id,out box)||box.View==null)return false;actualVisible=box.View.activeInHierarchy;return true;}
        // Only uncollected archive-owned boxes are removed. Collected slots retain
        // their actual source tombstone/event identity across plan generations.
        void RetirePreviousArchiveBoxes()
        {
            foreach(string id in archiveSupplyIds)
            {
                Box box;R24SupplyItem item;if(!boxes.TryGetValue(id,out box)||!state.TryRead(id,out item))continue;
                if(item.Collected){box.Visible=false;Refresh(box);continue;}
                if(!state.RemoveUncollected(id))throw new InvalidOperationException("Archive supply mutation attempted during actual collection");
                if(box.View!=null){box.View.SetActive(false);Destroy(box.View);}boxes.Remove(id);
            }
            archiveSupplyIds.Clear();
        }
        public void LeaveArchivePickupLayout()
        {
            if(archiveSupplyIds.Count==0&&!archiveBaseHidden)return;sourceLayoutGeneration++;RetirePreviousArchiveBoxes();archiveBaseHidden=false;
            foreach(var row in R24WorldPickupLayout.Ammo())SetWorldItemVisible(row.Id,true);
        }
        public R24PickupLayoutResult ApplyArchivePickupLayout(string mapId,Vector3[] validatedWorldLoot,Func<bool> collisionReady)
        {
            if(validatedWorldLoot==null||validatedWorldLoot.Length==0||collisionReady==null)throw new ArgumentException("Actual archive loot/collision readiness required");foreach(var point in validatedWorldLoot)if(!Finite(point))throw new ArgumentException("Actual finite archive loot required");R24ArchivePickupLayout.AmmoId(mapId,0);
            long generation=++sourceLayoutGeneration;sourceSettleResults.Clear();RetirePreviousArchiveBoxes();archiveBaseHidden=true;foreach(var row in R24WorldPickupLayout.Ammo())SetWorldItemVisible(row.Id,false);
            int created=0,moved=0,preserved=0;
            foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())
            {
                if(!row.IsSupply)continue;Vector3 at=validatedWorldLoot[R24ArchivePickupLayout.FoodLootIndex(row.SourceIndex,validatedWorldLoot.Length)];Box box;
                if(!boxes.TryGetValue(row.Id,out box)){if(!Spawn(row.Id,row.Kind,at)){preserved++;continue;}created++;box=boxes[row.Id];}
                else{if(box.Family!=row.Kind)throw new InvalidOperationException("Archive backpack identity changed");if(!RepositionWorldItem(row.Id,at)){preserved++;sourceSettleResults[row.Id]=Result(row.Id,R24PickupSettleOutcome.Preserved,at,box.Position);continue;}moved++;}
                box.Visible=true;Refresh(box);sourceSettleResults[row.Id]=Result(row.Id,R24PickupSettleOutcome.ArchiveAssigned,at,box.Position);
            }
            for(int k=0;k<R24ArchivePickupLayout.AmmoCount;k++)
            {
                string id=R24ArchivePickupLayout.AmmoId(mapId,k),family=R24ArchivePickupLayout.AmmoFamily(k);Vector3 at=validatedWorldLoot[R24ArchivePickupLayout.AmmoLootIndex(k,validatedWorldLoot.Length)];Box box;archiveSupplyIds.Add(id);
                if(!boxes.TryGetValue(id,out box)){if(!Spawn(id,family,at)){preserved++;continue;}created++;box=boxes[id];}
                else{if(box.Family!=family)throw new InvalidOperationException("Archive ammo identity changed");R24SupplyItem snapshot;state.TryRead(id,out snapshot);if(snapshot.Collected){box.Visible=false;Refresh(box);preserved++;sourceSettleResults[id]=Result(id,R24PickupSettleOutcome.Preserved,at,box.Position);continue;}if(!RepositionWorldItem(id,at)){preserved++;continue;}moved++;}
                box.Visible=true;Refresh(box);sourceSettleResults[id]=Result(id,R24PickupSettleOutcome.Pending,at,box.Position);
                StartCoroutine(SettleSourceBox(id,box,generation,collisionReady,new Float3(at.x,at.y,at.z)));
            }
            return new R24PickupLayoutResult(created,moved,preserved);
        }
        static R24PickupSettleResult Result(string id,R24PickupSettleOutcome outcome,Vector3 requested,Vector3 position)
        {return new R24PickupSettleResult(id,outcome,new Float3(requested.x,requested.y,requested.z),new Float3(position.x,position.y,position.z));}
    }
}
#endif
