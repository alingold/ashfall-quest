using System;using Ashfall.Core;using Ashfall.Modules.Construction;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases {
 sealed class BuildWorld:IConstructionWorld {internal bool Block,FloorNeedsSupport;public bool HasStaticContact(Panel p,float tolerance){return !FloorNeedsSupport||p.Kind=="wall";}public bool IsBuried(Panel p){return false;}public string Clearance(Panel p,string actor){return Block?"blocked":"";}public bool HasLineOfSight(string actor,Float3 a,Float3 b){return true;}}
 sealed class DropWorld:IR24LanSupplyDropWorld {internal bool Refuse;public bool TryPlace(string family,Float3 desired,out Float3 accepted){accepted=desired;return !Refuse;}}
 static void ResourceInventoryEquality(){
  var ordered=new System.Collections.Generic.Dictionary<string,int>{{"rifle",32},{"shotgun",8}};
  var reversed=new System.Collections.Generic.Dictionary<string,int>{{"shotgun",8},{"rifle",32}};
  var baseline=new R24LanResourcesState("p",1,1,0,6,3,ordered);
  Need(baseline.SameInventory(new R24LanResourcesState("p",1,2,1,6,3,reversed)),"Resource inventory equality ignores insertion order and receipt revisions");
  Need(!baseline.SameInventory(new R24LanResourcesState("p",1,2,1,5,3,ordered)),"Build debit requires resource reconciliation");
  Need(!baseline.SameInventory(new R24LanResourcesState("p",1,2,1,6,4,ordered)),"Capacity change requires resource reconciliation");
  reversed["rifle"]=31;Need(!baseline.SameInventory(new R24LanResourcesState("p",1,2,1,6,3,reversed)),"Reserve change requires resource reconciliation");
  reversed.Remove("rifle");Need(!baseline.SameInventory(new R24LanResourcesState("p",1,2,1,6,3,reversed)),"Missing reserve family cannot compare equal");
  reversed["pistol"]=32;Need(!baseline.SameInventory(new R24LanResourcesState("p",1,2,1,6,3,reversed)),"Different same-sized reserve families cannot compare equal");
 }
 static void SharedSnapshotIsolation(){using(var pair=new Pair()){
  string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.RegisterSharedSupply("snapshot/one","rifle",32,new Float3(0,1,0)),"Host registers snapshot fixture");
  var before=pair.Host.SharedSupplies;var id=before[0].Id;before[0]=null;
  Need(pair.Host.SharedSupplies[0].Id==id,"Mutating a caller snapshot cannot replace authoritative supply state");
  var retained=pair.Host.SharedSupplies;Need(pair.Host.RegisterSharedSupply("snapshot/two","rifle",32,new Float3(1,1,0)),"Host registers second snapshot fixture");
  Need(retained.Length==1&&retained[0].Id==id&&pair.Host.SharedSupplies.Length==2,"Retained snapshot remains stable while authoritative collection grows");
 }}
 // 2026-11-09: guests dropped every axis-2 (Z-facing) wall, including the automatic support wall under an unsupported floor.
 static void SupportWallReplication(){using(var pair=new Pair()){
  var world=new BuildWorld();world.FloorNeedsSupport=true;for(int i=0;i<5;i++)pair.Tick();pair.Host.BindConstructionWorld(world);pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return range;});string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);
  Action raw=delegate{pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0,7,rx:1.5f,ry:1.2f,rz:1.5f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};for(int i=0;i<4;i++)raw();
  Need(pair.Host.RequestBuild(1),"Unsupported floor is placed together with its automatic support wall: "+pair.Host.LastBuildRejection);for(int i=0;i<6;i++)raw();
  int zWalls=0,floors=0;foreach(var p in pair.Host.SharedPanels){if(p.Shape.Kind=="wall"&&p.Shape.Axis==2)zWalls++;if(p.Shape.Kind=="floor"&&p.Shape.Axis==1)floors++;}
  Need(zWalls==1&&floors==1&&pair.Host.SharedPanels.Length==2,"Host plan is one floor plus one Z-facing support wall, got "+pair.Host.SharedPanels.Length);
  Need(pair.Client.SharedPanels.Length==2,"Guest receives Z-facing (axis 2) walls, got "+pair.Client.SharedPanels.Length+" of 2 panels");
  for(int i=0;i<2;i++)Need(pair.Client.SharedPanels[i].Id==pair.Host.SharedPanels[i].Id&&pair.Client.SharedPanels[i].Shape.Axis==pair.Host.SharedPanels[i].Shape.Axis&&pair.Client.SharedPanels[i].Shape.Kind==pair.Host.SharedPanels[i].Shape.Kind&&pair.Client.SharedPanels[i].Shape.Center.Equals(pair.Host.SharedPanels[i].Shape.Center),"Guest panel "+i+" matches the host shape exactly");
 }}
 static void ConstructionAndResources(){ResourceInventoryEquality();SharedSnapshotIsolation();SupportWallReplication();using(var pair=new Pair()){
  var world=new BuildWorld();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindConstructionWorld(world);pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return range;});string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);
  Action raw=delegate{pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0,7,rx:1.5f,ry:1.2f,rz:1.5f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};for(int i=0;i<4;i++)raw();
  Need(pair.Host.RequestBuild(1),"Actual host raw hand requests source CandidateService placement: "+pair.Host.LastBuildRejection);Need(pair.Host.SharedPanels.Length>0,"Host owns real supported construction");var first=pair.Host.SharedPanels[0];R24LanResourcesState resources;for(int i=0;i<5;i++)raw();Need(pair.Host.TryReadOwnResources(out resources)&&resources.Builds<999,"Committed source panel debits finite host build charges");int after=resources.Builds;Need(pair.Client.SharedPanels.Length==pair.Host.SharedPanels.Length,"Guest receives actual shared panel identities");
  world.Block=true;Need(!pair.Host.RequestBuild(1),"Host world clearance, not preview, vetoes construction");for(int i=0;i<3;i++)raw();pair.Host.TryReadOwnResources(out resources);Need(resources.Builds==after,"Refused construction cannot spend charges");world.Block=false;
  float health=first.Health;Need(pair.Host.SubmitShot("cx4",0,new Float3(first.Shape.Center.X,1.5f,first.Shape.Center.Z),new[]{new Float3(0,-1,0)}),"Actual host firearm ray reaches shared panel");for(int i=0;i<5;i++)raw();Need(pair.Host.SharedPanels[0].Health<health&&pair.Client.SharedPanels[0].Health==pair.Host.SharedPanels[0].Health,"Shared cover damage is host-owned and replicated");
 }
 using(var pair=new Pair()){
  var world=new DropWorld();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindSupplyDropWorld(world);string hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false),hash);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);string id;Need(pair.Client.RequestSupplyDrop("rifle",1,new Float3(0,1.2f,.3f),out id),"Guest reserve-box drop command queued");for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);R24LanSupplyState box;Need(pair.Host.TryReadSharedSupply(id,out box)&&box.Amount==32&&box.Collector=="","Host derives the finite box, amount and world placement");int loaded,reserve;Need(pair.Host.TryReadAmmo(pair.Client.OwnActor,"cx4",out loaded,out reserve)&&reserve==2368,"Host reserve was debited exactly once");Need(pair.Client.RequestSupply(id,1),"Same box can be collected normally");for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadAmmo(pair.Client.OwnActor,"cx4",out loaded,out reserve)&&reserve==2400,"Drop/recollect conserves shared ammunition");
  world.Refuse=true;pair.Client.RequestSupplyDrop("rifle",1,new Float3(0,1.2f,.3f),out id);for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);Need(!pair.Host.TryReadSharedSupply(id,out box)&&pair.Host.TryReadAmmo(pair.Client.OwnActor,"cx4",out loaded,out reserve)&&reserve==2400,"Refused actual floor/clearance makes no debit or box");world.Refuse=false;
  Need(pair.Host.RegisterSharedSupply("build/supply","builds",6,new Float3(0,1.2f,.3f)),"Host registers source build supply");pair.Client.RequestSupply("build/supply",1);for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);R24LanResourcesState resources;Need(pair.Client.TryReadOwnResources(out resources)&&resources.Builds==1005,"Shared build pickup credits actual host balance");pair.Client.RequestSupplyDrop("builds",1,new Float3(0,1.2f,.3f),out id);for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);Need(pair.Client.TryReadOwnResources(out resources)&&resources.Builds==999&&pair.Client.TryReadSharedSupply(id,out box)&&box.Amount==6,"Finite shared build reserve drop conserves charge");
  Need(pair.Host.RegisterSharedSupply("capacity/one","backpack",1,new Float3(0,1.2f,.3f)),"Host adds one finite backpack");pair.Client.RequestSupply("capacity/one",1);for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);Need(pair.Client.TryReadOwnResources(out resources)&&resources.Capacity==4,"Host owns fourth slot");pair.Client.RequestSupplyDrop("backpack",1,new Float3(0,1.2f,.3f),out id);for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);Need(pair.Client.TryReadOwnResources(out resources)&&resources.Capacity==3&&pair.Client.TryReadSharedSupply(id,out box)&&box.Amount==1,"Backpack drop debits host capacity rather than minting another slot");pair.Client.RequestSupply(id,1);for(int i=0;i<8;i++)KeepRaw(pair,hash,0,8);Need(pair.Client.TryReadOwnResources(out resources)&&resources.Capacity==4,"Backpack recollect conserves exactly one capacity unit");

 }}
}
