using System;
using System.Collections.Generic;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace Ashfall.Integration.Playable.World.Destructibles
{
    [Serializable]public sealed class R23ArchivePanelDefinition
    {
        public string id="",sound="";public bool glass=false;public float health=1;
        public float[] center=null,size=null,probe=null,normal=null;
    }
    // archive_panel_r8: durability 1; first positive impact retires the assembly.
    public sealed class R23ArchivePanelState
    {
        readonly bool[] broken;
        public int Count {get{return broken.Length;}}
        public int BrokenCount {get;private set;}
        public R23ArchivePanelState(int count){if(count<0||count>4096)throw new ArgumentOutOfRangeException("count");broken=new bool[count];}
        public bool IsBroken(int index){return index>=0&&index<broken.Length&&broken[index];}
        public bool Damage(int index,float amount)
        {
            if(index<0||index>=broken.Length||broken[index]||Single.IsNaN(amount)||Single.IsInfinity(amount)||amount<=0)return false;
            broken[index]=true;BrokenCount++;return true;
        }
        public void Reset(){Array.Clear(broken,0,broken.Length);BrokenCount=0;}
    }
#if UNITY_6000_0_OR_NEWER
    // Exact map chunks are registered by the map loader; this owns no replacement art.
    public sealed class R23ArchiveDestructibles
    {
        readonly R23ArchivePanelDefinition[] definitions;readonly R23ArchivePanelState state;
        readonly List<GameObject>[] parts;readonly Dictionary<Collider,int> colliderOwners=new Dictionary<Collider,int>();
        readonly int[] collisionParts;
        readonly Bounds[] touchBounds;
        readonly int[] chunkOwners;
        public int Count {get{return state.Count;}}public int BrokenCount {get{return state.BrokenCount;}}
        public R23ArchivePanelDefinition[] Definitions {get{return(R23ArchivePanelDefinition[])definitions.Clone();}}
        public R23ArchiveDestructibles(R23ArchivePanelDefinition[] rows,int[] owners,int chunks)
        {
            if(rows==null||owners==null||owners.Length!=chunks)throw new ArgumentException("panel source membership");
            definitions=(R23ArchivePanelDefinition[])rows.Clone();state=new R23ArchivePanelState(rows.Length);chunkOwners=(int[])owners.Clone();parts=new List<GameObject>[rows.Length];collisionParts=new int[rows.Length];touchBounds=new Bounds[rows.Length];var ids=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<rows.Length;i++)
            {
                if(rows[i]==null||String.IsNullOrEmpty(rows[i].id)||!ids.Add(rows[i].id)||rows[i].health!=1)throw new ArgumentException("source panel definition");
                parts[i]=new List<GameObject>();
                if(rows[i].center==null||rows[i].center.Length!=3||rows[i].size==null||rows[i].size.Length!=3)throw new ArgumentException("source panel touch bounds");
                touchBounds[i]=new Bounds(new Vector3(rows[i].center[0],rows[i].center[1],rows[i].center[2]),new Vector3(rows[i].size[0],rows[i].size[1],rows[i].size[2])+Vector3.one*.96f);
            }
            foreach(int i in owners)if(i< -1||i>=rows.Length)throw new ArgumentException("source panel index");
        }
        public void Register(int chunk,GameObject exactSourcePart,Collider collider)
        {
            int owner=chunkOwners[chunk];if(owner<0)return;
            RegisterOwner(owner,exactSourcePart,collider);
        }
        public void RegisterOwner(int owner,GameObject exactSourcePart,Collider collider)
        {
            if(owner<0||owner>=parts.Length)throw new ArgumentOutOfRangeException("owner");
            if(exactSourcePart==null)throw new InvalidOperationException("panel part requires actual source object");
            parts[owner].Add(exactSourcePart);if(collider!=null){colliderOwners.Add(collider,owner);collisionParts[owner]++;}
        }
        public void Validate(){for(int i=0;i<parts.Length;i++)if(parts[i].Count==0||collisionParts[i]==0)throw new InvalidOperationException("Missing source panel geometry/collision "+definitions[i].id);}
        public bool TryDamageCollider(Collider collider,float amount)
        {
            int index;if(collider==null||!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy||!colliderOwners.TryGetValue(collider,out index))return false;
            return Damage(index,amount);
        }
        bool Damage(int index,float amount)
        {
            if(!state.Damage(index,amount))return false;
            foreach(var part in parts[index])
            {
                if(part==null)continue;foreach(var c in part.GetComponentsInChildren<Collider>(true))c.enabled=false;part.SetActive(false);
            }
            Physics.SyncTransforms();return true;
        }
        public void Touch(Vector3 rootLocal,Vector3 headLocal,Action<R23ArchivePanelDefinition> accepted)
        {
            // archive_panel_r8.touches: box.grow(.48).has_point(point+UP*.9).
            rootLocal+=Vector3.up*.9f;headLocal+=Vector3.up*.4f;
            for(int i=0;i<touchBounds.Length;i++)if(!state.IsBroken(i)&&(touchBounds[i].Contains(rootLocal)||touchBounds[i].Contains(headLocal))&&Damage(i,1))if(accepted!=null)accepted(definitions[i]);
        }
        public bool TryRead(Collider collider,out string id,out bool broken)
        {int index;if(collider!=null&&colliderOwners.TryGetValue(collider,out index)){id=definitions[index].id;broken=state.IsBroken(index);return true;}id=null;broken=false;return false;}
        public bool TryReadDefinition(Collider collider,out R23ArchivePanelDefinition definition)
        {int index;if(collider!=null&&colliderOwners.TryGetValue(collider,out index)){definition=definitions[index];return true;}definition=null;return false;}
        public void Reset()
        {
            state.Reset();foreach(var group in parts)foreach(var part in group)if(part!=null){foreach(var c in part.GetComponentsInChildren<Collider>(true))c.enabled=true;part.SetActive(true);}Physics.SyncTransforms();
        }
    }
#endif
}
