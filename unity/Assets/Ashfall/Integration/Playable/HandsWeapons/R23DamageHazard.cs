#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Put on a root-owned fall plane/hazard; assign the local player service.
    // Only a collider belonging to the supplied tracked rig may damage the player.
    public sealed class R23DamageHazard : MonoBehaviour
    {
        public R23HandsWeapons Player;
        public Transform PlayerRoot;
        public float Damage=25;
        public float Cooldown=1;
        float next;
        void Contact(Collider other) {
            if(Player==null||PlayerRoot==null||Time.time<next)return;
            if(other.transform!=PlayerRoot&&!other.transform.IsChildOf(PlayerRoot))return;
            next=Time.time+Mathf.Max(.1f,Cooldown);Player.ApplyPlayerDamage(Damage);
        }
        void OnTriggerEnter(Collider other) { Contact(other); }
        void OnTriggerStay(Collider other) { Contact(other); }
        void OnCollisionEnter(Collision other) { Contact(other.collider); }
    }
}
#endif
