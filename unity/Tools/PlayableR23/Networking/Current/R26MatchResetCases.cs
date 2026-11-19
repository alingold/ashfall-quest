using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;

namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanLobby {
  // Fixture preconditions only. Assertions consume the authenticated public receipts.
  internal void FixtureDirty(){
   foreach(var actor in combat){
    var owner=actor.Value;foreach(var gun in owner.Weapons.Values)gun.Ammo=0;
    owner.Capacity=5;owner.Reserves["smg"]=3;owner.Weapons["old-sidearm"]=new CombatWeapon{Profile="px4",Family="pistol",Stars=1,Capacity=12,Ammo=0};
    PublishFood(new R24LanFoodState(actor.Key+"/old-food","banana",actor.Key,new Float3(),false,++foodSequence));
    PublishGrenade(actor.Key+"/old-grenade","frag",actor.Key,new Float3(),false);
   }
  }
 }
}
partial class LobbyCases {
 sealed class ResetWorld:IR24LanSpawnWorld {
  public bool ReadyForSpawn(string map,bool warmup){return true;}
  public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("a",new Float3(0,.12f,0),-1,true,false),new SpawnSite("b",new Float3(8,.12f,0),-1,true,false)};}
  public bool IsSafe(string map,string actor,Float3 p){return p.IsFinite;}
 }
 static void MatchResetCases(){
  using(var p=new Pair()){
   string hash=new string('c',64);var host=p.Host;var observers=new[]{host,p.Client};
   var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("loadout","ump").With("protection",0f).With("respawn_delay",1f).With("time_limit",0).With("score_limit",0);
   Action<int> tick=delegate(int n){for(int i=0;i<n;i++)p.Tick();};
   tick(6);host.BindSpawnWorld(new ResetWorld());
   for(int phase=0;phase<5;phase++){
    bool warm=phase==0||phase==2||phase==4;string map=phase==4?"cyber_bird":"range";var r=rules.WithMap(map);if(phase==4)r=r.With("loadout","pistols");
    if(phase==2){Need(host.HostFinishMatchWithCelebration(),"finish");p.Now+=10.5;tick(3);Need(host.SelectedMap.Rules==null,"returned to warmup");}
    else Need(warm?host.BeginMapSelection(map,hash,r):host.BeginRoundSelection(r,hash,1),"begin phase");
    Need(host.ReportMapLoaded(hash),"host loaded");tick(4);Need(p.Client.ReportMapLoaded(hash),"guest loaded");tick(5);
    if(!warm){p.Now=host.SelectedMap.StartsAt+.1;tick(2);}
    Need(p.Client.RequestSpawn(),"guest placement");tick(5);Need(host.RequestSpawn(),"host placement");tick(18);
    foreach(var o in observers){
     var guns=new List<R26OwnedWeaponReceipt>();o.CopyOwnWeaponReceipts(guns);R24LanResourcesState res;R24LanFoodState food;R24LanGrenadeState grenade;bool ownsFood=o.TryReadFood(o.OwnActor+"/old-food",out food)&&food.Owner==o.OwnActor;bool ownsGrenade=o.TryReadGrenade(o.OwnActor+"/old-grenade",out grenade)&&grenade.Owner==o.OwnActor;
     Need(guns.Count==r.StartingCatalog.Length,"fresh configured slots");
     foreach(int index in r.StartingCatalog){string profile=R24LanLoadout.Profile(index);var gun=guns.Find(w=>w.Profile==profile);Need(gun!=null&&gun.Loaded==R24LanLoadout.Capacity(profile,1),"fresh full configured magazine");}
     Need(o.TryReadOwnResources(out res)&&res.Epoch==o.Epoch&&res.Capacity==3&&res.Reserves["smg"]==1200,"fresh bag and reserves");Need(!ownsFood&&!ownsGrenade,"old food and grenades retired");
    }
    Console.WriteLine("fresh host and guest epoch="+host.Epoch+" map="+map+" warmup="+warm+" loadout="+r.Loadout);
    if(phase==1){
     for(int n=1;n<=36;n++){
      foreach(var m in host.Members){R24LanHealthState h;Need(host.TryReadHealth(m.Actor,out h)&&host.ApplyHostHealthFall(m.Actor,h.Revision).Accepted,"authoritative fall");}
      tick(4);foreach(var o in observers)foreach(var m in host.Members){R24LanHealthState h;Need(o.TryReadHealth(m.Actor,out h)&&h.Life==LifeState.Dead&&h.Deaths==n,"replicated death");}
      p.Now+=1.1;tick(10);
      foreach(var o in observers)foreach(var m in host.Members){R24LanHealthState h;R24LanSpawn s;Need(o.TryReadHealth(m.Actor,out h)&&h.Life==LifeState.Alive&&h.Deaths==n&&o.TryReadSpawn(m.Actor,out s)&&s.Revision==n+1&&s.Epoch==host.Epoch,"replicated joint respawn");}
     }
     Console.WriteLine("36 complete death and respawn cycles per actor, 72 deaths, spawn revision 37 on both peers");
    }
    host.FixtureDirty();tick(16);
    foreach(var o in observers){var guns=new List<R26OwnedWeaponReceipt>();o.CopyOwnWeaponReceipts(guns);R24LanResourcesState res;R24LanFoodState food;R24LanGrenadeState grenade;bool ownsFood=o.TryReadFood(o.OwnActor+"/old-food",out food)&&food.Owner==o.OwnActor;bool ownsGrenade=o.TryReadGrenade(o.OwnActor+"/old-grenade",out grenade)&&grenade.Owner==o.OwnActor;
     Need(guns.Count==r.StartingCatalog.Length+1&&guns.TrueForAll(g=>g.Loaded==0)&&o.TryReadOwnResources(out res)&&res.Capacity==5&&res.Reserves["smg"]==3&&ownsFood&&ownsGrenade,"verified contaminated inventory");}
   }
   Console.WriteLine("MATCHRESET PASS");
  }
 }
}
