#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Security.Cryptography;
using Ashfall.Core;using Ashfall.Modules.Player;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.Explosives;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.UI;using Ashfall.Integration.Playable.World.Loot;
using UnityEngine;
namespace Ashfall.Editor.Playable {
 // One authored sparse archive. No synthetic sites, collision, Health or inventory.
 public sealed class R24SparseWorldSmoke {
  public const string Candidate="archive:the-white-house-gods-osprey-kat";
  const string SourceSha="4ce15e966db225cc08e8512a9f4cdbf976a42ffbad8375c2972c93894d55e331";
  const string CacheSha="08ffae1a057b38dd242824c8ae50186bcb1d64d7c633577d92aabc8177a9b802";
  [Serializable]sealed class Receipt {public bool success,nativeExecuted,headsetVerified=false,positiveExercised,refused,cleanupRestored;public string candidate=Candidate,error="",refusal="";public int initial,fallback,sparse,total,threshold,lowerQueries,lowerHits,longestLowerChain,targetIndex=-1,weapons,grenades;public List<string> notes=new List<string>();public List<Vector3> prefixSample=new List<Vector3>(),suffixSample=new List<Vector3>();}
  Receipt receipt;R23PlayableLoop loop;Saved saved;float deadline,exerciseEnd;List<Float3> expected;long generation;
  public bool Passed{get;private set;}public string Json{get;private set;}
  static Float3 Up(Float3 p,float y){return new Float3(p.X,p.Y+y,p.Z);}static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static bool Near(Float3 a,Float3 b){return(V(a)-V(b)).sqrMagnitude<.002f*.002f;}
  static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}void Note(string value){receipt.notes.Add(value);Debug.Log("R24 SPARSE WORLD "+value);}void Refuse(string why){receipt.refused=true;receipt.refusal=why;Note("REFUSED positive coverage: "+why);}
  void Bound(){Need(Time.realtimeSinceStartup<deadline,"Sparse native helper exceeded120s including cleanup");}void Pump(){if(loop!=null)loop.SmokeUiUpdate();Physics.SyncTransforms();}
  static string Sha(string path){using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
  public IEnumerator Run(R23PlayableLoop actualRoot){
   receipt=new Receipt();loop=actualRoot;
   if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){Refuse("Actual ready XR-disabled Root required");Finish();yield break;}
   receipt.nativeExecuted=true;
   if(loop.MatchActive||loop.ActualMatchRules!=null||loop.UI.SelectedMatchRules!=null||loop.Gameplay.Health<=0){Refuse("Actual live Explore/healthy actor precondition required; no mode or Health forced");Finish();yield break;}
   saved=new Saved(loop);deadline=Time.realtimeSinceStartup+120;exerciseEnd=deadline-40;
   try{
    loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});
    var work=Drive(Exercise());while(work.MoveNext())yield return work.Current;
    var cleanup=Drive(Cleanup());while(cleanup.MoveNext())yield return cleanup.Current;
   }
   finally{try{if(loop!=null&&loop.Ready)loop.ReturnToLobby();}catch(Exception e){receipt.error+=" Final lobby: "+e;}saved.Restore(loop);Finish();}
  }
  IEnumerator Drive(IEnumerator work){var stack=new Stack<IEnumerator>();stack.Push(work);try{while(stack.Count>0){bool moved=false;object value=null;try{Bound();moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}catch(Exception e){receipt.error+=e.ToString();Note("FAIL "+e);yield break;}if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}}finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}}
  IEnumerator Exercise(){
   Note("BEFORE candidate bind: source initial candidates0; area threshold209; existing135chunks/10.35MB; recorded spawns24; authored hazards0");
   Need(Sha(Application.streamingAssetsPath+"/PlayableR23/Loot/caches.json")==CacheSha,"Prepared105 source cache data changed from bound zero-site candidate");
   loop.ReturnToLobby();Need(loop.UI.SetSelectedMap(Candidate),"Actual sparse archive unavailable in current catalog");loop.UI.OpenSpawnChoice();Need(loop.UI.ConfirmMapSpawn(true),"Actual Explore GAME SPAWN callback refused");
   while(Time.realtimeSinceStartup<exerciseEnd&&loop.Failure==null&&(!loop.Ready||loop.Map.MapId!=Candidate||loop.UI.Page!=R23UiPage.Playing)){Pump();yield return null;}
   Note("AFTER actual UI Explore load: map="+loop.Map.MapId+" ready="+loop.Ready+" failure="+loop.Failure);
   Need(loop.Ready&&loop.Map.MapId==Candidate&&!loop.MatchActive&&loop.ActualMatchRules==null&&loop.Map.SourceSha256==SourceSha,"Actual authored Explore source binding/load failed");
   while(Time.realtimeSinceStartup<exerciseEnd&&loop.ArchiveWorldFailure==null&&loop.WorldLoot.Failure==null&&(!loop.ArchiveWorldReady||loop.WorldLoot.PreparationPending||loop.WorldLoot.PreparedMapId!=Candidate)){Pump();yield return null;}
   Note("BEFORE native layout asserts: context="+loop.ArchiveWorldReady+" contextFailure="+loop.ArchiveWorldFailure+" pending="+loop.WorldLoot.PreparationPending+" lootFailure="+loop.WorldLoot.Failure+" mode="+loop.WorldLoot.PlacementMode+" initial/fallback/sparse="+loop.WorldLoot.InitialAcceptedSites+"/"+loop.WorldLoot.FallbackSites+"/"+loop.WorldLoot.SparseAddedSites);
   Need(loop.ArchiveWorldReady&&!loop.WorldLoot.PreparationPending&&loop.WorldLoot.Failure==null&&loop.WorldLoot.PreparedMapId==Candidate,"Actual shared archive plan/cache preparation failed");generation=loop.ArchiveWorldGeneration;
   var plan=loop.ArchiveWorldPlan;var info=loop.ArchiveWorldInfo;var actual=loop.WorldLoot.SourceCacheSitePositions;var stats=loop.WorldLoot.SparseDiagnostics;
   receipt.initial=loop.WorldLoot.InitialAcceptedSites;receipt.fallback=loop.WorldLoot.FallbackSites;receipt.sparse=loop.WorldLoot.SparseAddedSites;receipt.total=actual.Length;receipt.threshold=stats==null?0:stats.Threshold;
   Need(loop.WorldLoot.CacheSourceGeneration==generation&&info.Id==Candidate&&info.SourceSha256==SourceSha&&stats!=null,"Actual map generation/source/sparse receipt mismatch");
   Need(receipt.initial==0&&loop.WorldLoot.InitialRejectedFloor==0&&loop.WorldLoot.InitialRejectedOutside==0&&loop.WorldLoot.InitialRejectedHazard==0,"Pinned zero-source-site candidate unexpectedly changed initial order");
   expected=new List<Float3>();var loot=plan.Loot;for(int i=0;i<loot.Length;i+=3)expected.Add(loot[i]);int prefix=expected.Count;
   Note("BEFORE source prefix/positive checks: plan spawns="+plan.Spawns.Length+" loot="+loot.Length+" every3rd="+prefix+" threshold="+stats.Threshold+" added="+stats.Added);
   Need(receipt.fallback==prefix&&receipt.threshold==209&&receipt.total==receipt.initial+receipt.fallback+receipt.sparse&&receipt.sparse==stats.Added&&receipt.sparse<=160,"Actual source fallback/threshold/cap counts differ");
   Need(actual.Length>=prefix,"Actual fallback prefix truncated");for(int i=0;i<prefix;i++){Need(Near(actual[i],expected[i]),"Actual every-third native loot-point prefix order differs at"+i);if(i<8)receipt.prefixSample.Add(V(actual[i]));}
   if(receipt.sparse==0){Refuse("Authored native terrain produced no positive suffix; lowerQueries="+stats.LowerFloorQueries+" spacingRejects="+stats.RejectedSpacing+" safeRejects="+stats.RejectedSafeSpawn);yield break;}
   receipt.positiveExercised=true;yield return ReplaySparse(plan.Spawns,loot,info.BoundsMin,info.BoundsMax);
   Note("BEFORE native sparse replay comparison: expected="+expected.Count+" actual="+actual.Length+" floorQueries="+receipt.lowerQueries+" hits="+receipt.lowerHits+" longestChain="+receipt.longestLowerChain+" runtimeQueries="+stats.LowerFloorQueries);
   Need(expected.Count==actual.Length&&receipt.lowerQueries==stats.LowerFloorQueries&&receipt.lowerQueries>=loot.Length&&receipt.lowerQueries<=loot.Length*3,"Native source lower-layer/order/counter replay differs");
   for(int i=0;i<actual.Length;i++){Need(Near(actual[i],expected[i]),"Native source sparse position/order differs at"+i);Float3 src;Vector3 world,converted;Need(loop.WorldLoot.TryReadCacheSite(i,out src,out world)&&Near(src,actual[i])&&loop.TryArchiveSourceToWorld(src,out converted)&&Vector3.Distance(world,converted)<.002f,"Source coordinate reflection/generation differs at"+i);if(i>=prefix&&i<prefix+16)receipt.suffixSample.Add(V(actual[i]));}
   int target=-1;float farthest=-1;for(int i=prefix;i<actual.Length;i++){bool grenade=false;foreach(var draw in R23LootRecipe.Cache(Candidate,i))grenade|=R23LootRecipe.GrenadeKind(draw.Catalog)!=null;if(!grenade)continue;Vector3 world;Need(loop.TryArchiveSourceToWorld(actual[i],out world),"Actual sparse dispatch frame unavailable");float distance=(world-loop.Rig.transform.position).sqrMagnitude;if(distance>farthest){target=i;farthest=distance;}}
   if(target<0){Refuse("Positive authored suffix has no grenade-containing source draw; no catalog substitution forced");yield break;}receipt.targetIndex=target;
   Float3 floor;Vector3 floorWorld=Vector3.zero;Need(loop.ArchiveWorldFloor(Up(actual[target],1),3,out floor)&&!loop.ArchiveWorldOutside(floor)&&!loop.ArchiveWorldHazard(Up(floor,.8f))&&loop.ArchiveWorldRoom(floor)&&loop.TryArchiveSourceToWorld(floor,out floorWorld),"Actual sparse activation point has no safe current native floor");
   Note("BEFORE actual sparse activation via Rig: index="+target+" source="+V(actual[target])+" floor="+floorWorld+" activated="+loop.WorldLoot.ActivatedSites);
   loop.Rig.SetSpawn(floorWorld+Vector3.up*.02f,floorWorld+Vector3.forward);loop.Calibration.NotifyRootPoseReset();Physics.SyncTransforms();
   Vector3 targetWorld;Need(loop.TryArchiveSourceToWorld(actual[target],out targetWorld),"Actual sparse target world pose missing");float activationEnd=Mathf.Min(exerciseEnd,Time.realtimeSinceStartup+8);
   while(Time.realtimeSinceStartup<activationEnd&&CountAt(targetWorld)<6){Pump();yield return null;}
   Note("BEFORE genuine owner dispatch asserts: near="+CountAt(targetWorld)+" activated="+loop.WorldLoot.ActivatedSites+" unsupported="+loop.WorldLoot.UnsupportedDraws+" generation="+loop.ArchiveWorldGeneration);
   Need(loop.ArchiveWorldGeneration==generation&&CountAt(targetWorld)==6&&loop.WorldLoot.UnsupportedDraws==0,"Actual sparse source site did not register its exact six owner instances");VerifyOwners(target,targetWorld);
  }
  IEnumerator ReplaySparse(Float3[] spawns,Float3[] loot,Float3 low,Float3 high){
   Need(expected.Count<Math.Max(16,(int)(((double)high.X-low.X)*((double)high.Z-low.Z)/(65*65))),"Actual source sparse gate should be eligible");var candidates=new List<Float3>(spawns);int work=0;
   foreach(var top in loot){candidates.Add(Up(top,-.48f));var previous=top;int hits=0;for(int layer=0;layer<3;layer++){Float3 below;receipt.lowerQueries++;bool hit=loop.ArchiveWorldFloor(Up(previous,-.9f),35,out below);if(++work%24==0){Pump();yield return null;}if(!hit||!below.IsFinite)break;receipt.lowerHits++;hits++;candidates.Add(Up(below,.12f));previous=below;}receipt.longestLowerChain=Math.Max(receipt.longestLowerChain,hits);}
   int original=expected.Count;foreach(var candidate in candidates){if(++work%24==0){Pump();yield return null;}if(!candidate.IsFinite||loop.ArchiveWorldOutside(candidate)||loop.ArchiveWorldHazard(Up(candidate,.8f))||!loop.ArchiveWorldSafeSpawn(candidate))continue;bool close=false;foreach(var old in expected){float x=old.X-candidate.X,z=old.Z-candidate.Z;if(Math.Abs(old.Y-candidate.Y)<3&&x*x+z*z<28*28){close=true;break;}}if(close)continue;expected.Add(Up(candidate,.53f));if(expected.Count-original>=160)break;}
  }
  int CountAt(Vector3 target){int count=0;foreach(var id in loop.WorldLoot.WorldInstanceActionIds){string instance;Vector3 center,at;Quaternion q;if(loop.Gameplay.TryReadWorldWeapon(id,out instance,out center,out at,out q)&&(at-target).sqrMagnitude<4)count++;}foreach(var id in loop.WorldLoot.WorldGrenadeInstanceIds){Vector3 at;Quaternion q;bool owned;if(loop.Explosives.TryReadWorldItem(id,out at,out q,out owned)&&!owned&&(at-target).sqrMagnitude<4)count++;}return count;}
  void VerifyOwners(int site,Vector3 target){
   var draws=R23LootRecipe.Cache(Candidate,site);var matched=new HashSet<int>();
   foreach(var id in loop.WorldLoot.WorldInstanceActionIds){string native;Vector3 center,at;Quaternion q;if(!loop.Gameplay.TryReadWorldWeapon(id,out native,out center,out at,out q)||(at-target).sqrMagnitude>=4)continue;R23WeaponInstanceInfo info;Need(loop.Gameplay.TryReadWeaponInstance(id,out info)&&info.InstanceId==native,"Real sparse firearm native identity missing");int catalog=R23WeaponPickupRules.CatalogIndex(info.ProfileId),slot=Array.FindIndex(draws,d=>d.Catalog==catalog);var state=loop.Gameplay.ReadWeaponState(id);Note("BEFORE weapon owner check: "+id+" profile="+info.ProfileId+" stars="+info.Stars+" loaded="+state.Loaded+" capacity="+info.Capacity+" phase="+state.Phase);int stars=catalog==14||catalog==15||catalog==16||catalog==29?1:slot<0?0:draws[slot].Stars;Need(slot>=0&&matched.Add(slot)&&info.Stars==stars&&state.Loaded==info.Capacity&&state.Phase==Ashfall.Modules.WeaponHandling.ReloadPhase.Ready,"Sparse firearm source catalog/rarity/native entitlement mismatch");receipt.weapons++;}
   foreach(var id in loop.WorldLoot.WorldGrenadeInstanceIds){Vector3 at;Quaternion q;bool owned;if(!loop.Explosives.TryReadWorldItem(id,out at,out q,out owned)||(at-target).sqrMagnitude>=4)continue;R23GrenadeItemState item;Need(loop.Explosives.TryReadItem(id,out item),"Real sparse grenade owner missing");int slot=Array.FindIndex(draws,d=>R23LootRecipe.GrenadeKind(d.Catalog)==item.Kind);Note("BEFORE grenade owner check: "+id+" kind="+item.Kind+" stars="+item.Stars+" draw="+item.SourceStars+" owned/spent="+item.Owned+"/"+item.Spent);Need(slot>=0&&matched.Add(slot)&&item.Stars==1&&item.SourceStars==draws[slot].Stars&&!item.Owned&&!item.Spent,"Sparse grenade source draw/finite custody mismatch");receipt.grenades++;}
   Need(matched.Count==6&&receipt.weapons>0&&receipt.grenades>0,"Actual sparse cache lacks both real firearm and finite grenade dispatch");
  }
  IEnumerator Cleanup(){
   Note("BEFORE actual source cleanup: restore="+saved.Map);loop.ReturnToLobby();if(loop.Map.MapId!=saved.Map){loop.SelectMap(saved.Map,false);while(Time.realtimeSinceStartup<deadline&&loop.Failure==null&&(!loop.Ready||loop.Map.MapId!=saved.Map)){Pump();yield return null;}}
   Need(loop.Ready&&loop.Map.MapId==saved.Map,"Original actual map could not be restored within finite cleanup");loop.ReturnToLobby();if(saved.SelectedMap!=null)Need(loop.UI.SetSelectedMap(saved.SelectedMap),"Original pending map selection unavailable");receipt.cleanupRestored=loop.Lobby.Visible&&!loop.MatchActive;Note("AFTER cleanup: actual map="+loop.Map.MapId+" lobby="+loop.Lobby.Visible);
  }
  void Finish(){Passed=receipt.nativeExecuted&&receipt.positiveExercised&&!receipt.refused&&receipt.error.Length==0&&receipt.cleanupRestored&&receipt.weapons>0&&receipt.grenades>0;receipt.success=Passed;Json=JsonUtility.ToJson(receipt,true);Debug.Log("R24 SPARSE WORLD TERMINAL "+Json);}
  sealed class Saved {public readonly string Map,SelectedMap;readonly bool manual,external,rig,pump,persist;readonly HandSample left,right;public Saved(R23PlayableLoop l){Map=l.Map.MapId;SelectedMap=l.UI.SelectedMapId;manual=l.ManualSmoke;external=l.ExternalProbePump;rig=l.Rig.enabled;pump=l.Gameplay.ExternalInputPump;persist=l.Calibration.SuppressPersistence;left=l.Rig.LeftInput;right=l.Rig.RightInput;}public void Restore(R23PlayableLoop l){if(l==null)return;l.ManualSmoke=manual;l.ExternalProbePump=external;l.Rig.enabled=rig;l.Gameplay.ExternalInputPump=pump;l.Calibration.SuppressPersistence=persist;l.Rig.SmokeTrackedHands(left,right);}}
 }
}
#endif
