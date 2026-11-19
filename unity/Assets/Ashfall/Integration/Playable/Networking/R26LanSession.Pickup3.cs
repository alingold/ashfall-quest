#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  internal bool Pickup3OpenWarmup(string id){if(R25NetBot.Arg("-ashfallNetBotPickup3")!="true"||!Hosting||mapWorker!=null)return false;var choice=World.R23MapCatalog.Find(id);if(choice==null)return false;preparingMap=true;mapWorker=StartCoroutine(PrepareHostMap(choice,peer));return true;}
  readonly bool pickup3Diagnostics=R25NetBot.Arg("-ashfallNetBotPickup3")=="true"||R25NetBot.Arg("-ashfallNetBotPickup4")=="true";double pickup3NextRefusal;
  bool Pickup3Refused(string local,string reason){if(pickup3Diagnostics&&UnityEngine.Time.realtimeSinceStartupAsDouble>=pickup3NextRefusal){pickup3NextRefusal=UnityEngine.Time.realtimeSinceStartupAsDouble+.5;UnityEngine.Debug.Log("ASHFALL pickup3 refusal "+local+" ("+reason+") "+root.Pickup3State);}return false;}
  string Pickup3Custody(){if(peer==null)return "offline";var rows=new System.Collections.Generic.List<R26OwnedWeaponReceipt>();peer.Lobby.CopyOwnWeaponReceipts(rows);var ids=new System.Collections.Generic.List<string>();foreach(var row in rows)ids.Add(row.Id+":"+row.Profile);return System.String.Join(",",ids.ToArray());}
  internal string Pickup3State {get{return "watch="+pickupWatches.Count+" pending="+pendingDrops.Count+" aliases="+lootAliases.Count+" epoch="+(peer==null?-1:peer.Lobby.Epoch)+" receipts="+Pickup3Custody()+" refusal="+(peer==null?"offline":peer.Lobby.LastPickupRejection);}}
 }
}
#endif
