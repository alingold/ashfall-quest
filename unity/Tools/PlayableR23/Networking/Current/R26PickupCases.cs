using System;using Ashfall.Core;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
// MP-PICKUP (playtest 2026-11-15) through the production lobby + simulated transport.
partial class LobbyCases {
 static void R26Setup(Pair pair,string hash){for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f),hash);for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);}
 internal static void R26Run(){R26FullInventorySwap();R26RejectionLog();R26BackpackCap();R26ReleaseShape();Console.WriteLine("PASS "+checks+" MP-PICKUP lobby observations");}
 static void R26FullInventorySwap(){using(var pair=new Pair()){string hash=new string('b',64);R26Setup(pair,hash);R24LanLootState s;string why;
  Need(pair.Host.RegisterWorldWeapon("r26/awp","awp",3,new Float3(.3f,1.2f,.4f)),"Host world gun beside the guest");for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);
  // Reproduction: the default {0,2,9} loadout fills all three starting slots; the plain collect the
  // client sent before the fix (make_room only after the grant) is always refused.
  pair.Client.RequestLoot("r26/awp",1,false,new Float3());for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.TryReadLoot("r26/awp",out s)&&s.Owner=="","BEFORE: full default loadout refuses the guest collect");Need(pair.Host.LastPickupRejection=="slots-full","BEFORE: refusal reason is the free-slot rule");
  // Fix contract: the held weapon's drop is queued ahead of the collect on the ordered lane.
  long before=pair.Client.CommandSequence;Need(pair.Client.RequestLoot("cx4",1,true,new Float3(.2f,1f,.3f))&&pair.Client.RequestLoot("r26/awp",1,false,new Float3()),"Swap queued in one frame");Need(pair.Client.CommandSequence==before+2,"Two ordered commands mark the queued make-room");
  for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.TryReadLoot("r26/awp",out s)&&s.Owner==pair.Client.OwnActor,"AFTER: collect admitted once the queued drop freed the slot");Need(pair.Host.TryReadLoot(pair.Client.OwnActor+"/cx4",out s)&&s.Owner=="","AFTER: swapped weapon is free in the world");
  Need(pair.Client.TryReadLoot("r26/awp",out s)&&s.Owner==pair.Client.OwnActor&&pair.Client.TryReadLoot(pair.Client.OwnActor+"/cx4",out s)&&s.Owner=="","Both receipts reach the guest");
  // Host's own swap: preview refuses on slots only, so the root may make room first.
  Need(pair.Host.RegisterWorldWeapon("r26/host","awp",2,new Float3(8.3f,1.2f,.4f)),"Host world gun beside the host");for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);
  Need(!pair.Host.PreviewOwnLoot("r26/host",1,false,out why)&&why=="slots-full","Host full loadout previews the slot refusal");Need(pair.Host.PreviewOwnLoot("r26/host",1,true,out why),"Ignoring slots the host collect is admissible");
  Need(!pair.Host.PreviewOwnLoot("r26/awp",1,true,out why)&&why=="claimed","A claimed gun is never a swap target");Need(!pair.Client.PreviewOwnLoot("r26/host",1,true,out why)&&why=="not-host","Guests cannot preview host admission");
  Need(pair.Host.RequestLoot("cx4",1,true,new Float3(8.2f,1f,.3f))&&pair.Host.RequestLoot("r26/host",1,false,new Float3()),"Host inline swap: drop then collect");
  Need(pair.Host.TryReadLoot("r26/host",out s)&&s.Owner==pair.Host.OwnActor,"Host owns the swapped-in gun");
 }}
 static void R26RejectionLog(){using(var pair=new Pair()){string hash=new string('b',64);R26Setup(pair,hash);int lines=0;string last=null;pair.Host.PickupRejectionLog=delegate(string line){lines++;last=line;};
  Need(pair.Host.RegisterWorldWeapon("r26/log","awp",3,new Float3(.3f,1.2f,.4f)),"Logged gun");for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);int rejected=pair.Host.RejectedPickups;
  for(int i=0;i<5;i++){pair.Client.RequestLoot("r26/log",1,false,new Float3());KeepRaw(pair,hash,0,8);}for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.RejectedPickups==rejected+5,"Every refusal is counted");Need(lines==1&&last.Contains("slots-full")&&last.Contains("r26/log"),"At most one development line per item per second: "+lines);
  for(int i=0;i<20;i++)KeepRaw(pair,hash,0,8);pair.Client.RequestLoot("r26/log",1,false,new Float3());for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);Need(lines==2,"Logged again after a second");
  pair.Host.PickupRejectionLog=null;pair.Client.RequestLoot("r26/log",1,false,new Float3());for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);Need(lines==2&&pair.Host.RejectedPickups==rejected+7,"No sink: counted only");
 }}
 static void R26BackpackCap(){using(var pair=new Pair()){string hash=new string('b',64);R26Setup(pair,hash);R24LanSupplyState b;
  for(int i=0;i<4;i++)Need(pair.Host.RegisterSharedSupply("r26/bag"+i,"backpack",1,new Float3(.3f,1.2f,.4f)),"Backpack "+i);for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);
  for(int i=0;i<3;i++){pair.Client.RequestSupply("r26/bag"+i,1);for(int k=0;k<4;k++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadSharedSupply("r26/bag"+i,out b)&&b.Collector==pair.Client.OwnActor,"Backpack collected "+i);}
  pair.Client.RequestSupply("r26/bag3",1);for(int k=0;k<4;k++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadSharedSupply("r26/bag3",out b)&&b.Collector=="","Six-slot cap refuses a fourth backpack");Need(pair.Host.LastPickupRejection=="capacity-max","Cap refusal reason (guest greys backpacks at capacity 6)");
  R24LanLootState s;Need(pair.Host.RegisterWorldWeapon("r26/after","awp",3,new Float3(.3f,1.2f,.4f)),"Gun after backpacks");for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);pair.Client.RequestLoot("r26/after",1,false,new Float3());for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
  Need(pair.Host.TryReadLoot("r26/after",out s)&&s.Owner==pair.Client.OwnActor,"Playtest: a backpack frees room so a plain collect then works");
 }}
 static void R26ReleaseShape(){using(var pair=new Pair()){string hash=new string('b',64);R26Setup(pair,hash);R24LanLootState s;
  Need(pair.Host.RegisterWorldWeapon("r26/rel","awp",3,new Float3(.3f,1.2f,.4f))&&pair.Host.RegisterWorldWeapon("r26/far","awp",3,new Float3(.3f,1.2f,.4f)),"Release guns");for(int i=0;i<2;i++)KeepRaw(pair,hash,0,8);
  pair.Client.RequestLoot("cx4",1,true,new Float3(.2f,1f,.3f));pair.Client.RequestLoot("r26/rel",1,false,new Float3());for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadLoot("r26/rel",out s)&&s.Owner==pair.Client.OwnActor,"Granted claim (never taken locally)");
  // Abandoned-grant release: the session sends an ordinary drop at the item's own point (within 3 m).
  pair.Client.RequestLoot("r26/rel",1,true,s.Position);for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadLoot("r26/rel",out s)&&s.Owner==""&&Math.Abs(s.Position.Y-1.2f)<.001f,"Released back to its own point; visible to everyone again");
  pair.Client.RequestLoot("r26/far",1,false,new Float3());for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadLoot("r26/far",out s)&&s.Owner==pair.Client.OwnActor,"The released slot is usable again");
  // Feet fallback when the claimant walked away: root + 0.3 m is inside the host drop reach.
  pair.Client.RequestLoot("r26/far",1,true,new Float3(0,.3f,0));for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.TryReadLoot("r26/far",out s)&&s.Owner=="","Feet release admitted");
  pair.Client.RequestLoot("r26/rel",1,true,new Float3(0,.3f,0));for(int i=0;i<3;i++)KeepRaw(pair,hash,0,8);Need(pair.Host.LastPickupRejection=="not-owned","A release never touches another custody");
 }}
}
