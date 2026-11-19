using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.Networking
{
    internal static class R26DeathLootLayout
    {
        internal const double Lifetime=60;
        internal static Float3 Site(Float3 center,int index,int count)
        {
            double angle=index*2*Math.PI/count;
            float radius=Math.Min(2.4f,Math.Max(.9f,count*.12f));
            return new Float3(center.X+(float)Math.Cos(angle)*radius,center.Y+.2f,center.Z+(float)Math.Sin(angle)*radius);
        }
    }

    public sealed partial class R24LanLobby
    {
        sealed class DeathDrop
        {
            internal string Kind, Value;
            internal int Amount;
            internal CombatWeapon Weapon;
        }
        readonly Dictionary<string,double> deathDrops=new Dictionary<string,double>(StringComparer.Ordinal);
        readonly List<string> expiredDeathDrops=new List<string>();
        long deathDropSerial;
        double nextDeathExpiry=Double.PositiveInfinity;

        void ClearDeathLoot()
        {
            deathDrops.Clear();
            nextDeathExpiry=Double.PositiveInfinity;
        }

        void DropDeathInventory(string actor)
        {
            CombatActor prior;
            if(!combat.TryGetValue(actor,out prior))return;
            var drops=new List<DeathDrop>();
            foreach(var weapon in prior.Weapons)
            {
                drops.Add(new DeathDrop{Kind="weapon",Weapon=weapon.Value});
                LootEntry entry;
                if(loot.TryGetValue(weapon.Key,out entry)&&entry.Owner==actor)
                {entry.Owner=RetiredLootOwner;PublishLoot(entry);}
            }

            var heldFoods=new List<R24LanFoodState>();
            foreach(var item in foods.Values)if(item.Owner==actor&&!item.Consumed)heldFoods.Add(item);
            foreach(var item in heldFoods)
            {
                drops.Add(new DeathDrop{Kind="food",Value=item.Kind});
                foodOwnedAt.Remove(item.Id);
                PublishFood(new R24LanFoodState(item.Id,item.Kind,RetiredLootOwner,item.Position,true,checked(++foodSequence)));
            }
            foreach(var item in SharedGrenades)
            {
                if(item.Owner!=actor||item.Spent)continue;
                drops.Add(new DeathDrop{Kind="grenade",Value=item.Kind});
                PublishGrenade(item.Id,item.Kind,RetiredLootOwner,item.Position,true);
            }
            foreach(var reserve in prior.Reserves)AddDeathSupply(drops,reserve.Key,reserve.Value);
            AddDeathSupply(drops,"builds",prior.Builds);
            for(int i=3;i<prior.Capacity;i++)AddDeathSupply(drops,"backpack",1);

            R24LanSpawn spawn;
            R24LanRawTrackingHistory tracking;
            var center=spawnStates.TryGetValue(actor,out spawn)?spawn.Feet:new Float3();
            if(rawTracking.TryGetValue(actor,out tracking)&&tracking.Latest!=null)
            {
                Float3 feet=tracking.Latest.Pose.Root.Position, landed;
                if(supplyDropWorld==null||supplyDropWorld.TryPlace("backpack",feet,out landed))center=feet;
            }
            for(int i=0;i<drops.Count;i++)
            {
                var drop=drops[i];
                var at=R26DeathLootLayout.Site(center,i,drops.Count);
                Float3 landed;
                string family=drop.Kind=="supply"?drop.Value:"backpack";
                if(supplyDropWorld!=null && (supplyDropWorld.TryPlace(family,at,out landed)||supplyDropWorld.TryPlace(family,center,out landed)))at=landed;
                string id=actor+"/death/"+checked(++deathDropSerial).ToString(System.Globalization.CultureInfo.InvariantCulture);
                if(!PublishDeathDrop(id,drop,at))continue;
                double expiry=clock.Now+R26DeathLootLayout.Lifetime;
                deathDrops.Add(id,expiry);
                nextDeathExpiry=Math.Min(nextDeathExpiry,expiry);
            }
            ResetRespawnInventory(actor);
        }

        static void AddDeathSupply(List<DeathDrop> drops,string family,int count)
        {
            while(count>0)
            {
                int amount=Math.Min(count,10000);
                drops.Add(new DeathDrop{Kind="supply",Value=family,Amount=amount});
                count-=amount;
            }
        }

        bool PublishDeathDrop(string id,DeathDrop drop,Float3 at)
        {
            switch(drop.Kind)
            {
                case "weapon":
                    if(loot.Count>=4096)return false;
                    var entry=new LootEntry{Id=id,Weapon=drop.Weapon,At=at};
                    loot.Add(id,entry);PublishLoot(entry);return true;
                case "food":return RegisterSharedFood(id,drop.Value,at);
                case "grenade":
                    if(grenades.Count>=4096)return false;
                    PublishGrenade(id,drop.Value,"",at,false);return true;
                default:return RegisterSharedSupply(id,drop.Value,drop.Amount,at);
            }
        }

        void ExpireDeathLoot()
        {
            if(clock.Now<nextDeathExpiry)return;
            nextDeathExpiry=Double.PositiveInfinity;
            foreach(var row in deathDrops)
            {
                if(row.Value<=clock.Now)expiredDeathDrops.Add(row.Key);
                else nextDeathExpiry=Math.Min(nextDeathExpiry,row.Value);
            }
            foreach(string id in expiredDeathDrops)
            {
                deathDrops.Remove(id);
                LootEntry weapon;R24LanFoodState food;R24LanGrenadeState grenade;R24LanSupplyState supply;
                if(loot.TryGetValue(id,out weapon)&&weapon.Owner==""){weapon.Owner=RetiredLootOwner;PublishLoot(weapon);}
                else if(foods.TryGetValue(id,out food)&&food.Owner==""&&!food.Consumed)RetireWorldFood(id);
                else if(grenades.TryGetValue(id,out grenade)&&grenade.Owner==""&&!grenade.Spent)PublishGrenade(id,grenade.Kind,"",grenade.Position,true);
                else if(sharedSupplies.TryGetValue(id,out supply)&&supply.Collector=="")
                    PublishSupply(new R24LanSupplyState(id,supply.Family,supply.Amount,supply.Position,RetiredLootOwner,checked(++supplySequence)));
            }
            expiredDeathDrops.Clear();
        }
    }
}
