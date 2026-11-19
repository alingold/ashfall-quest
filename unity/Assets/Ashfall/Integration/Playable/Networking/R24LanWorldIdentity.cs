using System;using System.Security.Cryptography;using System.Text;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking {
 public static class R24LanWorldIdentity {
  // Native peers generate their own map previews. A serial such as r6-ground/1
  // cannot bind the host's item to a different local profile/rarity or old map.
  // MP-PERF: a guest asks for the same ids every frame (once per shared grenade). The derivation is pure, so the
  // last answer per host id is kept; a hit costs one lookup and no allocation. Bounded: cleared at 8192 entries.
  sealed class Known {internal string Session,Map,Kind,Profile,Local;internal int Stars;}
  static readonly System.Collections.Generic.Dictionary<string,Known> known=new System.Collections.Generic.Dictionary<string,Known>(StringComparer.Ordinal);
  public static string LocalId(string session,string map,string kind,string id,string profile,int stars){
   Known hit;lock(known){if(id!=null&&known.TryGetValue(id,out hit)&&hit.Stars==stars&&hit.Session==session&&hit.Map==map&&hit.Kind==kind&&hit.Profile==profile)return hit.Local;}
   string local=Derive(session,map,kind,id,profile,stars);lock(known){if(known.Count>=8192)known.Clear();known[id]=new Known{Session=session,Map=map,Kind=kind,Profile=profile,Stars=stars,Local=local};}return local;}
  static string Derive(string session,string map,string kind,string id,string profile,int stars){if(!Wire.IsText(session,64,false)||!Wire.IsHash(map)||!Wire.IsText(id,128,false)||!Wire.IsText(profile,16,false)||stars<1||stars>5||kind!="weapon"&&kind!="grenade")throw new ArgumentException("Host world identity");using(var hash=SHA256.Create()){var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(session+"\n"+map+"\n"+kind+"\n"+id+"\n"+profile+"\n"+stars));return "lan/"+BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}}
 }
}
