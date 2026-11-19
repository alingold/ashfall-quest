using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;

namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        internal void ModesDirtyInventory(string actor)
        {
            var owner=combat[actor];
            owner.Weapons["ump"].Ammo=0;
            owner.Weapons["extra"]=new CombatWeapon{Profile="px4",Family="pistol",Stars=1,Capacity=12,Ammo=7};
            owner.Reserves["smg"]=17;
            owner.Capacity=5;
            owner.Kills=owner.Score=4;
            PublishFood(new R24LanFoodState("test-food","banana",actor,new Float3(),false,++foodSequence));
            var grenade=OwnerGrenades(actor,owner).State;
            grenade.RegisterWorldCopy("test-grenade","frag",1);
            if(!grenade.Pickup("test-grenade",2,true))throw new Exception("fixture grenade");
            PublishGrenade("test-grenade","frag",actor,new Float3(),false);
        }
    }
}

partial class LobbyCases
{
    sealed class DeathDropWorld:IR24LanSpawnWorld
    {
        public bool ReadyForSpawn(string map,bool warmup){return true;}
        public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("a",new Float3(0,.12f,0),-1,false,false),new SpawnSite("b",new Float3(.82f,.12f,0),-1,false,false)};}
        public bool IsSafe(string map,string actor,Float3 at){return true;}
    }
    static void RawDeathDrop(R24LanLobby lobby)
    {
        R24LanSpawn spawn;
        if(!lobby.TryReadSpawn(lobby.OwnActor,out spawn))return;
        var feet=spawn.Feet;
        lobby.PublishRawTracking(Body(DeathDropHash,spawn.Revision,feet.X,feet.Y,feet.Z));
    }
    static readonly string DeathDropHash=new string('b',64);
    static void VerifyDeathDrops(string preset)
    {
        using(var pair=new Pair())
        {
            var host=pair.Host;var guest=pair.Client;
            Action<int> tick=delegate(int n){for(int i=0;i<n;i++){RawDeathDrop(host);RawDeathDrop(guest);pair.Tick();}};
            tick(10);
            Need(host.MemberCount==2,preset+" member admission");
            bool revive=preset=="team_revive";
            if(revive)Need(host.HostAssignRoomTeam(guest.OwnActor,host.ReadAuthorityMember(host.OwnActor).Team),"Same-team revive fixture");
            var rules=R24PlayableMatchRules.ForPreset(preset).With("loadout","ump").With("zone_enabled",false).With("protection",0f).With("respawn_delay",1f);
            if(preset=="team_respawn")rules=rules.With("loot",false);
            StartRound(pair,new DeathDropWorld(),rules,DeathDropHash);
            tick(7);
            var before=Health(host,guest.OwnActor);
            host.ModesDirtyInventory(guest.OwnActor);tick(5);
            Need(host.ApplyHostHealthLoss(guest.OwnActor,before.Revision,before.Health,before.Shield,revive).Accepted,"authoritative damage");
            var hurt=Health(host,guest.OwnActor);
            Need(hurt.Life==(revive?LifeState.Downed:LifeState.Dead),"expected life after damage");
            tick(1);
            string prefix=guest.OwnActor+"/death/";
            var groundWeapons=Array.FindAll(host.LootStates,item=>item.Id.StartsWith(prefix)&&item.Owner=="");
            var groundSupplies=Array.FindAll(host.SharedSupplies,item=>item.Id.StartsWith(prefix)&&item.Collector=="");
            var groundFood=Array.Find(host.SharedFoods,item=>item.Id.StartsWith(prefix)&&item.Owner==""&&!item.Consumed);
            var groundGrenade=Array.Find(host.SharedGrenades,item=>item.Id.StartsWith(prefix)&&item.Owner==""&&!item.Spent);
            Need(groundWeapons.Length==2&&groundSupplies.Length==8&&groundFood!=null&&groundGrenade!=null,"all twelve separate death-drop items");
            Need(Array.Exists(groundSupplies,item=>item.Family=="smg"&&item.Amount==17),"old reserve is dropped");
            Need(groundWeapons[0].Position.X!=groundWeapons[1].Position.X||groundWeapons[0].Position.Z!=groundWeapons[1].Position.Z,"distinct positions on circle");
            double droppedAt=pair.Now-.05;
            tick(revive?229:34);
            var after=Health(host,guest.OwnActor);var remote=Health(guest,guest.OwnActor);
            Need(after.Life==LifeState.Alive&&remote.Life==LifeState.Alive&&after.Deaths==remote.Deaths,"same live authority/guest receipt");
            Need(revive?after.SpawnRevision==before.SpawnRevision&&after.Deaths==hurt.Deaths:after.SpawnRevision>before.SpawnRevision&&after.Deaths==before.Deaths+1,"distinct revive/respawn lifecycle");
            int mag,reserve;
            Need(host.TryReadAmmo(guest.OwnActor,"ump",out mag,out reserve)&&mag==28&&reserve==1200,"host magazine and reserve");
            R24LanResourcesState resources;
            Need(guest.TryReadOwnResources(out resources)&&resources.Capacity==3&&resources.Reserves["smg"]==1200,"guest resource snapshot");
            R24LanFoodState food;R24LanGrenadeState grenade;
            Need(guest.TryReadFood("test-food",out food)&&food.Consumed,"guest food custody");
            Need(guest.TryReadGrenade("test-grenade",out grenade)&&grenade.Spent,"guest grenade custody");
            Need(!host.TryReadAmmo(guest.OwnActor,"extra",out mag,out reserve),"host extra weapon custody");
            R24LanLootState copy;
            Need(guest.TryReadLoot(groundWeapons[0].Id,out copy)&&copy.Owner==""&&copy.Position.X==groundWeapons[0].Position.X,"guest sees the same unowned item at the same point");
            var claim=Array.Find(groundWeapons,item=>item.Profile=="px4");
            Need(guest.RequestLoot(claim.Id,1,false,claim.Position),"guest sends an ordinary ground pickup");
            tick(8);
            Need(host.TryReadAmmo(guest.OwnActor,claim.Id,out mag,out reserve)&&mag==7,"original magazine survives normal pickup");
            while(pair.Now<droppedAt+59.8)tick(1);
            Need(host.TryReadLoot(groundWeapons[0].Id,out copy),"death weapon still tracked before 60 seconds");
            R24LanFoodState expiryFood;
            Need(guest.TryReadFood(groundFood.Id,out expiryFood)&&!expiryFood.Consumed,"ground food available before expiry");
            tick(24);
            Need(guest.TryReadFood(groundFood.Id,out expiryFood)&&expiryFood.Consumed,"unclaimed food expired on guest");
            R24LanGrenadeState expiryGrenade;
            Need(guest.TryReadGrenade(groundGrenade.Id,out expiryGrenade)&&expiryGrenade.Spent,"unclaimed grenade expired on guest");
            Need(guest.TryReadLoot(claim.Id,out copy)&&copy.Owner==guest.OwnActor,"expiry preserves a recovered weapon");
            var unclaimed=Array.Find(groundWeapons,item=>item.Profile=="ump");
            Need(guest.TryReadLoot(unclaimed.Id,out copy)&&copy.Owner!="","unclaimed weapon expired on guest");
            R24LanSupplyState supply;
            Need(guest.TryReadSharedSupply(groundSupplies[0].Id,out supply)&&supply.Collector!="","unclaimed supplies expired on guest");
            Console.WriteLine(preset+" PASS beforeSpawn="+before.SpawnRevision+" afterSpawn="+after.SpawnRevision+" deaths="+after.Deaths);
        }
    }
    static void DeathDropCases()
    {
        VerifyDeathDrops("free_for_all");
        VerifyDeathDrops("team_respawn");
        VerifyDeathDrops("team_revive");
    }
}
