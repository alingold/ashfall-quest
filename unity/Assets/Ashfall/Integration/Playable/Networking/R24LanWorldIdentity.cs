using System;using System.Security.Cryptography;using System.Text;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking {
 public static class R24LanWorldIdentity {
  // Native peers generate their own map previews. A serial such as r6-ground/1
  // cannot bind the host's item to a different local profile/rarity or old map.
  public static string LocalId(string session,string map,string kind,string id,string profile,int stars){if(!Wire.IsText(session,64,false)||!Wire.IsHash(map)||!Wire.IsText(id,128,false)||!Wire.IsText(profile,16,false)||stars<1||stars>5||kind!="weapon"&&kind!="grenade")throw new ArgumentException("Host world identity");using(var hash=SHA256.Create()){var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(session+"\n"+map+"\n"+kind+"\n"+id+"\n"+profile+"\n"+stars));return "lan/"+BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}}
 }
}
