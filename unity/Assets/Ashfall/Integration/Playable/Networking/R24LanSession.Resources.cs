#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using Ashfall.Core;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  readonly List<string> completedSupplyDrops=new List<string>();
  R24LanResourcesState appliedResources;readonly Dictionary<string,double> supplyDropPending=new Dictionary<string,double>();
  sealed class RootSupplyDrop:IR24LanSupplyDropWorld {readonly R24LanSession owner;internal RootSupplyDrop(R24LanSession owner){this.owner=owner;}public bool TryPlace(string family,Float3 desired,out Float3 accepted){accepted=new Float3();if(owner.root==null||owner.root.Supplies==null)return false;var bounds=owner.root.Supplies.GetDropBounds(family);Vector3 at=new Vector3(desired.X,desired.Y,desired.Z);RaycastHit hit;if(!Physics.Raycast(at+Vector3.up*.5f,Vector3.down,out hit,3,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore)||hit.normal.y<.65f)return false;at=hit.point+Vector3.up*(-bounds.min.y+.015f);if(Physics.CheckBox(at+bounds.center,bounds.extents*.98f,Quaternion.identity,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))return false;accepted=V(at);return true;}}
  public bool QueueReserveDrop(string family,Vector3 desired){if(peer==null)return false;PublishRawTracking();string id;if(!peer.Lobby.RequestSupplyDrop(family,root.Gameplay.PrimaryHand,V(desired),out id))return false;supplyDropPending[id]=Time.realtimeSinceStartupAsDouble+3;return true;}
  void SyncResources(){if(peer==null||root==null||root.Gameplay==null)return;R24LanResourcesState row;if(peer.Lobby.TryReadOwnResources(out row)&&peer.Lobby.OwnsResources(row)){bool changed=appliedResources==null||appliedResources.Epoch!=row.Epoch||appliedResources.Acknowledged<peer.Lobby.ResourcesPredictionSequence||!row.SameInventory(appliedResources);if(changed&&root.ApplyLanResources(row))appliedResources=row;}
   if(supplyDropPending.Count==0)return;var done=completedSupplyDrops;done.Clear();foreach(var pending in supplyDropPending){R24LanSupplyState box;if(peer.Lobby.TryReadSharedSupply(pending.Key,out box)){root.UI.SetLoadoutStatus("DROPPED");done.Add(pending.Key);}else if(Time.realtimeSinceStartupAsDouble>pending.Value){root.UI.SetLoadoutStatus("DROP NOT ACCEPTED BY HOST");done.Add(pending.Key);}}foreach(string id in done)supplyDropPending.Remove(id);done.Clear();
  }
 }
}
#endif
