#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        enum LocalDeathDropKind { Weapon, Food, Grenade, Supply }
        sealed class LocalDeathDrop
        {
            internal LocalDeathDropKind Kind;
            internal string Value, Id;
            internal int Amount;
            internal double Expires;
        }
        readonly List<LocalDeathDrop> localDeathDrops=new List<LocalDeathDrop>();
        long localDeathDropSerial;
        Vector3? lastDeathDropFooting;

        bool TryDeathDropFloor(Vector3 feet,float depth,out Vector3 at)
        {
            RaycastHit hit;
            if(Physics.Raycast(feet+Vector3.up*.5f,Vector3.down,out hit,depth,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore)&&hit.normal.y>=.55f)
            {at=hit.point+Vector3.up*.18f;return true;}
            at=feet;
            return false;
        }

        void DropLocalDeathInventory()
        {
            var drops=new List<LocalDeathDrop>();
            foreach(string id in Gameplay.OwnedWeaponActionIds) drops.Add(new LocalDeathDrop{Kind=LocalDeathDropKind.Weapon,Value=id});
            foreach(var item in Consumables.OwnedItems) drops.Add(new LocalDeathDrop{Kind=LocalDeathDropKind.Food,Value=item.Kind});
            if(Explosives!=null)foreach(var item in Explosives.OwnedItems)
                drops.Add(new LocalDeathDrop{Kind=LocalDeathDropKind.Grenade,Value=item.Kind,Amount=item.SourceStars});
            foreach(string family in R24LanLoadout.StartingReserves().Keys)
            {
                int count=Gameplay.ReadStoredAmmoReserveFamily(family);
                if(count>0)drops.Add(new LocalDeathDrop{Kind=LocalDeathDropKind.Supply,Value=family,Amount=count});
            }
            if(Activities.Balance>0)drops.Add(new LocalDeathDrop{Kind=LocalDeathDropKind.Supply,Value="builds",Amount=Activities.Balance});
            for(int i=3;i<inventoryCapacity;i++)drops.Add(new LocalDeathDrop{Kind=LocalDeathDropKind.Supply,Value="backpack",Amount=1});

            Vector3 center,floor;
            float depth=Mathf.Max(3,Map.Bounds.size.y+5);
            if(!TryDeathDropFloor(Rig.transform.position,depth,out center))center=lastDeathDropFooting??Map.GameSpawn;
            for(int i=0;i<drops.Count;i++)
            {
                var drop=drops[i];
                var site=R26DeathLootLayout.Site(new Float3(center.x,center.y,center.z),i,drops.Count);
                var at=new Vector3(site.X,site.Y,site.Z);
                if(TryDeathDropFloor(at,depth,out floor))at=floor;
                drop.Id="local-death/"+checked(++localDeathDropSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
                bool published;
                switch(drop.Kind)
                {
                    case LocalDeathDropKind.Weapon:published=Gameplay.ScatterOwnedForDeath(drop.Value,at,out drop.Id);break;
                    case LocalDeathDropKind.Food:published=Consumables.Spawn(drop.Id,drop.Value,at,false);break;
                    case LocalDeathDropKind.Grenade:published=Explosives.SpawnWorldItem(drop.Id,drop.Value,drop.Amount,at,Quaternion.identity);break;
                    default:published=Supplies.Spawn(drop.Id,drop.Value,at,drop.Amount);break;
                }
                if(published){drop.Expires=Time.unscaledTimeAsDouble+R26DeathLootLayout.Lifetime;localDeathDrops.Add(drop);}
            }
            ResetRespawnLoadout(ActualMatchRules);
        }

        void TickLocalDeathLoot(bool clear)
        {
            if(!clear&&MatchLocalAlive)
            {
                Vector3 floor;
                if(TryDeathDropFloor(Rig.transform.position,1.1f,out floor))lastDeathDropFooting=floor;
            }
            if(clear)lastDeathDropFooting=null;

            double now=Time.unscaledTimeAsDouble;
            for(int i=localDeathDrops.Count-1;i>=0;i--)
            {
                var drop=localDeathDrops[i];
                if(!clear&&now<drop.Expires)continue;
                switch(drop.Kind)
                {
                    case LocalDeathDropKind.Weapon:Gameplay.DespawnWorldWeapon(drop.Id);break;
                    case LocalDeathDropKind.Food:Consumables.RetireUnownedWorldItem(drop.Id);break;
                    case LocalDeathDropKind.Grenade:
                        Explosives.R23GrenadeItemState state;
                        if(Explosives.TryReadItem(drop.Id,out state)&&!state.Owned)Explosives.ReconcileNetworkItem(drop.Id,false,true);
                        break;
                    default:
                        if(!Supplies.RetireUncollectedWorldItem(drop.Id))Supplies.SetWorldItemVisible(drop.Id,false);
                        break;
                }
                localDeathDrops.RemoveAt(i);
            }
        }
    }
}
#endif
