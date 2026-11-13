#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Editor.Playable
{
 public sealed class R24RagdollContactProbe:MonoBehaviour
 {
  public int WorldContacts {get;private set;}
  void OnCollisionEnter(Collision collision){Record(collision);}
  void OnCollisionStay(Collision collision){Record(collision);}
  void Record(Collision collision){if(collision.collider!=null&&((1<<collision.collider.gameObject.layer)&((1<<0)|(1<<8)))!=0&&collision.contactCount>0)WorldContacts++;}
 }
}
#endif
