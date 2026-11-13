using System;
using System.Collections.Generic;
namespace Ashfall.Modules.Combat
{
    public struct SwordEntry
    {
        public readonly string TargetId;
        public readonly WeaponLease Weapon;
        public readonly long Sequence;
        public SwordEntry(string target,long sequence){TargetId=target;Sequence=sequence;Weapon=null;}
        internal SwordEntry(string target,long sequence,WeaponLease weapon){TargetId=target;Sequence=sequence;Weapon=weapon;}
    }
    // Physics adapter supplies spatially ordered swept contacts plus current overlaps and
    // overlaps with .008m exit hysteresis at each .026m subdivision (melee_sword.gd).
    // IDs MUST be semantic owner IDs, not child collider instance IDs.
    public sealed class SwordContacts
    {
        public const float ContactRadius=.026f, ExitHysteresis=.008f;
        private readonly HashSet<string> touching=new HashSet<string>(StringComparer.Ordinal);
        private readonly int capacity;
        private readonly WeaponLease weapon;
        private bool ready;
        private long sequence;
        public SwordContacts(int targetCapacity){if(targetCapacity<1)throw new ArgumentException("targetCapacity");capacity=targetCapacity;}
        // Use this bound constructor for production; retain one tracker across Suspend.
        // The unbound constructor above is useful for standalone geometry sampling only.
        public SwordContacts(WeaponLease lease,int targetCapacity):this(targetCapacity)
        {
            if(lease==null||!lease.Active||lease.TrackerClaimed||!CombatRules.Sword(lease.Archetype))throw new ArgumentException("weapon tracker");
            weapon=lease;lease.TrackerClaimed=true;
        }
        public void Suspend(){ready=false;touching.Clear();}
        public SwordEntry[] Sample(bool usable,float delta,float bladeTravel,float bladeSpeed,string[] swept,string[] overlaps,string[] retained)
        {
            if((weapon!=null&&!weapon.Active)||!usable||!Finite(delta)||delta<=0||delta>.15f||!Finite(bladeTravel)||bladeTravel<0||bladeTravel>1||!Finite(bladeSpeed)||bladeSpeed<0){Suspend();return new SwordEntry[0];}
            Validate(swept);Validate(overlaps);Validate(retained);
            var result=new List<SwordEntry>();
            if(!ready){touching.Clear();foreach(string id in overlaps)touching.Add(id);ready=true;return result.ToArray();}
            var unique=new HashSet<string>(StringComparer.Ordinal);
            foreach(string id in swept)
            {
                if(!unique.Add(id))continue;
                if(!touching.Contains(id)&&bladeSpeed>=.2f){sequence=checked(sequence+1);result.Add(new SwordEntry(id,sequence,weapon));}
                touching.Add(id);
            }
            var keep=new HashSet<string>(retained,StringComparer.Ordinal);
            touching.IntersectWith(keep);
            foreach(string id in overlaps)touching.Add(id);
            if(touching.Count>capacity){Suspend();throw new InvalidOperationException("contact capacity");}
            return result.ToArray();
        }
        private void Validate(string[] ids){if(ids==null||ids.Length>capacity)throw new ArgumentException("contact data");foreach(string id in ids)if(String.IsNullOrEmpty(id))throw new ArgumentException("contact ID");}
        private static bool Finite(float f){return !Single.IsNaN(f)&&!Single.IsInfinity(f);}
    }
}
