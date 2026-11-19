using System;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        // Host keeps the canonical point; guests have the same ordinal actor ordering on every peer.
        // Read the committed roster directly: no per-frame snapshot allocations or wire changes.
        public int MenuSpawnSlot
        {
            get
            {
                R24LanMember own=null;foreach(var member in members)if(member.Actor==OwnActor){own=member;break;}
                if(own==null||own.Host)return 0;
                int slot=0;foreach(var member in members)if(member.Host||String.CompareOrdinal(member.Actor,OwnActor)<0)slot++;
                return slot;
            }
        }
    }
}
