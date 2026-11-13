using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.World.Loot
{
 public delegate bool R23LootFloor(Float3 from,float depth,out Float3 floor);
 public enum R23LootSiteRejection {None,Floor,Outside,Hazard}
 public sealed class R23SparseLootResult
 {
  public int Threshold{get;internal set;}public int Candidates{get;internal set;}public int LowerFloorQueries{get;internal set;}
  public int RejectedOutside{get;internal set;}public int RejectedHazard{get;internal set;}public int RejectedSafeSpawn{get;internal set;}public int RejectedSpacing{get;internal set;}public int Added{get;internal set;}
 }
 // Exact sparse_loot_r18.add_sites order. Coordinates remain original Godot
 // map-local until the caller converts accepted sites through its real map frame.
 public static class R23SparseLoot
 {
  static Float3 Up(Float3 p,float amount){return new Float3(p.X,p.Y+amount,p.Z);}
  public static bool TryInitialSite(Float3 candidate,R23LootFloor floor,Func<Float3,bool> outside,Func<Float3,bool> hazard,out Float3 site,out R23LootSiteRejection rejection){
   site=new Float3();Float3 at;rejection=R23LootSiteRejection.Floor;
   if(!candidate.IsFinite||!floor(Up(candidate,1.1f),4,out at)||!at.IsFinite)return false;
   if(outside(at)){rejection=R23LootSiteRejection.Outside;return false;}
   if(hazard(Up(at,.5f))){rejection=R23LootSiteRejection.Hazard;return false;}
   site=Up(at,.65f);rejection=R23LootSiteRejection.None;return true;
  }
  public static IEnumerator AddSites(List<Float3> sites,IList<Float3> spawns,IList<Float3> loot,Float3 boundsMin,Float3 boundsMax,R23LootFloor floor,Func<Float3,bool> outside,Func<Float3,bool> hazard,Func<Float3,bool> safeSpawn,R23SparseLootResult result)
  {
   if(sites==null||spawns==null||loot==null||floor==null||outside==null||hazard==null||safeSpawn==null||result==null)throw new ArgumentNullException("Actual ordered source points and world queries required");
   double area=((double)boundsMax.X-boundsMin.X)*((double)boundsMax.Z-boundsMin.Z);
   result.Threshold=Math.Max(16,(int)(area/(65*65)));int original=sites.Count;
   if(original>=result.Threshold)yield break;
   var candidates=new List<Float3>();foreach(var p in spawns)candidates.Add(p);int work=0;
   foreach(var top in loot){
    candidates.Add(Up(top,-.48f));var previous=top;
    for(int layer=0;layer<3;layer++){
     Float3 below;result.LowerFloorQueries++;bool found=floor(Up(previous,-.9f),35,out below);
     if(++work%32==0)yield return null;
     if(!found||!below.IsFinite)break;candidates.Add(Up(below,.12f));previous=below;
    }
   }
   result.Candidates=candidates.Count;
   foreach(var candidate in candidates){
    if(++work%32==0)yield return null;
    if(!candidate.IsFinite||outside(candidate)){result.RejectedOutside++;continue;}
    if(hazard(Up(candidate,.8f))){result.RejectedHazard++;continue;}
    if(!safeSpawn(candidate)){result.RejectedSafeSpawn++;continue;}
    bool close=false;foreach(var old in sites){float x=old.X-candidate.X,z=old.Z-candidate.Z;if(Math.Abs(old.Y-candidate.Y)<3&&x*x+z*z<28*28){close=true;break;}}
    if(close){result.RejectedSpacing++;continue;}
    sites.Add(Up(candidate,.53f));result.Added++;
    if(sites.Count-original>=160)break;
   }
  }
 }
}
