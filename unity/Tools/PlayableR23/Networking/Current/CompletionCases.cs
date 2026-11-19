using System;using System.Security.Cryptography;using Ashfall.Integration.Playable.TeamLaunch;using Ashfall.Modules.Flight;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases {
 static void SharedWeaponLoot(){using(var pair=new Pair()){
  for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
  string hash=new string('b',64);var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f);StartRound(pair,new HazardWorld(),rules,hash);
  for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
  Need(!pair.Client.RegisterWorldWeapon("forged","awp",5,new Float3(0,1,0)),"Client cannot register rarity or ammunition");
  Need(pair.Host.RegisterWorldWeapon("shared/awp","awp",3,new Float3(8,1,0)),"Host registers genuine world instance");
  Need(!pair.Host.RegisterWorldWeapon("shared/awp","awp",5,new Float3(8,1,0)),"Duplicate lifetime registration refused");
  Need(!pair.Host.RequestLoot("shared/awp",1,false,new Float3()),"Full inventory cannot gain a fourth weapon");
  Need(pair.Host.RequestLoot("cx4",1,true,new Float3(8,1,0)),"Initial loadout dropped with its host-owned magazine");
  string dropped=pair.Host.OwnActor+"/cx4";int loaded,reserve;
  Need(!pair.Host.TryReadAmmo(pair.Host.OwnActor,"cx4",out loaded,out reserve),"Drop revokes firing custody");
  Need(!pair.Host.SubmitShot("cx4",0,new Float3(8,1,0),new[]{new Float3(0,1,0)}),"Dropped original action cannot fire");
  Need(pair.Host.RequestLoot("shared/awp",1,false,new Float3()),"Host collects rare weapon after freeing a slot");
  Need(!pair.Host.RequestLoot("shared/awp",1,false,new Float3()),"Duplicate collect refused");
  for(int i=0;i<5;i++)KeepRaw(pair,hash,0,8);R24LanLootState state;
  Need(pair.Client.TryReadLoot("shared/awp",out state)&&state.Owner==pair.Host.OwnActor&&state.Stars==3,"Shared owner/rarity receipt delivered");
  pair.Client.RequestLoot(dropped,1,false,new Float3());for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.TryReadLoot(dropped,out state)&&state.Owner=="","Remote pickup outside validated reach rejected");
  var muzzle=new Float3(8,1.5f,0);Need(pair.Host.SubmitShot("awp",0,muzzle,new[]{new Float3(0,1,0)},"shared/awp"),"Collected instance fires using host rarity and ammo");
  Need(pair.Host.TryReadAmmo(pair.Host.OwnActor,"shared/awp",out loaded,out reserve),"Read collected ammo");int remaining=loaded;
  Need(pair.Host.RequestLoot("shared/awp",1,true,new Float3(8,1,0)),"Used instance drops");Need(pair.Host.RequestLoot("shared/awp",1,false,new Float3()),"Same instance reacquired");
  Need(pair.Host.TryReadAmmo(pair.Host.OwnActor,"shared/awp",out loaded,out reserve)&&loaded==remaining,"Drop/reacquire preserves consumed magazine");
  var valid=new R24LanShot("awp",0,pair.Host.EstimatedHostTime,muzzle,new[]{new Float3(0,1,0)},"shared/awp");R24LanShot decoded;Need(R24LanShot.TryDecode(valid.Encode(),out decoded)&&decoded.Instance=="shared/awp","Wire binds the actual instance");
 }}
 static void SharedSuppliesAndFood(){using(var pair=new Pair()){BindFood(pair);
  string hash=new string('b',64);var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0);StartRound(pair,new HazardWorld(),rules,hash);
  Action raw=delegate{pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0,7,rx:0,ry:1.65f,rz:0));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};for(int i=0;i<4;i++)raw();
  Need(pair.Host.RegisterSharedSupply("ammo/rifle","rifle",32,new Float3(8,1.5f,0)),"Host registered finite ammo box");
  Need(!pair.Client.RegisterSharedSupply("cheat","rifle",999,new Float3()),"Guest cannot register supply");
  Need(pair.Host.RequestSupply("ammo/rifle",1),"Reachable host supply collected");Need(!pair.Host.RequestSupply("ammo/rifle",1),"Supply collected once");int loaded,reserve;Need(pair.Host.TryReadAmmo(pair.Host.OwnActor,"cx4",out loaded,out reserve)&&reserve==2432,"Shared grant credits the host's actual firing reserve");
  Need(pair.Host.RegisterSharedSupply("bag","backpack",1,new Float3(8,1.5f,0))&&pair.Host.RequestSupply("bag",1),"Host capacity upgrade");
  Need(pair.Host.RegisterSharedFood("food/shaker","shaker",new Float3(8,1.5f,0)),"Food registered by host");Need(pair.Host.RequestFood("food/shaker",1,0,new Float3()),"Upgraded fourth slot admits food");
  Need(!pair.Host.RequestFood("food/shaker",1,1,new Float3()),"No immediate unprepared consumption");
  for(int i=0;i<130;i++)raw();Need(!pair.Host.RequestFood("food/shaker",1,1,new Float3()),"Time alone cannot prepare a shaker");PrepareFood(pair,true,"food/shaker","shaker");Need(ConsumeFoodPrepared(pair,true,"food/shaker"),"Owned, timed, mouth-contact shaker accepted");Need(Math.Abs(Health(pair.Host,pair.Host.OwnActor).Shield-50)<.01f,"Host applies real capped shield gain");Need(!pair.Host.RequestFood("food/shaker",1,1,new Float3()),"Consumption replay cannot apply a second gain");
  Need(pair.Host.RegisterSharedFood("food/soda","soda",new Float3(8,1.5f,0))&&pair.Host.RequestFood("food/soda",1,0,new Float3()),"Shared soda collected");for(int i=0;i<23;i++)raw();PrepareFood(pair,true,"food/soda","soda");Need(ConsumeFoodPrepared(pair,true,"food/soda"),"Soda starts host dose");for(int i=0;i<94;i++)raw();float shield=Health(pair.Host,pair.Host.OwnActor).Shield;Need(shield>74&&shield<76,"Soda runs at source 5 shield per second ("+shield+", dose="+pair.Host.PendingSharedSoda(pair.Host.OwnActor)+", host="+pair.Host.LastGainRejection+", life="+Health(pair.Host,pair.Host.OwnActor).Life+", elapsed="+pair.Host.RoundElapsed+")");for(int i=0;i<110;i++)raw();Need(Math.Abs(Health(pair.Host,pair.Host.OwnActor).Shield-100)<.01f,"Finite dose capped at100");
  R24LanFoodState food;R24LanSupplyState supply;for(int i=0;i<8;i++)raw();Need(pair.Client.TryReadFood("food/soda",out food)&&food.Consumed&&food.Owner==pair.Host.OwnActor,"Guest receives consumed ownership tombstone");Need(pair.Client.TryReadSharedSupply("ammo/rifle",out supply)&&supply.Collector==pair.Host.OwnActor,"Guest receives shared ammo outcome");
 }}
 static void GainReceiptWindow(){var ports=new ReceiptPorts();var health=new HealthService("gain",0,0,true,ports,ports,ports,60,5,0);Need(health.RegisterConfiguredSpawn(new CommandContext("gain","host","spawn",1,0,true),"p",-1,false,100,0).Accepted,"Gain fixture spawn");CommandContext old=new CommandContext();
  for(int i=2;i<9202;i++){ports.Time+=.05;var c=new CommandContext("gain","p","gain/"+i,i,0,true);if(i==10)old=c;Need(health.Synchronize(c).Accepted,"Advance gain receipt watermark");var h=health.Read("p");HealthService.PreparedGain gain;Need(health.PrepareGain(c,c.CommandId,"p",h.Revision,h.LifeGeneration,h.ConnectionGeneration,0,.001f,out gain),"Gain preparation past old 4096 cap");Need(gain.Validate(),"Gain validates");gain.CommitValidated();gain.Abort();health.DrainTransitions();}
  var checkpointHost=new CommandContext("gain","host","checkpoint",9300,0,true);var binding=new string('b',64);byte[] bytes=health.CreateCheckpoint(checkpointHost,binding);string digest;using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();HealthService restored;Need(HealthService.TryRestoreCheckpoint(checkpointHost,binding,digest,bytes,0,true,ports,ports,ports,out restored,60,5,0),"Long gain ledger restores with original logical receipts");
  var state=health.Read("p");HealthService.PreparedGain replay;Need(!health.PrepareGain(old,old.CommandId,"p",state.Revision,state.LifeGeneration,state.ConnectionGeneration,0,5,out replay),"Old gain replay refused");var fresh=new CommandContext("gain","p","fresh-retry",9300,0,true);Need(!health.PrepareGain(fresh,old.CommandId,"p",state.Revision,state.LifeGeneration,state.ConnectionGeneration,0,5,out replay)&&health.FindGain(fresh,old.CommandId,"p")!=null,"Logical gain identity survives command pruning");
 }

 static void RemoteLootAndMalformed(){using(var pair=new Pair()){BindFood(pair);
  string hash=new string('b',64);var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0);StartRound(pair,new HazardWorld(),rules,hash);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.RegisterSharedSupply("guest-box","pistol",16,new Float3(0,1.2f,0)),"Register guest ammo");Need(pair.Client.RequestSupply("guest-box",1),"Guest queues actual command");for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);int loaded,reserve;Need(pair.Host.TryReadAmmo(pair.Client.OwnActor,"px4",out loaded,out reserve)&&reserve==1216,"Guest supply command reaches host reserve");
  Need(pair.Host.RegisterSharedSupply("guest-bag","backpack",1,new Float3(0,1.2f,0)),"Guest backpack registered");pair.Client.RequestSupply("guest-bag",1);for(int i=0;i<5;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.RegisterSharedFood("guest-soda","soda",new Float3(0,1.2f,0)),"Guest food registered");pair.Client.RequestFood("guest-soda",1,0,new Float3());for(int i=0;i<25;i++)KeepRaw(pair,hash,0,8);
  PrepareFood(pair,false,"guest-soda","soda");Need(ConsumeFoodPrepared(pair,false,"guest-soda"),"Guest source gesture validates before consumption");for(int i=0;i<12;i++)KeepRaw(pair,hash,0,8);Need(Health(pair.Host,pair.Client.OwnActor).Shield>0,"Guest consumed command starts actual host Health dose");
  long badSequence=1000;foreach(string feature in new[]{"playable.loot.command/1","playable.supply.collect/1","playable.food.command/1"}){pair.HostPipe.Inbox.Enqueue(new Frame{Peer="client",Bytes=Wire.Encode(new Packet{Protocol=Protocol.Name,Schema=Protocol.Schema,Kind=PacketKind.Command,FeatureId=feature,ActorId=pair.Client.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=badSequence++,BaseSequence=-1,Payload=new byte[]{255,255}})});pair.Tick();}
  Need(pair.Host.State==R24LanLobbyState.Hosting&&pair.Client.State==R24LanLobbyState.Connected,"Malformed gameplay text cannot tear down either peer");
  foreach(string feature in new[]{"playable.loot/1","playable.supply/1","playable.food/1","playable.ammo/1","playable.food.reset/1","playable.resources/1","playable.grenade/1","playable.projectile/1","playable.build.panel/1"}){pair.ClientPipe.Inbox.Enqueue(new Frame{Peer="server",Bytes=Wire.Encode(new Packet{Protocol=Protocol.Name,Schema=Protocol.Schema,Kind=PacketKind.Snapshot,FeatureId=feature,ActorId=pair.Host.OwnActor,SessionId=pair.Host.SessionId,ContentHash=new string('a',64),Epoch=pair.Host.Epoch,Sequence=1000,BaseSequence=-1,Payload=new byte[]{255,255}})});pair.Tick();}
  Need(pair.Client.State==R24LanLobbyState.Connected,"Malformed host snapshots are refused without escaping the pump");
  pair.ClientPipe.Close("server");R24LanLootState returned;Need(pair.Host.TryReadLoot(pair.Client.OwnActor+"/cx4",out returned)&&returned.Owner=="","Departed actor starting weapon returns to shared world custody");Need(pair.Client.SharedFoods.Length==0&&pair.Client.SharedSupplies.Length==0&&pair.Client.LootStates.Length==0,"Disconnect revokes all shared presentation receipts");
 }}

 static void AdoptPodPresentation(){var runtime=new R24TeamLaunchRuntime(1);Need(runtime.Configure(new R24LaunchMap{Id="archive:alpha",Minimum=new Float3(-100,0,-100),Maximum=new Float3(100,0,100),Top=20,OriginY=0},new R24LaunchRules{Live=true,Mode="revive",Stage="match",Teams=2,Epoch=9}),"Actual presentation layout");
  var start=runtime.Layout.Pod(0,0).Origin;var plan=new CapsulePlan(runtime.Layout.PodId(0,0),0,0,start,new Float3(start.X,start.Y+5,start.Z-10),new Float3(start.X+10,start.Y+5,start.Z-10),new Float3(start.X+10,0,start.Z-10),new Float3(start.X+10,0,start.Z-12),new Float3(0,0,-1),4);
  Need(runtime.AdoptHostRide("player",plan,2,0,plan.Start),"Guest adopts host ride into existing presentation owner");var ride=runtime.ReadRide("player");Need(ride!=null&&ride.Plan==plan&&ride.State==R24LaunchRideState.Flying,"Guest controls can see actual flying state");
  Need(runtime.AdoptHostRide("player",plan,2,2,plan.ExitTarget)&&Object.ReferenceEquals(ride,runtime.ReadRide("player"))&&ride.State==R24LaunchRideState.Landed,"Terminal receipt updates same ride, stops flying controls");Need(!runtime.AdoptHostRide("player",plan,2,9,plan.ExitTarget),"Malformed phase cannot become presentation state");
 }

}
