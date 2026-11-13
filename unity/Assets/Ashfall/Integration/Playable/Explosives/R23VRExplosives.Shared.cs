#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable.Explosives {
 public sealed partial class R23VRExplosives {
  sealed class SharedView {internal GameObject Art,Zone;internal int Warnings;internal bool Active;}
  readonly Dictionary<string,SharedView> sharedViews=new Dictionary<string,SharedView>(StringComparer.Ordinal);
  public Func<Transform,GameObject> SharedRocketVisual;
  public void ClearSharedProjectiles(){if(FlashSmoke!=null)FlashSmoke.Clear();foreach(var view in sharedViews.Values)if(view.Art!=null)Destroy(view.Art);sharedViews.Clear();}
  // Passive host projection: no Rigidbody, collider, local fuse or local damage owner.
  internal void PresentSharedProjectiles(R24LanProjectileState[] rows){if(!Ready)return;var present=new HashSet<string>(StringComparer.Ordinal);foreach(var row in rows){present.Add(row.Id);SharedView view;sharedViews.TryGetValue(row.Id,out view);Vector3 at=V(row.Position);if(row.Kind=="flash"||row.Kind=="smoke")FlashSmoke.Shared(row);if(row.Ended){if(row.Kind!="flash"&&row.Kind!="smoke")Play(row.Kind=="frag"?"frag_blast":row.Kind=="smaw"?"rocket_impact":"zone_end",at,.6309573f,100,1,row.Kind!="zone");if(view!=null&&view.Art!=null)Destroy(view.Art);sharedViews.Remove(row.Id);continue;}
    if(view==null){GameObject art=row.Kind=="smaw"?(SharedRocketVisual==null?null:SharedRocketVisual(content)):Instantiate(Template(row.Kind).gameObject,content);if(art==null)continue;view=new SharedView{Art=art};view.Art.name="Host shared "+row.Kind+" "+row.Id;view.Art.SetActive(true);if(row.Kind!="smaw")view.Art.transform.localScale=Vector3.one*ArtScale(row.Kind)*(.035f/.047f);foreach(var collider in view.Art.GetComponentsInChildren<Collider>())collider.enabled=false;if(row.Kind=="zone"){view.Zone=new GameObject("Host source zone mist");view.Zone.layer=2;view.Zone.transform.SetParent(view.Art.transform,false);view.Zone.AddComponent<MeshFilter>().sharedMesh=zoneMesh;view.Zone.AddComponent<MeshRenderer>().sharedMaterial=zoneMaterial;view.Zone.SetActive(false);}sharedViews[row.Id]=view;}
    view.Art.transform.position=at;Vector3 speed=V(row.Velocity);if(speed.sqrMagnitude>.001f)view.Art.transform.rotation=Quaternion.LookRotation(speed.normalized);if(row.Kind=="frag"){var times=R23GrenadeMath.WarningTimes(3.5);int before=view.Warnings;while(view.Warnings<times.Length&&times[view.Warnings]<=row.Age)view.Warnings++;if(view.Warnings>before)Play("frag_warning",at,.4466836f,60);}if(row.Kind=="zone"&&row.Active&&!view.Active){view.Active=true;Play("zone_bloom",at,.6309573f,100,1,true);}if(view.Zone!=null){view.Zone.SetActive(row.Active);view.Zone.transform.localScale=Vector3.one*row.Radius/Mathf.Max(.001f,view.Art.transform.localScale.x);}}
   var gone=new List<string>();foreach(var pair in sharedViews)if(!present.Contains(pair.Key)){if(pair.Value.Art!=null)Destroy(pair.Value.Art);gone.Add(pair.Key);}foreach(string id in gone)sharedViews.Remove(id);
  }
 }
}
#endif
